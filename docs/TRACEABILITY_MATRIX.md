# G1 - Ma trận truy vết (code -> SP/SQL -> transaction -> test)

- Commit: `53af05dcde9d8c0da9942dfca3369098c7a30701`  |  sinh bởi `PCTP.QC.UI/Build-TraceabilityMatrix.py` (phân tích tĩnh, không mở DB)
- Dòng code: 88 (COMPLETE 22, **GAP 66**)  |  SQL script: 17  |  dòng TestMatrix không `implemented`: 22
- Tổng số gap: **105**. Gate G1 chỉ PASS khi bằng 0.

Giới hạn: regex trên chuỗi SQL trong C#; SQL ghép động (string concat từ biến) hoặc đặt tên bảng trong biến sẽ không bắt được - những dòng đó có cột `Ghi` rỗng nhưng vẫn gọi `Execute*` được liệt kê ở `hasSql`. Cần review tay các dòng này.

## Repository / Service

| ID | Module | Loại | File nguồn | SP | Bảng ghi | Transaction | Test unit | Test SQL | Trạng thái |
|---|---|---|---|---|---|---|---|---|---|
| G1-001 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/CurrentStockQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-002 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/DeliveryTraceReadService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-003 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/LotLocationQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-004 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/QualityHistoryQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-005 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/StockHistoryQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-006 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/CurrentStockRepository.cs` | sp_GetCurrentStockStatus | — | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-007 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/DeliveryTraceRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-008 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/LotLocationRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-009 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/QualityHistoryRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-010 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/StockHistoryRepository.cs` | sp_GetStockHistory | — | PARTICIPANT (doc) | BulkStockAdjustServiceTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-011 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Reports/Report.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-012 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/ItemFifoConfigRepository.cs` | — | FVN_ItemFifoConfig, FVN_ItemFifoConfig_History | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-013 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuCurrentQrFifoValidationRepository.cs` | — | — | PARTICIPANT | FifoSessionStateTests.cs | — | OK |
| G1-014 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuGiaoDBRepository.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | TMPPHIEUGIAODBHD, TMPPHIEUGIAOHANGDBCT | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-015 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuKhoRepository.cs` | Usp_Qrcode_LOAD_HANGTHIEU, Usp_Qrcode_LOAD_HANGTHIEUView, Usp_Qrcode_Update_Stock2405, Usp_Qrcode_Update_Stock_SP | LUUDOCQRCODE, LUUPHIEUGIAOHANG, STOCKTP | PARTICIPANT (doc) | DeliveryLegacyWriterInventoryTests.cs, DeliveryProductionBoundaryTests.cs, DeliveryTransactionContractTests.cs, OrderTableSourceContractTests.cs, YmvnDeliverySpecContractTests.cs | OrderTableDeliveryMarkingScenario.cs, Program.cs, YmvnDeliverySpecScenario.cs | OK |
| G1-016 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuLotRepository.cs` | Usp_Qrcode_Take_Lot2405, Usp_Qrcode_Take_LotYMVN2405, Usp_Qrcode_gheplot | — | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs, DeliveryLotServiceTestDoubles.cs, DeliveryLotServiceUnitTests.cs, DeliveryQrAllocationSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-017 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuLuuTruRepository.cs` | — | LUUPHIEUGIAOHANG | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-018 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuRepository.cs` | — | — | PARTICIPANT (doc) | DeliveryLegacyWriterInventoryTests.cs, FifoSessionStateTests.cs | — | OK |
| G1-019 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuTmpRepository.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | — | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-020 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuValidationRepository.cs` | — | — | PARTICIPANT | DeliveryLotServiceTestDoubles.cs, DeliveryLotServiceUnitTests.cs, DeliveryQrAllocationSourceContractTests.cs | — | OK |
| G1-021 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/TableOrderRepo.cs` | — | Purchase_Order_YMVN | OWNS (doc) | AtomicityFindingsSourceContractTests.cs, OrderLineKeyTests.cs, OrderTableSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-022 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/BulkStockAdjustService.cs` | — | — | OWNS (doc) | BulkStockAdjustServiceTests.cs, YmvnDeliverySpecContractTests.cs | YmvnDeliverySpecScenario.cs | OK |
| G1-023 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/CustomerAddressService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-024 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRScanEngine.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-025 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-026 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRSessionState.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-027 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRTableResolver.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-028 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/FifoSessionService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-029 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/FifoSessionState.cs` | — | — | DELEGATES | FifoSessionStateTests.cs | — | OK |
| G1-030 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/GiaoHangKhachFactory.cs` | — | — | PARTICIPANT | DeliveryLegacyWriterInventoryTests.cs | — | OK |
| G1-031 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/HangThieuCaNgayService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-032 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/InPhieuService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-033 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/MachinePermissionService.cs` | — | tbl_QR_MAY_DOCQR | OWNS (doc) | — | — | **GAP**: test |
| G1-034 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuGiaoDbService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-035 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuKhoService.cs` | — | — | DELEGATES (doc) | DeliveryServiceOrchestrationTests.cs | — | OK |
| G1-036 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuLoadService.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405, Usp_Qrcode_LOAD_PHIEU_DOCQRView2405 | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-037 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuLotService.cs` | — | — | DELEGATES | DeliveryLotServiceTestDoubles.cs, DeliveryLotServiceUnitTests.cs, DeliveryQrAllocationSourceContractTests.cs | — | OK |
| G1-038 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuService.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-039 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuYmvnService.cs` | — | — | DELEGATES | DeliveryQrAllocationSourceContractTests.cs, YmvnDeliverySpecContractTests.cs | — | OK |
| G1-040 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/YmvnGearQuantityValidator.cs` | — | — | DELEGATES | YmvnGearQuantityValidatorTests.cs | — | OK |
| G1-041 | KhoCore | Service | `PCTP/Modules/KhoCore/Application/Services/QuarantineReleaseService.cs` | — | — | OWNS (doc) | QuarantineReleaseServiceTests.cs, RemainingQcCaseContractTests.cs | — | OK |
| G1-042 | KhoCore | Service | `PCTP/Modules/KhoCore/Application/Services/StockMovementService.cs` | — | — | OWNS (doc) | BulkStockAdjustServiceTests.cs, DeliveryLegacyWriterInventoryTests.cs, DeliveryTransactionContractTests.cs, RemainingQcCaseContractTests.cs, StockMovementServiceTests.cs, YmvnDeliverySpecContractTests.cs | — | OK |
| G1-043 | KhoCore | Repository | `PCTP/Modules/KhoCore/Infrastructure/Tracking/PhieuTrackingReadRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-044 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/BulkStockSlotRepository.cs` | — | Rack, Slot, SlotLot, Warehouse | PARTICIPANT (doc) | BulkStockAdjustServiceTests.cs, DeliveryTransactionContractTests.cs | ProductionBulkStockSlotConcurrencyScenario.cs, ProductionBulkStockSlotRepositoryScenario.cs, Program.cs, SqlEngineWorkflowScenario.cs | OK |
| G1-045 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/InspectionConfigRepository.cs` | — | InspectionConfig | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-046 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/InspectionLogRepository.cs` | — | InspectionLog | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-047 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/QuarantineReleaseAuditRepository.cs` | — | PCTPQuarantineReleaseAudit | PARTICIPANT (doc) | QuarantineReleaseServiceTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-048 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/RackRepository.cs` | — | Rack, Slot | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-049 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/SlotRepository.cs` | — | Rack, Slot, SlotLot, Warehouse | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-050 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/StockHistoryRepository.cs` | — | StockHistory | PARTICIPANT (doc) | BulkStockAdjustServiceTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-051 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/WarehouseRepository.cs` | — | Warehouse | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-052 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/InspectionConfigService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-053 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/InspectionService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-054 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/RackService.cs` | — | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-055 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/SlotService.cs` | — | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-056 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/StockHistoryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-057 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/WarehouseService.cs` | — | — | OWNS | — | — | **GAP**: test |
| G1-058 | NhapKho | Repository | `PCTP/Modules/NhapKho/Application/Adapters/StockReceivingRepositoryAdapter.cs` | — | — | DELEGATES | DeliveryTransactionContractTests.cs | — | OK |
| G1-059 | NhapKho | Repository | `PCTP/Modules/NhapKho/Application/Adapters/StockSlotRepositoryAdapter.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-060 | NhapKho | Repository | `PCTP/Modules/NhapKho/NhapKhoDashboardRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-061 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTPRepository.cs` | — | tác | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | ProductionBulkStockSlotRepositoryScenario.cs, ProductionStockTpRepositoryScenario.cs, Program.cs | OK |
| G1-062 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTpImportGuardRepository.cs` | — | — | PARTICIPANT | DeliveryTransactionContractTests.cs | — | OK |
| G1-063 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTpProductionRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-064 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTpStatusRepository.cs` | — | STOCKTP | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-065 | NhapKho | Service | `PCTP/Modules/NhapKho/Services/NhapTpReceivingService.cs` | — | — | OWNS (doc) | DeliveryTransactionContractTests.cs | — | OK |
| G1-066 | NhapKho | Service | `PCTP/Modules/NhapKho/Services/StockTpLookupService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-067 | NhapKho | Service | `PCTP/Modules/NhapKho/Services/WarehouseDashboardService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-068 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/NoiBoLookupRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-069 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/PhieuGiaoRepository.cs` | — | LUUPHIEUGIAOHANG | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-070 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/PhieuTraHangRepository.cs` | — | FVN_PhieuTraHang, FVN_PhieuTraHangCT | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-071 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/PhieuXuLyBatThuongRepository.cs` | — | FVN_PhieuTraHangCT, FVN_PhieuXuLyBatThuong | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-072 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/TraHangQTChungRepository.cs` | — | FVN_TraHangQTChungGiao, FVN_TraHangQTChungNhapNG, FVN_TraHangQTChungQC, FVN_TraHangQTChungXuat, LatestGiao | PARTICIPANT (doc) | ReworkExportIdempotencyContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-073 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/TransactionalPhieuTraHangRepository.cs` | — | FVN_PhieuTraHang | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-074 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/AffectedLotTraceService.cs` | — | FVN_PhieuXuLyBatThuongAffectedLot | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-075 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/GiaoBuNGService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-076 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/KhachTraHangService.cs` | — | — | PARTICIPANT | ReturnDeliveryConsistencySourceContractTests.cs | — | OK |
| G1-077 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/QTChungService.cs` | — | — | OWNS (doc) | ReturnDeliveryConsistencySourceContractTests.cs, ReworkExportIdempotencyContractTests.cs | ReworkTwoSessionScenario.cs | OK |
| G1-078 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/QrItemParser.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-079 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/ReworkQuantityPolicy.cs` | — | — | DELEGATES | ReworkExportIdempotencyContractTests.cs, ReworkQuantityPolicyTests.cs | ReworkTwoSessionScenario.cs | OK |
| G1-080 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/ReworkStockService.cs` | — | — | OWNS (doc) | RemainingQcCaseContractTests.cs, ReworkExportIdempotencyContractTests.cs | ReworkTwoSessionScenario.cs | OK |
| G1-081 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/TraNoiBoService.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-082 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/XuLyHangLoiServiceBase.cs` | — | — | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-083 | XuatKho | Repository | `PCTP/Modules/XuatKho/Application/Adapters/StockExportRepositoryAdapter.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-084 | XuatKho | Repository | `PCTP/Modules/XuatKho/Repositories/HangChoGiaoRepository.cs` | — | FVN_HangChoGiao, FVN_HangChoGiaoPickDetail | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-085 | XuatKho | Repository | `PCTP/Modules/XuatKho/Repositories/StockExportHistoryRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-086 | XuatKho | Repository | `PCTP/Modules/XuatKho/Repositories/StockExportRepository.cs` | — | STOCKTP, tác | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-087 | XuatKho | Service | `PCTP/Modules/XuatKho/Services/StockExportService.cs` | — | — | OWNS (doc) | — | — | **GAP**: test |
| G1-088 | XuatKho | Service | `PCTP/Modules/XuatKho/Services/StockExportValidationService.cs` | — | — | DELEGATES | — | — | **GAP**: test |

