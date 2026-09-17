import { CommonModule } from "@angular/common";
import { Component, OnInit, OnDestroy, inject } from "@angular/core";
import { FormsModule, NgForm } from "@angular/forms";
import { forkJoin, finalize, Subscription } from "rxjs";
import { AuthService } from "../services/auth.service";
import {
  AccessTypeOption, AdministrationRole, AdministrationService, AdministrationUser,
  AuditEvent, PermissionOption, RolePermissionAssignment, UserSession,
} from "./administration.service";

import { PasswordMatchDirective } from "./password-match.directive";

type AdministrationTab = "users" | "roles" | "sessions" | "audit";
interface UserFormModel { empCode: string; userName: string; firstName: string; lastName: string; email: string; passportNumber: string; nationality: string; designation: string; password: string; confirmPassword: string; isActive: boolean; mustChangePassword: boolean; roleIds: number[]; }

@Component({
  selector: "app-administration",
  standalone: true,
  imports: [CommonModule, FormsModule, PasswordMatchDirective],
  templateUrl: "./administration.component.html",
  styleUrl: "./administration.component.scss",
})
export class AdministrationComponent implements OnInit, OnDestroy {
  activeSessions = 0;
  private sessionRequest?: Subscription;
  private loadRequest?: Subscription;
  private sessionsRefreshing = false;
  private destroyed = false;
  private refreshTimer?: ReturnType<typeof setInterval>;
  private readonly administration = inject(AdministrationService);
  readonly auth = inject(AuthService);
  activeTab: AdministrationTab = "users";
  users: AdministrationUser[] = [];
  roles: AdministrationRole[] = [];
  permissions: PermissionOption[] = [];
  accessTypes: AccessTypeOption[] = [];
  sessions: UserSession[] = [];
  auditEvents: AuditEvent[] = [];
  selectedRole: AdministrationRole | null = null;
  searchTerm = "";
  roleFilter = "";
  statusFilter = "";
  loading = true;
  saving = false;
  errorMessage = "";
  successMessage = "";
  userDialogOpen = false;
  editingUser: AdministrationUser | null = null;
  viewingUser: AdministrationUser | null = null;
  userForm: UserFormModel = this.emptyUserForm();

  ngOnInit(): void {
    this.loadAdministration();
    this.refreshTimer = setInterval(() => this.refreshSessions(), 30000);
  }
  ngOnDestroy(): void { this.destroyed = true; if (this.refreshTimer) clearInterval(this.refreshTimer); this.sessionRequest?.unsubscribe(); this.loadRequest?.unsubscribe(); }
  private refreshSessions(): void {
    if (this.destroyed || this.loading || this.sessionsRefreshing || document.hidden || !this.auth.isAuthenticated() || !this.auth.hasPermission("Sessions")) return;
    this.sessionsRefreshing = true;
    this.sessionRequest = forkJoin({ sessions: this.administration.getSessions(), count: this.administration.getActiveSessionCount() })
      .pipe(finalize(() => this.sessionsRefreshing = false)).subscribe({ next: result => { this.sessions = result.sessions; this.activeSessions = result.count; }, error: () => {} });
  }
  get activeUsers(): number { return this.users.filter((user) => user.isActive).length; }
  get filteredUsers(): AdministrationUser[] {
    const term = this.searchTerm.trim().toLocaleLowerCase();
    if (!term) return this.users;
    return this.users.filter((user) => {
      const matchesTerm = !term || `${user.empCode ?? ""} ${user.displayName} ${user.userName} ${user.email ?? ""} ${user.nationality ?? ""} ${user.designation ?? ""} ${user.roles.join(" ")}`.toLocaleLowerCase().includes(term);
      const matchesRole = !this.roleFilter || user.roleIds.includes(Number(this.roleFilter));
      const matchesStatus = !this.statusFilter || user.isActive === (this.statusFilter === "active");
      return matchesTerm && matchesRole && matchesStatus;
    });
  }

