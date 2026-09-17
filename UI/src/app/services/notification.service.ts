import { Injectable, inject, signal } from "@angular/core";
import { MonitoringService } from "./monitoring.service";
import { AuthService } from "./auth.service";

@Injectable({ providedIn: "root" })
export class NotificationService {
  private readonly auth = inject(AuthService);
  readonly count = signal(0);

  constructor() { inject(MonitoringService).alarms.subscribe(value => this.setCount(value)); }

  refresh(): void {
    if (!this.auth.hasAccess("Dashboard.SLA.View", "VIEW")) { this.clear(); return; }
    // Alarm changes arrive through the monitoring hub; no off-page SLA fetch.
  }

  setCount(value: number): void {
    this.count.set(Math.max(0, Math.trunc(value)));
  }

  add(amount = 1): void {
    this.setCount(this.count() + amount);
  }

  clear(): void {
    this.count.set(0);
  }
}
