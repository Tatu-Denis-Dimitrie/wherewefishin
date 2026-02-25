import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginRequest, RegisterRequest, AuthResponse } from '../models/auth.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = 'http://localhost:5033/api';
  private tokenKey = 'auth_token';
  private userIdKey = 'user_id';
  private roleKey = 'user_role';
  private usernameKey = 'user_name';

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, credentials)
      .pipe(
        tap(response => {
          localStorage.setItem(this.tokenKey, response.token);
          localStorage.setItem(this.userIdKey, response.userId.toString());
          localStorage.setItem(this.roleKey, response.role);
          localStorage.setItem(this.usernameKey, response.username);
        })
      );
  }

  register(userData: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/register`, userData)
      .pipe(
        tap(response => {
          localStorage.setItem(this.tokenKey, response.token);
          localStorage.setItem(this.userIdKey, response.userId.toString());
          localStorage.setItem(this.roleKey, response.role);
          localStorage.setItem(this.usernameKey, response.username);
        })
      );
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userIdKey);
    localStorage.removeItem(this.roleKey);
    localStorage.removeItem(this.usernameKey);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getUserId(): number | null {
    const userId = localStorage.getItem(this.userIdKey);
    return userId ? parseInt(userId, 10) : null;
  }

  getUsername(): string {
    return localStorage.getItem(this.usernameKey) || '';
  }

  getRole(): string {
    return localStorage.getItem(this.roleKey) || 'User';
  }

  isAdmin(): boolean {
    return this.getRole() === 'Admin';
  }

  isManager(): boolean {
    return this.getRole() === 'Manager';
  }

  isManagerOrAdmin(): boolean {
    return this.isAdmin() || this.isManager();
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }
}
