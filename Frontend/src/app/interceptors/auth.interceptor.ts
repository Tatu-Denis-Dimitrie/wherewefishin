import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

const INTERNAL_API_PREFIXES = ['/api', 'api/'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  // Only attach JWT to requests going to our own API
  const isOwnApi = isInternalApiRequest(req.url);

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

function isInternalApiRequest(url: string): boolean {
  const configuredBaseUrls = [environment.apiBaseUrl, environment.pythonServiceUrl]
    .map(baseUrl => baseUrl.trim())
    .filter((baseUrl): baseUrl is string => baseUrl.length > 0);

  if (configuredBaseUrls.some(baseUrl => url.startsWith(baseUrl))) {
    return true;
  }

  if (isRelativeUrl(url)) {
    return INTERNAL_API_PREFIXES.some(prefix => url.startsWith(prefix));
  }

  try {
    const parsedUrl = new URL(url);
    if (parsedUrl.origin !== window.location.origin) {
      return false;
    }

    return INTERNAL_API_PREFIXES.some(prefix => parsedUrl.pathname.startsWith(`/${prefix.replace(/^\//, '')}`));
  } catch {
    return false;
  }
}

function isRelativeUrl(url: string): boolean {
  return !/^[a-z][a-z0-9+.-]*:\/\//i.test(url) && !url.startsWith('//');
}
