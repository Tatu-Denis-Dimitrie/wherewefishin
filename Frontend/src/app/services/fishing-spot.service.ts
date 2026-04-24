import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { FishingSpot, CreateFishingSpot, UpdateFishingSpot } from '../models/fishing-spot.model';

@Injectable({
  providedIn: 'root'
})
export class FishingSpotService {
  private apiUrl = `${environment.apiBaseUrl}/api/fishingspots`;
  private allCache$: Observable<FishingSpot[]> | null = null;
  private managedCache$: Observable<FishingSpot[]> | null = null;

  constructor(private http: HttpClient) {}

  getAll(): Observable<FishingSpot[]> {
    if (!this.allCache$) {
      this.allCache$ = this.http.get<FishingSpot[]>(this.apiUrl).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }
    return this.allCache$;
  }

  getManaged(): Observable<FishingSpot[]> {
    if (!this.managedCache$) {
      this.managedCache$ = this.http.get<FishingSpot[]>(`${this.apiUrl}/managed`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }

    return this.managedCache$;
  }

  clearCache(): void {
    this.allCache$ = null;
    this.managedCache$ = null;
  }

  getById(id: number): Observable<FishingSpot> {
    return this.http.get<FishingSpot>(`${this.apiUrl}/${id}`);
  }

  create(spot: CreateFishingSpot): Observable<FishingSpot> {
    return this.http.post<FishingSpot>(this.apiUrl, spot).pipe(tap(() => this.clearCache()));
  }

  update(id: number, spot: UpdateFishingSpot): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, spot).pipe(tap(() => this.clearCache()));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(tap(() => this.clearCache()));
  }
}
