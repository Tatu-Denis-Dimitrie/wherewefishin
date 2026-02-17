import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { AuthService } from '../../services/auth.service';
import { VideoAnalysis } from '../../models/video-analysis.model';

@Component({
  selector: 'app-fish-recognition',
  imports: [CommonModule],
  templateUrl: './fish-recognition.html',
  styleUrl: './fish-recognition.css'
})
export class FishRecognition implements OnInit, OnDestroy {
  analyses: VideoAnalysis[] = [];
  selectedFile: File | null = null;
  videoPreviewUrl: string | null = null;
  uploading = false;
  loading = false;
  error = '';
  successMessage = '';
  uploadProgress = 0;
  serviceHealthy = false;
  supportedFish: string[] = [];

  constructor(
    private videoAnalysisService: VideoAnalysisService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.checkServiceHealth();
    this.loadSupportedFish();
    this.loadAnalyses();
  }

  ngOnDestroy(): void {
    // Cleanup video preview URL to avoid memory leaks
    this.cleanupPreviewUrl();
  }

  private cleanupPreviewUrl(): void {
    if (this.videoPreviewUrl) {
      URL.revokeObjectURL(this.videoPreviewUrl);
      this.videoPreviewUrl = null;
    }
  }

  checkServiceHealth(): void {
    this.videoAnalysisService.checkServiceHealth().subscribe({
      next: () => {
        this.serviceHealthy = true;
      },
      error: () => {
        this.serviceHealthy = false;
        this.error = 'Fish recognition service is unavailable';
      }
    });
  }

  loadSupportedFish(): void {
    this.videoAnalysisService.getSupportedFish().subscribe({
      next: (response) => {
        this.supportedFish = response.fishTypes;
      },
      error: (err) => {
        console.error('Error loading supported fish:', err);
      }
    });
  }

  loadAnalyses(): void {
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.loading = true;
    this.videoAnalysisService.getUserAnalyses(userId).subscribe({
      next: (analyses) => {
        this.analyses = analyses;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load analyses';
        this.loading = false;
        console.error('Error loading analyses:', err);
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.handleFile(input.files[0]);
    }
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.handleFile(event.dataTransfer.files[0]);
    }
  }

  private handleFile(file: File): void {
    const allowedTypes = ['video/mp4', 'video/avi', 'video/quicktime', 'video/x-matroska'];
    if (!allowedTypes.includes(file.type)) {
      this.error = 'Invalid file type. Please upload MP4, AVI, MOV, or MKV';
      return;
    }

    if (file.size > 100 * 1024 * 1024) {
      this.error = 'File size exceeds 100MB limit';
      return;
    }

    this.cleanupPreviewUrl();
    this.selectedFile = file;
    this.error = '';
    this.videoPreviewUrl = URL.createObjectURL(file);
  }

  uploadVideo(): void {
    if (!this.selectedFile) {
      this.error = 'Please select a video file';
      return;
    }

    if (!this.serviceHealthy) {
      this.error = 'Fish recognition service is unavailable. Please try again later.';
      return;
    }

    const userId = this.authService.getUserId();
    if (!userId) {
      this.error = 'User not authenticated';
      return;
    }

    this.uploading = true;
    this.error = '';
    this.uploadProgress = 0;

    // Simulare progres
    const progressInterval = setInterval(() => {
      if (this.uploadProgress < 90) {
        this.uploadProgress += 10;
      }
    }, 500);

    this.videoAnalysisService.uploadVideo(this.selectedFile, userId).subscribe({
      next: (result) => {
        clearInterval(progressInterval);
        this.uploadProgress = 100;
        
        if (result.success) {
          this.successMessage = 'Video analyzed successfully!';
          this.selectedFile = null;
          this.uploading = false;
          
          // Cleanup preview
          this.cleanupPreviewUrl();
          
          // Reset file input
          const fileInput = document.getElementById('videoFile') as HTMLInputElement;
          if (fileInput) fileInput.value = '';
          
          setTimeout(() => {
            this.successMessage = '';
            this.loadAnalyses();
          }, 2000);
        } else {
          this.error = result.error || 'Analysis failed';
          this.uploading = false;
        }
      },
      error: (err) => {
        clearInterval(progressInterval);
        this.error = 'Failed to upload and analyze video';
        this.uploading = false;
        console.error('Error uploading video:', err);
      }
    });
  }

  deleteAnalysis(id: number): void {
    if (!confirm('Are you sure you want to delete this analysis?')) {
      return;
    }

    this.videoAnalysisService.deleteAnalysis(id).subscribe({
      next: () => {
        this.successMessage = 'Analysis deleted successfully';
        this.loadAnalyses();
        setTimeout(() => {
          this.successMessage = '';
        }, 2000);
      },
      error: (err) => {
        this.error = 'Failed to delete analysis';
        console.error('Error deleting analysis:', err);
      }
    });
  }

  getFishCountsArray(fishCounts?: { [key: string]: number }): Array<{ type: string, count: number }> {
    if (!fishCounts) return [];
    return Object.entries(fishCounts).map(([type, count]) => ({ type, count }));
  }

  formatDuration(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${minutes}:${secs.toString().padStart(2, '0')}`;
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleString('ro-RO', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'completed': return 'status-completed';
      case 'processing': return 'status-processing';
      case 'failed': return 'status-failed';
      default: return 'status-pending';
    }
  }

  getVideoUrl(url: string | undefined): string {
    if (!url) return '';
    // If URL already starts with http, use it directly (backend returns full URL)
    if (url.startsWith('http')) return url;
    // Otherwise prepend the backend URL
    return 'http://localhost:5033' + url;
  }
}
