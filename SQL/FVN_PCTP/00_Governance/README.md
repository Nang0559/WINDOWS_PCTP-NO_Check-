# Governance, source inventory and evidence

This folder contains read-only source inventory scripts, ownership/dependency reviews, captured catalog exports, schema-generation tools and decision records. Source extraction scripts are for the legacy source database only; verify the guard at the top of each script before running. Evidence snapshots are historical and must be refreshed before a migration decision.

Nothing in this folder is part of the target deployment sequence. Never run source inventory or generator output against the new target as if it were an install script.
