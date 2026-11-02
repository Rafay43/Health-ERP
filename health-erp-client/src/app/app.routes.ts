import { Routes } from '@angular/router';
import { Shell } from './layout/shell/shell';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { roleGuard } from './core/guards/role.guard';
import { Roles } from './core/constants/roles';

import { patientLinkGuard } from './core/guards/patient-link.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/landing/landing.component').then((m) => m.LandingComponent),
  },
  {
    path: 'home',
    redirectTo: '',
    pathMatch: 'full',
  },
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
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password').then(
        (m) => m.ForgotPasswordComponent
      ),
  },
  {
    path: 'reset-password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/reset-password/reset-password').then(
        (m) => m.ResetPasswordComponent
      ),
  },
  {
    path: 'forbidden',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/forbidden/forbidden').then((m) => m.ForbiddenComponent),
  },
  {
    path: 'portal-activation',
    canActivate: [authGuard, roleGuard],
    data: { roles: [Roles.Patient] },
    loadComponent: () =>
      import('./features/portal-coming-soon/portal-coming-soon').then(
        (m) => m.PortalComingSoonComponent
      ),
  },
  {
    path: 'portal-coming-soon',
    redirectTo: 'portal-activation',
    pathMatch: 'full',
  },
  {
    path: 'portal',
    canActivate: [authGuard, roleGuard, patientLinkGuard],
    data: { roles: [Roles.Patient] },
    loadComponent: () =>
      import('./layout/patient-shell/patient-shell').then((m) => m.PatientPortalShellComponent),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/patient-portal/overview/portal-overview').then(
            (m) => m.PortalOverviewComponent
          ),
      },
      {
        path: 'appointments',
        loadComponent: () =>
          import('./features/patient-portal/appointments/portal-appointments').then(
            (m) => m.PortalAppointmentsComponent
          ),
      },
      {
        path: 'health-record',
        loadComponent: () =>
          import('./features/patient-portal/health-record/portal-health-record').then(
            (m) => m.PortalHealthRecordComponent
          ),
      },
      {
        path: 'prescriptions',
        loadComponent: () =>
          import('./features/patient-portal/prescriptions/portal-prescriptions').then(
            (m) => m.PortalPrescriptionsComponent
          ),
      },
      {
        path: 'lab-results',
        loadComponent: () =>
          import('./features/patient-portal/lab-results/portal-lab-results').then(
            (m) => m.PortalLabResultsComponent
          ),
      },
      {
        path: 'billing',
        loadComponent: () =>
          import('./features/patient-portal/billing/portal-billing').then(
            (m) => m.PortalBillingComponent
          ),
      },
      {
        path: 'profile',
        loadComponent: () =>
          import('./features/patient-portal/profile/portal-profile').then(
            (m) => m.PortalProfileComponent
          ),
      },
    ],
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      {
        path: 'doctor',
        canActivate: [authGuard, roleGuard],
        data: { roles: [Roles.Doctor] },
        loadComponent: () => import('./features/doctor-workspace/doctor-workspace').then((m) => m.DoctorWorkspace),
      },
      {
        path: 'dashboard',
        canActivate: [roleGuard],
        data: {
          roles: [
            Roles.Admin,
            Roles.Doctor,
            Roles.Nurse,
            Roles.Pharmacist,
            Roles.LabTechnician,
            Roles.Receptionist,
            Roles.BillingStaff,
          ],
        },
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'patients',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist] },
        loadComponent: () =>
          import('./features/patients/patient-list').then((m) => m.PatientList),
      },
      {
        path: 'patients/register',
        redirectTo: 'patients',
      },
      {
        path: 'patients/:id',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist] },
        loadComponent: () =>
          import('./features/patients/patient-ehr-detail').then(
            (m) => m.PatientEhrDetailComponent
          ),
      },
      {
        path: 'staff',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin] },
        loadComponent: () =>
          import('./features/staff/staff-list').then((m) => m.StaffList),
      },
      {
        path: 'staff/register',
        redirectTo: 'staff',
      },
      {
        path: 'appointments',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist] },
        loadComponent: () =>
          import('./features/appointments/appointment-list').then(
            (m) => m.AppointmentList
          ),
      },
      {
        path: 'appointments/book',
        redirectTo: 'appointments',
      },
      {
        path: 'billing',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.BillingStaff] },
        loadComponent: () =>
          import('./features/billing/billing-list').then((m) => m.BillingList),
      },
      {
        path: 'pharmacy',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Pharmacist] },
        loadComponent: () =>
          import('./features/pharmacy/pharmacy-list').then(
            (m) => m.PharmacyListComponent
          ),
      },
      {
        path: 'laboratory',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.LabTechnician] },
        loadComponent: () =>
          import('./features/laboratory/laboratory-list').then(
            (m) => m.LaboratoryListComponent
          ),
      },
      {
        path: 'inpatient',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist] },
        loadComponent: () =>
          import('./features/inpatient/inpatient-list').then(
            (m) => m.InpatientListComponent
          ),
      },
      {
        path: 'emergency',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin, Roles.Doctor, Roles.Nurse, Roles.Receptionist] },
        loadComponent: () =>
          import('./features/emergency/emergency-list').then(
            (m) => m.EmergencyListComponent
          ),
      },
      {
        path: 'reports',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin] },
        loadComponent: () =>
          import('./features/reports/reports-list').then(
            (m) => m.ReportsListComponent
          ),
      },
      {
        path: 'admin',
        canActivate: [roleGuard],
        data: { roles: [Roles.Admin] },
        loadComponent: () =>
          import('./features/admin/admin-list').then(
            (m) => m.AdminListComponent
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
