import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MonitoringService } from '../../services/monitoring.service';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../core/api.service';

@Component({
  standalone: true,
  selector: 'app-sla',
  imports: [CommonModule],
  templateUrl: './sla.component.html',
  styleUrl: './sla.component.scss'
})
export class SlaComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly monitoring = inject(MonitoringService);
  private readonly auth = inject(AuthService);
  bags: any[] = [];
  oldest = '—';
  breaches = 0;
  selectedHistory: any = null;

  ngOnInit(): void {
    if (!this.auth.hasAccess("Dashboard.SLA.View", "VIEW")) return;
    this.monitoring.sla.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => { this.bags = value ?? []; this.breaches = this.bags.filter(b => b.slaBreach ?? b.breached).length; this.oldest = this.bags.length ? (this.bags[0]?.dwellTime ?? '—') : '—'; });
    this.api.get<any[]>('dashboard/sla').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => {
        this.bags = value ?? [];
        this.breaches = this.bags.filter(b => b.slaBreach ?? b.breached).length;
        this.oldest = this.bags.length ? (this.bags[0]?.dwellTime ?? '—') : '—';
      }
    });
  }

  history(id: string): void {
    this.api.get<any>(`dashboard/bags/${encodeURIComponent(id)}/history`).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => this.selectedHistory = value
    });
  }
}
