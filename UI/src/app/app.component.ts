import { Component, HostListener, OnDestroy, OnInit } from "@angular/core";
import {
  ActivatedRoute,
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from "@angular/router";
import { Subscription, filter } from "rxjs";
import { AuthService } from "./services/auth.service";
import { NotificationService } from "./services/notification.service";
import { PageHeaderService, PageHeaderState } from "./core/page-header.service";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterLink, RouterOutlet, RouterLinkActive],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss",
})
export class AppComponent implements OnInit, OnDestroy {
  accountMenuOpen = false;
  pageHeader: PageHeaderState = { title: "" };
  private navSub?: Subscription;

  constructor(
    public auth: AuthService,
    public notifications: NotificationService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
    private readonly pageHeaderService: PageHeaderService,
  ) {}

  ngOnInit(): void {
    this.navSub = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => this.syncPageHeaderFromRoute());
    this.syncPageHeaderFromRoute();
  }

  ngOnDestroy(): void {
    this.navSub?.unsubscribe();
  }

  public isLoginView(): boolean {
    return this.router.url === "/login" || this.router.url === "/";
  }

  public get displayName(): string {
    return this.auth.getDisplayName() || "Admin";
  }

  public get userInitial(): string {
    return this.displayName.trim().charAt(0).toUpperCase() || "A";
  }

  public logout(): void {
    this.accountMenuOpen = false;
    this.auth.logout();
    void this.router.navigate(["/login"]);
  }

  public toggleAccountMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.accountMenuOpen = !this.accountMenuOpen;
  }

  public get roleLabel(): string {
    return this.auth.getRoles().join(", ") || "Authenticated user";
  }

  public get sessionExpiry(): string {
    const value = this.auth.getExpiresAt();
    return value
      ? new Intl.DateTimeFormat("en-AE", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
      : "—";
  }

  public get isSummaryRoute(): boolean {
    return this.router.url.startsWith("/dashboard/summary");
  }

  public get isSlaRoute(): boolean {
    return this.router.url.startsWith("/dashboard/sla");
  }

  public onAdminRefresh(): void {
    this.pageHeaderService.requestRefresh();
  }

  @HostListener("document:click")
  public closeAccountMenu(): void {
    this.accountMenuOpen = false;
  }

  private syncPageHeaderFromRoute(): void {
    if (this.isLoginView()) {
      this.pageHeader = { title: "" };
      this.pageHeaderService.clear();
      return;
    }
    let current: ActivatedRoute | null = this.route;
    while (current?.firstChild) current = current.firstChild;
    const data = current?.snapshot.data ?? {};
    this.pageHeader = {
      title: (data["title"] as string) || "",
      subtitle: (data["subtitle"] as string) || "",
      action: (data["headerAction"] as PageHeaderState["action"]) || null,
    };
    this.pageHeaderService.set(this.pageHeader);
  }
}
