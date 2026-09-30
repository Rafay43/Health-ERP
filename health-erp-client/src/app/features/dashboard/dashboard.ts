import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';

import { CardModule } from 'primeng/card';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ProgressBarModule } from 'primeng/progressbar';

import { DashboardService } from './dashboard.service';
import { DashboardMetrics } from './dashboard.model';
import { AuthService } from '../../core/services/auth.service';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    CardModule,
    ButtonModule,
    TagModule,
    TooltipModule,
    ProgressBarModule,
  ],
  templateUrl: './dashboard.html',
})
export class Dashboard implements OnInit {
  private dashboardService = inject(DashboardService);
  private authService = inject(AuthService);
  private router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  metrics = signal<DashboardMetrics | null>(null);
  isLoading = signal(true);
  currentDate = new Date();
  Math = Math;

  getPercentage(val: number, max: number): number {
    if (!max || max <= 0) return 0;
    const pct = Math.round((val / max) * 100);
    return Math.min(Math.max(pct, 5), 100);
  }

  ngOnInit(): void {
    this.loadMetrics();
  }

  loadMetrics(): void {
    this.isLoading.set(true);
    this.dashboardService.getMetrics().subscribe({
      next: (data) => {
        this.metrics.set(data);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  quickNavigate(path: string): void {
    this.router.navigate([path]);
  }

  getEsiClass(level: number): string {
    switch (level) {
      case 1: return 'bg-rose-500 text-white font-bold';
      case 2: return 'bg-rose-100 text-rose-800 border border-rose-200';
      case 3: return 'bg-amber-100 text-amber-800 border border-amber-200';
      case 4: return 'bg-blue-100 text-blue-800 border border-blue-200';
      default: return 'bg-slate-100 text-slate-700';
    }
  }

  getStatusSeverity(status: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status.toLowerCase()) {
      case 'completed': return 'success';
      case 'confirmed': return 'info';
      case 'scheduled': return 'secondary';
      case 'cancelled': return 'danger';
      default: return 'warn';
    }
  }

  onQuickActionAlert(actionName: string, path: string): void {
    Swal.fire({
      title: `Navigate to ${actionName}`,
      text: `Opening ${actionName} creation form...`,
      icon: 'info',
      timer: 1200,
      showConfirmButton: false,
    }).then(() => {
      this.router.navigate([path]);
    });
  }
}