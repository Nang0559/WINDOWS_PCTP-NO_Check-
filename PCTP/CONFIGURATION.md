# PCTP runtime database configuration (ClickOnce)

## Where to edit after publishing

Edit this file on the client machine:

```text
%ProgramData%\PCTP\PCTP.Database.config
```

For example, open Notepad as Administrator and enter the path above. Change the connection strings in the XML file, save it, then completely close and reopen PCTP.

Do **not** edit `PCTP.Database.config.deploy`, `PCTP.exe.manifest`, or any file under the ClickOnce `Application Files\PCTP_...\` directory.

## First launch and updates

- On the first launch, PCTP copies `PCTP.exe.config` and `PCTP.Database.config` from the ClickOnce application directory to `%ProgramData%\PCTP\`.
- The runtime config is selected before the application accesses SQL Server, Entity Framework, or XPO configuration.
- The copy operation is create-if-missing. Later ClickOnce updates do not overwrite the administrator's runtime files.
- After changing the database config, restart PCTP. No republish or reinstall is required.
- To intentionally reset to the published defaults, back up and delete both files in `%ProgramData%\PCTP\`, then start PCTP. The app will copy the defaults from its currently installed version.

## What to change

Keep the XML structure and connection names. Update the server, catalog, authentication and related options in all required aliases in `PCTP.Database.config`. SQL Server, Entity Framework and XPO entries are kept in this one file.

The canonical SQL Server connection is `B7R2_FCCdb`; the legacy names remain for older modules and generated EF/XPO contexts.

## Operational cautions

- Stop PCTP before editing the file; restart after saving.
- Protect this file because SQL credentials are stored in connection strings. Grant write access only to administrators and read access to PCTP users.
- Do not put production credentials in Git or distribute them in an unrestricted shared folder.
- DB14 production must remain read-only for the QC/test harness. Use approved isolated `B7R2_FCC_QC*` databases for writable QC tests.
- ClickOnce manifests remain immutable. Always publish and copy the complete publish output; never hand-edit a deployed `.deploy` file.
