namespace EnTrackBag.Authorization;

public static class PermissionCodes
{
    public const string Dashboard = "Dashboard", DashboardSla = "Dashboard.SLA.View",
        DeviceStatus = "DeviceStatus", DeviceDetails = "DeviceStatus.Details", TagReport = "TagReport",
        BagJourney = "BagJourney", BagJourneyConfiguration = "BagJourney.Configuration",
        Administration = "Administration", Users = "Users", Roles = "Roles", Sessions = "Sessions", AuditLog = "AuditLog";

    public static readonly string[] All = [
        Dashboard, DashboardSla, DeviceStatus, DeviceDetails,
        TagReport, BagJourney, BagJourneyConfiguration, Administration,
        Users, Roles, Sessions, AuditLog
    ];
}

public static class AccessTypeCodes
{
    public const string View = "VIEW", Create = "CREATE", Edit = "EDIT", Delete = "DELETE", Export = "EXPORT";
    public static readonly string[] All = [
        View, Create, Edit, Delete, Export
    ];
}
