import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { retry, throwError, timer } from 'rxjs';

const MAX_RETRIES = 3;
const INITIAL_DELAY_MS = 1500;
const RETRYABLE_STATUS_CODES = [0, 502, 503, 504];

export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error, retryCount) => {
        if (!RETRYABLE_STATUS_CODES.includes(error.status)) {
          return throwError(() => error);
        }
        if (!isIdempotent(req) && error.status !== 0 && error.status !== 502) {
          return throwError(() => error);
        }
        if (isLongRunningRequest(req)) {
          return throwError(() => error);
        }
        const baseDelay = INITIAL_DELAY_MS * Math.pow(2, retryCount - 1);
        const jitter = Math.random() * baseDelay * 0.3;
        const delayMs = baseDelay + jitter;
        return timer(delayMs);
      }
    })
  );
};

function isIdempotent(req: HttpRequest<unknown>): boolean {
  return ['GET', 'HEAD', 'OPTIONS', 'PUT', 'DELETE'].includes(req.method);
}

function isLongRunningRequest(req: HttpRequest<unknown>): boolean {
  const url = req.url.toLowerCase();
  return url.includes('/videoanalysis/upload') || url.includes('/processed-video/');
}