## SQL scripts (`SQL\`)

| File | Đối tượng | Code tham chiếu | Test tham chiếu | Trạng thái |
|---|---|---|---|---|
| `SQL/A0_SlotLot_Diagnose_Repair.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/NHAP_TP_HIS_Retire.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/PCTPQuarantineRelease.sql` | INDEX IX_PCTPQuarantineReleaseAudit_SlotLotId, TABLE PCTPQuarantineReleaseAudit | PCTPQuarantineReleaseAudit | — | **GAP**: no-test-reference |
| `SQL/PCTPStockMovementGuard.sql` | TABLE PCTPStockMovementGuard | — | PCTPStockMovementGuard | **GAP**: no-code-reference |
| `SQL/PCTP_EnforceUniqueStockTpLot.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/PCTP_StockTpIdentityAudit.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/PHASE14_SlotLot_NgQuarantine.sql` | TABLE SlotLot | SlotLot | SlotLot | OK |
| `SQL/PHASE15_HangChoGiaoPickDetails.sql` | INDEX IX_FVN_HangChoGiaoPickDetail_HangChoGiaoId, TABLE FVN_HangChoGiaoPickDetail | FVN_HangChoGiaoPickDetail | — | **GAP**: no-test-reference |
| `SQL/PHASE16_QC_Final_ExecutionTimeout_Fix.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/QC_G4_DeliveryFixture_Cleanup.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/QC_G4_DeliveryFixture_Seed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/Usp_Qrcode_LOAD_PHIEU_DOCQR2405.sql` | PROC Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | OK |
| `SQL/Usp_Qrcode_Take_Lot2405.sql` | PROC Usp_Qrcode_Take_Lot2405 | Usp_Qrcode_Take_Lot2405 | Usp_Qrcode_Take_Lot2405 | OK |
| `SQL/Usp_Qrcode_Take_LotYMVN2405.sql` | PROC Usp_Qrcode_Take_LotYMVN2405 | Usp_Qrcode_Take_LotYMVN2405 | Usp_Qrcode_Take_LotYMVN2405 | OK |
| `SQL/Usp_Qrcode_Update_Stock2405.sql` | PROC Usp_Qrcode_Update_Stock2405 | Usp_Qrcode_Update_Stock2405 | Usp_Qrcode_Update_Stock2405 | OK |
| `SQL/Usp_Qrcode_Update_Stock_SP.sql` | PROC Usp_Qrcode_Update_Stock_SP | Usp_Qrcode_Update_Stock_SP | Usp_Qrcode_Update_Stock_SP | OK |
| `SQL/sp_GetCurrentStockStatus.sql` | PROCEDURE sp_GetCurrentStockStatus | sp_GetCurrentStockStatus | sp_GetCurrentStockStatus | OK |

