-- Run manually against the existing BLTSMFT database, then sign in again.
-- Correct the verified permission ID without changing any RolePermissions grants.
-- The old code below is used only to locate the row being corrected, never for authorization.
USE [BLTSMFT];
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.Permissions WITH (UPDLOCK, HOLDLOCK)
           WHERE Id = 2 AND Code = 'Dashboard.SLA.View')
BEGIN
    COMMIT TRANSACTION;
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WITH (UPDLOCK, HOLDLOCK)
               WHERE Id = 2 AND Code = 'Dashboard.SLA')
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'The SLA permission differs from the inspected data. Review before applying.', 1;
END;

IF EXISTS (SELECT 1 FROM dbo.Permissions WITH (UPDLOCK, HOLDLOCK)
           WHERE Code = 'Dashboard.SLA.View' AND Id <> 2)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50002, 'Another canonical SLA permission exists. Review mappings before applying.', 1;
END;

UPDATE dbo.Permissions SET Code = 'Dashboard.SLA.View'
WHERE Id = 2 AND Code = 'Dashboard.SLA';

COMMIT TRANSACTION;
-- Admin, Supervisor and Site Manager retain their existing VIEW grants.
-- No new role/user grants are introduced; unauthorized users remain blocked.
