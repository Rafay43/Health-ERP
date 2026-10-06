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
        redirectTo: 'patients',
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
        redirectTo: 'staff',
      },
      {
        path: 'appointments',
        loadComponent: () =>
          import('./features/appointments/appointment-list').then((m) => m.AppointmentList),
      },
      {
        path: 'appointments/book',
        redirectTo: 'appointments',
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
        path: 'inpatient',
        loadComponent: () =>
          import('./features/inpatient/inpatient-list').then((m) => m.InpatientListComponent),
      },
      {
        path: 'emergency',
        loadComponent: () =>
          import('./features/emergency/emergency-list').then((m) => m.EmergencyListComponent),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports-list').then((m) => m.ReportsListComponent),
      },
      {
        path: 'admin',
        loadComponent: () =>
          import('./features/admin/admin-list').then((m) => m.AdminListComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
