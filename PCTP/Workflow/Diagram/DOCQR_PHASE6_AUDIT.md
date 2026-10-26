# DOCQR – Phase 6 audit

## Audit scope

Audited and refactored on `master`:

- `PCTP/Modules/GiaoHangKhach/Services/DocQRService.cs`
- `PCTP/Modules/GiaoHangKhach/Services/DocQRScanEngine.cs`
- `PCTP/Modules/GiaoHangKhach/Services/DocQRSessionState.cs`
- `PCTP/Modules/GiaoHangKhach/Services/DocQRTableResolver.cs`
- `PCTP/Presentation/Presenters/HVN_Presenter.cs`
- `PCTP/Domain/Interfaces/IRepositories.cs`
- `PCTP/Infrastructure/Repositories/DocQRRepository.cs`
- `PCTP/Domain/Entities/DocQRCode.cs`
- `PCTP/Shared/Helpers/ScanResult.cs`
- `PCTP/Domain/Events/DomainEvent.cs`
- QR/TMP working-state contracts and implementations.

## Result

`DocQRService` is now a thin facade. The previous QR parsing/business implementation has been moved to `DocQRScanEngine` with the existing routing, validation, LOT normalization, repository calls and event publication preserved.

### Facade responsibilities

`DocQRService` now owns only the public service boundary and session-mode coordination:

- `SetCheDoBanSP`
- `SetCheDoBan`
- `IsBanSP` / `IsBanOType`
- delegation of QR scan operations
- delegation of DOCQRCODE CRUD helpers
- delegation of quantity-mismatch confirmation

The existing constructor and public method signatures are preserved for current callers.

### Engine responsibilities

`DocQRScanEngine` owns the QR behavior extracted from the former monolith:

- QR route detection
- FCC / SP / O TYPE / HVN / YMVN parsing
- LOT normalization
- duplicate validation
- scan-order validation
- quantity validation
- DOCQRCODE repository calls
- `QRScannedEvent` publication
- quantity-mismatch confirmation
- quantity calculation against TMP

No QR business rule was intentionally redesigned during this extraction.

### Session/table boundary

`DocQRSessionState` owns the current MP/SP/O TYPE session state.

`DocQRTableResolver` owns DOCQRCODE/TMP table selection from `CustomerConfig`.

The distinction between current QR session state and machine QR capability remains intact; `_isMayBanQR` / `_isBanQR` are not merged.

## Caller boundary

`HVN_Presenter` continues to call `DocQRService`; callers do not need to know about `DocQRScanEngine`.

The presenter remains responsible for UI decisions and continues to provide the existing validation callbacks. Removing those callbacks is intentionally deferred until an explicit order/working-state validation contract exists, so this refactor does not alter behavior.

`IDocQRRepository` remains the persistence boundary. No SQL was moved into the facade or engine.

## Build note

The project is a legacy non-SDK `.csproj` with explicit `Compile` items. `PCTP/Directory.Build.targets` was added to include the three new QR refactor source files without rewriting the large legacy project file.

GitHub status for the final refactor commit returned `statuses: []`; there is no CI verification available. A local Visual Studio build of `WINDOWS_PCTP(NO_Check).sln` is still required before production deployment.

## Non-goals

This phase does not intentionally change QR parsing algorithms, LOT rules, duplicate rules, quantity rules, repository semantics, or event semantics.
