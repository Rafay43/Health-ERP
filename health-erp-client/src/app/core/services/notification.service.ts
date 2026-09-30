import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { AppNotification } from '../models/notification.model';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private http = inject(HttpClient);
  private baseUrl = '/api/v1/notifications';
  private hubConnection: signalR.HubConnection | null = null;

  notifications = signal<AppNotification[]>([]);

  unreadCount = computed(() => this.notifications().filter(n => !n.IsRead && !n.isRead).length);

  init(): void {
    this.loadNotifications();
    this.startSignalRConnection();
  }

  loadNotifications(): void {
    this.http.get<AppNotification[]>(this.baseUrl).subscribe({
      next: (data) => this.notifications.set(data),
      error: (err) => console.error('Failed to load notifications', err)
    });
  }

  private startSignalRConnection(): void {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('SignalR Notification Hub Connected.'))
      .catch((err) => console.warn('SignalR Notification Hub Connection Failed:', err));

    this.hubConnection.on('ReceiveNotification', (newNotification: AppNotification) => {
      this.notifications.update(current => [newNotification, ...current]);
    });
  }

  markAsRead(id: string): void {
    this.http.post<{ success: boolean }>(`${this.baseUrl}/${id}/read`, {}).subscribe({
      next: () => {
        this.notifications.update(current =>
          current.map(n => n.id === id || (n as any).Id === id ? { ...n, isRead: true, IsRead: true } : n)
        );
      }
    });
  }

  markAllAsRead(): void {
    this.http.post<{ success: boolean }>(`${this.baseUrl}/read-all`, {}).subscribe({
      next: () => {
        this.notifications.update(current =>
          current.map(n => ({ ...n, isRead: true, IsRead: true }))
        );
      }
    });
  }
}
