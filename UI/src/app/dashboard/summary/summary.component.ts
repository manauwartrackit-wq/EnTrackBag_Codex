import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MonitoringService } from '../../services/monitoring.service';
import { AuthService } from '../../services/auth.service';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';

@Component({
  standalone: true,
  imports: [RouterLink],
  selector: 'app-summary',
  templateUrl: './summary.component.html',
  styleUrl: './summary.component.scss'
})
export class SummaryComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly monitoring = inject(MonitoringService);
  readonly auth = inject(AuthService);
  kpi: any;

  ngOnInit(): void {
    this.monitoring.dashboard.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => { this.kpi = value; });
    this.api.get<any>('dashboard/kpis').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => this.kpi = value });
  }
}
