# SQL2 metadata constraint generator

The companion generator reads the full SSMS fixed-width metadata export and emits reviewed-scope index/key, CHECK, and internal FK DDL plus a JSON review report. It does not generate module bodies, data, permissions, or objects whose referenced tables are outside the allow-list.

Run from repository root:

```powershell
python .\SQL\QC_Schema\tools\gen_constraints_from_metadata.py --metadata .\SQL\Result_Export_SQL2_Complete_DDL_Metadata.txt --coverage .\docs\SCHEMA_COVERAGE.csv --out .\SQL\QC_Schema\03_Generated_Constraints_From_DB57.sql --report .\SQL\QC_Schema\SQL2_Metadata_Review_Report.json
python -m unittest discover -s .\SQL\QC_Schema\tools -p "test_gen_constraints_from_metadata.py" -v
```

Repeat the generator command with `--check` to detect stale outputs. Filtered/disabled indexes and disabled/untrusted constraints fail closed for manual review. DEFAULTs are inventory-only because the table generator already emits defaults; compare them. Module names/types are inventoried only because SSMS text exports may truncate or fold multiline definitions. Review duplicate unique constraints/indexes explicitly. Static checks do not prove SQL Server execution. Apply only in a disposable CI database, never DB57 or production.
