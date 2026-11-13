# SQL 00C result exports

Run `../00C_ApplicationUsageOwnershipEvidence.sql` against `B7R2_FCC` only.

Export each result set separately. Follow the exact file names and result mapping
in `../00C_ApplicationUsageOwnershipReview.md`, section 7.

Important:
- Keep the manifest and capture timestamp.
- Do not combine multiple result sets into one CSV.
- For empty results, preserve headers or record a zero-row export.
- If SQL Agent metadata is inaccessible, retain the `UNAVAILABLE` result and
  request the DBA export; do not label it as zero jobs.
- Do not upload credentials, connection strings, or business-row data.
- These files are review evidence only; they do not authorize migration.

Expected exports:
- 00C_00_RunManifest.csv
- 00C_00_TargetList.csv
- 00C_01_TargetTables.csv
- 00C_02_TargetColumns.csv
- 00C_03a_TargetIndexes.csv
- 00C_03b_TargetForeignKeys.csv
- 00C_04a_CheckConstraints.csv
- 00C_04b_TargetTriggers.csv
- 00C_05_ModuleReferences.csv
- 00C_06_StaticDependencies.csv
- 00C_07_SqlAgentJobSteps.csv
- 00C_08a_Synonyms.csv
- 00C_08b_UserDefinedTypes.csv
