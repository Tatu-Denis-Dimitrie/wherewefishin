import os
import cv2
import numpy as np
import subprocess
from collections import deque
from datetime import datetime
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
from ultralytics import YOLO
from werkzeug.utils import secure_filename
import torch

app = Flask(__name__)
CORS(app)

UPLOAD_FOLDER = 'uploads'
OUTPUT_FOLDER = 'outputs'
MODEL_PATH = 'models/best.pt'
ALLOWED_EXTENSIONS = {'mp4', 'avi', 'mov', 'mkv'}
ALLOWED_IMAGE_EXTENSIONS = {'jpg', 'jpeg', 'png', 'webp'}

# ── Video encoding ──────────────────────────────────────────
USE_FFMPEG_REENCODE = True
USE_AV1_CODEC = False

# ── Model & Tracker ─────────────────────────────────────────
USE_HALF_PRECISION = True
IMG_SIZE = 640
TRACKER_CONFIG = 'bytetrack.yaml'   # alternative: 'botsort.yaml' (more precise, slower)
TRACK_CONFIDENCE = 0.69               # minimum confidence threshold (higher = fewer false positives)
TRACK_PERSIST = True                 # keep track IDs between frames

# ── Trail / Tracking Points ─────────────────────────────────
TRAIL_ENABLED = True                 # draw the trail for each fish
TRAIL_MAX_POINTS = 35                # how many recent points to keep per fish (higher = longer trail)
TRAIL_DOT_RADIUS = 3                 # circle radius per trail point
TRAIL_LINE_THICKNESS = 2             # line thickness between points
TRAIL_FADE = True                    # older points become more transparent
TRAIL_RECLAIM_MIN_POINTS = 2         # min trail points inside bbox for re-association (higher = stricter)
TRAIL_FADE_OUT_FRAMES = 20          # remove trail after this many missing frames (0 = instant)

os.makedirs(UPLOAD_FOLDER, exist_ok=True)
os.makedirs(OUTPUT_FOLDER, exist_ok=True)

device = 'cuda' if torch.cuda.is_available() else 'cpu'
print(f"Using device: {device}")
if device == 'cuda':
    print(f"GPU: {torch.cuda.get_device_name(0)}")
    print(f"CUDA Version: {torch.version.cuda}")

print("Loading YOLO model...")
try:
    model = YOLO(MODEL_PATH)
    model.to(device)
    print(f"Model loaded on {device}. Classes: {model.names}")
    print(f"Tracker: {TRACKER_CONFIG} | persist={TRACK_PERSIST} | conf={TRACK_CONFIDENCE}")
except Exception as e:
    print(f"Error loading model: {e}")
    model = None

def reencode_video(input_path, output_path, use_av1=False):
    try:
        base_cmd = ['ffmpeg', '-i', input_path]
        if use_av1:
            video_opts = ['-c:v', 'libaom-av1', '-crf', '30', '-b:v', '0', '-cpu-used', '8', '-row-mt', '1']
            audio_opts = ['-c:a', 'libopus', '-b:a', '128k']
        else:
            video_opts = ['-c:v', 'libx264', '-preset', 'fast', '-crf', '23', '-pix_fmt', 'yuv420p', '-profile:v', 'baseline', '-level', '3.0']
            audio_opts = ['-c:a', 'aac', '-b:a', '128k']
        
        cmd = base_cmd + video_opts + audio_opts + ['-movflags', '+faststart', '-y', output_path]
        
        result = subprocess.run(cmd, capture_output=True, text=True)
        return result.returncode == 0
    except Exception as e:
        print(f"FFmpeg error: {e}")
        return False

def get_track_color(track_id):
    # Deterministic color so each track ID keeps the same color across frames.
    if track_id is None:
        return (0, 255, 0)

    seed = int(track_id)
    blue = 50 + (seed * 73) % 206
    green = 50 + (seed * 151) % 206
    red = 50 + (seed * 199) % 206
    return (blue, green, red)

def draw_trail(frame, points, color):
    """Draw fading trajectory trail for a tracked fish."""
    n = len(points)
    if n < 2:
        return
    for i in range(1, n):
        if TRAIL_FADE:
            alpha = i / n  # 0→1, older points are dimmer
        else:
            alpha = 1.0
        pt_color = tuple(int(c * alpha) for c in color)
        cv2.line(frame, points[i - 1], points[i], pt_color, TRAIL_LINE_THICKNESS)
        cv2.circle(frame, points[i], TRAIL_DOT_RADIUS, pt_color, -1)

