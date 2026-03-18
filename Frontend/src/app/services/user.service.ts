import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { shareReplay } from 'rxjs/operators';
import { User, UpdateUser } from '../models/user.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private apiUrl = `${environment.apiBaseUrl}/api/users`;
  private managersCache$: Observable<User[]> | null = null;

  constructor(private http: HttpClient) {}

  getUser(id: number): Observable<User> {
    return this.http.get<User>(`${this.apiUrl}/${id}`);
  }

  getManagers(): Observable<User[]> {
    if (!this.managersCache$) {
      this.managersCache$ = this.http.get<User[]>(`${this.apiUrl}/managers`).pipe(
        shareReplay({ bufferSize: 1, refCount: true })
      );
    }
    return this.managersCache$;
  }

  clearCache(): void {
    this.managersCache$ = null;
  }

  updateUser(id: number, userData: UpdateUser): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, userData);
  }

  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