  loadAdministration(): void {
    if (this.loadRequest && !this.loadRequest.closed) return;
    this.sessionRequest?.unsubscribe();
    this.loading = true;
    this.errorMessage = "";
    this.loadRequest = forkJoin({
      users: this.administration.getUsers(), roles: this.administration.getRoles(),
      permissions: this.administration.getPermissions(), accessTypes: this.administration.getAccessTypes(),
      sessions: this.administration.getSessions(), auditEvents: this.administration.getAuditEvents(),
      activeSessionCount: this.administration.getActiveSessionCount(),
    }).subscribe({
      next: (result) => {
        this.users = result.users; this.roles = result.roles; this.permissions = result.permissions;
        this.accessTypes = result.accessTypes; this.sessions = result.sessions; this.auditEvents = result.auditEvents;
        this.activeSessions = result.activeSessionCount;
        this.selectedRole = this.roles[0] ?? null; this.loading = false;
      },
      error: (error) => {
        this.errorMessage = error?.error?.message ?? "Administration data could not be loaded. Check the Identity API and database connection.";
        this.loading = false;
      },
    });
  }

  setTab(tab: AdministrationTab): void { this.activeTab = tab; this.clearMessages(); }
  openCreateUser(): void { this.editingUser = null; this.userForm = this.emptyUserForm(); this.userDialogOpen = true; this.clearMessages(); }
  openEditUser(user: AdministrationUser): void {
    if (user.isProtectedSystemAccount) { this.viewingUser = user; return; }
    this.editingUser = user;
    this.userForm = { empCode: user.empCode ?? "", userName: user.userName, firstName: user.firstName ?? "", lastName: user.lastName ?? "", email: user.email ?? "", passportNumber: "", nationality: user.nationality ?? "", designation: user.designation ?? "", password: "", confirmPassword: "", isActive: user.isActive, mustChangePassword: user.mustChangePassword, roleIds: [...user.roleIds] };
    this.userDialogOpen = true; this.clearMessages();
  }
  closeUserDialog(): void { this.userDialogOpen = false; }
  roleSelected(roleId: number): boolean { return this.userForm.roleIds.includes(roleId); }
  toggleUserRole(roleId: number, checked: boolean): void {
    this.userForm.roleIds = checked ? [...new Set([...this.userForm.roleIds, roleId])] : this.userForm.roleIds.filter((id) => id !== roleId);
  }

  saveUser(form: NgForm): void {
    if (this.saving || !form.valid) { form.control.markAllAsTouched(); return; }
    this.clearMessages();
    if (![this.userForm.userName, this.userForm.firstName, this.userForm.lastName, this.userForm.email].every((value) => value.trim())) { this.errorMessage = "User name, first name, last name and email are required."; return; }
    if (this.saving) return;
    if (this.userForm.password !== this.userForm.confirmPassword) { this.errorMessage = "Passwords do not match."; return; }
    if (!this.editingUser && this.userForm.password.length < 8) { this.errorMessage = "A password of at least 8 characters is required."; return; }
    this.saving = true;
    const request$ = this.editingUser
      ? this.administration.updateUser(this.editingUser.id, { empCode: this.userForm.empCode.trim(), userName: this.userForm.userName.trim(), firstName: this.userForm.firstName.trim(), lastName: this.userForm.lastName.trim(), email: this.userForm.email.trim(), passportNumber: this.userForm.passportNumber.trim() || null, nationality: this.userForm.nationality.trim(), designation: this.userForm.designation.trim() || null, isActive: this.userForm.isActive, mustChangePassword: this.userForm.mustChangePassword, roleIds: this.userForm.roleIds })
      : this.administration.createUser({ empCode: this.userForm.empCode.trim(), userName: this.userForm.userName.trim(), firstName: this.userForm.firstName.trim(), lastName: this.userForm.lastName.trim(), email: this.userForm.email.trim(), passportNumber: this.userForm.passportNumber.trim(), nationality: this.userForm.nationality.trim(), designation: this.userForm.designation.trim() || null, password: this.userForm.password, confirmPassword: this.userForm.confirmPassword, isActive: this.userForm.isActive, mustChangePassword: this.userForm.mustChangePassword, roleIds: this.userForm.roleIds });
    request$.subscribe({
      next: (saved) => {
        const index = this.users.findIndex((user) => user.id === saved.id);
        this.users = index >= 0 ? this.users.map((user) => user.id === saved.id ? saved : user) : [...this.users, saved];
        this.successMessage = this.editingUser ? "User updated successfully." : "User created successfully.";
        this.saving = false; this.userDialogOpen = false;
      },
      error: (error) => { this.errorMessage = error?.error?.message ?? "The user could not be saved."; this.saving = false; },
    });
  }

