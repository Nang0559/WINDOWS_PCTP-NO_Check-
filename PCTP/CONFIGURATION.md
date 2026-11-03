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
- The first copy is create-if-missing. On every later start the runtime `PCTP.Database.config` is brought in line with the shipped one (the file edited in the project and copied by the build): if they differ, the runtime file is backed up as `.bak-<timestamp>` and replaced. This is skipped while the shipped file still contains the `YOUR_SQL_*` template placeholders (so a published template never wipes real credentials) and when `PCTP_KEEP_RUNTIME_DATABASE_CONFIG=1` is set.
- After changing the database config, restart PCTP. No republish or reinstall is required.
- To intentionally reset to the published defaults, back up and delete both files in `%ProgramData%\PCTP\`, then start PCTP. The app will copy the defaults from its currently installed version.

## What to change

There is ONE SQL Server declaration: `B7R2_FCCdb`. Edit its server, catalog and login only. `SQLPROVIDER` (including the legacy `B7R2_FCCdbb` field), `SqlTableCreator` and all repositories read it.

The second entry, `accfast\bravo.B7R2_FCC.dbo`, exists only because four DevExpress report designers bind to that fixed name. It uses the XPO dialect and is rewritten from `B7R2_FCCdb` at every start-up (`ReportConnectionAlias`), so it cannot point at a different server. Keep it in the file but do not maintain it by hand.

Entity Framework and `Settings.Default` connection strings were removed: nothing in the application used them.

## Operational cautions

- Stop PCTP before editing the file; restart after saving.
- Protect this file because SQL credentials are stored in connection strings. Grant write access only to administrators and read access to PCTP users.
- Do not put production credentials in Git or distribute them in an unrestricted shared folder.
- DB14 production must remain read-only for the QC/test harness. Use approved isolated `B7R2_FCC_QC*` databases for writable QC tests.
- ClickOnce manifests remain immutable. Always publish and copy the complete publish output; never hand-edit a deployed `.deploy` file.

## Which file is really used, and how to check

- PCTP reads `%ProgramData%\PCTP\PCTP.Database.config`, which is refreshed from the shipped file at start-up (see above). To change the server from Visual Studio: edit `PCTP\PCTP.Database.config`, rebuild (the build copies it next to the exe), start PCTP. If the shipped file is still the `YOUR_SQL_*` template, edit the runtime file instead.
- Hover the `DB:` item on the ribbon: the tooltip shows the file path, its last-modified time and the server SQL Server reports. A `[!]` after the caption means the startup check found something (below).
- Startup check (never overwrites your values): an old `PCTP.exe.config` that holds `<connectionStrings>` inline (so `PCTP.Database.config` would be ignored) is replaced after a `.bak-<timestamp>` backup; connection names that exist in the shipped file but not in the runtime file are added (otherwise `Settings.Designer.cs` silently falls back to its own defaults); names whose server/database differ from the shipped file, and runtime files that point to several different servers, are reported in the tooltip.
- To discard the runtime files and start again from the shipped ones, set the environment variable `PCTP_RESET_RUNTIME_CONFIG=1` for one start (both files are backed up first).

