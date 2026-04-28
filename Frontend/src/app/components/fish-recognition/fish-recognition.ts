import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { AuthService } from '../../services/auth.service';
import { VideoAnalysis } from '../../models/video-analysis.model';
import { environment } from '../../../environments/environment';
import { AppIcon } from '../../shared/icons/app-icon';

type PaginationItem = number | 'ellipsis-left' | 'ellipsis-right';

@Component({
  selector: 'app-fish-recognition',
  imports: [CommonModule, AppIcon],
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
  currentPage = 1;
  readonly pageSize = 10;
  totalItems = 0;
  totalPages = 0;
  hasPreviousPage = false;
  hasNextPage = false;
  private pollingInterval: ReturnType<typeof setInterval> | null = null;
  private progressInterval: ReturnType<typeof setInterval> | null = null;
  private progressStartTime = 0;

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
    this.cleanupPreviewUrl();
    this.stopPolling();
    this.stopProgress();
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

  loadAnalyses(page = this.currentPage): void {
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.loading = true;
    this.videoAnalysisService.getUserAnalyses(userId, page, this.pageSize).subscribe({
      next: (response) => {
        if (!response.items.length && response.totalPages > 0 && response.page > response.totalPages) {
          this.loadAnalyses(response.totalPages);
          return;
        }

        this.currentPage = response.page;
        this.analyses = response.items;
        this.totalItems = response.totalItems;
        this.totalPages = response.totalPages;
        this.hasPreviousPage = response.hasPreviousPage;
        this.hasNextPage = response.hasNextPage;
        this.loading = false;
        this.checkForProcessingAnalyses();
      },
      error: (err) => {
        this.error = 'Failed to load analyses';
        this.loading = false;
        console.error('Error loading analyses:', err);
      }
    });
  }

  private checkForProcessingAnalyses(): void {
    const hasProcessing = this.analyses.some(a => a.status.toLowerCase() === 'processing');
    if (hasProcessing && !this.pollingInterval) {
      this.startPolling();
    } else if (!hasProcessing && this.pollingInterval) {
      this.stopPolling();
    }
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollingInterval = setInterval(() => {
      this.videoAnalysisService.clearUserAnalysesCache();
      const userId = this.authService.getUserId();
      if (!userId) return;
      this.videoAnalysisService.getUserAnalyses(userId, this.currentPage, this.pageSize).subscribe({
        next: (response) => {
          const wasProcessing = this.analyses.some(a => a.status.toLowerCase() === 'processing');
          this.currentPage = response.page;
          this.analyses = response.items;
          this.totalItems = response.totalItems;
          this.totalPages = response.totalPages;
          this.hasPreviousPage = response.hasPreviousPage;
          this.hasNextPage = response.hasNextPage;
          const stillProcessing = response.items.some(a => a.status.toLowerCase() === 'processing');
          if (wasProcessing && !stillProcessing) {
            this.stopPolling();
          }
        }
      });
    }, 5000);
  }

  private stopPolling(): void {
    if (this.pollingInterval) {
      clearInterval(this.pollingInterval);
      this.pollingInterval = null;
    }
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
    const allowedExtensions = ['mp4', 'avi', 'mov', 'mkv'];
    const fileExtension = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!allowedExtensions.includes(fileExtension)) {
      this.error = 'Invalid file type. Please upload MP4, AVI, MOV, or MKV';
      return;
    }

    if (file.size > 150 * 1024 * 1024) {
      this.error = 'File size exceeds 150MB limit';
      return;
    }

    this.cleanupPreviewUrl();
    this.selectedFile = file;
    this.error = '';
    this.videoPreviewUrl = URL.createObjectURL(file);
  }

  clearSelection(): void {
    this.selectedFile = null;
    this.error = '';
    this.successMessage = '';
    this.uploadProgress = 0;
    this.cleanupPreviewUrl();
    
    // Reset file input
    const fileInput = document.getElementById('videoFile') as HTMLInputElement;
    if (fileInput) fileInput.value = '';
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

    this.uploading = true;
    this.error = '';
    this.uploadProgress = 0;
    this.progressStartTime = Date.now();

    this.startAsymptoticProgress();

    // After 3s, reload analyses to pick up the "Processing" record and start polling
    setTimeout(() => {
      this.videoAnalysisService.clearUserAnalysesCache();
      this.loadAnalyses(1);
    }, 3000);

    this.videoAnalysisService.uploadVideo(this.selectedFile).subscribe({
      next: (result) => {
        this.stopProgress();
        this.uploadProgress = 100;
        
        if (result.success) {
          this.successMessage = 'Video analyzed successfully!';
          this.selectedFile = null;
          this.uploading = false;
          
          this.cleanupPreviewUrl();
          
          const fileInput = document.getElementById('videoFile') as HTMLInputElement;
          if (fileInput) fileInput.value = '';
          
          this.videoAnalysisService.clearUserAnalysesCache();
          this.loadAnalyses(1);
          setTimeout(() => {
            this.successMessage = '';
          }, 3000);
        } else {
          this.error = result.error || 'Analysis failed';
          this.uploading = false;
        }
      },
      error: (err) => {
        this.stopProgress();
        this.uploading = false;
        console.error('Error uploading video:', err);

        // The backend may have completed even if the HTTP call timed out.
        // Reload analyses to check.
        this.videoAnalysisService.clearUserAnalysesCache();
        this.loadAnalyses(1);
      }
    });
  }

  private startAsymptoticProgress(): void {
    this.stopProgress();
    this.progressInterval = setInterval(() => {
      const elapsed = (Date.now() - this.progressStartTime) / 1000;
      // Asymptotic curve: fast at start, slows down approaching 95%
      // ~50% at 5s, ~75% at 15s, ~85% at 30s, ~92% at 60s, ~95% at 120s
      this.uploadProgress = Math.min(95, Math.round(95 * (1 - Math.exp(-elapsed / 15))));
    }, 300);
  }

  private stopProgress(): void {
    if (this.progressInterval) {
      clearInterval(this.progressInterval);
      this.progressInterval = null;
    }
  }

  deleteAnalysis(id: number): void {
    if (!confirm('Are you sure you want to delete this analysis?')) {
      return;
    }

    this.videoAnalysisService.deleteAnalysis(id).subscribe({
      next: () => {
        this.successMessage = 'Analysis deleted successfully';
        this.videoAnalysisService.clearUserAnalysesCache();
        const targetPage = this.analyses.length === 1 && this.currentPage > 1
          ? this.currentPage - 1
          : this.currentPage;
        this.loadAnalyses(targetPage);
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

  get pageNumbers(): number[] {
    return Array.from({ length: this.totalPages }, (_, index) => index + 1);
  }

  get visiblePageItems(): PaginationItem[] {
    if (this.totalPages <= 5) {
      return this.pageNumbers;
    }

    const items: PaginationItem[] = [1];
    const startPage = Math.max(2, this.currentPage - 1);
    const endPage = Math.min(this.totalPages - 1, this.currentPage + 1);

    if (startPage > 2) {
      items.push('ellipsis-left');
    }

    for (let page = startPage; page <= endPage; page++) {
      items.push(page);
    }

    if (endPage < this.totalPages - 1) {
      items.push('ellipsis-right');
    }

    items.push(this.totalPages);
    return items;
  }

  get pageStartItem(): number {
    if (!this.totalItems) {
      return 0;
    }

    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get pageEndItem(): number {
    return this.pageStartItem + this.analyses.length - 1;
  }

  isPageNumber(item: PaginationItem): item is number {
    return typeof item === 'number';
  }

  changePage(page: number): void {
    const nextPage = Math.min(Math.max(page, 1), Math.max(this.totalPages, 1));
    if (nextPage === this.currentPage) {
      return;
    }

    this.loadAnalyses(nextPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
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
    return new Date(date).toLocaleString('en-US', {
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
    // Already an absolute URL — return as-is
    if (url.startsWith('http')) return url;

    const path = url.startsWith('/') ? url : '/' + url;

    // Processed video outputs are served directly by the Python service (local)
    // or via nginx /outputs/ proxy (Docker). Never route through the .NET backend.
    if (path.startsWith('/outputs/')) {
      return environment.pythonServiceUrl + path;
    }

    // Uploaded videos and everything else go through the .NET backend
    return environment.apiBaseUrl + path;
  }
}
