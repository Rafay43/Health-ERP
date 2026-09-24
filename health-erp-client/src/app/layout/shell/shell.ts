import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, AvatarModule, BadgeModule],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private authService = inject(AuthService);

  readonly currentUser = this.authService.currentUser;

  navItems = [
    { label: 'Dashboard', icon: 'pi pi-home', route: '/dashboard' },
    { label: 'Patients', icon: 'pi pi-users', route: '/patients' },
    { label: 'Appointments', icon: 'pi pi-calendar', route: '/appointments' },
    { label: 'Billing', icon: 'pi pi-wallet', route: '/billing' },
    { label: 'Pharmacy', icon: 'pi pi-heart', route: '/pharmacy' },
    { label: 'Laboratory', icon: 'pi pi-list', route: '/laboratory' },
    { label: 'Staff', icon: 'pi pi-id-card', route: '/staff' },
    { label: 'Reports', icon: 'pi pi-chart-bar', route: '/reports' },
    { label: 'Admin', icon: 'pi pi-cog', route: '/admin' },
  ];

  sidebarCollapsed = false;

  toggleSidebar() {
    this.sidebarCollapsed = !this.sidebarCollapsed;
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