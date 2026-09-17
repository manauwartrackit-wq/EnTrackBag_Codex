-- Explicitly approved Admin-only grants. Existing non-Admin mappings are unchanged.
USE [BLTSMFT];
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF (SELECT COUNT(*) FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK) WHERE Name='Admin' AND IsActive=1) <> 1
    THROW 50001, 'Expected exactly one active Admin role.', 1;

DECLARE @AdminId int = (SELECT Id FROM dbo.Roles WHERE Name='Admin' AND IsActive=1);

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, AccessTypeId)
SELECT @AdminId, p.Id, a.Id
FROM dbo.Permissions p CROSS JOIN dbo.AccessTypes a
WHERE p.IsActive=1 AND a.IsActive=1
AND NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions rp WITH (UPDLOCK, HOLDLOCK)
    WHERE rp.RoleId=@AdminId AND rp.PermissionId=p.Id AND rp.AccessTypeId=a.Id
);

SELECT @@ROWCOUNT AS AdminGrantsAdded;
COMMIT TRANSACTION;
