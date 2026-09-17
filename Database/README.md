# EnTrackBag Database Installation

The application uses the existing `BLTSMFT` SQL Server database. The install script creates only EnTrackBag-owned Identity/authorization/session/audit/exception tables; it does not recreate or modify operational baggage tables.

## EnTrackBag-owned tables
1. `Roles`
2. `Permissions`
3. `AccessTypes`
4. `Users`
5. `UserRoles`
6. `RolePermissions`
7. `UserSessions`
8. `AuditEvents`
9. `EnTrackBagExceptions`

## Authorization model
`Permissions` identify a page/function. `AccessTypes` identify the operation (`VIEW`, `CREATE`, `EDIT`, `DELETE`, `EXPORT`). `RolePermissions` joins Role + Permission + AccessType.

Role matrix:
- Ground Floor: Summary Dashboard VIEW only.
- Supervisor: Summary Dashboard VIEW + SLA Dashboard VIEW.
- Site Manager: all operational pages VIEW except Administration.
- Admin: all pages VIEW including Administration; Users VIEW/CREATE/EDIT/DELETE; Roles VIEW/EDIT; Sessions and Audit Log VIEW.

After installation, scaffold the tables with EF Core Database-First and verify the generated types against the live database. No migrations, `EnsureCreated()`, or `Database.Migrate()` are used.

## Single development deployment entry point

Use `Install-EnTrackBag-New-Tables-And-Admin.sql` for development deployment over an existing BLTSMFT database. Older install/patch files are historical, not additional deployment steps.

For a new database's first system account, run `New-DevelopmentPasswordHash.ps1` with PowerShell 7, enter a new password at its masked prompt, and paste only the emitted PBKDF2-HMAC-SHA512 hash into `@InitialPasswordHash` in the SQL script. No default/reversible password is shipped. A rerun does not require a new hash when admin already exists and never overwrites its password.

The script creates exactly the nine listed application-owned tables using existence checks, reconciles previously deployed identity columns, and seeds one username `admin` / display name `Administrator`. It captures the inserted ID with SCOPE_IDENTITY or selects the existing ID and seeds relationships without duplicates. Existing non-system users and operational data are preserved. Admin receives all existing active permissions/access types; Site Manager includes Bag Journey Configuration VIEW.

The application treats only username `admin` (case-insensitive) as the protected system account. It permits viewing and password changes, rejects profile/role/deactivation/deletion edits, and rejects changes to permissions of its assigned roles. Other users are not protected merely because they have an Admin role or the display name Administrator. Security audit/session history is retained; no ordinary per-user History action is exposed for the protected account.

Passwords use PBKDF2-HMAC-SHA512 with a random 16-byte salt, 210000 iterations and a 32-byte subkey, encoded in the ASP.NET Identity V3 format. Existing SHA512 Identity hashes verify and upgrade when appropriate. Plaintext, AES and legacy SHA256 password verification are removed; incompatible old accounts require an authorized password reset. Passport encryption is independent and unchanged.

The deployment script is saved, not automatically executed. Back up BLTSMFT before manual deployment. No EF migrations or operational seed data are used.
