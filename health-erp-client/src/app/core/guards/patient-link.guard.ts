import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { map, catchError, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PatientAccountService } from '../services/patient-account.service';

export const patientLinkGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const patientAccountService = inject(PatientAccountService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  if (!authService.isPatient()) {
    return router.createUrlTree(['/forbidden']);
  }

  return patientAccountService.getStatus().pipe(
    map((status) => {
      if (status.isLinked) {
        return true;
      }
      return router.createUrlTree(['/portal-activation']);
    }),
    catchError(() => {
      return of(router.createUrlTree(['/portal-activation']));
    })
  );
};
