import os
import cv2
import json
import subprocess
from pathlib import Path
from datetime import datetime
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
from ultralytics import YOLO
from werkzeug.utils import secure_filename
import numpy as np

app = Flask(__name__)
CORS(app)

# Configuration
UPLOAD_FOLDER = 'uploads'
OUTPUT_FOLDER = 'outputs'
MODEL_PATH = 'models/best.pt'
ALLOWED_EXTENSIONS = {'mp4', 'avi', 'mov', 'mkv'}
MAX_FILE_SIZE = 100 * 1024 * 1024  # 100MB

# Video encoding settings
USE_FFMPEG_REENCODE = True  # Set to False to use OpenCV encoding only
USE_AV1_CODEC = False  # Set to True for AV1, False for H.264 (recommended)

# Ensure directories exist
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
os.makedirs(OUTPUT_FOLDER, exist_ok=True)

# Load YOLO model
print("Loading YOLO model...")
try:
    model = YOLO(MODEL_PATH)
    print(f"Model loaded successfully. Classes: {model.names}")
except Exception as e:
    print(f"Error loading model: {e}")
    model = None

def allowed_file(filename):
    """Check if file extension is allowed"""
    return '.' in filename and filename.rsplit('.', 1)[1].lower() in ALLOWED_EXTENSIONS

def check_ffmpeg_installed():
    """Check if FFmpeg is installed and available"""
    try:
        result = subprocess.run(['ffmpeg', '-version'], capture_output=True, text=True)
        return result.returncode == 0
    except FileNotFoundError:
        return False

def reencode_video_with_ffmpeg(input_path, output_path, use_av1=False):
    """Reencode video using FFmpeg for web compatibility"""
    try:
        if use_av1:
            # AV1 codec - modern but slower encoding
            cmd = [
                'ffmpeg',
                '-i', input_path,
                '-c:v', 'libaom-av1',  # AV1 codec
                '-crf', '30',  # Quality (0-63, lower is better)
                '-b:v', '0',   # Variable bitrate
                '-cpu-used', '8',  # Speed preset (0-8, higher is faster)
                '-row-mt', '1',  # Row-based multithreading
                '-c:a', 'libopus',  # Audio codec
                '-b:a', '128k',
                '-movflags', '+faststart',  # Enable streaming
                '-y',  # Overwrite output file
                output_path
            ]
        else:
            # H.264 codec - widely compatible, faster encoding
            cmd = [
                'ffmpeg',
                '-i', input_path,
                '-c:v', 'libx264',  # H.264 codec
                '-preset', 'medium',  # Encoding speed (ultrafast, fast, medium, slow)
                '-crf', '23',  # Quality (0-51, lower is better, 23 is default)
                '-pix_fmt', 'yuv420p',  # Pixel format for compatibility
                '-c:a', 'aac',  # Audio codec
                '-b:a', '128k',
                '-movflags', '+faststart',  # Enable streaming (important for web!)
                '-y',  # Overwrite output file
                output_path
            ]
        
        print(f"Re-encoding video with FFmpeg ({('AV1' if use_av1 else 'H.264')})...")
        result = subprocess.run(cmd, capture_output=True, text=True)
        
        if result.returncode != 0:
            print(f"FFmpeg error: {result.stderr}")
            return False
        
        print(f"Video re-encoded successfully: {output_path}")
        return True
        
    except FileNotFoundError:
        print("ERROR: FFmpeg not found. Please install FFmpeg and add it to PATH.")
        return False
    except Exception as e:
        print(f"Error re-encoding video: {e}")
        return False

