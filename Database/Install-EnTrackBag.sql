USE [BLTSMFT];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
  EnTrackBag application-owned Identity/Authorization + Exception tables.
  Existing BLTSMFT operational tables are NOT recreated or modified.
  Database-First: scaffold these tables after installation and keep EF mappings aligned with the actual schema.
*/

CREATE TABLE dbo.Roles
(
    Id INT IDENTITY(1,1) NOT NULL,
    Name VARCHAR(100) NOT NULL,
    Description VARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT (1),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Roles_Name UNIQUE (Name)
);
GO

CREATE TABLE dbo.Permissions
(
    Id INT IDENTITY(1,1) NOT NULL,
    Code VARCHAR(150) NOT NULL,
    Name VARCHAR(150) NOT NULL,
    Description VARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Permissions_IsActive DEFAULT (1),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Permissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Permissions_Code UNIQUE (Code)
);
GO

CREATE TABLE dbo.AccessTypes
(
    Id INT IDENTITY(1,1) NOT NULL,
    Code VARCHAR(50) NOT NULL,
    Name VARCHAR(100) NOT NULL,
    Description VARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_AccessTypes_IsActive DEFAULT (1),
    CONSTRAINT PK_AccessTypes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_AccessTypes_Code UNIQUE (Code)
);
GO

CREATE TABLE dbo.Users
(
    Id INT IDENTITY(1,1) NOT NULL,
    UserName VARCHAR(256) NOT NULL,
    DisplayName VARCHAR(256) NULL,
    PasswordHash VARCHAR(1000) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT (1),
    LastLoginAt DATETIME2(3) NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(3) NULL,
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Users_UserName UNIQUE (UserName)
);
GO

CREATE TABLE dbo.UserRoles
(
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    AssignedAt DATETIME2(3) NOT NULL CONSTRAINT DF_UserRoles_AssignedAt DEFAULT (SYSUTCDATETIME()),
    AssignedBy INT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_UserRoles_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_UserRoles_Role FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES dbo.Users(Id)
);
GO

CREATE TABLE dbo.RolePermissions
(
    RoleId INT NOT NULL,
    PermissionId INT NOT NULL,
    AccessTypeId INT NOT NULL,
    AssignedAt DATETIME2(3) NOT NULL CONSTRAINT DF_RolePermissions_AssignedAt DEFAULT (SYSUTCDATETIME()),
    AssignedBy INT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RoleId, PermissionId, AccessTypeId),
    CONSTRAINT FK_RolePermissions_Role FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id),
    CONSTRAINT FK_RolePermissions_Permission FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(Id),
    CONSTRAINT FK_RolePermissions_AccessType FOREIGN KEY (AccessTypeId) REFERENCES dbo.AccessTypes(Id),
    CONSTRAINT FK_RolePermissions_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES dbo.Users(Id)
);
GO

CREATE TABLE dbo.UserSessions
(
    SessionId BIGINT IDENTITY(1,1) NOT NULL,
    UserId INT NOT NULL,
    LoginAt DATETIME2(3) NOT NULL CONSTRAINT DF_UserSessions_LoginAt DEFAULT (SYSUTCDATETIME()),
    LogoutAt DATETIME2(3) NULL,
    RemoteIp VARCHAR(64) NULL,
    UserAgent VARCHAR(1000) NULL,
    CorrelationId VARCHAR(100) NULL,
    TokenIssuedAt DATETIME2(3) NULL,
    TokenExpiresAt DATETIME2(3) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_UserSessions_IsActive DEFAULT (1),
    LogoutReason VARCHAR(250) NULL,
    CONSTRAINT PK_UserSessions PRIMARY KEY CLUSTERED (SessionId),
    CONSTRAINT FK_UserSessions_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);
GO

