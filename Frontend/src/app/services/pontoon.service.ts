import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
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

  constructor(private http: HttpClient) {}

  getSpotPontoons(fishingSpotId: number): Observable<Pontoon[]> {
    return this.http.get<Pontoon[]>(`${this.apiUrl}/spot/${fishingSpotId}`);
  }

  getPontoon(id: number): Observable<Pontoon> {
    return this.http.get<Pontoon>(`${this.apiUrl}/${id}`);
  }

  createPontoon(pontoon: CreatePontoon): Observable<Pontoon> {
    return this.http.post<Pontoon>(this.apiUrl, pontoon);
  }

  updatePontoon(id: number, pontoon: UpdatePontoon): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, pontoon);
  }

  deletePontoon(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
