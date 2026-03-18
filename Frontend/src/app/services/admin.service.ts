import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay } from 'rxjs/operators';
import { User } from '../models/user.model';
import { FishingSpot } from './fishing-spot.service';
import { environment } from '../../environments/environment';

export interface AdminStats {
  totalUsers: number;
  totalManagers: number;
  totalAdmins: number;
  totalAnalyses: number;
  completedAnalyses: number;
  failedAnalyses: number;
  totalSpots: number;
}

export interface UpdateFishingSpot {
  name?: string;
  description?: string;
  latitude?: number;
  longitude?: number;
  imageUrl?: string;
  pricePerHour?: number;
  managerId?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = `${environment.apiBaseUrl}/api/admin`;
  private statsCache$: Observable<AdminStats> | null = null;

  constructor(private http: HttpClient) {}

  getStats(): Observable<AdminStats> {
    if (!this.statsCache$) {
      this.statsCache$ = this.http.get<AdminStats>(`${this.apiUrl}/stats`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }
    return this.statsCache$;
  }

  clearStatsCache(): void {
    this.statsCache$ = null;
  }

  getUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/users`);
  }

  updateUserRole(userId: number, role: string): Observable<any> {
    return this.http.put(`${this.apiUrl}/users/${userId}/role`, { role });
  }

  toggleUserStatus(userId: number, enable: boolean): Observable<any> {
    return this.http.put(`${this.apiUrl}/users/${userId}/status`, { enable });
  }

  deleteUser(userId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/users/${userId}`);
  }

  getFishingSpots(): Observable<FishingSpot[]> {
    return this.http.get<FishingSpot[]>(`${this.apiUrl}/fishing-spots`);
  }

  updateFishingSpot(spotId: number, updates: UpdateFishingSpot): Observable<any> {
    return this.http.put(`${this.apiUrl}/fishing-spots/${spotId}`, updates);
  }

  deleteFishingSpot(spotId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/fishing-spots/${spotId}`);
  }
}
