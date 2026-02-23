import os
import cv2
import subprocess
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

USE_FFMPEG_REENCODE = True
USE_AV1_CODEC = False

BATCH_SIZE = 64
USE_HALF_PRECISION = True
IMG_SIZE = 640

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
    print(f"Batch size: {BATCH_SIZE}, Image size: {IMG_SIZE}")
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

def draw_detection(frame, x1, y1, x2, y2, fish_type, confidence):
    color = (0, 255, 0)
    cv2.rectangle(frame, (int(x1), int(y1)), (int(x2), int(y2)), color, 2)
    label = f"{fish_type}: {confidence:.2f}"
    label_size, _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 2)
    cv2.rectangle(frame, (int(x1), int(y1) - label_size[1] - 10), 
                (int(x1) + label_size[0], int(y1)), color, -1)
    cv2.putText(frame, label, (int(x1), int(y1) - 5), 
              cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 0), 2)

def process_video(video_path, output_path, use_ffmpeg_reencode=True, use_av1=False):
    if model is None:
        raise Exception("Model not loaded")
    
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        raise Exception("Cannot open video")
    
    fps = int(cap.get(cv2.CAP_PROP_FPS))
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    
    all_frames = []
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        all_frames.append(frame)
    cap.release()
    
    total_frames = len(all_frames)
    duration = total_frames / fps if fps > 0 else 0
    
    temp_output = output_path.replace('.mp4', '_temp.mp4') if use_ffmpeg_reencode else output_path
    out = cv2.VideoWriter(temp_output, cv2.VideoWriter_fourcc(*'mp4v'), fps, (width, height))
    
    detections = []
    fish_counts = {}
    
    print(f"Processing: {total_frames} frames (batch: {BATCH_SIZE})")
    
    for batch_start in range(0, total_frames, BATCH_SIZE):
        batch_end = min(batch_start + BATCH_SIZE, total_frames)
        batch_frames = all_frames[batch_start:batch_end]
        
        results = model(batch_frames, stream=False, verbose=False, imgsz=IMG_SIZE, 
                       half=(device == 'cuda' and USE_HALF_PRECISION), conf=0.6)
        
        for i, result in enumerate(results):
            frame_number = batch_start + i + 1
            timestamp = round(frame_number / fps, 2) if fps > 0 else 0
            frame = batch_frames[i]
            
            for box in result.boxes:
                x1, y1, x2, y2 = map(int, box.xyxy[0].cpu().numpy())
                confidence = float(box.conf[0])
                fish_type = model.names.get(int(box.cls[0]), f"Class_{int(box.cls[0])}")
                
                detections.append({
                    "fishType": fish_type,
                    "confidence": round(confidence, 3),
                    "timestamp": timestamp,
                    "frameNumber": frame_number,
                    "bBox": {"x": x1, "y": y1, "width": x2 - x1, "height": y2 - y1}
                })
                
                fish_counts[fish_type] = fish_counts.get(fish_type, 0) + 1
                draw_detection(frame, x1, y1, x2, y2, fish_type, confidence)
            
            out.write(frame)
        
        if batch_end % (BATCH_SIZE * 2) == 0 or batch_end == total_frames:
            print(f"{batch_end}/{total_frames} ({batch_end/total_frames*100:.1f}%)")
    
    out.release()
    
    if use_ffmpeg_reencode and reencode_video(temp_output, output_path, use_av1):
        try:
            os.remove(temp_output)
        except:
            pass
    elif temp_output != output_path:
        os.rename(temp_output, output_path)
    
    dominant_fish = max(fish_counts.items(), key=lambda x: x[1]) if fish_counts else None
    if dominant_fish:
        dominant_fish = {"type": dominant_fish[0], "count": dominant_fish[1]}
    
    print(f"Complete: {len(detections)} detections")
    
    return {
        "totalFrames": total_frames,
        "duration": round(duration, 2),
        "fps": fps,
        "detections": detections,
        "fishCounts": fish_counts,
        "dominantFish": dominant_fish,
        "totalDetections": len(detections)
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
        output_filename = f"processed_{unique_filename}"
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
