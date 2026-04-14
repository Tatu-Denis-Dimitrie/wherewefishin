import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface Pontoon {
  id: number;
  fishingSpotId: number;
  name: string;
  southWestLat: number;
  southWestLng: number;
  northEastLat: number;
  northEastLng: number;
  color?: string;
  coordinates?: string;
  createdAt: Date;
}

export interface CreatePontoon {
  fishingSpotId: number;
  name: string;
  southWestLat: number;
  southWestLng: number;
  northEastLat: number;
  northEastLng: number;
  color?: string;
  coordinates?: string;
}

export interface UpdatePontoon {
  name?: string;
  southWestLat?: number;
  southWestLng?: number;
  northEastLat?: number;
  northEastLng?: number;
  color?: string;
  coordinates?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PontoonService {
  private apiUrl = `${environment.apiBaseUrl}/api/pontoons`;
  private spotCache = new Map<number, Observable<Pontoon[]>>();

  constructor(private http: HttpClient) {}

  getSpotPontoons(fishingSpotId: number): Observable<Pontoon[]> {
    if (!this.spotCache.has(fishingSpotId)) {
      this.spotCache.set(
        fishingSpotId,
        this.http.get<Pontoon[]>(`${this.apiUrl}/spot/${fishingSpotId}`).pipe(
          shareReplay({ bufferSize: 1, refCount: true })
        )
      );
    }
    return this.spotCache.get(fishingSpotId)!;
  }

  clearCache(fishingSpotId?: number): void {
    if (fishingSpotId != null) {
      this.spotCache.delete(fishingSpotId);
    } else {
      this.spotCache.clear();
    }
  }

  getPontoon(id: number): Observable<Pontoon> {
    return this.http.get<Pontoon>(`${this.apiUrl}/${id}`);
  }

  createPontoon(pontoon: CreatePontoon): Observable<Pontoon> {
    return this.http.post<Pontoon>(this.apiUrl, pontoon).pipe(
      tap(() => this.clearCache(pontoon.fishingSpotId))
    );
  }

  updatePontoon(id: number, pontoon: UpdatePontoon): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, pontoon).pipe(
      tap(() => this.clearCache())
    );
  }

  deletePontoon(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.clearCache())
    );
  }
}
