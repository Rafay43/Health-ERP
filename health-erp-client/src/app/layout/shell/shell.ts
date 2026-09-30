import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';
import { TooltipModule } from 'primeng/tooltip';

import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { AppNotification } from '../../core/models/notification.model';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    AvatarModule,
    BadgeModule,
    TooltipModule
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell implements OnInit {
  private authService = inject(AuthService);
  public notificationService = inject(NotificationService);
  private router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  showNotificationDrawer = signal(false);

  navItems = [
    { label: 'Dashboard', icon: 'pi pi-home', route: '/dashboard' },
    { label: 'Patients', icon: 'pi pi-users', route: '/patients' },
    { label: 'Appointments', icon: 'pi pi-calendar', route: '/appointments' },
    { label: 'Billing', icon: 'pi pi-wallet', route: '/billing' },
    { label: 'Pharmacy', icon: 'pi pi-heart', route: '/pharmacy' },
    { label: 'Laboratory', icon: 'pi pi-list', route: '/laboratory' },
    { label: 'Inpatient Wards', icon: 'pi pi-building', route: '/inpatient' },
    { label: 'Emergency (ER)', icon: 'pi pi-bolt', route: '/emergency' },
    { label: 'Staff', icon: 'pi pi-id-card', route: '/staff' },
    { label: 'Reports', icon: 'pi pi-chart-bar', route: '/reports' },
    { label: 'Admin', icon: 'pi pi-cog', route: '/admin' },
  ];

  sidebarCollapsed = false;

  ngOnInit() {
    this.notificationService.init();
  }

  toggleSidebar() {
    this.sidebarCollapsed = !this.sidebarCollapsed;
  }

  toggleNotificationDrawer() {
    this.showNotificationDrawer.update(v => !v);
  }

  markAsRead(n: AppNotification, event: Event) {
    event.stopPropagation();
    this.notificationService.markAsRead(n.id);
  }

  markAllAsRead() {
    this.notificationService.markAllAsRead();
  }

  onNotificationClick(n: AppNotification) {
    this.notificationService.markAsRead(n.id);
    if (n.linkUrl) {
      this.router.navigateByUrl(n.linkUrl);
      this.showNotificationDrawer.set(false);
    }
  }

  getNotificationSeverityClass(severity: string | number): string {
    const sev = String(severity).toLowerCase();
    if (sev.includes('critical') || sev === '3') return 'border-l-4 border-rose-500 bg-rose-50/50';
    if (sev.includes('warning') || sev === '2') return 'border-l-4 border-amber-500 bg-amber-50/50';
    return 'border-l-4 border-blue-500 bg-blue-50/50';
  }

  logout() {
    this.authService.logout();
  }

  get userInitials(): string {
    const u = this.currentUser();
    if (!u) return 'U';
    return `${u.firstName?.[0] || ''}${u.lastName?.[0] || ''}`.toUpperCase() || 'U';
  }
}