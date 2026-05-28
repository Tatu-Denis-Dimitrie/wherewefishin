# WhereWeFishin — Fish Recognition Service

<p>
  <img alt="Python" src="https://img.shields.io/badge/Python_3.9+-3776AB?style=for-the-badge&logo=python&logoColor=white"/>
  <img alt="Flask" src="https://img.shields.io/badge/Flask-000000?style=for-the-badge&logo=flask&logoColor=white"/>
  <img alt="YOLOv8" src="https://img.shields.io/badge/YOLOv8-00FFFF?style=for-the-badge&logo=yolo&logoColor=black"/>
  <img alt="OpenCV" src="https://img.shields.io/badge/OpenCV-5C3EE8?style=for-the-badge&logo=opencv&logoColor=white"/>
  <img alt="PyTorch" src="https://img.shields.io/badge/PyTorch-EE4C2C?style=for-the-badge&logo=pytorch&logoColor=white"/>
</p>

Python Flask microservice for fish detection, tracking, and species classification. Uses a custom-trained YOLOv8 model with ByteTrack multi-object tracking to identify and count unique fish across video frames.

> **Note:** This service is not publicly exposed. All requests are proxied through the .NET backend after authentication.

---

## Table of Contents

- [How It Works](#how-it-works)
- [API Endpoints](#api-endpoints)
- [Configuration](#configuration)
- [Tracking — ByteTrack](#tracking--bytetrack)
- [Video Encoding — FFmpeg](#video-encoding--ffmpeg)
- [Model](#model)
- [Setup & Running](#setup--running)
- [GPU vs CPU](#gpu-vs-cpu)
- [Performance](#performance)
- [Troubleshooting](#troubleshooting)

---

## How It Works

### Video Analysis Pipeline

```
POST /api/analyze-video
  │
  ├─ 1. Load video frames with OpenCV
  ├─ 2. Run YOLOv8 inference at 640px resolution (FP16 on GPU)
  ├─ 3. ByteTrack assigns a persistent integer ID to each detected fish
  ├─ 4. Draw annotated frame: bounding box, label (Fish #N, confidence), colour trail
  ├─ 5. Accumulate unique track IDs → total unique fish count
  ├─ 6. Re-encode annotated video with FFmpeg (H.264/AAC, browser-compatible)
  └─ 7. Return: fish count, per-ID data, output video path
```

### Image Classification Pipeline

```
POST /api/analyze-image
  │
  ├─ 1. Load image
  ├─ 2. Run YOLOv8 inference (single frame, no tracking)
  └─ 3. Return: detected species with bounding boxes and confidence scores
```

### Trail Rendering

Each tracked fish gets a **colour-coded motion trail** drawn on the annotated video. The trail:
- Uses a deterministic colour per track ID (consistent across the entire video)
- Fades out older points (configurable via `TRAIL_FADE`)
- Automatically disappears after `TRAIL_FADE_OUT_FRAMES` frames without detection
- Re-associates the same trail if a fish re-enters the frame (based on spatial overlap)

---

## API Endpoints

| Method | Route | Description |
|---|---|---|
| `GET` | `/health` | Health check — returns service status and model load state |
| `GET` | `/api/supported-fish` | List of fish species the model can recognise |
| `POST` | `/api/analyze-video` | Analyse a video file with full tracking and annotation |
| `POST` | `/api/analyze-image` | Classify fish species from a single image |
| `DELETE` | `/api/delete-output/<filename>` | Delete a processed output file |
| `GET` | `/outputs/<filename>` | Serve an annotated output video |
| `GET` | `/uploads/<filename>` | Serve an original uploaded video |

### `POST /api/analyze-video`

**Form-data fields:**

| Field | Type | Default | Description |
|---|---|---|---|
| `video` | file | required | Video file (mp4, avi, mov, mkv) |
| `use_ffmpeg` | string | `"true"` | Re-encode output for browser compatibility |
| `use_av1` | string | `"false"` | Use AV1 codec instead of H.264 |

**Response:**

```json
{
  "success": true,
  "total_unique_fish": 7,
  "detections_per_id": { "1": 142, "2": 98, "3": 210 },
  "output_video": "outputs/analyzed_<timestamp>.mp4",
  "processing_time_seconds": 14.3
}
```

### `POST /api/analyze-image`

**Form-data fields:**

| Field | Type | Description |
|---|---|---|
| `image` | file | Image file (jpg, jpeg, png, webp) |

**Response:**

```json
{
  "success": true,
  "detections": [
    { "species": "Carp", "confidence": 0.91, "bbox": [120, 45, 380, 290] }
  ]
}
```

---

## Configuration

All tunable parameters are at the top of `app.py`:

```python
# ── Detection & Tracking ────────────────────────────────────
TRACK_CONFIDENCE   = 0.69          # Minimum detection confidence (0–1)
                                   # Higher → fewer detections, fewer false positives
TRACK_PERSIST      = True          # Keep track IDs between frames (required for ByteTrack)
TRACKER_CONFIG     = 'bytetrack.yaml'  # Tracker config ('botsort.yaml' = more precise, slower)
IMG_SIZE           = 640           # Input resolution for YOLO inference
USE_HALF_PRECISION = True          # FP16 inference on GPU (ignored on CPU)

# ── Video Encoding ──────────────────────────────────────────
USE_FFMPEG_REENCODE = True         # Re-encode with FFmpeg (required for browser playback)
USE_AV1_CODEC       = False        # True = AV1 (smaller files, 3–10× slower)
                                   # False = H.264 (recommended for production)

# ── Trail Rendering ─────────────────────────────────────────
TRAIL_ENABLED         = True       # Draw motion trail per fish
TRAIL_MAX_POINTS      = 35         # Trail history length (higher = longer tail)
TRAIL_DOT_RADIUS      = 3          # Radius of each trail dot in pixels
TRAIL_LINE_THICKNESS  = 2          # Thickness of trail line
TRAIL_FADE            = True       # Older points become more transparent
TRAIL_RECLAIM_MIN_POINTS = 2       # Min overlap points for track re-association
TRAIL_FADE_OUT_FRAMES = 20         # Frames before a lost trail is removed (0 = instant)
```

---

## Tracking — ByteTrack

ByteTrack was chosen over alternatives (DeepSORT, BoTSORT) for the following reasons:

| Criterion | ByteTrack | BoTSORT | DeepSORT |
|---|---|---|---|
| Speed | Fast | Moderate | Slow |
| Accuracy for similar objects | Good | Better | Good |
| Dependencies | Minimal (`lapx`) | Moderate | Heavy (re-ID model) |
| Integration with Ultralytics | Native | Native | Third-party |

**How IDs are maintained:**  
ByteTrack uses a two-stage association algorithm. In the first pass, high-confidence detections are matched to existing tracks using IoU. In the second pass, low-confidence detections and unmatched tracks are reconciled. This makes it robust to brief occlusions and overlapping fish.

**`lapx`** is the only extra dependency required — it provides the linear assignment problem solver used for the matching step. Everything else is bundled with `ultralytics`.

---

## Video Encoding — FFmpeg

FFmpeg re-encoding is **required** for browser video playback. Without it, the OpenCV-encoded output uses a codec that most browsers cannot decode inline.

### Codec Comparison

| | H.264 (libx264) | AV1 (libaom-av1) |
|---|---|---|
| **Browser compatibility** | Universal | Modern browsers only |
| **Encoding speed** | Fast | 3–10× slower |
| **File size** | Medium | Small |
| **Recommended for** | Production | Archiving |

### Installing FFmpeg

**Windows:**
```powershell
winget install Gyan.FFmpeg
# or
choco install ffmpeg
```

**Linux (Ubuntu/Debian):**
```bash
sudo apt update && sudo apt install ffmpeg
```

**macOS:**
```bash
brew install ffmpeg
```

Verify installation: `ffmpeg -version`

---

## Model

The trained YOLOv8 weights (`best.pt`) are **not committed** to this repository due to file size.

- **Expected path:** `fish-recognition-service/models/best.pt`
- **Docker:** mounted as a read-only volume via `docker-compose.yml` — never baked into the image
- The service logs the loaded model's class names on startup (`model.names`)

The model was trained on a custom dataset of freshwater fish species. See `GET /api/supported-fish` for the full list of recognisable species at runtime.

---

## Setup & Running

```bash
cd fish-recognition-service

# 1. Create and activate virtual environment
python -m venv venv
.\venv\Scripts\Activate.ps1      # Windows
source venv/bin/activate         # Linux / macOS

# 2. Install dependencies
pip install -r requirements.txt
# lapx (ByteTrack), torch, ultralytics, flask, opencv-python, numpy

# 3. Place model weights
#    Copy best.pt → fish-recognition-service/models/best.pt

# 4. Run
python app.py                    # → http://localhost:5001

# Verify FFmpeg
python test_ffmpeg.py
```

### Dependencies

```
flask==3.0.0
flask-cors==4.0.0
ultralytics        # YOLOv8 + ByteTrack/BoTSORT
lapx               # Linear assignment solver for ByteTrack
opencv-python      # Frame extraction and annotation
torch              # PyTorch inference backend
numpy
```

---

## GPU vs CPU

On startup, the service automatically detects CUDA availability:

```
Using device: cuda
GPU: NVIDIA GeForce RTX 3060
CUDA Version: 12.1
```

If no CUDA-capable GPU is found, it falls back to CPU automatically. Inference is significantly slower on CPU — expect 3–10× longer processing times for video analysis.

FP16 half-precision (`USE_HALF_PRECISION = True`) is only active on GPU. On CPU it is silently ignored.

---

## Performance

| Encoding | Additional processing time | Browser compatibility |
|---|---|---|
| None (OpenCV raw) | — | Poor (codec issues) |
| H.264 (FFmpeg) | +10–30% | Universal |
| AV1 (FFmpeg) | +200–500% | Modern browsers only |

**Recommendation for production: H.264 with FFmpeg re-encoding.**

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `Error loading model` | Verify `models/best.pt` exists and is a valid YOLOv8 weights file |
| Video not playing in browser | Ensure `USE_FFMPEG_REENCODE = True` and FFmpeg is in PATH |
| Encoding too slow | Switch to H.264: `USE_AV1_CODEC = False` |
| FFmpeg not found | Add FFmpeg to PATH and restart the terminal |
| `lapx` import error | Run `pip install lapx` in the active virtual environment |
| Service starts but no GPU | Install CUDA-compatible PyTorch: `pip install torch --index-url https://download.pytorch.org/whl/cu121` |
| Too many false positives | Increase `TRACK_CONFIDENCE` (e.g., `0.75`) |
| Fish not being tracked across frames | Decrease `TRACK_CONFIDENCE` or check video quality/lighting |
