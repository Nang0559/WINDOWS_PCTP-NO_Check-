# G1 - Ma trận truy vết (code -> SP/SQL -> transaction -> test)

- Commit: `8ad10ecda97ec42e86fdb7ea471627d2a59829e9`  |  sinh bởi `PCTP.QC.UI/Build-TraceabilityMatrix.py` (phân tích tĩnh, không mở DB)
- Dòng code: 144 (COMPLETE 43, **GAP 101**)  |  SQL script: 56  |  dòng TestMatrix không `implemented`: 24
- Tổng số gap: **208**. Gate G1 chỉ PASS khi bằng 0.

Giới hạn: static inventory bằng regex, không phải compiler hay SQL parser. SQL ghép động, tên bảng lấy từ biến, reflection, DI runtime và delegate có thể không được nhận diện. Test chỉ nhắc tên class không chứng minh đã gọi production method; cần review call-site và fixture thực tế trước khi dùng làm bằng chứng release.

## Repository / Service

| ID | Module | Loại | File nguồn | SP | Bảng ghi | Transaction | Test unit | Test SQL | Trạng thái |
|---|---|---|---|---|---|---|---|---|---|
| G1-001 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/DeliveryCommandSqlScenario.cs` | — | DOCQRCODE, FVN_DeliveryCommandLedger, FVN_DeliveryCommandQr, LUUDOCQRCODE, LUUPHIEUGIAOHANG, TMPPHIEUGIAOHANG | PARTICIPANT | — | DeliveryCommandSqlScenario.cs, DeliveryQrAllocationScenario.cs, Program.cs | **GAP**: transaction-boundary(not-documented) |
| G1-002 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/DeliveryProductionAcceptanceScenario.cs` | Usp_Qrcode_Update_Stock2405 | TMPPHIEUGIAOHANG | PARTICIPANT | — | DeliveryProductionAcceptanceScenario.cs, Program.cs | **GAP**: transaction-boundary(not-documented) |
| G1-003 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/DeliveryQrAllocationScenario.cs` | Usp_Qrcode_Take_Lot2405, sp_rename | DOCQRCODE, TMPPHIEUGIAOHANG | PARTICIPANT | OrderTableSourceContractTests.cs | DeliveryQrAllocationScenario.cs, Program.cs | **GAP**: transaction-boundary(not-documented) |
| G1-004 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/DeliveryReliabilityScenario.cs` | — | — | PARTICIPANT | — | DeliveryReliabilityScenario.cs, Program.cs | OK |
| G1-005 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/DeliveryTransactionBoundaryScenario.cs` | — | — | PARTICIPANT | — | DeliveryTransactionBoundaryScenario.cs, Program.cs | OK |
| G1-006 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/InTransactionScenario.cs` | — | — | OWNS (doc) | AtomicityFindingsSourceContractTests.cs | InTransactionScenario.cs, Program.cs | OK |
| G1-007 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/OrderTableDeliveryMarkingScenario.cs` | — | any, exactly | OWNS | OrderTableSqlTests.cs | OrderTableDeliveryMarkingScenario.cs, Program.cs | **GAP**: transaction-boundary(not-documented) |
| G1-008 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/ProductionBulkStockSlotConcurrencyScenario.cs` | — | — | OWNS | — | ProductionBulkStockSlotConcurrencyScenario.cs, Program.cs | OK |
| G1-009 | PCTP.QC.SqlIntegration | Repository | `PCTP.QC.SqlIntegration/ProductionBulkStockSlotRepositoryScenario.cs` | — | — | OWNS | — | ProductionBulkStockSlotRepositoryScenario.cs, Program.cs | OK |
| G1-010 | PCTP.QC.SqlIntegration | Repository | `PCTP.QC.SqlIntegration/ProductionReworkRepositorySqlScenario.Contracts.cs` | — | — | OWNS | — | ProductionReworkRepositorySqlScenario.Contracts.cs, ProductionReworkRepositorySqlScenario.cs, Program.cs | OK |
| G1-011 | PCTP.QC.SqlIntegration | Repository | `PCTP.QC.SqlIntegration/ProductionReworkRepositorySqlScenario.cs` | — | should | OWNS | — | ProductionReworkRepositorySqlScenario.Contracts.cs, ProductionReworkRepositorySqlScenario.cs, Program.cs | **GAP**: transaction-boundary(not-documented) |
| G1-012 | PCTP.QC.SqlIntegration | Repository | `PCTP.QC.SqlIntegration/ProductionStockTpRepositoryScenario.cs` | — | delta | OWNS (doc) | AtomicityFindingsSourceContractTests.cs | ProductionBulkStockSlotRepositoryScenario.cs, ProductionStockTpRepositoryScenario.cs, Program.cs | OK |
| G1-013 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/Program.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405, Usp_Qrcode_Take_Lot2405, Usp_Qrcode_Take_LotYMVN2405, Usp_Qrcode_Update_Stock2405, Usp_Qrcode_Update_Stock_SP, sp_GetCurrentStockStatus, sp_getapplock | — | PARTICIPANT | CiWorkflowCoversDeliveryPreflightTests.cs | Program.cs | **GAP**: transaction-boundary(not-documented) |
| G1-014 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/QuarantineReleaseScenario.cs` | — | — | PARTICIPANT (doc) | — | Program.cs, QuarantineReleaseScenario.cs | OK |
| G1-015 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/ReworkTwoSessionScenario.cs` | — | — | PARTICIPANT | QuarantineReleaseServiceTests.cs | DeliveryCommandSqlScenario.cs, OrderTableDeliveryMarkingScenario.cs, ProductionReworkRepositorySqlScenario.Contracts.cs, Program.cs, ReworkTwoSessionScenario.cs | OK |
| G1-016 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/SqlEngineWorkflowScenario.cs` | — | — | OWNS | — | Program.cs, SqlEngineWorkflowScenario.cs | OK |
| G1-017 | PCTP.QC.SqlIntegration | Repository | `PCTP.QC.SqlIntegration/SqlRepositoryTransactionProbe.cs` | — | — | PARTICIPANT | — | InTransactionScenario.cs, SqlRepositoryTransactionProbe.cs | OK |
| G1-018 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/TestPhieuSqlExecutor.cs` | — | — | PARTICIPANT | — | DeliveryCommandSqlScenario.cs, InTransactionScenario.cs, ProductionBulkStockSlotConcurrencyScenario.cs, ProductionBulkStockSlotRepositoryScenario.cs, ProductionReworkRepositorySqlScenario.Contracts.cs, ProductionReworkRepositorySqlScenario.cs, ProductionStockTpRepositoryScenario.cs, SqlEngineWorkflowScenario.cs, SqlRepositoryTransactionProbe.cs, TestPhieuSqlExecutor.cs | OK |
| G1-019 | PCTP.QC.SqlIntegration | SQL caller | `PCTP.QC.SqlIntegration/YmvnDeliverySpecScenario.cs` | — | STOCKTP, must | PARTICIPANT | YmvnDeliverySpecContractTests.cs | Program.cs, ReworkTwoSessionScenario.cs, YmvnDeliverySpecScenario.cs | **GAP**: transaction-boundary(not-documented) |
| G1-020 | PCTP | SQL caller | `PCTP/Acess Image/FRMSHOW.cs` | — | — | NONE | — | — | **GAP**: test |
| G1-021 | PCTP | SQL caller | `PCTP/Acess Image/ImagesControl.cs` | — | B20ImageStore | NONE (doc) | — | — | **GAP**: transaction-boundary(NONE); test |
| G1-022 | PCTP | Repository | `PCTP/Infrastructure/Repositories/DocQRRepository.cs` | — | — | NONE | — | — | **GAP**: test |
| G1-023 | PCTP | Repository | `PCTP/Infrastructure/Repositories/GioXuatRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-024 | PCTP | Repository | `PCTP/Infrastructure/Repositories/PhieuTrackingRepository.cs` | — | SlotLot | PARTICIPANT | — | — | **GAP**: transaction-boundary(not-documented); test |
| G1-025 | PCTP | Repository | `PCTP/Infrastructure/Repositories/SqlRepository.cs` | Usp_gheplotPrint | TMPLOTGHEP | PARTICIPANT | — | — | **GAP**: transaction-boundary(not-documented); test |
| G1-026 | PCTP | SQL caller | `PCTP/Infrastructure/Sql/SQL.cs` | — | — | BEGINS-ONLY (doc) | — | DeliveryCommandSqlScenario.cs, InTransactionScenario.cs, OrderTableDeliveryMarkingScenario.cs, ProductionBulkStockSlotConcurrencyScenario.cs, ProductionBulkStockSlotRepositoryScenario.cs, ProductionReworkRepositorySqlScenario.Contracts.cs, ProductionReworkRepositorySqlScenario.cs, ProductionStockTpRepositoryScenario.cs, SqlEngineWorkflowScenario.cs, TestSqlProvider.cs | OK |
| G1-027 | PCTP | Repository | `PCTP/Infrastructure/Stock/LegacyStockBalanceRepositoryAdapter.cs` | — | STOCKTP, tác | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-028 | PCTP | Repository | `PCTP/Infrastructure/Stock/LegacyStockSlotRepositoryAdapter.cs` | — | — | PARTICIPANT (doc) | RemainingQcCaseContractTests.cs | — | OK |
| G1-029 | PCTP | Repository | `PCTP/Infrastructure/Stock/StockReceivingRepository.cs` | — | tác | PARTICIPANT | DeliveryTransactionContractTests.cs, StockMovementServiceTests.cs | ProductionStockTpRepositoryScenario.cs | **GAP**: transaction-boundary(not-documented) |
| G1-030 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/CurrentStockQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-031 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/DeliveryTraceReadService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-032 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/LotLocationQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-033 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/QualityHistoryQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-034 | BaoCao | Service | `PCTP/Modules/BaoCao/Infrastructure/Queries/StockHistoryQueryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-035 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/CurrentStockRepository.cs` | sp_GetCurrentStockStatus | — | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-036 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/DeliveryTraceRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-037 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/LotLocationRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-038 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/QualityHistoryRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-039 | BaoCao | Repository | `PCTP/Modules/BaoCao/Infrastructure/Repositories/StockHistoryRepository.cs` | sp_GetStockHistory | — | PARTICIPANT (doc) | BulkStockAdjustServiceTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-040 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/ComaprePart/ComaparePart.cs` | — | tbl_QR_ComparePart | OWNS (doc) | LegacyTransactionBoundarySourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-041 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/HVN/Edit_DH.cs` | — | tmpphieugiaohang | NONE (doc) | LegacyTransactionBoundarySourceContractTests.cs | — | **GAP**: transaction-boundary(NONE); sql-test(only-fakes) |
| G1-042 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/OrderLoading/IFS/IfsOrderLoadStrategy.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | — | DELEGATES | — | — | **GAP**: transaction-boundary(not-documented); test |
| G1-043 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/OrderLoading/TableOrder/OrderTableLoadStrategy.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | — | DELEGATES | — | — | **GAP**: transaction-boundary(not-documented); test |
| G1-044 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/PhieuSqlExecutor.cs` | — | — | PARTICIPANT (doc) | — | DeliveryCommandSqlScenario.cs, InTransactionScenario.cs, ProductionBulkStockSlotConcurrencyScenario.cs, ProductionBulkStockSlotRepositoryScenario.cs, ProductionReworkRepositorySqlScenario.Contracts.cs, ProductionReworkRepositorySqlScenario.cs, ProductionStockTpRepositoryScenario.cs, SqlEngineWorkflowScenario.cs, SqlRepositoryTransactionProbe.cs, TestPhieuSqlExecutor.cs | OK |
| G1-045 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Reports/Report.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-046 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/DeliveryCommandLedgerRepository.cs` | — | FVN_DeliveryCommandLedger, FVN_DeliveryCommandQr | PARTICIPANT | — | DeliveryCommandSqlScenario.cs | **GAP**: transaction-boundary(not-documented) |
| G1-047 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/DeliveryHangChoGiaoPostStep.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-048 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/DeliveryTmpPendingGate.cs` | — | — | PARTICIPANT | DeliveryLegacyWriterInventoryTests.cs | DeliveryCommandSqlScenario.cs | OK |
| G1-049 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/ItemFifoConfigRepository.cs` | — | FVN_ItemFifoConfig, FVN_ItemFifoConfig_History | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-050 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuCurrentQrFifoValidationRepository.cs` | — | — | PARTICIPANT | FifoSessionStateTests.cs, FifoSessionStateRegressionTests.cs | — | OK |
| G1-051 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuGiaoDBRepository.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | TMPPHIEUGIAODBHD, TMPPHIEUGIAOHANGDBCT | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-052 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuKhoRepository.cs` | Usp_Qrcode_LOAD_HANGTHIEU, Usp_Qrcode_LOAD_HANGTHIEUView, Usp_Qrcode_Update_Stock2405, Usp_Qrcode_Update_Stock_SP | LUUDOCQRCODE, LUUPHIEUGIAOHANG, STOCKTP | PARTICIPANT (doc) | DeliveryLegacyWriterInventoryTests.cs, DeliveryProductionBoundaryTests.cs, DeliveryTransactionContractTests.cs, OrderTableSourceContractTests.cs, YmvnDeliverySpecContractTests.cs | OrderTableDeliveryMarkingScenario.cs, Program.cs, YmvnDeliverySpecScenario.cs | OK |
| G1-053 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuLotRepository.cs` | Usp_Qrcode_Take_Lot2405, Usp_Qrcode_Take_LotYMVN2405, Usp_Qrcode_gheplot | — | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs, DeliveryLotServiceTestDoubles.cs, DeliveryLotServiceUnitTests.cs, DeliveryQrAllocationSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-054 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuLuuTruRepository.cs` | — | LUUPHIEUGIAOHANG | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-055 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuRepository.cs` | — | — | PARTICIPANT (doc) | DeliveryLegacyWriterInventoryTests.cs, FifoSessionStateTests.cs, FifoSessionStateRegressionTests.cs | — | OK |
| G1-056 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuTmpRepository.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | — | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-057 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuValidationRepository.cs` | — | — | PARTICIPANT | DeliveryLotServiceTestDoubles.cs, DeliveryLotServiceUnitTests.cs, DeliveryQrAllocationSourceContractTests.cs | — | OK |
| G1-058 | GiaoHangKhach | Repository | `PCTP/Modules/GiaoHangKhach/Repositories/TableOrderRepo.cs` | — | Purchase_Order_YMVN | OWNS (doc) | AtomicityFindingsSourceContractTests.cs, OrderLineKeyTests.cs, OrderTableSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-059 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/BulkStockAdjustService.cs` | — | — | OWNS (doc) | BulkStockAdjustServiceTests.cs, YmvnDeliverySpecContractTests.cs | YmvnDeliverySpecScenario.cs | OK |
| G1-060 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/CustomerAddressService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-061 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DeliveryStockCommandHandler.cs` | — | — | OWNS | DeliveryLegacyWriterInventoryTests.cs, DeliveryStockCommandHandlerTests.cs | DeliveryCommandSqlScenario.cs | OK |
| G1-062 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRScanEngine.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-063 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-064 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRSessionState.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-065 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/DocQRTableResolver.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-066 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/FifoSessionService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-067 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/FifoSessionState.cs` | — | — | DELEGATES | FifoSessionStateTests.cs, FifoSessionStateRegressionTests.cs | — | OK |
| G1-068 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/GiaoHangKhachFactory.cs` | — | — | PARTICIPANT | DeliveryLegacyWriterInventoryTests.cs | — | OK |
| G1-069 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/HangThieuCaNgayService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-070 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/InPhieuService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-071 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/MachinePermissionService.cs` | — | tbl_QR_MAY_DOCQR | OWNS (doc) | — | — | **GAP**: test |
| G1-072 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuGiaoDbService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-073 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuKhoService.cs` | — | — | DELEGATES (doc) | DeliveryServiceOrchestrationTests.cs | — | OK |
| G1-074 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuLoadService.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405, Usp_Qrcode_LOAD_PHIEU_DOCQRView2405 | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-075 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuLotService.cs` | — | — | DELEGATES | DeliveryLotServiceTestDoubles.cs, DeliveryLotServiceUnitTests.cs, DeliveryQrAllocationSourceContractTests.cs | — | OK |
| G1-076 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuService.cs` | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-077 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/PhieuYmvnService.cs` | — | — | DELEGATES | DeliveryQrAllocationSourceContractTests.cs, YmvnDeliverySpecContractTests.cs | — | OK |
| G1-078 | GiaoHangKhach | Service | `PCTP/Modules/GiaoHangKhach/Services/YmvnGearQuantityValidator.cs` | — | — | DELEGATES | YmvnGearQuantityValidatorTests.cs | — | OK |
| G1-079 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/SubForm/FRM_SUALOTHVN.cs` | — | DOCQRCODE, TMPPHIEUGIAOHANG | OWNS (doc) | LegacyTransactionBoundarySourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-080 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/SubForm/FRM_UploadMikrun.cs` | — | — | NONE | — | — | **GAP**: test |
| G1-081 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/SubForm/UF_TACHLOT.cs` | Usp_BarCodeView_ThongKe_Tmp5, Usp_BarCodeView_ThongKe_Tmp5_YMVN | TMPLOTTACH | NONE (doc) | — | — | **GAP**: transaction-boundary(NONE); test |
| G1-082 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/YMVN/GIAOHANGYMN.cs` | Usp_BarCodeView_Ghep_YMVN, Usp_Qrcode_gheplotYM1, Usp_Qrcode_gheplotYM2, Usp_gheplotPrintYM1 | LUUDOCQRCODE, LUUPHIEUGIAOHANG, Purchase_Order_YMVN, SP_DOCQRCODE, SP_TMPPHIEUGIAOHANG, SP_tmpphieugiaohang, TMPLOTGHEPTEST, YMVN_DOCQRCODE, YMVN_TMPPHIEUGIAOHANG, sp_tmpphieugiaohang, stocktp, thông | NONE (doc) | — | — | **GAP**: transaction-boundary(NONE); test |
| G1-083 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/YMVN/YAMAHAQRCDE_SP.cs` | — | SP_DOCQRCODE, SP_TMPPHIEUGIAOHANG | NONE (doc) | — | — | **GAP**: transaction-boundary(NONE); test |
| G1-084 | GiaoHangKhach | SQL caller | `PCTP/Modules/GiaoHangKhach/YMVN/YMVNDOCQRCODE.cs` | Usp_Qrcode_Take_LotYMVN | SP_DOCQRCODE, YMVN_DOCQRCODE | NONE (doc) | — | — | **GAP**: transaction-boundary(NONE); test |
| G1-085 | KhoCore | Service | `PCTP/Modules/KhoCore/Application/Services/QuarantineReleaseService.cs` | — | — | OWNS (doc) | QuarantineReleaseServiceTests.cs, RemainingQcCaseContractTests.cs | — | OK |
| G1-086 | KhoCore | Service | `PCTP/Modules/KhoCore/Application/Services/StockMovementService.cs` | — | — | OWNS (doc) | BulkStockAdjustServiceTests.cs, DeliveryLegacyWriterInventoryTests.cs, DeliveryStockCommandHandlerTests.cs, DeliveryTransactionContractTests.cs, RemainingQcCaseContractTests.cs, StockMovementServiceTests.cs, YmvnDeliverySpecContractTests.cs | DeliveryCommandSqlScenario.cs | OK |
| G1-087 | KhoCore | SQL caller | `PCTP/Modules/KhoCore/Fuction/CheckInfor.cs` | — | Rack, Slot | OWNS (doc) | LegacyTransactionBoundarySourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-088 | KhoCore | Repository | `PCTP/Modules/KhoCore/Infrastructure/Tracking/PhieuTrackingReadRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-089 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/BulkStockSlotRepository.cs` | — | Rack, Slot, SlotLot, Warehouse | PARTICIPANT (doc) | BulkStockAdjustServiceTests.cs, DeliveryTransactionContractTests.cs | ProductionBulkStockSlotConcurrencyScenario.cs, ProductionBulkStockSlotRepositoryScenario.cs, Program.cs, SqlEngineWorkflowScenario.cs | OK |
| G1-090 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/InspectionConfigRepository.cs` | — | InspectionConfig | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-091 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/InspectionLogRepository.cs` | — | InspectionLog | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-092 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/QuarantineReleaseAuditRepository.cs` | — | FVN_PCTPQuarantineReleaseAudit | PARTICIPANT (doc) | QuarantineReleaseServiceTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-093 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/RackRepository.cs` | — | Rack, Slot | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs, RackRepositoryColumnContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-094 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/SlotRepository.cs` | — | Rack, Slot, SlotLot, Warehouse | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-095 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/StockHistoryRepository.cs` | — | StockHistory | PARTICIPANT (doc) | BulkStockAdjustServiceTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-096 | KhoCore | Repository | `PCTP/Modules/KhoCore/Repositories/WarehouseRepository.cs` | — | Warehouse | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-097 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/InspectionConfigService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-098 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/InspectionService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-099 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/RackService.cs` | — | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-100 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/SlotService.cs` | — | — | DELEGATES (doc) | — | — | **GAP**: test |
| G1-101 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/StockHistoryService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-102 | KhoCore | Service | `PCTP/Modules/KhoCore/Services/WarehouseService.cs` | — | — | OWNS | RackRepositoryColumnContractTests.cs | — | OK |
| G1-103 | KhoVatLy | SQL caller | `PCTP/Modules/KhoVatLy/FormInspectionConfig.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-104 | NhapKho | Repository | `PCTP/Modules/NhapKho/Application/Adapters/StockReceivingRepositoryAdapter.cs` | — | — | DELEGATES | DeliveryTransactionContractTests.cs | — | OK |
| G1-105 | NhapKho | Repository | `PCTP/Modules/NhapKho/Application/Adapters/StockSlotRepositoryAdapter.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-106 | NhapKho | Repository | `PCTP/Modules/NhapKho/NhapKhoDashboardRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-107 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTPRepository.cs` | — | tác | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | ProductionBulkStockSlotRepositoryScenario.cs, ProductionStockTpRepositoryScenario.cs, Program.cs | OK |
| G1-108 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTpImportGuardRepository.cs` | — | — | PARTICIPANT | DeliveryTransactionContractTests.cs | — | OK |
| G1-109 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTpProductionRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-110 | NhapKho | Repository | `PCTP/Modules/NhapKho/Repository/StockTpStatusRepository.cs` | — | STOCKTP | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-111 | NhapKho | Service | `PCTP/Modules/NhapKho/Services/NhapTpReceivingService.cs` | — | — | OWNS (doc) | DeliveryTransactionContractTests.cs | — | OK |
| G1-112 | NhapKho | Service | `PCTP/Modules/NhapKho/Services/StockTpLookupService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-113 | NhapKho | Service | `PCTP/Modules/NhapKho/Services/WarehouseDashboardService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-114 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/NoiBoLookupRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-115 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/PhieuGiaoRepository.cs` | — | LUUPHIEUGIAOHANG | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-116 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/PhieuTraHangRepository.cs` | — | FVN_PhieuTraHang, FVN_PhieuTraHangCT | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-117 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/PhieuXuLyBatThuongRepository.cs` | — | FVN_PhieuTraHangCT, FVN_PhieuXuLyBatThuong | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | ProductionReworkRepositorySqlScenario.Contracts.cs, ProductionReworkRepositorySqlScenario.cs | OK |
| G1-118 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/TraHangQTChungRepository.cs` | — | FVN_TraHangQTChungGiao, FVN_TraHangQTChungNhapNG, FVN_TraHangQTChungQC, FVN_TraHangQTChungXuat, LatestGiao | PARTICIPANT (doc) | ReworkExportIdempotencyContractTests.cs | ProductionReworkRepositorySqlScenario.Contracts.cs, ProductionReworkRepositorySqlScenario.cs | OK |
| G1-119 | XuLyHangLoi | Repository | `PCTP/Modules/XuLyHangLoi/Repository/TransactionalPhieuTraHangRepository.cs` | — | FVN_PhieuTraHang | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-120 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/AffectedLotTraceService.cs` | — | FVN_PhieuXuLyBatThuongAffectedLot | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-121 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/GiaoBuNGService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-122 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/KhachTraHangService.cs` | — | — | PARTICIPANT | ReturnDeliveryConsistencySourceContractTests.cs | — | OK |
| G1-123 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/QTChungService.cs` | — | — | OWNS (doc) | ReturnDeliveryConsistencySourceContractTests.cs, ReworkExportIdempotencyContractTests.cs | ReworkTwoSessionScenario.cs | OK |
| G1-124 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/QrItemParser.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-125 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/ReworkQuantityPolicy.cs` | — | — | DELEGATES | ReworkExportIdempotencyContractTests.cs, ReworkQuantityPolicyTests.cs | ReworkTwoSessionScenario.cs | OK |
| G1-126 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/ReworkStockService.cs` | — | — | OWNS (doc) | RemainingQcCaseContractTests.cs, ReworkExportIdempotencyContractTests.cs | ReworkTwoSessionScenario.cs | OK |
| G1-127 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/TraNoiBoService.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-128 | XuLyHangLoi | Service | `PCTP/Modules/XuLyHangLoi/Services/XuLyHangLoiServiceBase.cs` | — | — | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-129 | XuatKho | Repository | `PCTP/Modules/XuatKho/Application/Adapters/StockExportRepositoryAdapter.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-130 | XuatKho | Repository | `PCTP/Modules/XuatKho/Repositories/HangChoGiaoRepository.cs` | — | FVN_HangChoGiao, FVN_HangChoGiaoPickDetail | PARTICIPANT (doc) | — | — | **GAP**: test |
| G1-131 | XuatKho | Repository | `PCTP/Modules/XuatKho/Repositories/StockExportHistoryRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-132 | XuatKho | Repository | `PCTP/Modules/XuatKho/Repositories/StockExportRepository.cs` | — | STOCKTP, tác | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-133 | XuatKho | Service | `PCTP/Modules/XuatKho/Services/StockExportService.cs` | — | — | OWNS (doc) | — | — | **GAP**: test |
| G1-134 | XuatKho | Service | `PCTP/Modules/XuatKho/Services/StockExportValidationService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-135 | PCTP | SQL caller | `PCTP/SQLTABLECREATOR.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-136 | PCTP | Repository | `PCTP/Shared/Common/SqlRepositoryBase.cs` | — | — | PARTICIPANT (doc) | AtomicityFindingsSourceContractTests.cs | InTransactionScenario.cs, ProductionReworkRepositorySqlScenario.cs, Program.cs, ReworkTwoSessionScenario.cs, SqlRepositoryTransactionProbe.cs, TestPhieuSqlExecutor.cs | OK |
| G1-137 | PCTP | Service | `PCTP/Shared/Services/PrintService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-138 | PCTP | Repository | `PCTP/Shared/UiMd/WorkflowRepository.cs` | — | — | PARTICIPANT | — | — | **GAP**: test |
| G1-139 | PCTP | Service | `PCTP/Shared/UiMd/WorkflowTransitionService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-140 | PCTP | Service | `PCTP/Shell/Help/WmsHelpService.cs` | — | — | DELEGATES | — | — | **GAP**: test |
| G1-141 | PCTP | Service | `PCTP/Shell/Services/QrMachineSwitchService.cs` | — | tbl_QR_MAY_DOCQR | OWNS | — | — | **GAP**: transaction-boundary(not-documented); test |
| G1-142 | PCTP | SQL caller | `PCTP/Shell/Widgets/MainAppDashboardController.cs` | — | — | NONE | — | — | **GAP**: test |
| G1-143 | PCTP | SQL caller | `PCTP/UF_CHANGETIME.cs` | — | QRCODE_CHANGETIME | STATEMENT-ATOMIC (doc) | LegacyTransactionBoundarySourceContractTests.cs | — | **GAP**: sql-test(only-fakes) |
| G1-144 | WINDOWS_PCTP | SQL caller | `WINDOWS_PCTP/MAIN.cs` | — | — | NONE | — | — | **GAP**: test |

## SQL scripts (toàn repository: SQL/, Modules/*/Database/, tooling)

| File | Đối tượng | Code tham chiếu | Test tham chiếu | Trạng thái |
|---|---|---|---|---|
| `PCTP.QC.UI/journeys/g6/sql/delivery-hvn/assert.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/delivery-hvn/cleanup.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/delivery-hvn/seed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/delivery-hvn/verify.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-direction/assert.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-direction/cleanup.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-direction/seed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-direction/verify.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-final/assert.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-final/cleanup.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-final/seed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/qc-final/verify.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/receive/assert.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/receive/cleanup.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/receive/seed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/receive/verify.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/selftest/assert.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/selftest/cleanup.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/selftest/seed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP.QC.UI/journeys/g6/sql/selftest/verify.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE10_PhieuTraHang_CustomerNo.sql` | TABLE FVN_PhieuTraHang | FVN_PhieuTraHang | FVN_PhieuTraHang | OK |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE11_PhieuTraHangCT_LienKetPhieuXuLy.sql` | INDEX IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId, TABLE FVN_PhieuTraHangCT | FVN_PhieuTraHangCT | FVN_PhieuTraHangCT | OK |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE12_PhieuXuLyBatThuong_PhieuTraHangCTId.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE13_QTChung_ReworkTables.sql` | INDEX IX_FVN_TraHangQTChungGiao_Phieu_ThoiGian, INDEX IX_FVN_TraHangQTChungNhapNG_Phieu_Ngay, INDEX IX_FVN_TraHangQTChungQC_Phieu_ThoiGian, INDEX IX_FVN_TraHangQTChungXuat_Phieu_Ngay, TABLE FVN_TraHangQTChungGiao, TABLE FVN_TraHangQTChungNhapNG, TABLE FVN_TraHangQTChungQC, TABLE FVN_TraHangQTChungXuat | FVN_TraHangQTChungGiao, FVN_TraHangQTChungNhapNG, FVN_TraHangQTChungQC, FVN_TraHangQTChungXuat | FVN_TraHangQTChungGiao, FVN_TraHangQTChungNhapNG, FVN_TraHangQTChungQC, FVN_TraHangQTChungXuat | OK |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE14_ReworkExportIdempotency.sql` | TABLE FVN_TraHangQTChungXuat | FVN_TraHangQTChungXuat | FVN_TraHangQTChungXuat | OK |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE2_AffectedLotSnapshot.sql` | INDEX IX_FVN_PXLBTAffectedLot_Phieu, INDEX IX_FVN_PXLBTAffectedLot_TraceKey, TABLE FVN_PhieuXuLyBatThuongAffectedLot | FVN_PhieuXuLyBatThuongAffectedLot | — | **GAP**: no-test-reference |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE3_InitialQC.sql` | INDEX IX_FVN_PXLB_QCInitial_Phieu, TABLE FVN_PhieuXuLyBatThuongQCInitial | — | — | **GAP**: no-code-reference; no-test-reference |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE4_9_ReworkDispositionCompensationWorkflowReport.sql` | INDEX IX_FVN_PXLB_ReworkMovement_Phieu, INDEX IX_FVN_PXLB_WorkflowAudit_Phieu, TABLE FVN_PXLB_Compensation, TABLE FVN_PXLB_CompensationMovement, TABLE FVN_PXLB_Disposition, TABLE FVN_PXLB_ReworkMovement, TABLE FVN_PXLB_ReworkPlan, TABLE FVN_PXLB_ReworkQC, TABLE FVN_PXLB_WorkflowAudit, VIEW vFVN_PXLB_ProcessReport | — | FVN_PXLB_Disposition, FVN_PXLB_ReworkQC | **GAP**: no-code-reference |
| `PCTP/Modules/XuLyHangLoi/Database/PHASE7_WorkflowSeed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/A0_SlotLot_Diagnose_Repair.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/FVN_PCTP_RuntimeTables.sql` | INDEX IX_FVN_Purchase_Order_HVNDB_NgayGiao, INDEX IX_FVN_Purchase_Order_HVNDB_OrderPart, INDEX IX_FVN_sys_WorkflowTransitions_Process_Active, TABLE FVN_Purchase_Order_HVNDB, TABLE FVN_sys_WorkflowTransitions | FVN_sys_WorkflowTransitions | — | **GAP**: no-test-reference |
| `SQL/NHAP_TP_HIS_Retire.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/PCTPQuarantineRelease.sql` | INDEX IX_FVN_PCTPQuarantineReleaseAudit_SlotLotId, TABLE FVN_PCTPQuarantineReleaseAudit | FVN_PCTPQuarantineReleaseAudit | — | **GAP**: no-test-reference |
| `SQL/PCTPStockMovementGuard.sql` | TABLE PCTPStockMovementGuard | — | PCTPStockMovementGuard | **GAP**: no-code-reference |
| `SQL/PCTP_DeliveryCommandLedger.sql` | TABLE FVN_DeliveryCommandLedger, TABLE FVN_DeliveryCommandQr | FVN_DeliveryCommandLedger, FVN_DeliveryCommandQr | FVN_DeliveryCommandLedger, FVN_DeliveryCommandQr | OK |
| `SQL/PCTP_EnforceUniqueStockTpLot.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/PCTP_LuuPhieuGiaoHang_CustomerIsolation.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/PCTP_StockTpIdentityAudit.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/PHASE14_SlotLot_NgQuarantine.sql` | TABLE SlotLot | SlotLot | SlotLot | OK |
| `SQL/PHASE15_HangChoGiaoPickDetails.sql` | INDEX IX_FVN_HangChoGiaoPickDetail_HangChoGiaoId, TABLE FVN_HangChoGiaoPickDetail | FVN_HangChoGiaoPickDetail | — | **GAP**: no-test-reference |
| `SQL/PHASE16_QC_Final_ExecutionTimeout_Fix.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/QC_G4_DeliveryFixture_Cleanup.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/QC_G4_DeliveryFixture_Seed.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/QC_Schema/01_Tables.sql` | TABLE DOCQRCODE, TABLE DOCQRCODE_SP, TABLE DONHANG, TABLE GIAO_HANG, TABLE LUUDOCQRCODE, TABLE LUUPHIEUGIAOHANG, TABLE Rack, TABLE STOCKTP, TABLE Slot, TABLE SlotLot, TABLE TMPPHIEUGIAOHANG, TABLE TMPPHIEUGIAOHANG_SP, TABLE Warehouse | DOCQRCODE, LUUDOCQRCODE, LUUPHIEUGIAOHANG, Rack, STOCKTP, Slot, SlotLot, TMPPHIEUGIAOHANG, Warehouse | DOCQRCODE, DOCQRCODE_SP, DONHANG, LUUDOCQRCODE, LUUPHIEUGIAOHANG, Rack, STOCKTP, Slot, SlotLot, TMPPHIEUGIAOHANG, TMPPHIEUGIAOHANG_SP, Warehouse | OK |
| `SQL/QC_Schema/02_Generated_From_DB57.sql` | TABLE b20dept, TABLE b20gear, TABLE b20imagestore, TABLE b20item, TABLE b20itemquycach, TABLE docqrcode, TABLE docqrcode_100003, TABLE docqrcode_sp, TABLE fvn_deliverycommandledger, TABLE fvn_deliverycommandqr, TABLE fvn_hangchogiao, TABLE fvn_hangchogiaopickdetail, TABLE fvn_itemfifoconfig, TABLE fvn_itemfifoconfig_history, TABLE fvn_pctpquarantinereleaseaudit, TABLE fvn_phieutrahang, TABLE fvn_phieutrahangct, TABLE fvn_phieuxulybatthuong, TABLE fvn_phieuxulybatthuongaffectedlot, TABLE fvn_phieuxulybatthuongqcinitial, TABLE fvn_purchase_order_hvndb, TABLE fvn_pxlb_compensation, TABLE fvn_pxlb_compensationmovement, TABLE fvn_pxlb_disposition, TABLE fvn_pxlb_reworkmovement, TABLE fvn_pxlb_reworkplan, TABLE fvn_pxlb_reworkqc, TABLE fvn_pxlb_workflowaudit, TABLE fvn_sys_workflowtransitions, TABLE fvn_trahangqtchunggiao, TABLE fvn_trahangqtchungnhapng, TABLE fvn_trahangqtchungqc, TABLE fvn_trahangqtchungxuat, TABLE inspectionconfig, TABLE inspectionlog, TABLE luudocqrcode, TABLE luuphieugiaohang, TABLE purchase_order_htn, TABLE purchase_order_ymvn, TABLE qrcode_changetime, TABLE rack, TABLE slot, TABLE slotlot, TABLE sp_docqrcode, TABLE sp_tmpphieugiaohang, TABLE stockhistory, TABLE stocktp, TABLE tbl_qr_comparepart, TABLE tbl_qr_may_docqr, TABLE tmplotghep, TABLE tmplotgheptest, TABLE tmplottach, TABLE tmpphieugiaodbhd, TABLE tmpphieugiaohang, TABLE tmpphieugiaohang_100002, TABLE tmpphieugiaohang_100003, TABLE tmpphieugiaohang_sp, TABLE tmpphieugiaohangdbct, TABLE warehouse, TABLE ymvn_docqrcode, TABLE ymvn_tmpphieugiaohang | b20gear, b20imagestore, b20item, b20itemquycach, docqrcode, fvn_deliverycommandledger, fvn_deliverycommandqr, fvn_hangchogiao, fvn_hangchogiaopickdetail, fvn_itemfifoconfig, fvn_itemfifoconfig_history, fvn_pctpquarantinereleaseaudit, fvn_phieutrahang, fvn_phieutrahangct, fvn_phieuxulybatthuong, fvn_phieuxulybatthuongaffectedlot, fvn_sys_workflowtransitions, fvn_trahangqtchunggiao, fvn_trahangqtchungnhapng, fvn_trahangqtchungqc, fvn_trahangqtchungxuat, inspectionconfig, inspectionlog, luudocqrcode, luuphieugiaohang, purchase_order_ymvn, qrcode_changetime, rack, slot, slotlot, sp_docqrcode, sp_tmpphieugiaohang, stockhistory, stocktp, tbl_qr_comparepart, tbl_qr_may_docqr, tmplotghep, tmplotgheptest, tmplottach, tmpphieugiaodbhd, tmpphieugiaohang, tmpphieugiaohangdbct, warehouse, ymvn_docqrcode, ymvn_tmpphieugiaohang | b20item, docqrcode, docqrcode_100003, docqrcode_sp, fvn_deliverycommandledger, fvn_deliverycommandqr, fvn_phieutrahang, fvn_phieutrahangct, fvn_phieuxulybatthuong, fvn_pxlb_disposition, fvn_pxlb_reworkqc, fvn_trahangqtchunggiao, fvn_trahangqtchungnhapng, fvn_trahangqtchungqc, fvn_trahangqtchungxuat, luudocqrcode, luuphieugiaohang, purchase_order_htn, purchase_order_ymvn, qrcode_changetime, rack, slot, slotlot, sp_docqrcode, sp_tmpphieugiaohang, stockhistory, stocktp, tmpphieugiaohang, tmpphieugiaohang_100003, tmpphieugiaohang_sp, warehouse, ymvn_docqrcode | OK |
| `SQL/QC_Schema/03_Generated_Constraints_From_DB57.sql` | TABLE B20Gear, TABLE B20Item, TABLE FVN_HangChoGiao, TABLE FVN_ItemFifoConfig, TABLE FVN_ItemFifoConfig_History, TABLE FVN_PhieuTraHang, TABLE FVN_PhieuTraHangCT, TABLE FVN_PhieuXuLyBatThuong, TABLE InspectionConfig, TABLE InspectionLog, TABLE StockHistory, TABLE TMPPHIEUGIAODBHD, TABLE TMPPHIEUGIAOHANGDBCT, TABLE sys_WorkflowTransitions | B20Gear, B20Item, FVN_HangChoGiao, FVN_ItemFifoConfig, FVN_ItemFifoConfig_History, FVN_PhieuTraHang, FVN_PhieuTraHangCT, FVN_PhieuXuLyBatThuong, InspectionConfig, InspectionLog, StockHistory, TMPPHIEUGIAODBHD, TMPPHIEUGIAOHANGDBCT | B20Item, FVN_PhieuTraHang, FVN_PhieuTraHangCT, FVN_PhieuXuLyBatThuong, StockHistory | OK |
| `SQL/QC_Schema/03_Generated_From_DB57_Constraints.sql` | TABLE B20Dept, TABLE B20Gear, TABLE B20ImageStore, TABLE B20Item, TABLE B20ItemQuyCach, TABLE FVN_DeliveryCommandLedger, TABLE FVN_DeliveryCommandQr, TABLE FVN_HangChoGiao, TABLE FVN_HangChoGiaoPickDetail, TABLE FVN_ItemFifoConfig, TABLE FVN_ItemFifoConfig_History, TABLE FVN_PCTPQuarantineReleaseAudit, TABLE FVN_PXLB_Compensation, TABLE FVN_PXLB_CompensationMovement, TABLE FVN_PXLB_Disposition, TABLE FVN_PXLB_ReworkMovement, TABLE FVN_PXLB_ReworkPlan, TABLE FVN_PXLB_ReworkQC, TABLE FVN_PXLB_WorkflowAudit, TABLE FVN_PhieuTraHang, TABLE FVN_PhieuTraHangCT, TABLE FVN_PhieuXuLyBatThuong, TABLE FVN_PhieuXuLyBatThuongAffectedLot, TABLE FVN_PhieuXuLyBatThuongQCInitial, TABLE FVN_Purchase_Order_HVNDB, TABLE FVN_TraHangQTChungGiao, TABLE FVN_TraHangQTChungNhapNG, TABLE FVN_TraHangQTChungQC, TABLE FVN_TraHangQTChungXuat, TABLE FVN_sys_WorkflowTransitions, TABLE InspectionConfig, TABLE InspectionLog, TABLE LUUPHIEUGIAOHANG, TABLE Rack, TABLE STOCKTP, TABLE Slot, TABLE SlotLot, TABLE StockHistory, TABLE TMPPHIEUGIAODBHD, TABLE TMPPHIEUGIAOHANGDBCT, TABLE Warehouse | B20Gear, B20ImageStore, B20Item, B20ItemQuyCach, FVN_DeliveryCommandLedger, FVN_DeliveryCommandQr, FVN_HangChoGiao, FVN_HangChoGiaoPickDetail, FVN_ItemFifoConfig, FVN_ItemFifoConfig_History, FVN_PCTPQuarantineReleaseAudit, FVN_PhieuTraHang, FVN_PhieuTraHangCT, FVN_PhieuXuLyBatThuong, FVN_PhieuXuLyBatThuongAffectedLot, FVN_TraHangQTChungGiao, FVN_TraHangQTChungNhapNG, FVN_TraHangQTChungQC, FVN_TraHangQTChungXuat, FVN_sys_WorkflowTransitions, InspectionConfig, InspectionLog, LUUPHIEUGIAOHANG, Rack, STOCKTP, Slot, SlotLot, StockHistory, TMPPHIEUGIAODBHD, TMPPHIEUGIAOHANGDBCT, Warehouse | B20Item, FVN_DeliveryCommandLedger, FVN_DeliveryCommandQr, FVN_PXLB_Disposition, FVN_PXLB_ReworkQC, FVN_PhieuTraHang, FVN_PhieuTraHangCT, FVN_PhieuXuLyBatThuong, FVN_TraHangQTChungGiao, FVN_TraHangQTChungNhapNG, FVN_TraHangQTChungQC, FVN_TraHangQTChungXuat, LUUPHIEUGIAOHANG, Rack, STOCKTP, Slot, SlotLot, StockHistory, Warehouse | OK |
| `SQL/QC_Schema/05_Split.sql` | FUNCTION Split | — | Split | **GAP**: no-code-reference |
| `SQL/QC_Schema/Export_SQL2_Complete_DDL_Metadata.sql` | — | — | — | **GAP**: no-code-reference; no-test-reference |
| `SQL/Usp_Qrcode_LOAD_PHIEU_DOCQR2405.sql` | PROC Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | Usp_Qrcode_LOAD_PHIEU_DOCQR2405 | OK |
| `SQL/Usp_Qrcode_LUU_PHIEU_RetireUnsafeLegacy.sql` | PROCEDURE Usp_Qrcode_LUU_PHIEU | — | Usp_Qrcode_LUU_PHIEU | **GAP**: no-code-reference |
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
| DEL-CMD-002 | GiaoHangKhach/DeliveryStockCommand/SQL | partial | Real SQL Server run of DeliveryStockCommandHandler with the real ledger repository and TMP pending gate: replay, duplicate QR, rollback of stock+ledger+QR+TMP+scan rows, two sessions on one BusinessKey and on one QR |
| DEL-CMD-001 | GiaoHangKhach/DeliveryStockCommand | partial | Delivery stock command handler (B0-01 slice 1): one UoW for STOCKTP+SlotLot via IStockMovementService.Export, durable BusinessKey/QR ledger, all-or-nothing pending close |
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
