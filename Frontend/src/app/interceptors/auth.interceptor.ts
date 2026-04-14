import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.getToken();

  // Only attach JWT to requests going to our own API
  const isOwnApi = req.url.startsWith(environment.apiBaseUrl) || req.url.startsWith(environment.pythonServiceUrl);

  let request = req;
  if (token && isOwnApi) {
    request = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(request).pipe(
    catchError(error => {
      // If backend returns 401 Unauthorized, token is invalid/expired → logout
      if (error.status === 401) {
        // Don't logout if this is the login/register request itself
        const isAuthRequest = req.url.includes('/auth/login') || req.url.includes('/auth/register');
        if (!isAuthRequest && authService.isLoggedIn()) {
          authService.logout();
        }
      }
      return throwError(() => error);
    })
  );
};