def find_trail_owner(x1, y1, x2, y2, track_trails):
    """Return the track_id whose recent trail has the most points inside this bounding box.
    Used to re-associate a fish when ByteTrack assigns a new ID after a brief occlusion.
    """
    best_id = None
    best_count = 0
    for tid, trail in track_trails.items():
        count = sum(1 for (cx, cy) in trail if x1 <= cx <= x2 and y1 <= cy <= y2)
        if count > best_count:
            best_count = count
            best_id = tid
    return best_id if best_count >= TRAIL_RECLAIM_MIN_POINTS else None

def draw_detection(frame, x1, y1, x2, y2, fish_type, confidence, track_id=None):
    color = get_track_color(track_id)
    cv2.rectangle(frame, (int(x1), int(y1)), (int(x2), int(y2)), color, 2)

    if track_id is not None:
        label = f"{fish_type} #{track_id} ({confidence:.2f})"
    else:
        label = f"{fish_type}: {confidence:.2f}"

    label_size, _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 2)
    label_top = max(0, int(y1) - label_size[1] - 10)
    cv2.rectangle(frame, (int(x1), label_top),
                (int(x1) + label_size[0], int(y1)), color, -1)
    cv2.putText(frame, label, (int(x1), max(15, int(y1) - 5)),
              cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 0), 2)