def process_video(video_path, output_path, use_ffmpeg_reencode=True, use_av1=False):
    """Process video and detect fish using YOLO"""
    if model is None:
        raise Exception("YOLO model not loaded")
    
    cap = cv2.VideoCapture(video_path)
    
    if not cap.isOpened():
        raise Exception("Could not open video file")
    
    # Get video properties
    fps = int(cap.get(cv2.CAP_PROP_FPS))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    duration = total_frames / fps if fps > 0 else 0
    
    # Video writer for output
    # First write to temporary file with OpenCV codec
    temp_output = output_path.replace('.mp4', '_temp.mp4') if use_ffmpeg_reencode else output_path
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(temp_output, fourcc, fps, (width, height))
    
    detections = []
    fish_counts = {}
    frame_number = 0
    
    print(f"Processing video: {total_frames} frames at {fps} FPS")
    
    while cap.isOpened():
        ret, frame = cap.read()
        if not ret:
            break
        
        frame_number += 1
        timestamp = frame_number / fps if fps > 0 else 0
        
        # Run YOLO detection
        results = model(frame, verbose=False)
        
        # Process detections
        for result in results:
            boxes = result.boxes
            for box in boxes:
                # Get bounding box coordinates
                x1, y1, x2, y2 = box.xyxy[0].cpu().numpy()
                confidence = float(box.conf[0])
                class_id = int(box.cls[0])
                
                # Get class name (fish type)
                fish_type = model.names[class_id] if class_id in model.names else f"Class_{class_id}"
                
                # Only include detections with confidence > 0.6
                if confidence > 0.6:
                    # Add to detections list
                    detection = {
                        "fishType": fish_type,
                        "confidence": round(confidence, 3),
                        "timestamp": round(timestamp, 2),
                        "frameNumber": frame_number,
                        "bBox": {
                            "x": int(x1),
                            "y": int(y1),
                            "width": int(x2 - x1),
                            "height": int(y2 - y1)
                        }
                    }
                    detections.append(detection)
                    
                    # Update fish counts
                    fish_counts[fish_type] = fish_counts.get(fish_type, 0) + 1
                    
                    # Draw bounding box on frame
                    color = (0, 255, 0)  # Green
                    cv2.rectangle(frame, (int(x1), int(y1)), (int(x2), int(y2)), color, 2)
                    
                    # Add label with fish type and confidence
                    label = f"{fish_type}: {confidence:.2f}"
                    label_size, _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 2)
                    cv2.rectangle(frame, (int(x1), int(y1) - label_size[1] - 10), 
                                (int(x1) + label_size[0], int(y1)), color, -1)
                    cv2.putText(frame, label, (int(x1), int(y1) - 5), 
                              cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 0), 2)
        
        # Write frame to output video
        out.write(frame)
        
        # Progress indicator
        if frame_number % 30 == 0:
            print(f"Processed {frame_number}/{total_frames} frames ({frame_number/total_frames*100:.1f}%)")
    
    cap.release()
    out.release()
    
    # Re-encode with FFmpeg for web compatibility
    if use_ffmpeg_reencode:
        print("Re-encoding video for web compatibility...")
        if reencode_video_with_ffmpeg(temp_output, output_path, use_av1):
            # Remove temporary file
            try:
                os.remove(temp_output)
                print(f"Temporary file removed: {temp_output}")
            except Exception as e:
                print(f"Warning: Could not remove temp file: {e}")
        else:
            # If FFmpeg fails, use the temp file as output
            print("WARNING: FFmpeg re-encoding failed, using OpenCV output")
            if temp_output != output_path:
                os.rename(temp_output, output_path)
    
    # Calculate dominant fish type
    dominant_fish = None
    if fish_counts:
        dominant_type = max(fish_counts, key=fish_counts.get)
        dominant_fish = {
            "type": dominant_type,
            "count": fish_counts[dominant_type]
        }
    
    print(f"Processing complete. Total detections: {len(detections)}")
    
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
    """Health check endpoint"""
    model_loaded = model is not None
    return jsonify({
        'status': 'healthy' if model_loaded else 'degraded',
        'model_loaded': model_loaded,
        'timestamp': datetime.now().isoformat()
    }), 200 if model_loaded else 503

@app.route('/api/supported-fish', methods=['GET'])
def get_supported_fish():
    """Get list of supported fish types from model"""
    if model is None:
        return jsonify({
            'error': 'Model not loaded'
        }), 503
    
    fish_types = list(model.names.values()) if hasattr(model, 'names') else []
    
    return jsonify({
        'fishTypes': fish_types,
        'total': len(fish_types)
    }), 200

