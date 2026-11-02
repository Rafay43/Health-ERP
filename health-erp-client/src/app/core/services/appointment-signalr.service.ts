import { Injectable, signal, inject, effect } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root',
})
export class AppointmentSignalRService {
  private auth = inject(AuthService);
  private hubConnection: signalR.HubConnection | null = null;
  private sessionToken: string | null = null;
  public appointmentUpdated$ = new Subject<void>();
  public isConnected = signal(false);

  constructor() {
    effect(() => {
      const token = this.auth.token();
      if (!this.auth.isAuthenticated() || this.auth.isPatient() || (this.sessionToken && this.sessionToken !== token)) {
        this.stopConnection();
      }
    });
  }

  public startConnection(): void {
    if (!this.auth.isAuthenticated() || this.auth.isPatient()) return;
    if (this.hubConnection) {
      return;
    }

    const token = this.auth.getToken();
    this.sessionToken = token;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/appointments', {
        accessTokenFactory: () => this.auth.getToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.None)
      .build();
    this.hubConnection = connection;

    connection
      .start()
      .then(() => {
        if (this.hubConnection === connection && this.auth.getToken() === token) this.isConnected.set(true);
      })
      .catch(() => {
        if (this.hubConnection === connection) {
          this.hubConnection = null;
          this.isConnected.set(false);
        }
      });

    connection.on('AppointmentUpdated', () => {
      if (this.hubConnection === connection && this.auth.getToken() === token && this.auth.isAuthenticated() && !this.auth.isPatient()) {
        this.appointmentUpdated$.next();
      }
    });
  }

  public stopConnection(): void {
    if (this.hubConnection) {
      void this.hubConnection.stop().catch(() => {});
      this.hubConnection = null;
      this.isConnected.set(false);
    }
    this.sessionToken = null;
  }
}
