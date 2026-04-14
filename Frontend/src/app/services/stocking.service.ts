import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { FishStocking, CreateFishStocking } from '../models/stocking.model';
import { SpotStatistics } from '../models/fishing-spot.model';

@Injectable({
  providedIn: 'root'
})
export class StockingService {
  private apiUrl = `${environment.apiBaseUrl}/api/fishingspots`;

  constructor(private http: HttpClient) {}

  getStockings(spotId: number): Observable<FishStocking[]> {
    return this.http.get<FishStocking[]>(`${this.apiUrl}/${spotId}/stockings`);
  }

  createStocking(spotId: number, stocking: CreateFishStocking): Observable<FishStocking> {
    return this.http.post<FishStocking>(`${this.apiUrl}/${spotId}/stockings`, stocking);
  }

  updateStocking(spotId: number, id: number, stocking: Partial<CreateFishStocking>): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${spotId}/stockings/${id}`, stocking);
  }

  deleteStocking(spotId: number, id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${spotId}/stockings/${id}`);
  }

  getStatistics(spotId: number): Observable<SpotStatistics> {
    return this.http.get<SpotStatistics>(`${this.apiUrl}/${spotId}/statistics`);
  }
}
