import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
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

  constructor(private http: HttpClient) {}

  getSpotEmployees(spotId: number): Observable<SpotEmployee[]> {
    return this.http.get<SpotEmployee[]>(`${this.apiUrl}/spot/${spotId}`);
  }

  assignEmployee(request: AssignEmployeeRequest): Observable<SpotEmployee> {
    return this.http.post<SpotEmployee>(this.apiUrl, request);
  }

  removeEmployee(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getAvailableEmployees(): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/available`);
  }

  getMyAssignedSpots(): Observable<SpotEmployee[]> {
    return this.http.get<SpotEmployee[]>(`${this.apiUrl}/my-spots`);
  }

  verifyQr(request: VerifyQrRequest): Observable<QrVerificationResult> {
    return this.http.post<QrVerificationResult>(`${this.apiUrl}/verify-qr`, request);
  }
}
