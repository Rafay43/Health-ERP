import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, AvatarModule, BadgeModule],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
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
}