CREATE TABLE dbo.AuditEvents
(
    Id BIGINT IDENTITY(1,1) NOT NULL,
    OccurredAt DATETIME2(3) NOT NULL CONSTRAINT DF_AuditEvents_OccurredAt DEFAULT (SYSUTCDATETIME()),
    UserId INT NULL,
    UserName VARCHAR(256) NULL,
    SessionId BIGINT NULL,
    Action VARCHAR(200) NOT NULL,
    EntityType VARCHAR(200) NULL,
    EntityId VARCHAR(100) NULL,
    Description NVARCHAR(2000) NULL,
    RemoteIp VARCHAR(64) NULL,
    UserAgent VARCHAR(1000) NULL,
    CorrelationId VARCHAR(100) NULL,
    Success BIT NOT NULL CONSTRAINT DF_AuditEvents_Success DEFAULT (1),
    AdditionalData NVARCHAR(MAX) NULL,
    CONSTRAINT PK_AuditEvents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_AuditEvents_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_AuditEvents_Session FOREIGN KEY (SessionId) REFERENCES dbo.UserSessions(SessionId)
);
GO

CREATE TABLE dbo.EnTrackBagExceptions
(
    ExceptionID BIGINT IDENTITY(1,1) NOT NULL,
    OccurredAt DATETIME2(3) NOT NULL CONSTRAINT DF_EnTrackBagExceptions_OccurredAt DEFAULT (SYSUTCDATETIME()),
    CorrelationId VARCHAR(100) NULL,
    HttpMethod VARCHAR(20) NULL,
    RequestPath VARCHAR(1000) NULL,
    QueryString VARCHAR(2000) NULL,
    StatusCode INT NULL,
    ExceptionType VARCHAR(500) NULL,
    Message NVARCHAR(4000) NULL,
    StackTrace NVARCHAR(MAX) NULL,
    InnerException NVARCHAR(MAX) NULL,
    UserName VARCHAR(256) NULL,
    RemoteIp VARCHAR(64) NULL,
    UserAgent VARCHAR(1000) NULL,
    Source VARCHAR(100) NULL,
    IsHandled BIT NOT NULL CONSTRAINT DF_EnTrackBagExceptions_IsHandled DEFAULT (0),
    AdditionalData NVARCHAR(MAX) NULL,
    CONSTRAINT PK_EnTrackBagExceptions PRIMARY KEY CLUSTERED (ExceptionID)
);
GO

CREATE INDEX IX_Users_IsActive ON dbo.Users(IsActive);
CREATE INDEX IX_UserRoles_RoleId ON dbo.UserRoles(RoleId);
CREATE INDEX IX_RolePermissions_PermissionId ON dbo.RolePermissions(PermissionId);
CREATE INDEX IX_RolePermissions_AccessTypeId ON dbo.RolePermissions(AccessTypeId);
CREATE INDEX IX_UserSessions_UserId ON dbo.UserSessions(UserId);
CREATE INDEX IX_UserSessions_IsActive ON dbo.UserSessions(IsActive);
CREATE INDEX IX_UserSessions_LoginAt ON dbo.UserSessions(LoginAt);
CREATE INDEX IX_UserSessions_ActiveUser ON dbo.UserSessions(UserId, IsActive);
CREATE INDEX IX_AuditEvents_OccurredAt ON dbo.AuditEvents(OccurredAt);
CREATE INDEX IX_AuditEvents_UserId ON dbo.AuditEvents(UserId);
CREATE INDEX IX_AuditEvents_SessionId ON dbo.AuditEvents(SessionId);
CREATE INDEX IX_AuditEvents_CorrelationId ON dbo.AuditEvents(CorrelationId);
CREATE INDEX IX_AuditEvents_Action ON dbo.AuditEvents(Action);
CREATE INDEX IX_EnTrackBagExceptions_OccurredAt ON dbo.EnTrackBagExceptions(OccurredAt);
CREATE INDEX IX_EnTrackBagExceptions_CorrelationId ON dbo.EnTrackBagExceptions(CorrelationId);
CREATE INDEX IX_EnTrackBagExceptions_StatusCode ON dbo.EnTrackBagExceptions(StatusCode);
CREATE INDEX IX_EnTrackBagExceptions_ExceptionType ON dbo.EnTrackBagExceptions(ExceptionType);
GO

