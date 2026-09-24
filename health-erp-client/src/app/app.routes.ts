import { Routes } from '@angular/router';
import { Shell } from './layout/shell/shell';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'patients',
        loadComponent: () =>
          import('./features/patients/patient-list').then((m) => m.PatientList),
      },
      {
        path: 'patients/register',
        loadComponent: () =>
          import('./features/patients/feature-registration/patient-registration').then(
            (m) => m.PatientRegistration
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];