import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { retry, timer } from 'rxjs';

const MAX_RETRIES = 3;
const INITIAL_DELAY_MS = 1000;
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
        const delayMs = INITIAL_DELAY_MS * Math.pow(2, retryCount - 1);
        return timer(delayMs);
      }
    })
  );
};

function isIdempotent(req: HttpRequest<unknown>): boolean {
  return ['GET', 'HEAD', 'OPTIONS', 'PUT', 'DELETE'].includes(req.method);
}