INSERT INTO dbo.AccessTypes (Code, Name, Description)
VALUES
('VIEW', 'View', 'Read/view access.'),
('CREATE', 'Create', 'Create/add access.'),
('EDIT', 'Edit', 'Modify/update access.'),
('DELETE', 'Delete', 'Delete/remove access.'),
('EXPORT', 'Export', 'Export/download access.');
GO

INSERT INTO dbo.Roles (Name, Description)
VALUES
('Ground Floor', 'Summary Dashboard only.'),
('Supervisor', 'Summary Dashboard and SLA Dashboard.'),
('Site Manager', 'All operational pages except Administration.'),
('Admin', 'Full access including Administration.');
GO

INSERT INTO dbo.Permissions (Code, Name, Description)
VALUES
('Dashboard', 'Summary Dashboard', 'Access to Summary Dashboard.'),
('Dashboard.SLA', 'SLA Dashboard', 'Access to SLA Dashboard.'),
('DeviceStatus', 'Device & System Status', 'Access to Device & System Status.'),
('DeviceStatus.Details', 'Device Details', 'Access to detailed device information.'),
('TagReport', 'Tag Report', 'Access to Tag Report.'),
('BagJourney', 'Bag Journey', 'Access to Bag Journey.'),
('BagJourney.Configuration', 'Bag Journey Configuration', 'Configure journey routing and thresholds.'),
('Administration', 'Administration', 'Access to Administration.'),
('Users', 'Users', 'User administration capability.'),
('Roles', 'Roles', 'Role and permission administration capability.'),
('Sessions', 'Sessions', 'View/manage active user sessions.'),
('AuditLog', 'Audit Log', 'View audit events.');
GO

DECLARE @VIEW INT = (SELECT Id FROM dbo.AccessTypes WHERE Code='VIEW');
DECLARE @CREATE INT = (SELECT Id FROM dbo.AccessTypes WHERE Code='CREATE');
DECLARE @EDIT INT = (SELECT Id FROM dbo.AccessTypes WHERE Code='EDIT');
DECLARE @DELETE INT = (SELECT Id FROM dbo.AccessTypes WHERE Code='DELETE');

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AccessTypeId)
SELECT r.Id,p.Id,@VIEW FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Name='Ground Floor' AND p.Code='Dashboard';

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AccessTypeId)
SELECT r.Id,p.Id,@VIEW FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Name='Supervisor' AND p.Code IN ('Dashboard','Dashboard.SLA');

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AccessTypeId)
SELECT r.Id,p.Id,@VIEW FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Name='Site Manager' AND p.Code IN ('Dashboard','Dashboard.SLA','DeviceStatus','DeviceStatus.Details','TagReport','BagJourney');

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AccessTypeId)
SELECT r.Id,p.Id,@VIEW FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Name='Admin' AND p.Code IN ('Dashboard','Dashboard.SLA','DeviceStatus','DeviceStatus.Details','TagReport','BagJourney','Administration','Sessions','AuditLog');

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AccessTypeId)
SELECT r.Id,p.Id,a.Id FROM dbo.Roles r CROSS JOIN dbo.Permissions p CROSS JOIN dbo.AccessTypes a
WHERE r.Name='Admin' AND p.Code='Users' AND a.Code IN ('VIEW','CREATE','EDIT','DELETE');

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AccessTypeId)
SELECT r.Id,p.Id,a.Id FROM dbo.Roles r CROSS JOIN dbo.Permissions p CROSS JOIN dbo.AccessTypes a
WHERE r.Name='Admin' AND p.Code IN ('Roles','BagJourney.Configuration') AND a.Code IN ('VIEW','EDIT');
GO

PRINT 'EnTrackBag Identity + AccessTypes + Exception tables installed successfully.';
GO