def process_video(video_path, output_path, use_ffmpeg_reencode=True, use_av1=False):
    if model is None:
        raise Exception("Model not loaded")
    
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        raise Exception("Cannot open video")
    
    fps = int(cap.get(cv2.CAP_PROP_FPS))
    if fps <= 0:
        fps = 30

    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    estimated_total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    cap.release()
    
    temp_output = output_path.replace('.mp4', '_temp.mp4') if use_ffmpeg_reencode else output_path
    out = cv2.VideoWriter(temp_output, cv2.VideoWriter_fourcc(*'mp4v'), fps, (width, height))
    if not out.isOpened():
        raise Exception("Cannot create output video")
    
    detections = []
    unique_track_ids = set()
    track_best_species = {}
    track_trails = {}               # track_id -> deque of (cx, cy) centers
    track_last_seen = {}            # track_id -> last frame_number in which fish was visible
    id_remap = {}                   # bytetrack raw_id -> canonical track_id (re-asociere dupa traseu)
    total_frame_detections = 0
    processed_frames = 0
    
    print(f"Tracking with {TRACKER_CONFIG}: ~{estimated_total_frames} frames")

    track_results = model.track(
        source=video_path,
        stream=True,
        verbose=False,
        imgsz=IMG_SIZE,
        half=(device == 'cuda' and USE_HALF_PRECISION),
        conf=TRACK_CONFIDENCE,
        tracker=TRACKER_CONFIG,
        persist=TRACK_PERSIST
    )

    for result in track_results:
        processed_frames += 1
        frame_number = processed_frames
        timestamp = round(frame_number / fps, 2)
        frame = result.orig_img.copy()
        in_frame_count = 0

        boxes = result.boxes
        track_ids = boxes.id.tolist() if boxes is not None and boxes.id is not None else []

        if boxes is not None:
            for idx, box in enumerate(boxes):
                x1, y1, x2, y2 = map(int, box.xyxy[0].cpu().numpy())
                confidence = float(box.conf[0])
                class_id = int(box.cls[0])
                fish_type = model.names.get(class_id, f"Class_{class_id}")

                track_id = None
                if idx < len(track_ids):
                    raw_id = int(track_ids[idx])

                    if raw_id in id_remap:
                        # Already remapped from a previous frame
                        track_id = id_remap[raw_id]
                    elif raw_id in track_trails:
                        # ByteTrack is tracking this fish correctly, no remap needed
                        track_id = raw_id
                    else:
                        # New ID assigned by ByteTrack — check if trail recognizes this fish
                        owner = find_trail_owner(x1, y1, x2, y2, track_trails)
                        if owner is not None:
                            id_remap[raw_id] = owner
                            track_id = owner
                        else:
                            track_id = raw_id

                    unique_track_ids.add(track_id)

                    previous = track_best_species.get(track_id)
                    if previous is None or confidence > previous['confidence']:
                        track_best_species[track_id] = {
                            'fishType': fish_type,
                            'confidence': confidence
                        }

                    # Store center point for trail
                    if TRAIL_ENABLED:
                        cx, cy = (x1 + x2) // 2, (y1 + y2) // 2
                        if track_id not in track_trails:
                            track_trails[track_id] = deque(maxlen=TRAIL_MAX_POINTS)
                        track_trails[track_id].append((cx, cy))
                        track_last_seen[track_id] = frame_number

                detections.append({
                    "fishType": fish_type,
                    "confidence": round(confidence, 3),
                    "timestamp": timestamp,
                    "frameNumber": frame_number,
                    "trackId": track_id,
                    "bBox": {"x": x1, "y": y1, "width": x2 - x1, "height": y2 - y1}
                })

                total_frame_detections += 1
                in_frame_count += 1
                draw_detection(frame, x1, y1, x2, y2, fish_type, confidence, track_id)

        # Fade-out and purge stale trails (fish no longer in frame)
        if TRAIL_ENABLED:
            stale_ids = []
            for tid, trail in track_trails.items():
                frames_absent = frame_number - track_last_seen.get(tid, frame_number)
                if TRAIL_FADE_OUT_FRAMES == 0 and frames_absent > 0:
                    stale_ids.append(tid)
                elif frames_absent > TRAIL_FADE_OUT_FRAMES:
                    stale_ids.append(tid)
                elif frames_absent > 0 and len(trail) >= 2:
                    # Still in fade-out window: draw with reduced opacity
                    fade_factor = 1.0 - frames_absent / max(TRAIL_FADE_OUT_FRAMES, 1)
                    base_color = get_track_color(tid)
                    faded_color = tuple(int(c * fade_factor) for c in base_color)
                    draw_trail(frame, list(trail), faded_color)
                elif len(trail) >= 2:
                    draw_trail(frame, list(trail), get_track_color(tid))
            for tid in stale_ids:
                track_trails.pop(tid, None)
                track_last_seen.pop(tid, None)

        cv2.putText(frame, f"In frame: {in_frame_count}", (10, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
        cv2.putText(frame, f"Total unique fish: {len(unique_track_ids)}", (10, 60),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)

        out.write(frame)

        if processed_frames % 120 == 0:
            if estimated_total_frames > 0:
                pct = processed_frames / estimated_total_frames * 100
                print(f"{processed_frames}/{estimated_total_frames} ({pct:.1f}%)")
            else:
                print(f"Processed {processed_frames} frames")
    
    out.release()
    
    if use_ffmpeg_reencode and reencode_video(temp_output, output_path, use_av1):
        try:
            os.remove(temp_output)
        except OSError:
            pass
    elif temp_output != output_path:
        os.rename(temp_output, output_path)

    fish_counts = {}
    for tracked_fish in track_best_species.values():
        fish_type = tracked_fish['fishType']
        fish_counts[fish_type] = fish_counts.get(fish_type, 0) + 1

    total_unique_fish = len(unique_track_ids)
    duration = processed_frames / fps if fps > 0 else 0
    
    dominant_fish = max(fish_counts.items(), key=lambda x: x[1]) if fish_counts else None
    if dominant_fish:
        dominant_fish = {"type": dominant_fish[0], "count": dominant_fish[1]}
    
    print(f"Complete: {total_unique_fish} unique fish, {total_frame_detections} frame detections")
    
    return {
        "totalFrames": processed_frames,
        "duration": round(duration, 2),
        "fps": fps,
        "detections": detections,
        "fishCounts": fish_counts,
        "dominantFish": dominant_fish,
        "totalUniqueFish": total_unique_fish,
        "totalDetections": total_unique_fish,
        "totalFrameDetections": total_frame_detections,
        "tracker": TRACKER_CONFIG,
        "trackingEnabled": True
    }

@app.route('/health', methods=['GET'])
def health_check():
    model_loaded = model is not None
    return jsonify({
        'status': 'healthy' if model_loaded else 'degraded',
        'model_loaded': model_loaded,
        'timestamp': datetime.now().isoformat()
    }), 200 if model_loaded else 503

@app.route('/api/supported-fish', methods=['GET'])
def get_supported_fish():
    if not model:
        return jsonify({'error': 'Model not loaded'}), 503
    fish_types = list(model.names.values())
    return jsonify({'fishTypes': fish_types, 'total': len(fish_types)}), 200

@app.route('/api/analyze-video', methods=['POST'])
def analyze_video():
    if not model:
        return jsonify({'success': False, 'error': 'Model not loaded'}), 503
    
    file = request.files.get('video')
    if not file or file.filename == '':
        return jsonify({'success': False, 'error': 'No video file'}), 400
    
    ext = file.filename.rsplit('.', 1)[1].lower() if '.' in file.filename else ''
    if ext not in ALLOWED_EXTENSIONS:
        return jsonify({'success': False, 'error': f'Invalid type. Allowed: {", ".join(ALLOWED_EXTENSIONS)}'}), 400
    
    try:
        filename = secure_filename(file.filename)
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        unique_filename = f"{timestamp}_{filename}"
        
        video_path = os.path.join(UPLOAD_FOLDER, unique_filename)
        output_stem = os.path.splitext(unique_filename)[0]
        output_filename = f"processed_{output_stem}.mp4"
        output_path = os.path.join(OUTPUT_FOLDER, output_filename)
        
        file.save(video_path)
        
        use_av1 = request.form.get('use_av1', 'false').lower() == 'true'
        use_ffmpeg = request.form.get('use_ffmpeg', 'true').lower() == 'true'
        
        results = process_video(video_path, output_path, use_ffmpeg_reencode=use_ffmpeg, use_av1=use_av1)
        results['processed_video_url'] = f"outputs/{output_filename}"

        try:
            if os.path.exists(video_path):
                os.remove(video_path)
        except Exception as e:
            print(f"Warning: could not delete upload file: {e}")

        return jsonify({'success': True, 'results': results}), 200
        
    except Exception as e:
        print(f"Error: {str(e)}")
        import traceback
        traceback.print_exc()
        return jsonify({'success': False, 'error': str(e)}), 500

def _build_class_probs(pred, x1, y1, x2, y2, h_orig, w_orig, detected_class_id, detected_confidence, top_k=8):
    """
    Build a class-probability distribution for a detected bounding box.

    Chooses the best anchor among nearby candidates using the detected class
    logit, then uses that anchor's class scores directly for ranking alternatives.
    We avoid cross-class softmax here because it tends to flatten low-probability
    classes and make alternatives look artificially equal.
    """
    try:
        nc = len(model.names)
        if pred.dim() != 3 or pred.shape[1] < 4 + nc:
            return []

        # Map detected-box center from original pixels → IMG_SIZE space
        scale_x = IMG_SIZE / w_orig
        scale_y = IMG_SIZE / h_orig
        det_cx = ((x1 + x2) / 2.0) * scale_x
        det_cy = ((y1 + y2) / 2.0) * scale_y

        # Candidate anchors near this box center.
        anchor_cx = pred[0, 0, :].float()
        anchor_cy = pred[0, 1, :].float()
        dist_sq = (anchor_cx - det_cx) ** 2 + (anchor_cy - det_cy) ** 2
        cls_logits_all = pred[0, 4:4 + nc, :].float()

        if 0 <= detected_class_id < nc:
            # Prefer nearby anchors that strongly support the detected class,
            # not only the geometrically closest anchor.
            num_anchors = pred.shape[2]
            k = min(64, num_anchors)
            nearest_idx = torch.topk(-dist_sq, k).indices
            detected_logits = cls_logits_all[detected_class_id, nearest_idx]
            best_local = int(torch.argmax(detected_logits).item())
            best = int(nearest_idx[best_local].item())
        else:
            best = int(dist_sq.argmin().item())

        cls_scores = cls_logits_all[:, best]
        # Depending on the exact Ultralytics path, scores may already be in [0, 1]
        # or still be logits. Convert to probabilities only when needed.
        if float(cls_scores.min()) < 0.0 or float(cls_scores.max()) > 1.0:
            cls_scores = cls_scores.sigmoid()
        cls_scores = cls_scores.clamp(0.0, 1.0)

        if not (0 <= detected_class_id < nc):
            detected_class_id = int(torch.argmax(cls_scores).item())

        sorted_idx = torch.argsort(cls_scores, descending=True).tolist()
        alt_idx = [i for i in sorted_idx if i != detected_class_id]

        if alt_idx:
            alt_max = float(cls_scores[alt_idx[0]].item())
            min_keep = alt_max * 0.15  # keep only meaningful alternatives
            alt_idx = [i for i in alt_idx if float(cls_scores[i].item()) >= min_keep]
        alt_idx = alt_idx[:max(0, min(top_k - 1, nc - 1))]

        result = [{
            'fishType': model.names[int(detected_class_id)],
            'confidence': float(max(0.0, min(1.0, detected_confidence)))
        }]
        for i in alt_idx:
            result.append({
                'fishType': model.names[int(i)],
                'confidence': float(cls_scores[i].item())
            })
        return result
    except Exception as e:
        print(f"Warning: class prob extraction failed: {e}")
        return []


@app.route('/api/analyze-image', methods=['POST'])
def analyze_image():
    if not model:
        return jsonify({'success': False, 'error': 'Model not loaded'}), 503

    file = request.files.get('image')
    if not file or file.filename == '':
        return jsonify({'success': False, 'error': 'No image file provided'}), 400

    ext = file.filename.rsplit('.', 1)[1].lower() if '.' in file.filename else ''
    if ext not in ALLOWED_IMAGE_EXTENSIONS:
        return jsonify({'success': False, 'error': f'Invalid type. Allowed: {", ".join(ALLOWED_IMAGE_EXTENSIONS)}'}), 400

    try:
        file_bytes = np.frombuffer(file.read(), dtype=np.uint8)
        frame = cv2.imdecode(file_bytes, cv2.IMREAD_COLOR)
        if frame is None:
            return jsonify({'success': False, 'error': 'Cannot decode image'}), 400

        h_orig, w_orig = frame.shape[:2]

        # ── 1. Standard detection (preprocessing + NMS handled by ultralytics) ──
        results = model.predict(
            source=frame,
            verbose=False,
            imgsz=IMG_SIZE,
            half=(device == 'cuda' and USE_HALF_PRECISION),
            conf=TRACK_CONFIDENCE
        )

        # ── 2. Single raw forward pass on the full image for class-score extraction ──
        #       (same image, same scale as predict — no crop, no re-inference)
        img_rs = cv2.resize(frame, (IMG_SIZE, IMG_SIZE))
        img_t = torch.from_numpy(img_rs[:, :, ::-1].copy()).float() / 255.0
        img_t = img_t.permute(2, 0, 1).unsqueeze(0).to(device)
        if USE_HALF_PRECISION and device == 'cuda':
            img_t = img_t.half()
        with torch.no_grad():
            raw = model.model(img_t)
        pred = (raw[0] if isinstance(raw, (list, tuple)) else raw).float()

        # ── 3. Build detections with mathematically correct class distributions ──
        frame_out = frame.copy()
        detections = []

        for result in results:
            boxes = result.boxes
            if boxes is None:
                continue
            for box in boxes:
                x1, y1, x2, y2 = map(int, box.xyxy[0].cpu().numpy())
                confidence = float(box.conf[0])
                class_id = int(box.cls[0])
                fish_type = model.names.get(class_id, f"Class_{class_id}")

                class_probs = _build_class_probs(pred, x1, y1, x2, y2, h_orig, w_orig, class_id, confidence)
                draw_detection(frame_out, x1, y1, x2, y2, fish_type, confidence)

                detections.append({
                    'fishType': fish_type,
                    'confidence': round(confidence, 3),
                    'bbox': {'x': x1, 'y': y1, 'width': x2 - x1, 'height': y2 - y1},
                    'classProbs': class_probs
                })

        detections.sort(key=lambda d: d['confidence'], reverse=True)
        dominant = detections[0] if detections else None

        # ── 4. Save annotated image ──
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S_%f')
        safe_name = secure_filename(file.filename)
        output_filename = f"img_{timestamp}_{os.path.splitext(safe_name)[0]}.jpg"
        output_path = os.path.join(OUTPUT_FOLDER, output_filename)
        cv2.imwrite(output_path, frame_out, [cv2.IMWRITE_JPEG_QUALITY, 90])

        return jsonify({
            'success': True,
            'detections': detections,
            'dominantDetection': dominant,
            'processedImageUrl': f"outputs/{output_filename}",
            'totalDetections': len(detections)
        }), 200

    except Exception as e:
        print(f"Error analyzing image: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({'success': False, 'error': str(e)}), 500


@app.route('/api/delete-output/<path:filename>', methods=['DELETE'])
def delete_output(filename):
    file_path = os.path.join(OUTPUT_FOLDER, filename)
    try:
        if os.path.exists(file_path):
            os.remove(file_path)
        return jsonify({'success': True}), 200
    except Exception as e:
        print(f"Error deleting output file: {e}")
        return jsonify({'success': False, 'error': str(e)}), 500

def serve_file(folder, filename):
    response = send_from_directory(folder, filename)
    response.headers['Access-Control-Allow-Origin'] = '*'
    response.headers['Accept-Ranges'] = 'bytes'
    return response

@app.route('/outputs/<path:filename>')
def serve_output(filename):
    return serve_file(OUTPUT_FOLDER, filename)

@app.route('/uploads/<path:filename>')
def serve_upload(filename):
    return serve_file(UPLOAD_FOLDER, filename)

@app.route('/')
def index():
    return jsonify({'service': 'WhereWeFishin', 'status': 'running', 'model': model is not None}), 200

if __name__ == '__main__':
    print("="*60)
    print("WhereWeFishin Fish Recognition Service")
    print("="*60)
    if model is not None and hasattr(model, 'names'):
        print(f"Supported classes: {list(model.names.values())}")
    print("="*60)
    app.run(host='0.0.0.0', port=5001, debug=True, threaded=True)
