import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FishingSpot {
  id: number;
  name: string;
  description?: string;
  latitude: number;
  longitude: number;
  imageUrl?: string;
  pricePerHour: number;
  userId: number;
  managerId?: number;
  managerName?: string;
  createdAt: Date;
}

export interface CreateFishingSpot {
  name: string;
  description?: string;
  latitude: number;
  longitude: number;
  pricePerHour?: number;
  userId: number;
  managerId?: number;
}

@Injectable({
  providedIn: 'root'
})
export class FishingSpotService {
  private apiUrl = 'http://localhost:5033/api/fishingspots';

  constructor(private http: HttpClient) {}

  getAll(): Observable<FishingSpot[]> {
    return this.http.get<FishingSpot[]>(this.apiUrl);
  }

  getById(id: number): Observable<FishingSpot> {
    return this.http.get<FishingSpot>(`${this.apiUrl}/${id}`);
  }

  create(spot: CreateFishingSpot): Observable<FishingSpot> {
    return this.http.post<FishingSpot>(this.apiUrl, spot);
  }

  update(id: number, spot: Partial<FishingSpot>): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, spot);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
