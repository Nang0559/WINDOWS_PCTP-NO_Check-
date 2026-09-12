# DOCQR – Phase 6 audit

## Audit scope

Audited on `master`:

- `PCTP/Modules/GiaoHangKhach/Services/DocQRService.cs`
- `PCTP/Presentation/Presenters/HVN_Presenter.cs`
- `PCTP/Domain/Interfaces/IRepositories.cs`
- `PCTP/Infrastructure/Repositories/DocQRRepository.cs`
- `PCTP/Domain/Entities/DocQRCode.cs`
- `PCTP/Shared/Helpers/ScanResult.cs`
- `PCTP/Domain/Events/DomainEvent.cs`
- QR/TMP working-state contracts and implementations.

## Findings

### 1. DocQRService was still the QR orchestration monolith

It owns all of the following responsibilities:

- QR mode state (`_isBanSP`, `_isBanOType`)
- DOCQRCODE/TMP table selection
- QR route detection
- FCC / SP / O TYPE / HVN / YMVN parsing
- LOT normalization
- duplicate validation
- scan-order validation
- quantity validation
- DOCQRCODE persistence calls
- `QRScannedEvent` publication
- confirmation of quantity mismatch
- DOCQRCODE CRUD helpers used by the presenter

It contains no WinForms/DevExpress UI dependency in its public workflow, which is good. The remaining problem is internal responsibility concentration, not UI coupling.

### 2. HVN_Presenter is already a caller/orchestrator, not a QR parser

`HVN_Presenter` delegates scan processing to `DocQRService.ProcessScan` / `ProcessScanYMVN` and delegates DOCQRCODE persistence helpers (`LoadAll`, `XoaDong`, `XoaToanBo`, `CapNhapSlHvn`, `ConfirmSlKhacBiet`).

The presenter still supplies two validation callbacks backed by `PhieuService`. This is an integration seam, not QR parsing logic. It should be removed only after the order/working-state validation contract is introduced, to avoid changing business behavior during this phase.

### 3. Repository boundary is already appropriate

`IDocQRRepository` owns DOCQRCODE persistence and QR-specific database queries. `DocQRService` does not execute SQL directly.

### 4. QR state was still implicit inside DocQRService

The service duplicated table selection logic in multiple places and kept category state in private booleans. This made the distinction between session state and persistence-table selection harder to audit.

## Phase 6.4–6.8 hardening implemented

### 6.4 – QR service audit / state boundary

Added:

- `DocQRSessionState`
- `DocQRTableResolver`

The new state object explicitly represents MP/SP/O TYPE mode and derives the active DOCQRCODE/TMP table from configuration.

### 6.5 – PhieuService / QR service boundary

Existing architecture verified: `PhieuService` does not own QR scan parsing. `HVN_Presenter` calls `DocQRService` for scan and DOCQRCODE operations.

No public API was broken.

### 6.6 – Presenter boundary

Presenter remains responsible for UI decisions/dialogs and delegates QR business operations to `DocQRService`. No QR SQL was found in the presenter.

### 6.7 – DOCQRCODE/TMP working-state boundary

`PhieuTmpRepository` / `IDeliveryWorkingState` remain the persistence/working-state owners. The new table resolver/state types make the QR session/table distinction explicit without moving persistence into the service.

### 6.8 – Verification gate

GitHub Actions/status checks were queried for the resulting `master` commit. No CI checks are configured/returned (`statuses: []`). Therefore this phase is **not compile-verified by CI**.

A local Visual Studio build of `WINDOWS_PCTP(NO_Check).sln` is still required before declaring the refactor production-ready.

## Important non-goals

This phase intentionally does **not** rewrite the QR parsing algorithms or change LOT/quantity/duplicate rules. Those are behavior-sensitive and should be extracted only with tests or captured before/after examples.

It also does not merge `_isMayBanQR` and `_isBanQR`; machine capability and current QR session remain separate concepts.
