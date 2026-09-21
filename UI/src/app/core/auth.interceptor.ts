import { HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, throwError } from "rxjs";
import { AuthService } from "../services/auth.service";
import { BACKGROUND_REQUEST } from "./request-activity";
import { environment } from "../../environments/environment";

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const trusted = [environment.apiUrl, environment.identityApiUrl]
    .some(base => req.url.startsWith(base + "/"));
  const token = localStorage.getItem("access_token");
  if (!trusted || !token || req.url.endsWith("/auth/login")) return next(req);
  const auth = inject(AuthService);
  // Foreground API calls count as user activity and renew the sliding idle timer.
  // Background polls (BACKGROUND_REQUEST) must not extend the session.
  if (!req.context.get(BACKGROUND_REQUEST)) auth.recordActivity();
  else auth.markRequestActivity();
  if (auth.getAccessToken() !== token) return throwError(() => new Error("Session expired"));
  const headers: Record<string, string> = { Authorization: `Bearer ${token}` };

  return next(req.clone({ setHeaders: headers })).pipe(catchError(error => {
    // An old request must not clear a newer login.
    if (error.status === 401 && auth.getAccessToken() === token) auth.clearSession();
    return throwError(() => error);
  }));
};
