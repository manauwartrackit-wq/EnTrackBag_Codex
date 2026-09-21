import { Injectable } from "@angular/core";
import { Subject } from "rxjs";

export type PageHeaderAction = "dashboard-toggle" | "admin-refresh" | null;

export interface PageHeaderState {
  title: string;
  subtitle?: string;
  action?: PageHeaderAction;
}

@Injectable({ providedIn: "root" })
export class PageHeaderService {
  private current: PageHeaderState = { title: "" };
  private readonly changes = new Subject<PageHeaderState>();
  private readonly refreshRequests = new Subject<void>();

  readonly changes$ = this.changes.asObservable();
  readonly refreshRequests$ = this.refreshRequests.asObservable();

  get snapshot(): PageHeaderState {
    return this.current;
  }

  set(state: PageHeaderState): void {
    this.current = {
      title: state.title ?? "",
      subtitle: state.subtitle ?? "",
      action: state.action ?? null,
    };
    this.changes.next(this.current);
  }

  clear(): void {
    this.set({ title: "", subtitle: "", action: null });
  }

  requestRefresh(): void {
    this.refreshRequests.next();
  }
}
