import { Injectable } from '@angular/core';
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class MonitoringService {
  readonly dashboard = new Subject<any>();
  readonly devices = new Subject<any>();
  readonly sla = new Subject<any[]>();
  readonly alarms = new Subject<number>();
  readonly disconnected = new Subject<void>();
  private channel = '';
  private starting?: Promise<void>;
  private generation = 0;
  private subscribed?: string;
  private stopping?: Promise<void>;
  private readonly connection = new HubConnectionBuilder()
    .withUrl(environment.monitoringHubUrl, { accessTokenFactory: () => localStorage.getItem('access_token') ?? '' })
    .withAutomaticReconnect().build();

  constructor() {
    this.connection.on('DashboardUpdated', value => this.dashboard.next(value));
    this.connection.on('SystemUpdated', value => this.devices.next(value));
    this.connection.on('SlaUpdated', value => this.sla.next(value));
    this.connection.on('AlarmUpdated', value => this.alarms.next(value));
    this.connection.onreconnecting(() => this.disconnected.next());
    this.connection.onreconnected(() => { this.subscribed = undefined; void this.subscribePage().catch(() => this.disconnected.next()); });
    this.connection.onclose(() => this.disconnected.next());
  }

  async openPage(url: string): Promise<void> {
    this.channel = url.split('?')[0] === '/dashboard/summary' ? 'Dashboard'
      : url.split('?')[0] === '/dashboard/sla' ? 'Dashboard.SLA'
      : url.split('?')[0] === '/device-status' ? 'DeviceStatus' : '';
    if (!localStorage.getItem('access_token')) return;
    const generation = this.generation;
    try {
      await this.stopping;
      if (generation !== this.generation || !localStorage.getItem('access_token')) return;
      if (this.connection.state === HubConnectionState.Disconnected && !this.starting)
        this.starting = this.connection.start().finally(() => this.starting = undefined);
      await this.starting;
      if (generation === this.generation) await this.subscribePage();
    } catch { this.disconnected.next(); }
  }

  private async subscribePage(): Promise<void> {
    if (this.connection.state === HubConnectionState.Connected && localStorage.getItem('access_token') && this.subscribed !== this.channel) {
      this.subscribed = this.channel;
      try { await this.connection.invoke('SubscribePage', this.channel); }
      catch (error) { this.subscribed = undefined; throw error; }
    }
  }

  async disconnect(): Promise<void> {
    this.generation++;
    this.channel = '';
    this.alarms.next(0);
    this.subscribed = undefined;
    this.stopping = this.connection.stop();
    await this.stopping;
  }
}
