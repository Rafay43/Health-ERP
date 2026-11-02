import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { Roles } from '../constants/roles';

export const roleGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  const requiredRoles: string[] = route.data?.['roles'] || [];

  // Patient role safe check: Patient cannot access ERP administrative pages
  if (authService.isPatient()) {
    if (requiredRoles.includes(Roles.Patient)) {
      return true;
    }
    return router.createUrlTree(['/portal']);
  }

  if (requiredRoles.length === 0 || authService.hasAnyRole(requiredRoles)) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
