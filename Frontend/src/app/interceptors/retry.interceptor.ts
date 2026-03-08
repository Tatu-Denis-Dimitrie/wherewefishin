import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { retry, timer } from 'rxjs';

const MAX_RETRIES = 2;
const INITIAL_DELAY_MS = 800;
const RETRYABLE_STATUS_CODES = [0, 502, 503, 504];

export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  // Only retry safe, idempotent methods (GET/HEAD/OPTIONS)
  if (!isRetryable(req)) {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error, retryCount) => {
        if (!RETRYABLE_STATUS_CODES.includes(error.status)) {
          throw error;
        }
        const delayMs = INITIAL_DELAY_MS * Math.pow(2, retryCount - 1);
        return timer(delayMs);
      }
    })
  );
};

function isRetryable(req: HttpRequest<unknown>): boolean {
  return ['GET', 'HEAD', 'OPTIONS'].includes(req.method);
}
