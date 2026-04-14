import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay, tap } from 'rxjs/operators';
import {
  SpotEmployee,
  AssignEmployeeRequest,
  VerifyQrRequest,
  QrVerificationResult
} from '../models/employee.model';
import { User } from '../models/user.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class EmployeeService {
  private apiUrl = `${environment.apiBaseUrl}/api/employees`;
  private spotEmployeeCache = new Map<number, Observable<SpotEmployee[]>>();
  private availableCache$: Observable<User[]> | null = null;

  constructor(private http: HttpClient) {}

  getSpotEmployees(spotId: number): Observable<SpotEmployee[]> {
    if (!this.spotEmployeeCache.has(spotId)) {
      this.spotEmployeeCache.set(
        spotId,
        this.http.get<SpotEmployee[]>(`${this.apiUrl}/spot/${spotId}`).pipe(
          shareReplay({ bufferSize: 1, refCount: true })
        )
      );
    }
    return this.spotEmployeeCache.get(spotId)!;
  }

  clearCache(spotId?: number): void {
    if (spotId != null) {
      this.spotEmployeeCache.delete(spotId);
    } else {
      this.spotEmployeeCache.clear();
    }
    this.availableCache$ = null;
  }

  assignEmployee(request: AssignEmployeeRequest): Observable<SpotEmployee> {
    return this.http.post<SpotEmployee>(this.apiUrl, request).pipe(
      tap(() => this.clearCache(request.fishingSpotId))
    );
  }

  removeEmployee(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.clearCache())
    );
  }

  getAvailableEmployees(): Observable<User[]> {
    if (!this.availableCache$) {
      this.availableCache$ = this.http.get<User[]>(`${this.apiUrl}/available`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }
    return this.availableCache$;
  }

  getMyAssignedSpots(): Observable<SpotEmployee[]> {
    return this.http.get<SpotEmployee[]>(`${this.apiUrl}/my-spots`);
  }

  verifyQr(request: VerifyQrRequest): Observable<QrVerificationResult> {
    return this.http.post<QrVerificationResult>(`${this.apiUrl}/verify-qr`, request);
  }
}
