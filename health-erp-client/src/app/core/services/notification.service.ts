import { Injectable, inject, signal, computed, effect, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { AppNotification } from '../models/notification.model';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private baseUrl = '/api/v1/notifications';
  private hubConnection: signalR.HubConnection | null = null;
  private enabled = signal(false);
  private sessionToken: string | null = null;
  private loadVersion = 0;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  notifications = signal<AppNotification[]>([]);

  unreadCount = computed(() => this.notifications().filter(n => !n.IsRead && !n.isRead).length);

  constructor() {
    effect(() => {
      const token = this.auth.token();
      const active = this.enabled() && this.auth.isAuthenticated() && !this.auth.isPatient();
      untracked(() => {
        if (!active || !token) {
          this.disconnect();
        } else if (token !== this.sessionToken) {
          this.disconnect();
          this.sessionToken = token;
          this.loadNotifications();
          this.startSignalRConnection();
        }
      });
    });
  }

  init(): void {
    this.enabled.set(true);
  }

  stop(): void {
    this.enabled.set(false);
    this.disconnect();
  }

  private isCurrentSession(token: string | null): boolean {
    return !!token && this.enabled() && this.auth.isAuthenticated() && !this.auth.isPatient()
      && token === this.sessionToken && token === this.auth.getToken();
  }

  private disconnect(): void {
    if (this.retryTimer) clearTimeout(this.retryTimer);
    this.retryTimer = null;
    const connection = this.hubConnection;
    this.hubConnection = null;
    this.sessionToken = null;
    this.loadVersion++;
    this.notifications.set([]);
    if (connection) void connection.stop().catch(() => {});
  }

  loadNotifications(): void {
    const token = this.sessionToken;
    if (!this.isCurrentSession(token)) return;
    const version = ++this.loadVersion;
    this.http.get<AppNotification[]>(this.baseUrl).subscribe({
      next: data => {
        if (this.isCurrentSession(token) && version === this.loadVersion) this.notifications.set(data);
      },
      error: () => {}
    });
  }

  private startSignalRConnection(): void {
    if (this.hubConnection || !this.isCurrentSession(this.sessionToken)) return;
    const token = this.sessionToken;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', { accessTokenFactory: () => this.auth.getToken() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.None)
      .build();
    this.hubConnection = connection;
    connection.on('ReceiveNotification', (notification: AppNotification) => {
      if (!this.isCurrentSession(token) || this.hubConnection !== connection) return;
      this.loadVersion++;
      this.notifications.update(current => [notification, ...current.filter(n => n.id !== notification.id)].slice(0, 50));
    });
    connection.onreconnected(() => {
      if (this.isCurrentSession(token) && this.hubConnection === connection) this.loadNotifications();
    });
    const retry = () => {
      if (!this.isCurrentSession(token) || this.hubConnection !== connection) return;
      // SignalR uses its own HTTP client. Revalidate through Angular's HTTP pipeline
      // so a revoked session's 401 triggers the existing interceptor/logout lifecycle.
      this.loadNotifications();
      this.hubConnection = null;
      this.retryTimer = setTimeout(() => {
        this.retryTimer = null;
        if (this.isCurrentSession(token)) this.startSignalRConnection();
      }, 5000);
    };
    connection.onclose(retry);
    void connection.start().then(() => {
      if (this.isCurrentSession(token) && this.hubConnection === connection) this.loadNotifications();
    }).catch(retry);
  }

  markAsRead(id: string): void {
    const token = this.sessionToken;
    if (!this.isCurrentSession(token)) return;
    this.http.post<{ success: boolean }>(`${this.baseUrl}/${id}/read`, {}).subscribe({
      next: result => {
        if (!result.success || !this.isCurrentSession(token)) return;
        this.loadVersion++;
        this.notifications.update(current =>
          current.map(n => n.id === id || n.Id === id ? { ...n, isRead: true, IsRead: true } : n)
        );
      },
      error: () => {}
    });
  }

  markAllAsRead(): void {
    const token = this.sessionToken;
    if (!this.isCurrentSession(token)) return;
    this.http.post<{ success: boolean }>(`${this.baseUrl}/read-all`, {}).subscribe({
      next: result => {
        if (!result.success || !this.isCurrentSession(token)) return;
        this.loadVersion++;
        this.notifications.update(current =>
          current.map(n => ({ ...n, isRead: true, IsRead: true }))
        );
      },
      error: () => {}
    });
  }
}
