import { Routes } from "@angular/router";
import { authGuard } from "./core/auth.guard";

export const routes: Routes = [
  { path: "login", loadComponent: () => import("./login/login.component").then(m => m.LoginComponent) },
  {
    path: "dashboard/summary",
    loadComponent: () => import("./dashboard/summary/summary.component").then(m => m.SummaryComponent),
    canActivate: [authGuard],
    data: {
      permission: "Dashboard",
      accessType: "VIEW",
      title: "Dashboard",
      subtitle: "Operational summary and current bag flow.",
      headerAction: "dashboard-toggle",
    },
  },
  {
    path: "dashboard/sla",
    loadComponent: () => import("./dashboard/sla/sla.component").then(m => m.SlaComponent),
    canActivate: [authGuard],
    data: {
      permission: "Dashboard.SLA.View",
      accessType: "VIEW",
      title: "Dashboard",
      subtitle: "Service-level monitoring for active bags.",
      headerAction: "dashboard-toggle",
    },
  },
  {
    path: "device-status",
    loadComponent: () => import("./device-status/device-status.component").then(m => m.DeviceStatusComponent),
    canActivate: [authGuard],
    data: {
      permission: "DeviceStatus",
      accessType: "VIEW",
      title: "Device & System Status",
      subtitle: "Current communication and device status from verified operational data.",
    },
  },
  {
    path: "tag-report",
    loadComponent: () => import("./tag-report/tag-report.component").then(m => m.TagReportComponent),
    canActivate: [authGuard],
    data: {
      permission: "TagReport",
      accessType: "VIEW",
      title: "Tag Report",
      subtitle: "Search and review tag activity from operational records.",
    },
  },
  {
    path: "bag-journey",
    loadComponent: () => import("./bag-journey/bag-journey.component").then(m => m.BagJourneyComponent),
    canActivate: [authGuard],
    data: {
      permission: "BagJourney",
      accessType: "VIEW",
      title: "Bag Journey",
      subtitle: "Track bag movement across verified journey stages.",
    },
  },
  {
    path: "administration",
    loadComponent: () => import("./administration/administration.component").then(m => m.AdministrationComponent),
    canActivate: [authGuard],
    data: {
      permission: "Administration",
      accessType: "VIEW",
      title: "Administration",
      subtitle: "Manage users, roles, active sessions and system audit activity.",
      headerAction: "admin-refresh",
    },
  },
  {
    path: "bag-journey-configuration",
    loadComponent: () => import("./bag-journey-configuration/bag-journey-configuration.component").then(m => m.BagJourneyConfigurationComponent),
    canActivate: [authGuard],
    data: {
      permission: "BagJourney.Configuration",
      accessType: "VIEW",
      title: "Bag Journey Configuration",
      subtitle: "Configure bag journey stages, routing and service-level thresholds.",
    },
  },
  { path: "", pathMatch: "full", redirectTo: "login" },
  { path: "**", redirectTo: "login" },
];
