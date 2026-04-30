import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { EmployeeService } from '../services/employee.service';
import { catchError, map, of } from 'rxjs';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (authService.isLoggedIn()) return true;
  router.navigate(['/login']);
  return false;
};

export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (authService.isAdmin()) return true;
  router.navigate(['/home']);
  return false;
};

export const managerGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (authService.isManagerOrAdmin()) return true;
  router.navigate(['/home']);
  return false;
};

export const employeeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (authService.isEmployee()) return true;
  router.navigate(['/home']);
  return false;
};

export const nonEmployeeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  if (!authService.isEmployee()) return true;
  return router.createUrlTree(['/home']);
};

export const employeeAssignedSpotGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isEmployee()) {
    return true;
  }

  const spotId = Number(route.paramMap.get('id'));
  if (!Number.isFinite(spotId) || spotId <= 0) {
    return router.createUrlTree(['/home']);
  }

  const employeeService = inject(EmployeeService);
  return employeeService.getMyAssignedSpots().pipe(
    map(spots => spots.some(spot => spot.fishingSpotId === spotId)
      ? true
      : router.createUrlTree(['/home'])),
    catchError(() => of(router.createUrlTree(['/home'])))
  );
};
