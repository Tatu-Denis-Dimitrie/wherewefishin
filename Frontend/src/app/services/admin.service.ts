import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
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

  constructor(private http: HttpClient) {}

  getStats(): Observable<AdminStats> {
    return this.http.get<AdminStats>(`${this.apiUrl}/stats`);
  }

  getUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/users`);
  }

  updateUserRole(userId: number, role: string): Observable<any> {
    return this.http.put(`${this.apiUrl}/users/${userId}/role`, { role });
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
