# Data migration

No production data-copy script is approved yet. This phase must provide explicit source-to-target mappings, staging, deterministic keys, batch checkpoints, reject capture, retry behavior, row counts, checksums and business reconciliation. Never bulk-copy all B7R2_FCC objects. Preserve source evidence and do not alter the source database.
