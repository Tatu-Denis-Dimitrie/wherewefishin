# =============================================
# WhereWeFishin Fish Recognition Service
# PyTorch 2.5.1 + CUDA 12.4 (GPU support)
# =============================================

# Base image already includes Python 3.11, torch 2.5.1, torchvision, CUDA 12.4, cuDNN 9
FROM pytorch/pytorch:2.5.1-cuda12.4-cudnn9-runtime

WORKDIR /app

# Install system dependencies for OpenCV and FFmpeg
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies - skip torch (already in base image with CUDA)
COPY requirements.txt .
RUN grep -vE '^(torch|torchvision|torchaudio)' requirements.txt > /tmp/requirements-docker.txt && \
    pip install --no-cache-dir -r /tmp/requirements-docker.txt && \
    rm /tmp/requirements-docker.txt

# Copy application code
COPY . .

# Create necessary directories
RUN mkdir -p uploads outputs

# Expose port
EXPOSE 5001

# Set environment variables
ENV FLASK_ENV=production
ENV PYTHONUNBUFFERED=1

CMD ["python", "app.py"]