  resetPassword(user: AdministrationUser): void {
    const temporaryPassword = prompt(`Enter a new password for ${user.displayName}:`);
    if (temporaryPassword === null) return;
    if (temporaryPassword.length < 8) { this.errorMessage = "Password must be at least 8 characters."; return; }
    this.administration.resetPassword(user.id, temporaryPassword, !user.isProtectedSystemAccount).subscribe({
      next: () => this.successMessage = user.isProtectedSystemAccount ? "Administrator password changed." : "Temporary password set. The user must change it at next login.",
      error: (error) => this.errorMessage = error?.error?.message ?? "Password could not be reset.",
    });
  }

  removeUser(user: AdministrationUser): void {
    if (user.isProtectedSystemAccount) return;
    if (!confirm(`Remove ${user.displayName}? Accounts with audit history will be deactivated instead.`)) return;
    this.clearMessages();
    this.administration.removeUser(user.id).subscribe({
      next: () => {
        this.administration.getUsers().subscribe({ next: (users) => this.users = users });
        this.successMessage = "User removed successfully.";
      },
      error: (error) => this.errorMessage = error?.error?.message ?? "The user could not be removed.",
    });
  }

  selectRole(role: AdministrationRole): void { this.selectedRole = { ...role, permissions: role.permissions.map((item) => ({ ...item })) }; this.clearMessages(); }
  hasRolePermission(permissionId: number, accessTypeId: number): boolean {
    return this.selectedRole?.permissions.some((item) => item.permissionId === permissionId && item.accessTypeId === accessTypeId) ?? false;
  }
  toggleRolePermission(permissionId: number, accessTypeId: number, checked: boolean): void {
    if (!this.selectedRole || this.selectedRole.isProtected) return;
    const matches = (item: RolePermissionAssignment) => item.permissionId === permissionId && item.accessTypeId === accessTypeId;
    this.selectedRole.permissions = checked
      ? [...this.selectedRole.permissions.filter((item) => !matches(item)), { permissionId, accessTypeId }]
      : this.selectedRole.permissions.filter((item) => !matches(item));
  }
  saveRolePermissions(): void {
    if (!this.selectedRole || this.selectedRole.isProtected) return;
    this.saving = true; this.clearMessages();
    this.administration.updateRolePermissions(this.selectedRole.id, this.selectedRole.permissions).subscribe({
      next: (saved) => {
        this.roles = this.roles.map((role) => role.id === saved.id ? saved : role); this.selectedRole = saved;
        this.successMessage = `${saved.name} permissions updated successfully.`; this.saving = false;
      },
      error: (error) => { this.errorMessage = error?.error?.message ?? "Role permissions could not be saved."; this.saving = false; },
    });
  }

  formatDate(value: string | null): string {
    if (!value) return "—";
    return new Intl.DateTimeFormat("en-AE", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
  }
  initials(name: string): string { return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase(); }
  private emptyUserForm(): UserFormModel { return { empCode: "", userName: "", firstName: "", lastName: "", email: "", passportNumber: "", nationality: "", designation: "", password: "", confirmPassword: "", isActive: true, mustChangePassword: false, roleIds: [] }; }
  private clearMessages(): void { this.errorMessage = ""; this.successMessage = ""; }
}
