import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ManagerApplication,
  RejectManagerApplication,
  UpsertManagerApplication
} from '../models/manager-application.model';

@Injectable({
  providedIn: 'root'
})
export class ManagerApplicationService {
  private apiUrl = `${environment.apiBaseUrl}/api/managerapplications`;

  constructor(private http: HttpClient) {}

  getMine(): Observable<ManagerApplication[]> {
    return this.http.get<ManagerApplication[]>(`${this.apiUrl}/mine`);
  }

  create(payload: UpsertManagerApplication): Observable<ManagerApplication> {
    return this.http.post<ManagerApplication>(this.apiUrl, payload);
  }

  update(id: number, payload: UpsertManagerApplication): Observable<ManagerApplication> {
    return this.http.put<ManagerApplication>(`${this.apiUrl}/${id}`, payload);
  }

  resubmit(id: number): Observable<ManagerApplication> {
    return this.http.post<ManagerApplication>(`${this.apiUrl}/${id}/resubmit`, {});
  }

  approve(id: number): Observable<ManagerApplication> {
    return this.http.post<ManagerApplication>(`${this.apiUrl}/${id}/approve`, {});
  }

  reject(id: number, payload: RejectManagerApplication): Observable<ManagerApplication> {
    return this.http.post<ManagerApplication>(`${this.apiUrl}/${id}/reject`, payload);
  }
}