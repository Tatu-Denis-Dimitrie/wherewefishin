import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { VideoAnalysis, AnalysisResult, SupportedFishResponse } from '../models/video-analysis.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class VideoAnalysisService {
  private apiUrl = `${environment.apiBaseUrl}/api/videoanalysis`;

  constructor(private http: HttpClient) {}

  uploadVideo(videoFile: File): Observable<AnalysisResult> {
    const formData = new FormData();
    formData.append('video', videoFile);

    return this.http.post<AnalysisResult>(`${this.apiUrl}/upload`, formData);
  }

  getUserAnalyses(userId: number): Observable<VideoAnalysis[]> {
    return this.http.get<VideoAnalysis[]>(`${this.apiUrl}/user/${userId}`);
  }

  getAnalysis(id: number): Observable<VideoAnalysis> {
    return this.http.get<VideoAnalysis>(`${this.apiUrl}/${id}`);
  }

  deleteAnalysis(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  checkServiceHealth(): Observable<any> {
    return this.http.get(`${this.apiUrl}/health`);
  }

  getSupportedFish(): Observable<SupportedFishResponse> {
    return this.http.get<SupportedFishResponse>(`${this.apiUrl}/supported-fish`);
  }
}
