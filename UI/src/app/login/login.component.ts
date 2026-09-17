import { CommonModule } from "@angular/common";
import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import { AuthService, LoginRequest } from "../services/auth.service";

@Component({
  standalone: true,
  selector: "app-login",
  imports: [CommonModule, FormsModule],
  templateUrl: "./login.component.html",
  styleUrl: "./login.component.scss",
})
export class LoginComponent {
  userName = "";
  password = "";
  error = "";
  isSubmitting = false;

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  login(): void {
    if (this.isSubmitting) return;

    this.error = "";
    this.isSubmitting = true;

    const request: LoginRequest = {
      userName: this.userName.trim(),
      password: this.password,
    };

    this.authService.login(request).subscribe({
      next: () => {
        this.isSubmitting = false;

        this.router.navigateByUrl("/dashboard/summary");
      },
      error: () => {
        this.isSubmitting = false;
        this.error =
          "Invalid credentials or unavailable identity service.";
      },
    });
  }
}
