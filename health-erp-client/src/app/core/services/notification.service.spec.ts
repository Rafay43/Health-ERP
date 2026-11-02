import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { NotificationService } from './notification.service';
import { AppointmentSignalRService } from './appointment-signalr.service';
import { AppNotification } from '../models/notification.model';

const hubs = vi.hoisted(() => ({ connections: [] as any[] }));
vi.mock('@microsoft/signalr', () => ({
  LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    private options: any;
    withUrl(_url: string, options: any) { this.options = options; return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      const connection = {
        options: this.options,
        events: {} as Record<string, (value: AppNotification) => void>,
        reconnected: () => {},
        closed: () => {},
        on(name: string, callback: (value: AppNotification) => void) { this.events[name] = callback; },
        onreconnected(callback: () => void) { this.reconnected = callback; },
        onclose(callback: () => void) { this.closed = callback; },
        start: vi.fn(() => Promise.resolve()),
        stop: vi.fn(() => Promise.resolve()),
      };
      hubs.connections.push(connection);
      return connection;
    }
  },
}));

const notification: AppNotification = {
  id: 'notification-a', title: 'Private', message: 'Owned', typeName: 'SystemInfo', typeValue: 5,
  severityName: 'Info', severityValue: 0, isRead: false, createdAtUtc: '2026-10-11T00:00:00Z',
};

