# PCTP compile inventory — WMS branch

Baseline: `fe73e91cf25737def51a85ab0642723e5711085f`  
Project: `PCTP/PCTP.csproj` (legacy non-SDK project with explicit Compile items)

## Classification of the 24 files absent from the baseline project

### Production sources — added to PCTP.csproj (6)

- `Modules/KhoCore/Application/Contracts/Stock/QuarantineRelease.cs`
- `Modules/KhoCore/Application/Services/QuarantineReleaseService.cs`
- `Modules/KhoCore/Repositories/IQuarantineReleaseAuditRepository.cs`
- `Modules/KhoCore/Repositories/QuarantineReleaseAuditRepository.cs`
- `Modules/XuLyHangLoi/Application/ReworkWorkflowTransaction.cs`
- `Shared/Helpers/QRCodeWorkflowAdapter.cs`

These are application/runtime types, not test-only helpers. Their presence in QC test compilation did not make them part of the WinForms executable.

### Test-only source — intentionally not added (1)

- `Workflow/Tests/FifoSessionStateRegressionTests.cs`

It belongs to regression/test scope and must not be compiled into the production WinForms assembly.

### Legacy/UI candidates — left excluded pending business-owner decision (17)

- `Modules/GiaoHangKhach/NhanLaiNG/ImPortPGHDB.Designer.cs`
- `Modules/GiaoHangKhach/NhanLaiNG/ImPortPGHDB.cs`
- `Modules/GiaoHangKhach/NhanLaiNG/NhanLaiTP/frmNhanLaiTP.Designer.cs`
- `Modules/GiaoHangKhach/NhanLaiNG/NhanLaiTP/frmNhanLaiTP.cs`
- `Modules/GiaoHangKhach/NhanLaiNG/TACKMH.cs`
- `Modules/GiaoHangKhach/Reports/IN_GHP_LOT_YMVN.Designer.cs`
- `Modules/GiaoHangKhach/Reports/IN_GHP_LOT_YMVN.cs`
- `Modules/GiaoHangKhach/YMVN/ERR_CNK.Designer.cs`
- `Modules/GiaoHangKhach/YMVN/ERR_CNK.cs`
- `Modules/GiaoHangKhach/YMVN/GIAOHANGYMN.Designer.cs`
- `Modules/GiaoHangKhach/YMVN/GIAOHANGYMN.cs`
- `Modules/GiaoHangKhach/YMVN/YAMAHAQRCDE_SP.Designer.cs`
- `Modules/GiaoHangKhach/YMVN/YAMAHAQRCDE_SP.cs`
- `Modules/GiaoHangKhach/YMVN/YMVNDOCQRCODE.Designer.cs`
- `Modules/GiaoHangKhach/YMVN/YMVNDOCQRCODE.cs`
- `Modules/GiaoHangKhach/YMVN/YMVN_CHONGIAO.Designer.cs`
- `Modules/GiaoHangKhach/YMVN/YMVN_CHONGIAO.cs`

Reason for exclusion pending review: `PCTP/Program.cs` starts `Main_APP`; the visible YMVN entry points `GIAOHANGYMN` and `YAMAHAQRCDE_SP` are commented out. This is evidence of current startup exclusion, not proof that all related forms/reports are permanently retired. Confirm active callers, menu/navigation reachability, deployment usage, designer resources and customer workflow before adding or deleting them.

Phân loại chi tiết từng file, bằng chứng và việc còn lại: xem `WMS_LEGACY_FILE_CLASSIFICATION.md`.

## Required verification

- Run a case-insensitive tree-vs-`<Compile Include>` audit after each project edit.
- Run full solution MSBuild on Windows with the required DevExpress assemblies, restored NuGet packages, WebView2 targets and signing certificate.
- Run the production executable smoke test and verify the active navigation paths.
- Do not interpret QC test project compilation as production project compilation.


## Delivery command contract additions (2026-10-26)

The following two production contract files are now explicit compile items in `PCTP/PCTP.csproj`:

- `Modules/GiaoHangKhach/Contracts/Delivery/DeliveryStockCommand.cs`
- `Modules/GiaoHangKhach/Services/IDeliveryStockCommandHandler.cs`

They define PART-scoped LOT allocation and a stable business key boundary. They are **contract scaffolding only**: there is no production handler, no DI registration, and no cutover from the legacy stored procedures. See `WMS_B0_01_DELIVERY_PARITY.md`.

The 17 legacy form/report files remain excluded. The new delivery contract does not establish whether any legacy UI is still reachable.
