import { Routes } from '@angular/router';
import { Shell } from './layout/shell/shell';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
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
      {
        path: 'patients/:id',
        loadComponent: () =>
          import('./features/patients/patient-ehr-detail').then((m) => m.PatientEhrDetailComponent),
      },
      {
        path: 'staff',
        loadComponent: () =>
          import('./features/staff/staff-list').then((m) => m.StaffList),
      },
      {
        path: 'staff/register',
        loadComponent: () =>
          import('./features/staff/feature-registration/staff-registration').then(
            (m) => m.StaffRegistration
          ),
      },
      {
        path: 'appointments',
        loadComponent: () =>
          import('./features/appointments/appointment-list').then((m) => m.AppointmentList),
      },
      {
        path: 'appointments/book',
        loadComponent: () =>
          import('./features/appointments/feature-booking/book-appointment').then(
            (m) => m.BookAppointment
          ),
      },
      {
        path: 'billing',
        loadComponent: () =>
          import('./features/billing/billing-list').then((m) => m.BillingList),
      },
      {
        path: 'pharmacy',
        loadComponent: () =>
          import('./features/pharmacy/pharmacy-list').then((m) => m.PharmacyListComponent),
      },
      {
        path: 'laboratory',
        loadComponent: () =>
          import('./features/laboratory/laboratory-list').then((m) => m.LaboratoryListComponent),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports-list').then((m) => m.ReportsListComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
