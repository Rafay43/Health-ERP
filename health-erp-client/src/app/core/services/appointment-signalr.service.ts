import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class AppointmentSignalRService {
  private hubConnection: signalR.HubConnection | null = null;
  public appointmentUpdated$ = new Subject<void>();
  public isConnected = signal(false);

  public startConnection(): void {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/appointments', {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => {
        this.isConnected.set(true);
      })
      .catch((err) => {
        this.isConnected.set(false);
        console.warn('SignalR connection warning:', err);
      });

    this.hubConnection.on('AppointmentUpdated', () => {
      this.appointmentUpdated$.next();
    });
  }

  public stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.isConnected.set(false);
    }
  }
}
