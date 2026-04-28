import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import { VideoAnalysis, AnalysisResult, SupportedFishResponse, ImageAnalysisResult, ImageAnalysisSummary, PagedResponse, VideoAnalysisOverview } from '../models/video-analysis.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class VideoAnalysisService {
  private apiUrl = `${environment.apiBaseUrl}/api/videoanalysis`;
  private imageApiUrl = `${environment.apiBaseUrl}/api/imageanalysis`;
  private userAnalysesCache = new Map<string, Observable<PagedResponse<VideoAnalysis>>>();
  private userAnalysesOverviewCache = new Map<number, Observable<VideoAnalysisOverview>>();
  private userImageAnalysesCache = new Map<string, Observable<PagedResponse<ImageAnalysisSummary>>>();
  private supportedFishCache$: Observable<SupportedFishResponse> | null = null;

  constructor(private http: HttpClient) {}

  uploadVideo(videoFile: File): Observable<AnalysisResult> {
    const formData = new FormData();
    formData.append('video', videoFile);

    return this.http.post<AnalysisResult>(`${this.apiUrl}/upload`, formData).pipe(
      tap(() => this.clearUserAnalysesCache())
    );
  }

  getUserAnalyses(userId: number, page = 1, pageSize = 10): Observable<PagedResponse<VideoAnalysis>> {
    const cacheKey = `${userId}:${page}:${pageSize}`;

    if (!this.userAnalysesCache.has(cacheKey)) {
      const req$ = this.http.get<PagedResponse<VideoAnalysis>>(
        `${this.apiUrl}/user/${userId}?page=${page}&pageSize=${pageSize}`
      ).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
      this.userAnalysesCache.set(cacheKey, req$);
    }
    return this.userAnalysesCache.get(cacheKey)!;
  }

  getUserAnalysesOverview(userId: number): Observable<VideoAnalysisOverview> {
    if (!this.userAnalysesOverviewCache.has(userId)) {
      const req$ = this.http.get<VideoAnalysisOverview>(`${this.apiUrl}/user/${userId}/overview`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
      this.userAnalysesOverviewCache.set(userId, req$);
    }

    return this.userAnalysesOverviewCache.get(userId)!;
  }

  clearUserAnalysesCache(): void {
    this.userAnalysesCache.clear();
    this.userAnalysesOverviewCache.clear();
  }

  getAnalysis(id: number): Observable<VideoAnalysis> {
    return this.http.get<VideoAnalysis>(`${this.apiUrl}/${id}`);
  }

  deleteAnalysis(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.clearUserAnalysesCache())
    );
  }

  analyzeImage(imageFile: File): Observable<ImageAnalysisResult> {
    const formData = new FormData();
    formData.append('image', imageFile);
    return this.http.post<ImageAnalysisResult>(`${this.imageApiUrl}/analyze`, formData).pipe(
      tap(() => this.clearUserImageAnalysesCache())
    );
  }

  getUserImageAnalyses(userId: number, page = 1, pageSize = 10): Observable<PagedResponse<ImageAnalysisSummary>> {
    const cacheKey = `${userId}:${page}:${pageSize}`;

    if (!this.userImageAnalysesCache.has(cacheKey)) {
      const req$ = this.http.get<PagedResponse<ImageAnalysisSummary>>(
        `${this.imageApiUrl}/user/${userId}?page=${page}&pageSize=${pageSize}`
      ).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
      this.userImageAnalysesCache.set(cacheKey, req$);
    }
    return this.userImageAnalysesCache.get(cacheKey)!;
  }

  clearUserImageAnalysesCache(): void {
    this.userImageAnalysesCache.clear();
  }

  deleteImageAnalysis(id: number): Observable<void> {
    return this.http.delete<void>(`${this.imageApiUrl}/${id}`).pipe(
      tap(() => this.clearUserImageAnalysesCache())
    );
  }

  checkServiceHealth(): Observable<any> {
    return this.http.get(`${this.apiUrl}/health`);
  }

  checkBackendHealth(): Observable<any> {
    return this.http.get(`${environment.apiBaseUrl}/api/health`);
  }

  getSupportedFish(): Observable<SupportedFishResponse> {
    if (!this.supportedFishCache$) {
      this.supportedFishCache$ = this.http.get<SupportedFishResponse>(`${this.apiUrl}/supported-fish`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }
    return this.supportedFishCache$;
  }
}
