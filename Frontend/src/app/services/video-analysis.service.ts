import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import { VideoAnalysis, AnalysisResult, SupportedFishResponse } from '../models/video-analysis.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class VideoAnalysisService {
  private apiUrl = `${environment.apiBaseUrl}/api/videoanalysis`;
  private userAnalysesCache = new Map<number, Observable<VideoAnalysis[]>>();
  private supportedFishCache$: Observable<SupportedFishResponse> | null = null;

  constructor(private http: HttpClient) {}

  uploadVideo(videoFile: File): Observable<AnalysisResult> {
    const formData = new FormData();
    formData.append('video', videoFile);

    return this.http.post<AnalysisResult>(`${this.apiUrl}/upload`, formData).pipe(
      tap(() => this.clearUserAnalysesCache())
    );
  }

  getUserAnalyses(userId: number): Observable<VideoAnalysis[]> {
    if (!this.userAnalysesCache.has(userId)) {
      const req$ = this.http.get<VideoAnalysis[]>(`${this.apiUrl}/user/${userId}`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
      this.userAnalysesCache.set(userId, req$);
    }
    return this.userAnalysesCache.get(userId)!;
  }

  clearUserAnalysesCache(): void {
    this.userAnalysesCache.clear();
  }

  getAnalysis(id: number): Observable<VideoAnalysis> {
    return this.http.get<VideoAnalysis>(`${this.apiUrl}/${id}`);
  }

  deleteAnalysis(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.clearUserAnalysesCache())
    );
  }

  checkServiceHealth(): Observable<any> {
    return this.http.get(`${this.apiUrl}/health`);
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
