"""
Test script to verify FFmpeg installation and video encoding
"""

import subprocess
import os

def check_ffmpeg():
    """Check if FFmpeg is installed"""
    print("Checking FFmpeg installation...")
    try:
        result = subprocess.run(['ffmpeg', '-version'], capture_output=True, text=True)
        if result.returncode == 0:
            lines = result.stdout.split('\n')
            print("✅ FFmpeg is installed!")
            print(f"   {lines[0]}")  # Version line
            return True
        else:
            print("❌ FFmpeg returned error")
            return False
    except FileNotFoundError:
        print("❌ FFmpeg not found!")
        print("\nInstallation instructions:")
        print("Windows (Chocolatey): choco install ffmpeg")
        print("Windows (Manual): Download from https://www.gyan.dev/ffmpeg/builds/")
        print("Linux: sudo apt install ffmpeg")
        print("macOS: brew install ffmpeg")
        return False

def check_codecs():
    """Check available codecs"""
    print("\nChecking available codecs...")
    
    codecs_to_check = ['libx264', 'libaom-av1', 'aac', 'libopus']
    
    try:
        result = subprocess.run(['ffmpeg', '-codecs'], capture_output=True, text=True)
        output = result.stdout
        
        for codec in codecs_to_check:
            if codec in output:
                print(f"✅ {codec} - Available")
            else:
                print(f"⚠️  {codec} - Not available")
                if codec == 'libaom-av1':
                    print("   (AV1 codec not available - use H.264 instead)")
    except Exception as e:
        print(f"Error checking codecs: {e}")

def test_simple_encode():
    """Test simple video encoding"""
    print("\nTesting simple encoding...")
    
    # Create a test video using FFmpeg
    test_input = "test_input.mp4"
    test_output = "test_output.mp4"
    
    # Generate a 1-second test video
    try:
        print("Creating test video...")
        cmd = [
            'ffmpeg',
            '-f', 'lavfi',
            '-i', 'testsrc=duration=1:size=640x480:rate=30',
            '-y',
            test_input
        ]
        result = subprocess.run(cmd, capture_output=True)
        
        if result.returncode != 0:
            print("❌ Failed to create test video")
            return
        
        print("✅ Test video created")
        
        # Test H.264 encoding
        print("Testing H.264 encoding...")
        cmd = [
            'ffmpeg',
            '-i', test_input,
            '-c:v', 'libx264',
            '-preset', 'medium',
            '-crf', '23',
            '-pix_fmt', 'yuv420p',
            '-movflags', '+faststart',
            '-y',
            test_output
        ]
        result = subprocess.run(cmd, capture_output=True)
        
        if result.returncode == 0 and os.path.exists(test_output):
            print("✅ H.264 encoding works!")
            file_size = os.path.getsize(test_output)
            print(f"   Output size: {file_size} bytes")
        else:
            print("❌ H.264 encoding failed")
        
        # Cleanup
        for f in [test_input, test_output]:
            if os.path.exists(f):
                os.remove(f)
                
    except Exception as e:
        print(f"❌ Error during test: {e}")

if __name__ == "__main__":
    print("="*60)
    print("FFmpeg Installation Test")
    print("="*60)
    
    ffmpeg_ok = check_ffmpeg()
    
    if ffmpeg_ok:
        check_codecs()
        test_simple_encode()
        print("\n" + "="*60)
        print("✅ FFmpeg is ready for use!")
        print("="*60)
    else:
        print("\n" + "="*60)
        print("⚠️  Please install FFmpeg before using video encoding")
        print("="*60)
