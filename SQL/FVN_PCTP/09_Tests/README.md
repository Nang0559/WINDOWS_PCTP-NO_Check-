# Tests

Only isolated disposable-database tests belong here. Fixtures and cleanup scripts are test-only; never run against the source or production database. Smoke tests must verify object existence, migration ledger, integrity constraints, stock-ledger reconciliation, workflow posting, idempotency, rollback and concurrency.


## IFS synchronization

`IFS_SyncSmokeTest.sql` is a read-only verification script. Run it only after
migration 012 and a successful full IFS sync. It verifies that the latest
successful batch has target rows and checks Alternative/Header and
Component/Alternative referential consistency. It also displays the latest
captured sync errors.
