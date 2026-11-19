# PCTP Application Authentication & Authorization

## Migration

Run `037_PCTP_ApplicationAuthenticationAndRbac.sql` against `FVN_PCTP`
after MES migration 036. The script creates the `SEC` schema, application
accounts, roles, function/action permissions, data scopes, login audit and
administration audit. It does not create SQL Server logins or database users.

No default username or password is seeded. On the first application start after
migration 037, the login window detects an empty account table and allows the
first administrator to be created. The first account is assigned
`SYSTEM_ADMIN` and receives explicit grants for the function/action catalog
present at migration time, plus an explicit `ALL` data-scope row. This is not
an implicit code-level administrator bypass. New function codes added later
must be added to the catalog and granted explicitly.

## Credential policy

- Passwords are stored as PBKDF2-HMAC-SHA256 hashes with a per-account random
  salt and 210,000 iterations; no plaintext password is stored.
- Minimum password length is 12 characters.
- Five failed attempts lock an account for 15 minutes.
- Reset passwords require a change at next login.
- Login success/failure and account/role/permission changes are audited.
- Passwords, hashes and salts must never be written to audit logs.

## Authorization model

- One account may have multiple roles.
- Permissions are explicit pairs of `FunctionCode + ActionCode`: VIEW,
  CREATE, EDIT, DELETE, CONFIRM, APPROVE, CANCEL, EXPORT, PRINT, ADMIN.
- A per-user override can ALLOW or DENY a role grant; DENY wins.
- Data scopes are explicit: ALL, SITE, DEPARTMENT, WAREHOUSE, LINE, PROCESS,
  OWN. A missing scope is denied; it is not treated as ALL.
- Menu visibility is only a usability layer. Application services must call
  `AuthorizationGuard` for each protected action and data scope.

The initial function catalog covers the main warehouse, MES, LOT, shipping,
quality, reporting, IFS synchronization and administration entry points. Add a
catalog row and wire its service-layer guard before exposing any new function.

## Initial rollout

1. Back up `FVN_PCTP`.
2. Apply migration 037.
3. Launch the app and create the first administrator.
4. Create operational roles (warehouse receiver, warehouse operator, MES planner,
   MES operator, QC, shipping, report viewer, etc.).
5. Configure each role's function/action matrix and data scopes.
6. Create user accounts, link EmployeeCode where applicable, and assign roles.
7. Test with a non-admin account; verify denied actions and out-of-scope sites.
8. Review `SEC.LoginAudit` and `SEC.AdminAudit`.

## Important deployment boundary

The current WinForms application connects directly to SQL Server using the
shared `FVN_PCTP` connection string. The application RBAC guards prevent normal
UI/service use without a grant, but they are not a hard security boundary against
a technically privileged user who can extract the shared SQL credential and
execute SQL outside the application. Strong isolation against that threat
requires moving business operations behind a trusted API or using distinct
database identities/permissions per authenticated user. Do not claim client
menu hiding alone provides database-level security.