## TestMatrix.json chưa `implemented`

| ID | Module | Trạng thái | Kịch bản |
|---|---|---|---|
| QR-004 | QRCode/Integration | planned | Trace supplied QR payloads through actual receiving and customer-delivery UI/service/parser and SQL persistence |
| DEL-003 | GiaoHangKhach | partial | Execute production customer-delivery stock deduction across STOCKTP, SlotLot, stock history and pending-delivery records |
| DEL-004 | GiaoHangKhach | partial | Repeat delivery confirmation or replay the same delivery reference |
| DEL-007 | GiaoHangKhach | partial | Verify transaction boundary for stored-procedure delivery routes and post-delivery finalization |
| RWK-002 | XuLyHangLoi | partial | Quarantine and release |
| REP-001 | BaoCao | partial | Reconcile opening+in-out=closing stock |
| PERF-002 | Performance | planned | Repeated QC Final and concurrent LOT requests |
| UI-001 | UI Automation | planned | Receive/export/delivery/rework high-value journeys |
| QC-RELEASE-001 | XuLyHangLoi/KhoCore | planned | Authorize release of a quarantined SlotLot only after QC disposition, preserve audit trail, and reconcile available STOCKTP/Slot header atomically |
| UI-003 | WinForms/Receiving | planned | Open the actual receiving workflow from the PCTP shell and verify the receiving worklist window |
| UI-004 | WinForms/Export | planned | Open ExportFormSV from a dedicated seeded physical slot and verify slot/item/LOT/available quantity before pick |
| UI-005 | WinForms/CustomerDelivery | planned | Open the HVN customer-delivery workspace and verify QR/order controls are accessible |
| UI-006 | WinForms/QC-Rework | planned | Open defect/rework worklist and QC direction/final queues from shell |
| UI-007 | WinForms/BusinessE2E | planned | Execute receiving, export, delivery and rework/QC business transactions against isolated QC data with database postcondition assertions and cleanup |
| RWK-008 | XuLyHangLoi/Rework-to-Quarantine | planned | Execute real ReworkStockService NG output path and verify created SlotLot is PhieuStatus=3 |
| IN-TXN-001 | NhapKho/UnitOfWork | partial | Receive one master QR into physical Slot or virtual A0 and synchronize STOCKTP, Slot header, SlotLot, production tracking and stock history |
| STK-LOCK-001 | KhoCore/QuarantineRelease | partial | Two QC operators release the same quarantined SlotLot concurrently |
| RWK-IDEM-001 | XuLyHangLoi/StockMovementGuard | partial | Rework export, OK receive, NG quarantine receive and cancellation return use operation-scoped idempotency keys |
| WMS-E2E-001 | WMS/EndToEnd | planned | Receive → Rack/Slot/A0 → deliver → NG quarantine → QC/rework/release → stock reconciliation |
| STK-IDENTITY-001 | KhoCore/STOCKTP | partial | Determine the actual STOCKTP business key and reconcile duplicate LOT / LOT+PART rows with active SlotLot |
| YMVN-SPEC-001 | GiaoHangKhach/YMVN/B0-01 | partial | Characterize the YMVN delivery writer: input parsing before the transaction, lock-read then shortage rollback, TruStockTpFifo order/guard, SlotLot in the same UoW, ledger/TMP guard/scan-row order, commit; inventory of the three legacy STOCKTP writers; FIFO gate before the first YMVN stock call |
| YMVN-SPEC-002 | GiaoHangKhach/YMVN/B0-01 | partial | YMVN delivery specification on SQL Server: FIFO order and tie-break, exact 20-char key isolation, legacy 13-char key fallback, multi-LOT composite, partial delivery equation, shortage rollback, malformed input, SlotLot shortage fail-closed, injected failure rollback, retry of the same STT, two sessions on the same remaining quantity, same STT from two sessions, negative control and guard-only defense in depth, scan-row scoping |
