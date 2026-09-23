import { Component } from '@angular/core';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <h1 class="text-2xl font-bold text-[var(--color-text-primary)]">Dashboard</h1>
    <p class="text-[var(--color-text-secondary)] mt-2">Welcome to Health ERP.</p>
  `,
})
export class Dashboard {}