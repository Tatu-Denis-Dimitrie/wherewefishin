import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { VideoAnalysisService } from '../../services/video-analysis.service';
import { AuthService } from '../../services/auth.service';
import { ImageAnalysisResult, ImageAnalysisSummary, ClassProbability, ImageDetection } from '../../models/video-analysis.model';
import { AppIcon } from '../../shared/icons/app-icon';
import { environment } from '../../../environments/environment';

type PaginationItem = number | 'ellipsis-left' | 'ellipsis-right';

@Component({
  selector: 'app-image-classification',
  imports: [CommonModule, AppIcon],
  templateUrl: './image-classification.html',
  styleUrl: './image-classification.css'
})
export class ImageClassification implements OnInit, OnDestroy {
  analyses: ImageAnalysisSummary[] = [];
  selectedFile: File | null = null;
  previewUrl: string | null = null;
  analyzing = false;
  loading = false;
  error = '';
  successMessage = '';
  result: ImageAnalysisResult | null = null;
  serviceHealthy = false;
  supportedFish: string[] = [];
  currentPage = 1;
  readonly pageSize = 10;
  totalItems = 0;
  totalPages = 0;
  hasPreviousPage = false;
  hasNextPage = false;

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
    this.revokePreview();
  }

  private revokePreview(): void {
    if (this.previewUrl) {
      URL.revokeObjectURL(this.previewUrl);
      this.previewUrl = null;
    }
  }

  checkServiceHealth(): void {
    this.videoAnalysisService.checkServiceHealth().subscribe({
      next: () => { this.serviceHealthy = true; },
      error: () => { this.serviceHealthy = false; }
    });
  }

  loadSupportedFish(): void {
    this.videoAnalysisService.getSupportedFish().subscribe({
      next: (response) => { this.supportedFish = response.fishTypes; },
      error: () => {}
    });
  }

  loadAnalyses(page = this.currentPage): void {
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.loading = true;
    this.videoAnalysisService.getUserImageAnalyses(userId, page, this.pageSize).subscribe({
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
      },
      error: () => { this.loading = false; }
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
    const allowed = ['jpg', 'jpeg', 'png', 'webp'];
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!allowed.includes(ext)) {
      this.error = 'Invalid file type. Please upload JPG, JPEG, PNG or WEBP';
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.error = 'File size exceeds 10MB limit';
      return;
    }
    this.revokePreview();
    this.selectedFile = file;
    this.result = null;
    this.error = '';
    this.previewUrl = URL.createObjectURL(file);
  }

  clearSelection(): void {
    this.selectedFile = null;
    this.result = null;
    this.error = '';
    this.revokePreview();
    const input = document.getElementById('imageFile') as HTMLInputElement;
    if (input) input.value = '';
  }

  private clearFile(): void {
    this.selectedFile = null;
    this.revokePreview();
    const input = document.getElementById('imageFile') as HTMLInputElement;
    if (input) input.value = '';
  }

  analyzeImage(): void {
    if (!this.selectedFile) {
      this.error = 'Please select an image file';
      return;
    }
    if (!this.serviceHealthy) {
      this.error = 'Fish recognition service is unavailable. Please try again later.';
      return;
    }
    this.analyzing = true;
    this.error = '';
    this.result = null;

    this.videoAnalysisService.analyzeImage(this.selectedFile).subscribe({
      next: (result) => {
        this.analyzing = false;
        if (result.success) {
          this.result = result;
          this.clearFile();
          this.successMessage = 'Image analyzed successfully!';
          setTimeout(() => { this.successMessage = ''; }, 4000);
          this.videoAnalysisService.clearUserImageAnalysesCache();
          this.loadAnalyses(1);
        } else {
          this.error = result.error || 'Analysis failed';
        }
      },
      error: (err) => {
        this.analyzing = false;
        this.error = err?.error?.error || 'Failed to analyze image. Please try again.';
      }
    });
  }

  deleteAnalysis(id: number): void {
    this.videoAnalysisService.deleteImageAnalysis(id).subscribe({
      next: () => {
        this.successMessage = 'Analysis deleted.';
        setTimeout(() => { this.successMessage = ''; }, 3000);
        this.videoAnalysisService.clearUserImageAnalysesCache();
        const targetPage = this.analyses.length === 1 && this.currentPage > 1
          ? this.currentPage - 1
          : this.currentPage;
        this.loadAnalyses(targetPage);
      },
      error: () => { this.error = 'Failed to delete analysis.'; }
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

  getImageUrl(url: string): string {
    if (!url) {
      return '';
    }

    if (url.startsWith('http')) {
      return url;
    }

    const path = url.startsWith('/') ? url : `/${url}`;

    if (path.startsWith('/outputs/')) {
      return environment.pythonServiceUrl + path;
    }

    return environment.apiBaseUrl + path;
  }

  getConfidencePercent(confidence: number): string {
    return (confidence * 100).toFixed(1) + '%';
  }

  getDisplayClassProbabilities(det: ImageDetection): ClassProbability[] {
    if (!det.classProbabilities?.length) {
      return [];
    }

    const detectedFish = det.fishType.trim().toLowerCase();
    const alternativeProbs = det.classProbabilities.filter(cp => cp.fishType.trim().toLowerCase() !== detectedFish);
    if (!alternativeProbs.length) {
      return [];
    }

    const remainder = Math.max(0, 1 - det.confidence);
    if (!remainder) {
      return [];
    }

    const total = alternativeProbs.reduce((sum, cp) => sum + cp.confidence, 0);
    if (!total) {
      return [];
    }

    return alternativeProbs
      .map(cp => ({
        fishType: cp.fishType,
        confidence: (cp.confidence / total) * remainder
      }))
      .filter(cp => cp.confidence >= 0.005)
      .sort((a, b) => b.confidence - a.confidence);
  }

  probPercent(cp: ClassProbability): string {
    return (cp.confidence * 100).toFixed(2) + '%';
  }

  probBarWidth(cp: ClassProbability): number {
    return cp.confidence * 100;
  }

  formatDate(date: Date | string): string {
    return new Date(date).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  get resultImageSrc(): string {
    if (!this.result?.processedImageUrl) return '';
    return this.getImageUrl(this.result.processedImageUrl);
  }
}

