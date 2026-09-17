import { HttpClient, HttpParams, HttpContext } from "@angular/common/http";
import { BACKGROUND_REQUEST } from "../core/request-activity";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../environments/environment";

export interface AdministrationUser {
  isProtectedSystemAccount: boolean;
  id: number;
  empCode: string | null;
  userName: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  email: string | null;
  passportMasked: string | null;
  nationality: string | null;
  designation: string | null;
  isActive: boolean;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
  lastLogoutAt: string | null;
  failedLoginCount: number;
  lockedUntil: string | null;
  createdAt: string;
  updatedAt: string | null;
  createdBy: number | null;
  updatedBy: number | null;
  roleIds: number[];
  roles: string[];
}

export interface RolePermissionAssignment { permissionId: number; accessTypeId: number; }
export interface AdministrationRole { isProtected: boolean; id: number; name: string; description: string | null; isActive: boolean; permissions: RolePermissionAssignment[]; }
export interface PermissionOption { id: number; code: string; name: string; description: string | null; }
export interface AccessTypeOption { id: number; code: string; name: string; }
export interface UserSession { sessionId: number; userId: number; userName: string; displayName: string; loginAt: string; logoutAt: string | null; remoteIp: string | null; userAgent: string | null; tokenExpiresAt: string | null; isActive: boolean; lastActivityAt: string; idleExpiresAt: string; status: string; }
export interface AuditEvent { id: number; occurredAt: string; userName: string | null; action: string; entityType: string | null; entityId: string | null; description: string | null; success: boolean; correlationId: string | null; }
export interface SaveUserRequest { empCode: string; userName: string; firstName: string; lastName: string; email: string; passportNumber: string | null; nationality: string; designation?: string | null; isActive: boolean; mustChangePassword: boolean; roleIds: number[]; }
export interface CreateUserRequest extends Omit<SaveUserRequest, "passportNumber"> { passportNumber: string; password: string; confirmPassword: string; }

@Injectable({ providedIn: "root" })
export class AdministrationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.identityApiUrl}/administration`;

  getUsers(search = "", roleId?: number, isActive?: boolean): Observable<AdministrationUser[]> {
    let params = new HttpParams();
    if (search.trim()) params = params.set("search", search.trim());
    if (roleId !== undefined) params = params.set("roleId", roleId);
    if (isActive !== undefined) params = params.set("isActive", isActive);
    return this.http.get<AdministrationUser[]>(`${this.baseUrl}/users`, { params });
  }
  createUser(request: CreateUserRequest): Observable<AdministrationUser> { return this.http.post<AdministrationUser>(`${this.baseUrl}/users`, request); }
  updateUser(id: number, request: SaveUserRequest): Observable<AdministrationUser> { return this.http.put<AdministrationUser>(`${this.baseUrl}/users/${id}`, request); }
  resetPassword(id: number, temporaryPassword: string, mustChangePassword = true): Observable<void> { return this.http.put<void>(`${this.baseUrl}/users/${id}/reset-password`, { temporaryPassword, mustChangePassword }); }
  removeUser(id: number): Observable<void> { return this.http.delete<void>(`${this.baseUrl}/users/${id}`); }
  getRoles(): Observable<AdministrationRole[]> { return this.http.get<AdministrationRole[]>(`${this.baseUrl}/roles`); }
  getPermissions(): Observable<PermissionOption[]> { return this.http.get<PermissionOption[]>(`${this.baseUrl}/permissions`); }
  getAccessTypes(): Observable<AccessTypeOption[]> { return this.http.get<AccessTypeOption[]>(`${this.baseUrl}/access-types`); }
  updateRolePermissions(id: number, permissions: RolePermissionAssignment[]): Observable<AdministrationRole> { return this.http.put<AdministrationRole>(`${this.baseUrl}/roles/${id}/permissions`, { permissions }); }
  getSessions(): Observable<UserSession[]> { return this.http.get<UserSession[]>(`${this.baseUrl}/sessions`, { context: new HttpContext().set(BACKGROUND_REQUEST, true) }); }
  getActiveSessionCount(): Observable<number> { return this.http.get<number>(`${this.baseUrl}/sessions/active-count`, { context: new HttpContext().set(BACKGROUND_REQUEST, true) }); }
  getAuditEvents(): Observable<AuditEvent[]> { return this.http.get<AuditEvent[]>(`${this.baseUrl}/audit-events`); }
}
