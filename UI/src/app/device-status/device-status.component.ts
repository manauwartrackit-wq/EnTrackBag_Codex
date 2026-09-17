import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MonitoringService } from '../services/monitoring.service';
import { ApiService } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-device-status',
  imports: [CommonModule],
  templateUrl: './device-status.component.html',
  styleUrl: './device-status.component.scss'
})
export class DeviceStatusComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly monitoring = inject(MonitoringService);
  summary: any;
  details: any[] = [];
  selectedCategory = '';

  ngOnInit(): void {
    this.monitoring.devices.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => { this.summary = value.summary; this.details = (value.details ?? []).filter((item: any) => !this.selectedCategory || item.category === this.selectedCategory); }); this.load(); }

  load(category = ''): void {
    this.selectedCategory = category;
    this.api.get<any>('device-status/summary').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => this.summary = value });
    const query = category ? `?category=${encodeURIComponent(category)}` : '';
    this.api.get<any[]>(`device-status/details${query}`).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => this.details = value ?? [] });
  }
}
