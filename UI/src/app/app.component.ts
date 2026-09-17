import { Component, HostListener } from "@angular/core";
import {
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from "@angular/router";
import { AuthService } from "./services/auth.service";
import { NotificationService } from "./services/notification.service";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterLink, RouterOutlet, RouterLinkActive],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss",
})
export class AppComponent {
  accountMenuOpen = false;

  constructor(
    public auth: AuthService,
    public notifications: NotificationService,
    private _router: Router,
  ) {

  }

  public isLoginView(): boolean {
    return this._router.url === "/login" || this._router.url === "/";
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
    void this._router.navigate(["/login"]);
  }

  public toggleAccountMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.accountMenuOpen = !this.accountMenuOpen;
  }

  public get roleLabel(): string { return this.auth.getRoles().join(", ") || "Authenticated user"; }
  public get sessionExpiry(): string {
    const value = this.auth.getExpiresAt();
    return value ? new Intl.DateTimeFormat("en-AE", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)) : "—";
  }

  @HostListener("document:click")
  public closeAccountMenu(): void { this.accountMenuOpen = false; }
}
