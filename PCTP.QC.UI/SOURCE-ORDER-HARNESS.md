# DB14 source-order -> DB57 acceptance harness

## Objective

The preferred G5/G6 fixture is now a **real recent delivery group from SQL14**, not a completely invented order.

The harness deliberately separates source and target:

```text
SQL14 / BRAVO / B7R2_FCC
        |
        | SELECT only, read-only login
        v
LUUPHIEUGIAOHANG + LUUDOCQRCODE
        |
        | choose newest day + one delivery group
        | join QR/LOT/STOCKTP by MAHANG + LEFT(LOT,13)
        v
runner TEMP fixture JSON
        |
        | transform identity only
        v
SQL57 / B7R2_FCC
        |
        + B20Item fixture
        + STOCKTP fixture
        + TMPPHIEUGIAOHANG pending rows
        |
        | real PCTP.exe UI scan
        v
DOCQRCODE / LUUPHIEUGIAOHANG / LUUDOCQRCODE
        |
        v
stock / slot / history / guard assertions
        |
        v
prefix-scoped cleanup
```

## Safety rules

1. SQL14 is read-only. `PctpLiveData.Common.ps1` proves the source login has no INSERT/UPDATE/DELETE/EXECUTE/ALTER privileges before reading business data.
2. The source fixture JSON is kept only in `$RUNNER_TEMP`; it is not uploaded as an artifact.
3. SQL57 is the only write target. The target is hard-pinned to `192.168.200.57 / B7R2_FCC` by the source-driven seed script.
4. Every SQL57 fixture row is keyed by `QCUI_<8 chars>`.
5. The real result tables (`DOCQRCODE`, `LUUPHIEUGIAOHANG`, `LUUDOCQRCODE`) are **not seeded**. They must be produced by the real UI/procedure.
6. DB14 is never updated as part of a test.
7. A production promotion is not part of this harness. Only reviewed SQL/application diffs may later be promoted to DB14.

## Preferred workflow

Run **PCTP DB57 source-order harness** manually.

### Phase `seed`

Confirm both:

- `confirm_db14_read_only = true`
- `confirm_db57_test_writes = true`

The runner:

1. reads the newest `LUUPHIEUGIAOHANG.NGAYGIAO` day;
2. selects one `LUUPHIEUGIAOHANG.STT` group with the strongest QR coverage;
3. normalizes bare single-LOT rows to `LOT-quantity` in the runner-local fixture;
4. creates new DB57 item identities;
5. preserves the source LOT format while replacing only the encoded item-id segment with the new DB57 item id;
6. creates matching `STOCKTP` rows with enough quantity for the requested shipment;
7. creates pending `TMPPHIEUGIAOHANG` rows.

The actual QR/result rows are then created by the real PCTP.exe flow.

### Phase `assert`

After the real HVN - MP UI has scanned/confirmed the fixture, run `assert` with the printed prefix.

The current hard assertions are:

- `STOCKTP`: `SLNHAP - SLXUAT = SLCONLAI`;
- `SLCONLAI >= 0`;
- `SLXUAT <= SLNHAP`;
- delivered quantity does not exceed issued stock;
- every QR row created by this run resolves to its fixture stock by `MAHANG + LEFT(LOT,13)`.

Information-only columns must not be promoted to hard assertions until the live data proves they are invariants.

### Phase `cleanup`

Run cleanup with the same prefix. Cleanup removes the fixture and UI-created rows tied to the QC item prefix. The run is considered complete only when the verification query reports `Remaining = 0`.

The current harness still records a limitation: external movement/command ledger rows that do not carry the QC prefix require a separate verified cleanup strategy. They must not be deleted by a broad table cleanup.

## Why the source data is transformed instead of copied

The purpose is to test production SQL/business code against the **real shape and relationships** while guaranteeing that DB14 business rows are untouched. The LOT's structural components are retained; the item identity is regenerated on DB57 so all resulting writes remain unambiguously owned by the test run.

The measured live relationship remains:

`MAHANG/PART + LEFT(LOT,13)`

for `LUUPHIEUGIAOHANG`, `LUUDOCQRCODE` and `STOCKTP`. `LUUDOCQRCODE.STT` is not treated as the delivery-slip key.
