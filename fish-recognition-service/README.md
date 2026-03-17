# Fish Recognition Service

Python Flask service for real-time fish detection and recognition in videos using YOLOv8.

## Features

- Real-time fish detection from videos
- ByteTrack tracking with a persistent ID for each fish
- Full statistics: detection count, fish types, dominant species
- Processed video generation with bounding boxes and labels
- Web-optimized encoding (H.264/AV1)

## Requirements

### Python Packages
```bash
pip install -r requirements.txt
```

`lapx` is the extra dependency required for the assignment algorithm (Linear Assignment Problem) used by ByteTrack. Everything else is already included in `ultralytics`.

### FFmpeg (REQUIRED for web compatibility)

Processed videos should be re-encoded with FFmpeg to work reliably in browsers and VS Code.

#### **Windows:**

**Option 1: Chocolatey (Recommended)**
```powershell
# Install Chocolatey if needed
# Then install FFmpeg:
choco install ffmpeg
```

**Option 2: Manual**
1. Download FFmpeg from: https://www.gyan.dev/ffmpeg/builds/
2. Download `ffmpeg-release-essentials.zip`
3. Extract to `C:\ffmpeg`
4. Add `C:\ffmpeg\bin` to PATH:
  - Start -> "Environment Variables" -> Path -> Edit
   - Add: `C:\ffmpeg\bin`
5. Restart terminal and test:
   ```powershell
   ffmpeg -version
   ```

**Option 3: Winget**
```powershell
winget install Gyan.FFmpeg
```

#### **Linux:**
```bash
# Ubuntu/Debian
sudo apt update
sudo apt install ffmpeg

# Fedora
sudo dnf install ffmpeg

# Arch
sudo pacman -S ffmpeg
```

#### **macOS:**
```bash
brew install ffmpeg
```

## Video Encoding Configuration

In `app.py`, you can configure:

```python
USE_FFMPEG_REENCODE = True  # Enable/disable FFmpeg
USE_AV1_CODEC = False       # True = AV1, False = H.264
```

### Available codecs

- **H.264 (libx264)** - **RECOMMENDED**
  - Maximum compatibility with all browsers
  - Fast encoding speed
  - Excellent streaming support
  - Reasonable file size

- **AV1 (libaom-av1)**
  - Better compression (smaller files)
  - MUCH slower encoding (3-10x)
  - Limited support in older browsers
  - Better for archiving, not live processing

## ByteTrack Tracking

ByteTrack was chosen because:

- It is already integrated in `ultralytics` (without complex extra dependencies)
- It is faster and lighter than DeepSORT
- It works very well for visually similar objects (such as fish)
- It uses `model.track()` instead of `model()`

In `app.py`, tracking runs with:

- `tracker="bytetrack.yaml"`
- `persist=True`
- `conf=0.4`

### What changed compared to the previous version

| Before (detection) | Now (ByteTrack tracking) |
|---|---|
| `model(frame)` detected fish without identity | `model.track(frame, tracker="bytetrack.yaml", persist=True)` gives each fish a unique ID |
| Counted only fish from the current frame | Counts unique fish across the entire video |
| Identical boxes | Each fish gets a different color based on ID |
| `Fish: 0.85` labels | `Fish #3 (0.85)` labels |

### What you see on screen

- `In frame: X` - how many fish are currently in frame
- `Total unique fish: Y` - how many unique fish have been detected since video start
- Persistent ID for each fish (`Fish #1`, `Fish #2`) maintained frame-to-frame

### Tunable parameters

- `conf=0.4` - increase if you get false positives, decrease if you miss fish
- `persist=True` - essential to keep tracking IDs between frames
- `tracker="botsort.yaml"` - more precise alternative, but slower

## Start the service

```bash
# Activate virtual environment
.\venv\Scripts\Activate.ps1   # Windows
source venv/bin/activate       # Linux/macOS

# Start service
python app.py
```

The script uses ByteTrack by default.

Service URL: **http://localhost:5001**

## Endpoints

- `GET /` - Service info and FFmpeg status
- `GET /health` - Health check
- `GET /api/supported-fish` - List of supported fish types
- `POST /api/analyze-video` - Analyze video (upload)
  - Form-data:
    - `video`: video file
    - `use_ffmpeg`: "true"/"false" (optional, default: true)
    - `use_av1`: "true"/"false" (optional, default: false)
- `GET /outputs/{filename}` - Download processed video
- `GET /uploads/{filename}` - Download original video

## FFmpeg verification

```bash
# Test whether FFmpeg is installed
ffmpeg -version

# Or access:
http://localhost:5001/
```

If FFmpeg is NOT installed, you will see console warnings and videos may **not work in browsers**.

## Troubleshooting

### Videos do not play in browser
- Verify FFmpeg is installed: `ffmpeg -version`
- Verify config in `app.py`: `USE_FFMPEG_REENCODE = True`
- Check console for FFmpeg warnings

### Encoding is too slow
- Switch from AV1 to H.264: `USE_AV1_CODEC = False`
- Or adjust the preset in `reencode_video()`

### FFmpeg not found
- Verify it is in PATH: `echo $env:Path` (Windows) or `echo $PATH` (Linux/macOS)
- Restart terminal after installation
- On Windows, a system restart might be required

## Performance

- **OpenCV encoding**: Fast, but web-incompatible videos
- **H.264 encoding**: Adds ~10-30% processing time, 100% compatibility
- **AV1 encoding**: Adds 200-500% processing time, optimal compression

**For production, use H.264!**
