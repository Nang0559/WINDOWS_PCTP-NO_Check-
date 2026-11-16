# Tests

Only isolated disposable-database tests belong here. Fixtures and cleanup scripts are test-only; never run against the source or production database. Smoke tests must verify object existence, migration ledger, integrity constraints, stock-ledger reconciliation, workflow posting, idempotency, rollback and concurrency.
