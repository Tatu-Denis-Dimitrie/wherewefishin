import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { retry, timer } from 'rxjs';

const MAX_RETRIES = 3;
const INITIAL_DELAY_MS = 1500;
// Status 0 = network error (connection reset/refused)
const RETRYABLE_STATUS_CODES = [0, 502, 503, 504];

export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  // Retry all methods on connection-level errors (0, 502, 503, 504)
  // These errors indicate the server is unreachable, not a business logic error
  return next(req).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error, retryCount) => {
        // Only retry on connection/gateway errors
        if (!RETRYABLE_STATUS_CODES.includes(error.status)) {
          throw error;
        }
        // For non-idempotent methods, only retry on status 0 (network error) and 502
        if (!isIdempotent(req) && error.status !== 0 && error.status !== 502) {
          throw error;
        }
        // Don't retry upload/video endpoints (they are long-running)
        if (isLongRunningRequest(req)) {
          throw error;
        }
        // Exponential backoff with jitter to avoid thundering herd
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