describe('Notification authentication and lifecycle', () => {
  let service: NotificationService;
  let http: HttpTestingController;
  let auth: { token: ReturnType<typeof signal<string | null>>; isAuthenticated: ReturnType<typeof computed<boolean>>;
    isPatient: ReturnType<typeof vi.fn>; getToken: () => string | null };

  beforeEach(() => {
    hubs.connections.length = 0;
    const token = signal<string | null>('token-a');
    auth = { token, isAuthenticated: computed(() => !!token()), isPatient: vi.fn(() => false), getToken: () => token() };
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: AuthService, useValue: auth }] });
    service = TestBed.inject(NotificationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => { service.stop(); http.verify(); });

  async function initialize() {
    service.init(); TestBed.tick();
    http.expectOne('/api/v1/notifications').flush([notification]);
    await Promise.resolve();
    http.expectOne('/api/v1/notifications').flush([notification]);
  }

  it('does not load or connect when logged out or on the patient portal', () => {
    auth.token.set(null); service.init(); TestBed.tick();
    http.expectNone('/api/v1/notifications'); expect(hubs.connections).toHaveLength(0);
    auth.token.set('patient-token'); auth.isPatient.mockReturnValue(true); TestBed.tick();
    http.expectNone('/api/v1/notifications'); expect(hubs.connections).toHaveLength(0);
  });

  it('passes the current token, loads notifications, and does not duplicate connections', async () => {
    await initialize(); service.init(); TestBed.tick();
    expect(hubs.connections).toHaveLength(1);
    expect(hubs.connections[0].options.accessTokenFactory()).toBe('token-a');
    expect(service.notifications()).toEqual([notification]); expect(service.unreadCount()).toBe(1);
    auth.token.set('new-token');
    expect(hubs.connections[0].options.accessTokenFactory()).toBe('new-token');
    auth.token.set('token-a');
  });

  it('receives and deduplicates targeted events and refreshes after reconnect', async () => {
    await initialize();
    const event = { ...notification, id: 'new-notification' };
    hubs.connections[0].events.ReceiveNotification(event);
    hubs.connections[0].events.ReceiveNotification(event);
    expect(service.notifications().map(n => n.id)).toEqual(['new-notification', 'notification-a']);
    hubs.connections[0].reconnected();
    http.expectOne('/api/v1/notifications').flush([event]);
    expect(service.notifications()).toEqual([event]);
  });

  it('marks the owner notification read only after a successful server response', async () => {
    await initialize(); service.markAsRead(notification.id);
    http.expectOne('/api/v1/notifications/notification-a/read').flush({}, { status: 404, statusText: 'Not Found' });
    expect(service.unreadCount()).toBe(1);
    service.markAsRead(notification.id);
    http.expectOne('/api/v1/notifications/notification-a/read').flush({ success: true });
    expect(service.unreadCount()).toBe(0);
  });

  it('marks all owned notifications read on success', async () => {
    await initialize(); service.markAllAsRead();
    http.expectOne('/api/v1/notifications/read-all').flush({ success: true });
    expect(service.unreadCount()).toBe(0);
  });

  it('disconnects and clears private state on logout and ignores late responses and events', async () => {
    await initialize();
    service.loadNotifications(); const pending = http.expectOne('/api/v1/notifications');
    auth.token.set(null); TestBed.tick();
    expect(hubs.connections[0].stop).toHaveBeenCalledOnce();
    expect(service.notifications()).toEqual([]);
    pending.flush([notification]);
    hubs.connections[0].events.ReceiveNotification(notification);
    expect(service.notifications()).toEqual([]);
    service.markAsRead(notification.id); http.expectNone('/api/v1/notifications/notification-a/read');
  });

  it('replaces the connection on account change and rejects the old account response', async () => {
    await initialize();
    service.loadNotifications(); const oldRequest = http.expectOne('/api/v1/notifications');
    auth.token.set('token-b'); TestBed.tick();
    expect(hubs.connections[0].stop).toHaveBeenCalledOnce();
    expect(service.notifications()).toEqual([]);
    oldRequest.flush([notification]);
    hubs.connections[0].events.ReceiveNotification(notification);
    expect(service.notifications()).toEqual([]);
    http.expectOne('/api/v1/notifications').flush([]);
    await Promise.resolve(); http.expectOne('/api/v1/notifications').flush([]);
    expect(hubs.connections).toHaveLength(2);
  });

  it('does not let an older list response overwrite a newly received event', async () => {
    await initialize(); service.loadNotifications();
    const pending = http.expectOne('/api/v1/notifications');
    const event = { ...notification, id: 'new-notification' };
    hubs.connections[0].events.ReceiveNotification(event);
    pending.flush([notification]);
    expect(service.notifications()[0]).toEqual(event);
  });

  it('recovers after reconnect attempts are exhausted and cancels recovery on logout', async () => {
    await initialize();
    vi.useFakeTimers();
    try {
      hubs.connections[0].closed();
      http.expectOne('/api/v1/notifications').flush([notification]);
      await vi.advanceTimersByTimeAsync(5000);
      expect(hubs.connections).toHaveLength(2);
      http.expectOne('/api/v1/notifications').flush([notification]);
      hubs.connections[1].closed();
      const sessionCheck = http.expectOne('/api/v1/notifications');
      auth.token.set(null); TestBed.tick();
      sessionCheck.flush({}, { status: 401, statusText: 'Unauthorized' });
      await vi.advanceTimersByTimeAsync(5000);
      expect(hubs.connections).toHaveLength(2);
      expect(service.notifications()).toEqual([]);
    } finally { vi.useRealTimers(); }
  });

  it('authenticates the appointment hub and stops staff change events on account change', async () => {
    const appointments = TestBed.inject(AppointmentSignalRService);
    const received = vi.fn();
    appointments.appointmentUpdated$.subscribe(received);
    appointments.startConnection(); appointments.startConnection();
    expect(hubs.connections).toHaveLength(1);
    expect(hubs.connections[0].options.accessTokenFactory()).toBe('token-a');
    hubs.connections[0].events.AppointmentUpdated();
    expect(received).toHaveBeenCalledOnce();
    auth.token.set('patient-token'); auth.isPatient.mockReturnValue(true); TestBed.tick();
    await Promise.resolve();
    expect(hubs.connections[0].stop).toHaveBeenCalledOnce();
    expect(appointments.isConnected()).toBe(false);
    hubs.connections[0].events.AppointmentUpdated();
    expect(received).toHaveBeenCalledOnce();
    appointments.startConnection(); expect(hubs.connections).toHaveLength(1);
    appointments.stopConnection();
  });
});