@app.route('/api/analyze-video', methods=['POST'])
def analyze_video():
    """Analyze video for fish detection"""
    if model is None:
        return jsonify({
            'success': False,
            'error': 'YOLO model not loaded. Please check model path.'
        }), 503
    
    # Check if video file is present
    if 'video' not in request.files:
        return jsonify({
            'success': False,
            'error': 'No video file provided'
        }), 400
    
    file = request.files['video']
    
    if file.filename == '':
        return jsonify({
            'success': False,
            'error': 'No file selected'
        }), 400
    
    if not allowed_file(file.filename):
        return jsonify({
            'success': False,
            'error': f'Invalid file type. Allowed: {", ".join(ALLOWED_EXTENSIONS)}'
        }), 400
    
    try:
        # Save uploaded video
        filename = secure_filename(file.filename)
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        unique_filename = f"{timestamp}_{filename}"
        
        video_path = os.path.join(UPLOAD_FOLDER, unique_filename)
        output_filename = f"processed_{unique_filename}"
        output_path = os.path.join(OUTPUT_FOLDER, output_filename)
        
        file.save(video_path)
        print(f"Video saved: {video_path}")
        
        # Get encoding preferences from form data (optional)
        use_av1 = request.form.get('use_av1', 'false').lower() == 'true'
        use_ffmpeg = request.form.get('use_ffmpeg', 'true').lower() == 'true'
        
        # Process video
        print("Starting video analysis...")
        codec_type = "AV1" if use_av1 else "H.264"
        print(f"Using FFmpeg re-encoding: {use_ffmpeg} (Codec: {codec_type})")
        
        results = process_video(video_path, output_path, use_ffmpeg_reencode=use_ffmpeg, use_av1=use_av1)
        
        # Add processed video URL
        results['processed_video_url'] = f"outputs/{output_filename}"
        
        return jsonify({
            'success': True,
            'results': results
        }), 200
        
    except Exception as e:
        print(f"Error analyzing video: {str(e)}")
        import traceback
        traceback.print_exc()
        
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500

@app.route('/outputs/<path:filename>', methods=['GET'])
def serve_output(filename):
    """Serve processed video files"""
    response = send_from_directory(OUTPUT_FOLDER, filename)
    response.headers['Access-Control-Allow-Origin'] = '*'
    response.headers['Access-Control-Allow-Methods'] = 'GET'
    response.headers['Accept-Ranges'] = 'bytes'
    return response

@app.route('/uploads/<path:filename>', methods=['GET'])
def serve_upload(filename):
    """Serve uploaded video files"""
    response = send_from_directory(UPLOAD_FOLDER, filename)
    response.headers['Access-Control-Allow-Origin'] = '*'
    response.headers['Access-Control-Allow-Methods'] = 'GET'
    response.headers['Accept-Ranges'] = 'bytes'
    return response

@app.route('/', methods=['GET'])
def index():
    """Index endpoint"""
    ffmpeg_installed = check_ffmpeg_installed()
    
    return jsonify({
        'service': 'WhereWeFishin Fish Recognition Service',
        'version': '1.0.0',
        'status': 'running',
        'model_loaded': model is not None,
        'ffmpeg_installed': ffmpeg_installed,
        'video_encoding': {
            'ffmpeg_reencode': USE_FFMPEG_REENCODE,
            'codec': 'AV1' if USE_AV1_CODEC else 'H.264',
            'warning': 'FFmpeg not installed! Videos may not play in browsers.' if not ffmpeg_installed and USE_FFMPEG_REENCODE else None
        },
        'endpoints': {
            'health': '/health',
            'analyze': '/api/analyze-video',
            'supported_fish': '/api/supported-fish'
        }
    }), 200

if __name__ == '__main__':
    print("="*60)
    print("WhereWeFishin Fish Recognition Service")
    print("="*60)
    print(f"Model path: {MODEL_PATH}")
    print(f"Model loaded: {model is not None}")
    if model is not None and hasattr(model, 'names'):
        print(f"Supported classes: {list(model.names.values())}")
    print("-"*60)
    print("Video Encoding Configuration:")
    ffmpeg_available = check_ffmpeg_installed()
    print(f"FFmpeg installed: {ffmpeg_available}")
    print(f"FFmpeg re-encoding: {USE_FFMPEG_REENCODE}")
    print(f"Codec: {'AV1' if USE_AV1_CODEC else 'H.264 (recommended)'}")
    
    if USE_FFMPEG_REENCODE and not ffmpeg_available:
        print("\n⚠️  WARNING: FFmpeg not found!")
        print("Videos will use OpenCV encoding which may not play in browsers.")
        print("Install FFmpeg: https://ffmpeg.org/download.html")
        print("Or set USE_FFMPEG_REENCODE = False in app.py")
    print("="*60)
    
    # Run Flask app
    app.run(host='0.0.0.0', port=5001, debug=True, threaded=True)
