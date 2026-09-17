/*
    EnTrackBag manual installation for an EXISTING BLTSMFT database
    ----------------------------------------------------------------
    Creates only the nine EnTrackBag-owned identity/security tables:
      Roles, Permissions, AccessTypes, Users, UserRoles, RolePermissions,
      UserSessions, AuditEvents, EnTrackBagExceptions.

    Existing MFT operational tables and their data are never recreated,
    truncated, deleted or modified by this script.

    DEVELOPMENT DEPLOYMENT: the single maintained installation entry point.
    For a new system account, generate an Identity V3 PBKDF2-HMAC-SHA512 hash
    with New-DevelopmentPasswordHash.ps1 and set @InitialPasswordHash below.
    No plaintext or reversible password is embedded. Existing users are never reseeded.
    Back up BLTSMFT before applying. Run this file in SSMS.
*/

USE [BLTSMFT];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.AlarmList', N'U') IS NULL
    THROW 51000, 'BLTSMFT operational schema was not found. Run this only after installing the MFT database.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Roles
        (
            Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED,
            Name VARCHAR(100) NOT NULL,
            Description VARCHAR(500) NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT (1),
            CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT UQ_Roles_Name UNIQUE (Name)
        );
    END;

    IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Permissions
        (
            Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED,
            Code VARCHAR(150) NOT NULL,
            Name VARCHAR(150) NOT NULL,
            Description VARCHAR(500) NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_Permissions_IsActive DEFAULT (1),
            CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Permissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT UQ_Permissions_Code UNIQUE (Code)
        );
    END;

    IF OBJECT_ID(N'dbo.AccessTypes', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AccessTypes
        (
            Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccessTypes PRIMARY KEY CLUSTERED,
            Code VARCHAR(50) NOT NULL,
            Name VARCHAR(100) NOT NULL,
            Description VARCHAR(500) NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_AccessTypes_IsActive DEFAULT (1),
            CONSTRAINT UQ_AccessTypes_Code UNIQUE (Code)
        );
    END;

    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Users
        (
            Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY CLUSTERED,
            EmpCode VARCHAR(50) NULL,
            UserName VARCHAR(256) NOT NULL,
            FirstName VARCHAR(100) NULL,
            LastName VARCHAR(100) NULL,
            DisplayName VARCHAR(256) NULL,
            Email VARCHAR(256) NULL,
            PassportNumberEncrypted VARBINARY(512) NULL,
            PassportLast4 VARCHAR(4) NULL,
            Nationality VARCHAR(100) NULL,
            Designation VARCHAR(150) NULL,
            PasswordHash VARCHAR(1000) NOT NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
            MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT (1),
            LastLoginAt DATETIME2(3) NULL,
            LastLogoutAt DATETIME2(3) NULL,
            FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
            LockedUntil DATETIME2(3) NULL,
            CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt DATETIME2(3) NULL,
            CreatedBy INT NULL,
            UpdatedBy INT NULL,
            CONSTRAINT UQ_Users_UserName UNIQUE (UserName),
            CONSTRAINT FK_Users_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_Users_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id)
        );
    END;

    IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
    BEGIN
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
    END;

    IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
    BEGIN
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
    END;

    IF OBJECT_ID(N'dbo.UserSessions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.UserSessions
        (
            SessionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserSessions PRIMARY KEY CLUSTERED,
            UserId INT NOT NULL,
            LoginAt DATETIME2(3) NOT NULL CONSTRAINT DF_UserSessions_LoginAt DEFAULT (SYSUTCDATETIME()),
            LastActivityAt DATETIME2 NOT NULL,
            LogoutAt DATETIME2(3) NULL,
            RemoteIp VARCHAR(64) NULL,
            UserAgent VARCHAR(1000) NULL,
            CorrelationId VARCHAR(100) NULL,
            TokenIssuedAt DATETIME2(3) NULL,
            TokenExpiresAt DATETIME2(3) NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_UserSessions_IsActive DEFAULT (1),
            LogoutReason VARCHAR(250) NULL,
            CONSTRAINT FK_UserSessions_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
        );
    END;

    IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AuditEvents
        (
            Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditEvents PRIMARY KEY CLUSTERED,
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
            CONSTRAINT FK_AuditEvents_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
            CONSTRAINT FK_AuditEvents_Session FOREIGN KEY (SessionId) REFERENCES dbo.UserSessions(SessionId)
        );
    END;

    IF OBJECT_ID(N'dbo.EnTrackBagExceptions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EnTrackBagExceptions
        (
            ExceptionID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EnTrackBagExceptions PRIMARY KEY CLUSTERED,
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
            AdditionalData NVARCHAR(MAX) NULL
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Reconcile a Users table created by an earlier EnTrackBag identity script. */
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH('dbo.Users','EmpCode') IS NULL ALTER TABLE dbo.Users ADD EmpCode VARCHAR(50) NULL;
    IF COL_LENGTH('dbo.Users','FirstName') IS NULL ALTER TABLE dbo.Users ADD FirstName VARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Users','LastName') IS NULL ALTER TABLE dbo.Users ADD LastName VARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Users','Email') IS NULL ALTER TABLE dbo.Users ADD Email VARCHAR(256) NULL;
    IF COL_LENGTH('dbo.Users','PassportNumberEncrypted') IS NULL ALTER TABLE dbo.Users ADD PassportNumberEncrypted VARBINARY(512) NULL;
    IF COL_LENGTH('dbo.Users','PassportLast4') IS NULL ALTER TABLE dbo.Users ADD PassportLast4 VARCHAR(4) NULL;
    IF COL_LENGTH('dbo.Users','Nationality') IS NULL ALTER TABLE dbo.Users ADD Nationality VARCHAR(100) NULL;
    IF COL_LENGTH('dbo.Users','Designation') IS NULL ALTER TABLE dbo.Users ADD Designation VARCHAR(150) NULL;
    IF COL_LENGTH('dbo.Users','LastLogoutAt') IS NULL ALTER TABLE dbo.Users ADD LastLogoutAt DATETIME2(3) NULL;
    IF COL_LENGTH('dbo.Users','FailedLoginCount') IS NULL ALTER TABLE dbo.Users ADD FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0);
    IF COL_LENGTH('dbo.Users','LockedUntil') IS NULL ALTER TABLE dbo.Users ADD LockedUntil DATETIME2(3) NULL;
    IF COL_LENGTH('dbo.Users','CreatedBy') IS NULL ALTER TABLE dbo.Users ADD CreatedBy INT NULL;
    IF COL_LENGTH('dbo.Users','UpdatedBy') IS NULL ALTER TABLE dbo.Users ADD UpdatedBy INT NULL;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Bring older identity-only session schemas to the verified current shape. */
IF COL_LENGTH('dbo.UserSessions','LastActivityAt') IS NULL
    ALTER TABLE dbo.UserSessions ADD LastActivityAt DATETIME2 NULL;
GO
UPDATE dbo.UserSessions SET LastActivityAt=LoginAt WHERE LastActivityAt IS NULL;
ALTER TABLE dbo.UserSessions ALTER COLUMN LastActivityAt DATETIME2 NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.UserSessions') AND name='UX_UserSessions_OneActivePerUser')
BEGIN
    IF EXISTS (SELECT UserId FROM dbo.UserSessions WHERE IsActive=1 GROUP BY UserId HAVING COUNT(*)>1)
        THROW 51004, 'Resolve duplicate active sessions before installing the unique session index; history must be preserved.', 1;
    CREATE UNIQUE INDEX UX_UserSessions_OneActivePerUser ON dbo.UserSessions(UserId) WHERE IsActive=1;
END;
GO

/* Indexes are created only when missing. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Users') AND name='IX_Users_IsActive') CREATE INDEX IX_Users_IsActive ON dbo.Users(IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Users') AND name='UQ_Users_EmpCode') CREATE UNIQUE INDEX UQ_Users_EmpCode ON dbo.Users(EmpCode) WHERE EmpCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Users') AND name='UQ_Users_Email') CREATE UNIQUE INDEX UQ_Users_Email ON dbo.Users(Email) WHERE Email IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.UserRoles') AND name='IX_UserRoles_RoleId') CREATE INDEX IX_UserRoles_RoleId ON dbo.UserRoles(RoleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.RolePermissions') AND name='IX_RolePermissions_PermissionId') CREATE INDEX IX_RolePermissions_PermissionId ON dbo.RolePermissions(PermissionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.RolePermissions') AND name='IX_RolePermissions_AccessTypeId') CREATE INDEX IX_RolePermissions_AccessTypeId ON dbo.RolePermissions(AccessTypeId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.UserSessions') AND name='IX_UserSessions_UserId') CREATE INDEX IX_UserSessions_UserId ON dbo.UserSessions(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.UserSessions') AND name='IX_UserSessions_IsActive') CREATE INDEX IX_UserSessions_IsActive ON dbo.UserSessions(IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.UserSessions') AND name='IX_UserSessions_LoginAt') CREATE INDEX IX_UserSessions_LoginAt ON dbo.UserSessions(LoginAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.UserSessions') AND name='IX_UserSessions_ActiveUser') CREATE INDEX IX_UserSessions_ActiveUser ON dbo.UserSessions(UserId,IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AuditEvents') AND name='IX_AuditEvents_OccurredAt') CREATE INDEX IX_AuditEvents_OccurredAt ON dbo.AuditEvents(OccurredAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AuditEvents') AND name='IX_AuditEvents_UserId') CREATE INDEX IX_AuditEvents_UserId ON dbo.AuditEvents(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AuditEvents') AND name='IX_AuditEvents_SessionId') CREATE INDEX IX_AuditEvents_SessionId ON dbo.AuditEvents(SessionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AuditEvents') AND name='IX_AuditEvents_CorrelationId') CREATE INDEX IX_AuditEvents_CorrelationId ON dbo.AuditEvents(CorrelationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AuditEvents') AND name='IX_AuditEvents_Action') CREATE INDEX IX_AuditEvents_Action ON dbo.AuditEvents(Action);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.EnTrackBagExceptions') AND name='IX_EnTrackBagExceptions_OccurredAt') CREATE INDEX IX_EnTrackBagExceptions_OccurredAt ON dbo.EnTrackBagExceptions(OccurredAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.EnTrackBagExceptions') AND name='IX_EnTrackBagExceptions_CorrelationId') CREATE INDEX IX_EnTrackBagExceptions_CorrelationId ON dbo.EnTrackBagExceptions(CorrelationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.EnTrackBagExceptions') AND name='IX_EnTrackBagExceptions_StatusCode') CREATE INDEX IX_EnTrackBagExceptions_StatusCode ON dbo.EnTrackBagExceptions(StatusCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.EnTrackBagExceptions') AND name='IX_EnTrackBagExceptions_ExceptionType') CREATE INDEX IX_EnTrackBagExceptions_ExceptionType ON dbo.EnTrackBagExceptions(ExceptionType);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.Users') AND name='FK_Users_CreatedBy')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT FK_Users_CreatedBy FOREIGN KEY(CreatedBy) REFERENCES dbo.Users(Id)';
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.Users') AND name='FK_Users_UpdatedBy')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT FK_Users_UpdatedBy FOREIGN KEY(UpdatedBy) REFERENCES dbo.Users(Id)';
GO


/* Reference data, exactly one system-user seed, and relationships in one transaction. */
DECLARE @InitialPasswordHash VARCHAR(1000) = 'REPLACE_WITH_PBKDF2_SHA512_HASH';
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult INT;
    EXEC @LockResult=sys.sp_getapplock @Resource='EnTrackBag.IdentitySeed',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 51005, 'Cannot acquire the identity seed lock.', 1;

    -- Rename the earlier permission in place, preserving existing grants.
    IF EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code='Dashboard.SLA')
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code='Dashboard.SLA.View')
            THROW 51006, 'Both SLA permission codes exist. Review their grants before deploying.', 1;
        UPDATE dbo.Permissions SET Code='Dashboard.SLA.View' WHERE Code='Dashboard.SLA';
    END;

    INSERT dbo.AccessTypes(Code,Name,Description)
    SELECT v.Code,v.Name,v.Description FROM (VALUES
    ('VIEW','View','Read/view access.'),('CREATE','Create','Create/add access.'),
    ('EDIT','Edit','Modify/update access.'),('DELETE','Delete','Delete/remove access.'),
    ('EXPORT','Export','Export/download access.')) v(Code,Name,Description)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.AccessTypes x WHERE x.Code=v.Code);

    INSERT dbo.Roles(Name,Description)
    SELECT v.Name,v.Description FROM (VALUES
    ('Ground Floor','Summary Dashboard only.'),('Supervisor','Summary and SLA dashboards.'),
    ('Site Manager','Operational pages including Bag Journey Configuration.'),
    ('Admin','Full application administration; the system account is protected.')) v(Name,Description)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles x WHERE x.Name=v.Name);

    INSERT dbo.Permissions(Code,Name,Description)
    SELECT v.Code,v.Name,v.Description FROM (VALUES
    ('Dashboard','Summary Dashboard','Access to Summary Dashboard.'),
    ('Dashboard.SLA.View','SLA Dashboard','Access to SLA Dashboard.'),
    ('DeviceStatus','Device and System Status','Access to device status.'),
    ('DeviceStatus.Details','Device Details','Access to detailed device information.'),
    ('TagReport','Tag Report','Access to Tag Report.'),
    ('BagJourney','Bag Journey','Access to Bag Journey.'),
    ('BagJourney.Configuration','Bag Journey Configuration','Configure journey routing and thresholds.'),
    ('Administration','Administration','Access to Administration.'),
    ('Users','Users','User administration capability.'),
    ('Roles','Roles','Role and permission administration capability.'),
    ('Sessions','Sessions','View active user sessions.'),
    ('AuditLog','Audit Log','View audit events.')) v(Code,Name,Description)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions x WHERE x.Code=v.Code);

    DECLARE @AdminRoleId INT=(SELECT Id FROM dbo.Roles WHERE Name='Admin');
    DECLARE @AdminUserId INT;
    IF (SELECT COUNT(*) FROM dbo.Users WHERE LOWER(LTRIM(RTRIM(UserName)))='admin') > 1
        THROW 51007, 'Multiple admin usernames found; review existing data without deleting history.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE LOWER(LTRIM(RTRIM(UserName)))='admin')
    BEGIN
        IF @InitialPasswordHash='REPLACE_WITH_PBKDF2_SHA512_HASH'
            THROW 51008, 'Supply a generated PBKDF2-HMAC-SHA512 password hash before creating admin.', 1;
        DECLARE @Decoded VARBINARY(MAX)=CAST(N'' AS XML).value('xs:base64Binary(sql:variable("@InitialPasswordHash"))','varbinary(max)');
        IF DATALENGTH(@Decoded)<>61 OR SUBSTRING(@Decoded,1,1)<>0x01
            OR CONVERT(INT,SUBSTRING(@Decoded,2,4))<>2
            OR CONVERT(INT,SUBSTRING(@Decoded,6,4)) NOT BETWEEN 210000 AND 1000000
            OR CONVERT(INT,SUBSTRING(@Decoded,10,4))<>16
            THROW 51009, 'Expected an Identity V3 PBKDF2-HMAC-SHA512 hash from the generator.', 1;
        INSERT dbo.Users(UserName,DisplayName,PasswordHash,IsActive,MustChangePassword,FailedLoginCount,CreatedAt)
        VALUES('admin','Administrator',@InitialPasswordHash,1,0,0,SYSUTCDATETIME());
        SET @AdminUserId=CONVERT(INT,SCOPE_IDENTITY());
    END
    ELSE
        SELECT @AdminUserId=Id FROM dbo.Users WHERE LOWER(LTRIM(RTRIM(UserName)))='admin';

    -- Never overwrite an existing password, profile or other users on rerun.
    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId=@AdminUserId AND RoleId=@AdminRoleId)
        INSERT dbo.UserRoles(UserId,RoleId,AssignedBy) VALUES(@AdminUserId,@AdminRoleId,@AdminUserId);

    INSERT dbo.RolePermissions(RoleId,PermissionId,AccessTypeId,AssignedBy)
    SELECT r.Id,p.Id,a.Id,@AdminUserId
    FROM dbo.Roles r CROSS JOIN dbo.Permissions p CROSS JOIN dbo.AccessTypes a
    WHERE r.IsActive=1 AND p.IsActive=1 AND a.IsActive=1 AND
      ((r.Name='Admin')
       OR (r.Name='Ground Floor' AND p.Code='Dashboard' AND a.Code='VIEW')
       OR (r.Name='Supervisor' AND p.Code IN ('Dashboard','Dashboard.SLA.View') AND a.Code='VIEW')
       OR (r.Name='Site Manager' AND p.Code IN
          ('Dashboard','Dashboard.SLA.View','DeviceStatus','DeviceStatus.Details','TagReport','BagJourney','BagJourney.Configuration')
          AND a.Code='VIEW'))
    AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp
        WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id AND rp.AccessTypeId=a.Id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
PRINT 'Identity deployment complete. No BLTSMFT operational table or data was modified.';
