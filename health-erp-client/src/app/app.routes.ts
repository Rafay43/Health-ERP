import { Routes } from '@angular/router';
import { Shell } from './layout/shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
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
];