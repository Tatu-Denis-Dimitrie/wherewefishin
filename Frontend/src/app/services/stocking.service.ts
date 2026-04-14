import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { FishStocking, CreateFishStocking } from '../models/stocking.model';
import { SpotStatistics } from '../models/fishing-spot.model';

@Injectable({
  providedIn: 'root'
})
export class StockingService {
  private apiUrl = `${environment.apiBaseUrl}/api/fishingspots`;
  private stockingCache = new Map<number, Observable<FishStocking[]>>();

  constructor(private http: HttpClient) {}

  getStockings(spotId: number): Observable<FishStocking[]> {
    if (!this.stockingCache.has(spotId)) {
      this.stockingCache.set(
        spotId,
        this.http.get<FishStocking[]>(`${this.apiUrl}/${spotId}/stockings`).pipe(
          shareReplay({ bufferSize: 1, refCount: true })
        )
      );
    }
    return this.stockingCache.get(spotId)!;
  }

  clearCache(spotId?: number): void {
    if (spotId != null) {
      this.stockingCache.delete(spotId);
    } else {
      this.stockingCache.clear();
    }
  }

  createStocking(spotId: number, stocking: CreateFishStocking): Observable<FishStocking> {
    return this.http.post<FishStocking>(`${this.apiUrl}/${spotId}/stockings`, stocking).pipe(
      tap(() => this.clearCache(spotId))
    );
  }

  updateStocking(spotId: number, id: number, stocking: Partial<CreateFishStocking>): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${spotId}/stockings/${id}`, stocking).pipe(
      tap(() => this.clearCache(spotId))
    );
  }

  deleteStocking(spotId: number, id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${spotId}/stockings/${id}`).pipe(
      tap(() => this.clearCache(spotId))
    );
  }

  getStatistics(spotId: number): Observable<SpotStatistics> {
    return this.http.get<SpotStatistics>(`${this.apiUrl}/${spotId}/statistics`);
  }
}
