# PCTP C# cutover inventory (legacy table names)

Generated from commit b0c46c8 by static text scan. **Not** a compile check. Names matched case-insensitively, whole word, in `.cs` only.

Files: 304 (production 265, test 39).


## Status legend
- DONE-READS: read queries use `dbo.FVN_*` + `SiteCode`
- TODO: still uses legacy name (works only through migration-013 synonyms, single site)
- REDESIGN: writes Slot/SlotLot/STOCKTP quantities directly; must post MES ledger first (`usp_FVN_PCTP_PostStockMovement`) then `usp_FVN_Slot_SaveLots`


## Production files by reference count
| Refs | File | Tables |
|---|---|---|
| 142 | `PCTP/Modules/XuLyHangLoi/FormTaoPhieuTraNoiBo.cs` | Slot:87, PhieuTraHangCT:49, PhieuTraHang:2, B20Dept:2, SlotLot:1, PhieuXuLyBatThuong:1 |
| 91 | `PCTP/Modules/KhoCore/Repositories/SlotRepository.cs` | Slot:37, SlotLot:23, Rack:21, Warehouse:10 |
| 78 | `PCTP/Modules/KhoVatLy/MainStockSV.Canvas.cs` | Rack:43, Slot:35 |
| 76 | `PCTP/Modules/GiaoHangKhach/Services/YmvnGearQuantityValidator.cs` | Gear:76 |
| 71 | `PCTP/Modules/KhoCore/Repositories/BulkStockSlotRepository.cs` | Slot:29, SlotLot:26, STOCKTP:8, Rack:5, Warehouse:3 |
| 68 | `PCTP/Modules/XuLyHangLoi/FormTiepNhanPhieuKhachTra.cs` | PhieuTraHangCT:51, Slot:10, PhieuTraHang:3, SlotLot:1, StockHistory:1, LUUPHIEUGIAOHANG:1, PhieuXuLyBatThuong:1 |
| 65 | `PCTP/Modules/KhoCore/Repositories/RackRepository.cs` | Rack:48, Slot:14, Warehouse:2, SlotLot:1 |
| 58 | `PCTP/Modules/GiaoHangKhach/YMVN/GIAOHANGYMN.cs` | Gear:41, STOCKTP:10, LUUPHIEUGIAOHANG:3, DocQrCode:2, B20Item:2 |
| 58 | `PCTP/Modules/XuLyHangLoi/Services/ReworkStockService.cs` | SlotLot:42, Slot:10, STOCKTP:6 |
| 50 | `PCTP/Modules/KhoVatLy/ExportFormSV.cs` | Slot:44, Rack:5, SlotLot:1 |
| 49 | `PCTP/Modules/GiaoHangKhach/SubForm/FormGhepLotThuCong.cs` | Gear:29, STOCKTP:11, B20Item:9 |
| 43 | `PCTP/Modules/GiaoHangKhach/YMVN/YMVNDOCQRCODE.cs` | Gear:40, B20Item:2, DocQrCode:1 |
| 40 | `PCTP/Modules/KhoCore/Repositories/ISlotRepository.cs` | Slot:26, SlotLot:8, Rack:5, STOCKTP:1 |
| 40 | `PCTP/Modules/KhoCore/Services/SlotService.cs` | Slot:31, Rack:7, SlotLot:2 |
| 36 | `PCTP/Modules/XuLyHangLoi/Repository/IPhieuTraHangRepository.cs` | PhieuTraHang:21, PhieuTraHangCT:12, PhieuXuLyBatThuong:3 |
| 33 | `PCTP/Modules/KhoCore/Interfaces/ISlotService.cs` | Slot:28, SlotLot:3, STOCKTP:1, Rack:1 |
| 32 | `PCTP/Modules/KhoCore/Fuction/CheckInfor.cs` | Slot:16, Rack:9, Warehouse:5, SlotLot:2 |
| 32 | `PCTP/Modules/KhoVatLy/FormEnterItemSV.cs` | Slot:24, STOCKTP:3, SlotLot:2, Rack:2, Warehouse:1 |
| 31 | `PCTP/Modules/KhoCore/Application/Services/StockMovementService.cs` | SlotLot:16, Slot:11, STOCKTP:4 |
| 29 | `PCTP/Infrastructure/Repositories/DocQRRepository.cs` | Gear:15, DocQrCode:13, B20Item:1 |
| 27 | `PCTP/Modules/XuLyHangLoi/FormQuanLyTienTrinhHangLoi.cs` | PhieuTraHang:9, PhieuXuLyBatThuong:9, Slot:4, PhieuTraHangCT:4, SlotLot:1 |
| 25 | `PCTP/Modules/GiaoHangKhach/Services/DocQRScanEngine.cs` | DocQrCode:14, Gear:11 |
| 25 | `PCTP/Modules/KhoCore/Helpers/SlotParser.cs` | Rack:11, Slot:10, Warehouse:4 |
| 25 | `PCTP/Modules/XuatKho/Repositories/StockExportRepository.cs` | STOCKTP:19, SlotLot:5, Slot:1 |
| 24 | `PCTP/Modules/XuLyHangLoi/Repository/PhieuTraHangRepository.cs` | PhieuTraHang:14, PhieuTraHangCT:10 |
| 23 | `PCTP/Modules/KhoVatLy/FormChonSlotNoiBo.cs` | Slot:20, Rack:2, SlotLot:1 |
| 23 | `PCTP/Modules/XuatKho/Repositories/InternalTransferRecallRepository.cs` | Slot:15, SlotLot:5, STOCKTP:1, Rack:1, Warehouse:1 |
| 22 | `PCTP/Modules/BaoCao/Infrastructure/Repositories/StockHistoryRepository.cs` | Slot:8, StockHistory:6, SlotLot:5, Rack:2, Warehouse:1 |
| 22 | `PCTP/Modules/XuatKho/FormChuyenHangNoiBo.cs` | Slot:14, STOCKTP:7, Rack:1 |
| 21 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuKhoRepository.cs` | STOCKTP:8, SlotLot:7, LUUPHIEUGIAOHANG:2, Gear:2, DocQrCode:1, HangChoGiao:1 |
| 21 | `PCTP/Modules/GiaoHangKhach/SubForm/TONKHOTP.TonKho.cs` | Slot:18, Rack:2, STOCKTP:1 |
| 21 | `PCTP/Modules/KhoVatLy/UCControls/SlotDetailPanel.cs` | Slot:18, STOCKTP:3 |
| 21 | `PCTP/Modules/NhapKho/Repository/StockTPRepository.cs` | STOCKTP:21 |
| 21 | `PCTP/Modules/XuLyHangLoi/Repository/IPhieuXuLyBatThuongRepository.cs` | PhieuXuLyBatThuong:14, PhieuTraHangCT:4, PhieuTraHang:3 |
| 20 | `PCTP/Modules/NhapKho/NhapKhoDashboardRepository.cs` | SlotLot:6, STOCKTP:5, Slot:3, Rack:3, Warehouse:3 |
| 20 | `PCTP/Modules/XuLyHangLoi/FormQCXacNhanCuoi.cs` | Slot:18, SlotLot:1, PhieuXuLyBatThuong:1 |
| 20 | `PCTP/Modules/XuLyHangLoi/Repository/PhieuXuLyBatThuongRepository.cs` | PhieuXuLyBatThuong:18, PhieuTraHangCT:2 |
| 19 | `PCTP/Modules/GiaoHangKhach/Services/BulkStockAdjustService.cs` | STOCKTP:7, SlotLot:7, Slot:5 |
| 19 | `PCTP/Modules/KhoCore/UI/FormDieuChinhTonKhoLot.cs` | STOCKTP:9, SlotLot:7, Slot:3 |
| 19 | `PCTP/Modules/XuLyHangLoi/Services/GiaoBuNGService.cs` | Slot:8, HangChoGiao:6, STOCKTP:5 |
| 18 | `PCTP/Modules/XuatKho/FormThuHoiHangNoiBo.cs` | Slot:15, STOCKTP:2, Rack:1 |
| 17 | `PCTP/Modules/NhapKho/Services/NhapTpReceivingService.cs` | Slot:8, STOCKTP:4, SlotLot:4, StockHistory:1 |
| 17 | `PCTP/Modules/XuLyHangLoi/SlotDichLookup.cs` | Slot:11, Rack:6 |
| 15 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuTmpRepository.cs` | Gear:11, DocQrCode:3, IFSPHIEUGIAOHANG:1 |
| 15 | `PCTP/Modules/KhoCore/Repositories/IBulkStockSlotRepository.cs` | Slot:8, SlotLot:3, STOCKTP:2, Rack:1, Warehouse:1 |
| 15 | `PCTP/Modules/XuLyHangLoi/Repository/TransactionalPhieuTraHangRepository.cs` | PhieuTraHang:10, PhieuTraHangCT:5 |
| 15 | `PCTP/Modules/XuLyHangLoi/Services/KhachTraHangService.cs` | PhieuTraHang:7, PhieuXuLyBatThuong:5, LUUPHIEUGIAOHANG:2, PhieuTraHangCT:1 |
| 15 | `PCTP/Modules/XuLyHangLoi/Services/LotClaimGuard.cs` | Slot:12, PhieuTraHangCT:2, SlotLot:1 |
| 15 | `PCTP/Modules/XuLyHangLoi/Services/QTChungService.cs` | PhieuXuLyBatThuong:9, PhieuTraHangCT:3, STOCKTP:1, Slot:1, PhieuTraHang:1 |
| 15 | `PCTP/Modules/XuatKho/Interfaces/IStockExportService.cs` | Slot:4, STOCKTP:3, StockHistory:3, HangChoGiao:3, SlotLot:2 |
| 15 | `PCTP/Modules/XuatKho/Repositories/HangChoGiaoRepository.cs` | HangChoGiao:12, Slot:3 |
| 14 | `PCTP/Domain/Interfaces/IRepositories.cs` | DocQrCode:11, Gear:3 |
| 13 | `PCTP/Modules/GiaoHangKhach/Repositories/TableOrderRepo.cs` | Gear:7, Slot:3, LUUPHIEUGIAOHANG:2, STOCKTP:1 |
| 13 | `PCTP/Modules/GiaoHangKhach/SubForm/TONKHOTP.cs` | STOCKTP:6, SlotLot:4, Slot:2, StockHistory:1 |
| 13 | `PCTP/Modules/KhoCore/Repositories/WarehouseRepository.cs` | Warehouse:10, Rack:2, B20Item:1 |
| 13 | `PCTP/Modules/XuLyHangLoi/FormReworkProcess.cs` | Slot:6, SlotLot:2, PhieuXuLyBatThuong:2, B20Dept:2, Rack:1 |
| 13 | `PCTP/Modules/XuLyHangLoi/Services/XuLyHangLoiServiceBase.cs` | PhieuTraHang:8, Slot:5 |
| 13 | `PCTP/Shared/Models/StockItem.cs` | Slot:5, SlotLot:3, Rack:3, STOCKTP:2 |
| 12 | `PCTP/Modules/KhoCore/Repositories/IRackRepository.cs` | Rack:9, Slot:3 |
| 12 | `PCTP/Modules/XuLyHangLoi/Repository/NoiBoLookupRepository.cs` | SlotLot:5, Slot:2, STOCKTP:1, StockHistory:1, Rack:1, Warehouse:1, LUUPHIEUGIAOHANG:1 |
| 11 | `PCTP/Modules/BaoCao/Application/Contracts/Queries/StockOnHandSummary.cs` | Slot:7, STOCKTP:3, SlotLot:1 |
| 11 | `PCTP/Modules/BaoCao/UI/Timeline/TraceTimelineNode.cs` | STOCKTP:5, LUUPHIEUGIAOHANG:4, StockHistory:2 |
| 11 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuRepository.cs` | Gear:8, STOCKTP:1, IFSPHIEUGIAOHANG:1, DocQrCode:1 |
| 11 | `PCTP/Modules/KhoCore/Application/Services/QuarantineReleaseService.cs` | SlotLot:5, Slot:5, STOCKTP:1 |
| 11 | `PCTP/Modules/XuatKho/Services/StockExportService.cs` | HangChoGiao:7, Slot:3, SlotLot:1 |
| 10 | `PCTP/Modules/BaoCao/Infrastructure/Repositories/QualityHistoryRepository.cs` | InspectionLog:10 |
| 10 | `PCTP/Modules/GiaoHangKhach/Services/FifoSessionState.cs` | Gear:10 |
| 10 | `PCTP/Modules/XuLyHangLoi/Models/NoiBoLookupModels.cs` | Slot:7, SlotLot:2, Rack:1 |
| 10 | `PCTP/Shared/Helpers/LotNoHelper.cs` | STOCKTP:4, Slot:2, SlotLot:1, Rack:1, Warehouse:1, Gear:1 |
| 9 | `PCTP/Infrastructure/Repositories/PhieuTrackingRepository.cs` | SlotLot:6, Slot:1, Rack:1, Warehouse:1 |
| 9 | `PCTP/Modules/XuLyHangLoi/Repository/PhieuGiaoRepository.cs` | LUUPHIEUGIAOHANG:9 |
| 9 | `PCTP/Modules/XuLyHangLoi/Services/IXuLyHangLoiService.cs` | PhieuTraHang:4, PhieuXuLyBatThuong:4, Slot:1 |
| 9 | `PCTP/Shared/Common/LotCodeHelper.cs` | Gear:7, STOCKTP:2 |
| 8 | `PCTP/Modules/GiaoHangKhach/Repositories/UploadedOrderRepository.cs` | Gear:8 |
| 8 | `PCTP/Modules/KhoCore/Services/RackService.cs` | Rack:8 |
| 8 | `PCTP/Modules/KhoVatLy/Application/Adapters/KhoCoreSlotQueryAdapter.cs` | Slot:8 |
| 8 | `PCTP/Modules/XuLyHangLoi/FormNhapLaiHangNG.cs` | Slot:8 |
| 8 | `PCTP/Modules/XuLyHangLoi/Services/IReworkStockService.cs` | SlotLot:3, STOCKTP:2, StockHistory:2, Slot:1 |
| 8 | `PCTP/Modules/XuLyHangLoi/Services/QrItemParser.cs` | SlotLot:7, Slot:1 |
| 8 | `PCTP/Modules/XuatKho/Services/StockExportValidationService.cs` | STOCKTP:4, Slot:3, SlotLot:1 |
| 8 | `PCTP/Shell/Help/WmsHelpCatalog.cs` | Slot:5, STOCKTP:2, SlotLot:1 |
| 7 | `PCTP/Modules/BaoCao/Infrastructure/Repositories/DefectTicketHistoryReader.cs` | StockHistory:4, InspectionLog:3 |
| 7 | `PCTP/Modules/GiaoHangKhach/Contracts/Delivery/DeliveryStockCommand.cs` | Gear:5, STOCKTP:1, SlotLot:1 |
| 7 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuLuuTruRepository.cs` | LUUPHIEUGIAOHANG:6, STOCKTP:1 |
| 7 | `PCTP/Modules/GiaoHangKhach/SubForm/FRM_CHON_LOT_KHO.cs` | Gear:7 |
| 7 | `PCTP/Modules/KhoCore/Infrastructure/Tracking/PhieuTrackingReadRepository.cs` | SlotLot:4, Slot:1, Rack:1, Warehouse:1 |
| 7 | `PCTP/Modules/KhoCore/Interfaces/IWarehouseService.cs` | Warehouse:4, Rack:2, Slot:1 |
| 7 | `PCTP/Modules/KhoVatLy/MainStockSV.Navigation.cs` | Rack:4, Slot:3 |
| 7 | `PCTP/Modules/XuLyHangLoi/FormGiaoBuNG.cs` | HangChoGiao:7 |
| 7 | `PCTP/Presentation/Presenters/PhieuPresenter.cs` | DocQrCode:6, Gear:1 |
| 6 | `PCTP/Infrastructure/Stock/LegacyStockSlotRepositoryAdapter.cs` | Slot:5, SlotLot:1 |
| 6 | `PCTP/Modules/BaoCao/Application/Contracts/Queries/CurrentStockRow.cs` | Slot:3, SlotLot:2, STOCKTP:1 |
| 6 | `PCTP/Modules/GiaoHangKhach/Repositories/DeliveryTmpPendingGate.cs` | LUUPHIEUGIAOHANG:3, Gear:3 |
| 6 | `PCTP/Modules/GiaoHangKhach/Services/InPhieuService.cs` | Gear:5, B20Item:1 |
| 6 | `PCTP/Modules/GiaoHangKhach/SubForm/FRM_UploadMikrun.cs` | Gear:6 |
| 6 | `PCTP/Modules/KhoCore/Models/SlotLotInfo.cs` | Slot:4, SlotLot:2 |
| 6 | `PCTP/Modules/KhoVatLy/MainStockSV.Dashboard.cs` | STOCKTP:2, Slot:2, Rack:2 |
| 6 | `PCTP/Modules/NhapKho/Repository/StockTpStatusRepository.cs` | STOCKTP:6 |
| 6 | `PCTP/Modules/XuLyHangLoi/Models/PhieuTraHangCT.cs` | Slot:3, SlotLot:1, PhieuTraHang:1, PhieuTraHangCT:1 |
| 6 | `PCTP/Modules/XuatKho/Interfaces/IHangChoGiaoRepository.cs` | HangChoGiao:6 |
| 6 | `PCTP/Presentation/Presenters/DocQrPresenter.cs` | DocQrCode:6 |
| 5 | `PCTP/Infrastructure/Stock/StockReceivingRepository.cs` | STOCKTP:3, SlotLot:1, Slot:1 |
| 5 | `PCTP/Modules/BaoCao/UI/FormBaoCaoQualityHistory.cs` | InspectionLog:5 |
| 5 | `PCTP/Modules/GiaoHangKhach/Configuration/GiaoHangKhachConfiguration.cs` | SlotLot:2, STOCKTP:1, Slot:1, DocQrCode:1 |
| 5 | `PCTP/Modules/GiaoHangKhach/HVN/Controls/PhieuGridControl.cs` | Gear:5 |
| 5 | `PCTP/Modules/GiaoHangKhach/Models/OrderLotMerger.cs` | Gear:4, LUUPHIEUGIAOHANG:1 |
| 5 | `PCTP/Modules/GiaoHangKhach/Repositories/ComparePartRepository.cs` | B20Item:5 |
| 5 | `PCTP/Modules/GiaoHangKhach/SubForm/FRM_SUALOTHVN.cs` | DocQrCode:5 |
| 5 | `PCTP/Modules/KhoVatLy/FormRegisterRack.Designer.cs` | Slot:3, Rack:1, Warehouse:1 |
| 5 | `PCTP/Modules/KhoVatLy/UCControls/FormBulkSlotView.cs` | Slot:5 |
| 5 | `PCTP/Modules/NhapKho/Interfaces/IStockTpImportGuardRepository.cs` | SlotLot:3, StockHistory:2 |
| 5 | `PCTP/Modules/XuLyHangLoi/Enums/PhieuTraHangStatus.cs` | PhieuXuLyBatThuong:3, PhieuTraHangCT:2 |
| 5 | `PCTP/Modules/XuLyHangLoi/Repository/INoiBoLookupRepository.cs` | SlotLot:2, StockHistory:1, Slot:1, LUUPHIEUGIAOHANG:1 |
| 5 | `PCTP/Modules/XuatKho/Models/StockExportItem.cs` | Slot:3, SlotLot:1, StockHistory:1 |
| 5 | `PCTP/Shared/Common/TemFccParseResult.cs` | Gear:5 |
| 5 | `PCTP/Shared/Helpers/ScanResult.cs` | DocQrCode:5 |
| 5 | `PCTP/Shared/Models/BulkImportConfig.cs` | Slot:5 |
| 4 | `PCTP/Domain/Interfaces/ISqlRepository.cs` | LUUPHIEUGIAOHANG:1, IFSPHIEUGIAOHANG:1, DocQrCode:1, B20Item:1 |
| 4 | `PCTP/Infrastructure/Repositories/SqlRepository.cs` | B20Item:2, LUUPHIEUGIAOHANG:1, IFSPHIEUGIAOHANG:1 |
| 4 | `PCTP/Modules/BaoCao/Infrastructure/Repositories/DeliveryTraceRepository.cs` | LUUPHIEUGIAOHANG:4 |
| 4 | `PCTP/Modules/BaoCao/Infrastructure/Repositories/LotLocationRepository.cs` | SlotLot:1, Slot:1, Rack:1, Warehouse:1 |
| 4 | `PCTP/Modules/BaoCao/UI/FormBaoCaoStockHistory.cs` | Slot:2, Rack:1, LUUPHIEUGIAOHANG:1 |
| 4 | `PCTP/Modules/GiaoHangKhach/Contracts/PhieuGiao/IPhieuTmpRepository.cs` | Gear:4 |
| 4 | `PCTP/Modules/GiaoHangKhach/Controls/UploadedOrderManagerControl.cs` | Gear:4 |
| 4 | `PCTP/Modules/GiaoHangKhach/Repositories/DeliveryHangChoGiaoPostStep.cs` | HangChoGiao:4 |
| 4 | `PCTP/Modules/GiaoHangKhach/Services/PhieuYmvnService.cs` | Gear:4 |
| 4 | `PCTP/Modules/GiaoHangKhach/YMVN/YAMAHAQRCDE_SP.cs` | Gear:4 |
| 4 | `PCTP/Modules/KhoCore/Models/LotInfo.cs` | Slot:3, SlotLot:1 |
| 4 | `PCTP/Modules/KhoCore/Repositories/InspectionLogRepository.cs` | InspectionLog:4 |
| 4 | `PCTP/Modules/KhoVatLy/FormHangChoGiao.cs` | Slot:3, STOCKTP:1 |
| 4 | `PCTP/Modules/NhapKho/FormNhapKhoTienTrinh.cs` | Slot:3, Rack:1 |
| 4 | `PCTP/Modules/XuLyHangLoi/Services/IQTChungService.cs` | Slot:3, PhieuXuLyBatThuong:1 |
| 4 | `PCTP/Modules/XuatKho/Interfaces/IStockExportRepository.cs` | STOCKTP:2, Slot:2 |
| 4 | `PCTP/Shared/Models/HangChoGiao.cs` | Slot:3, HangChoGiao:1 |
| 4 | `PCTP/Shared/Models/QRCODE.cs` | Slot:2, STOCKTP:1, Gear:1 |
| 3 | `PCTP/Domain/Entities/DocQRCode.cs` | DocQrCode:2, Gear:1 |
| 3 | `PCTP/Modules/BaoCao/Application/Contracts/Queries/ItemHistoryRow.cs` | StockHistory:3 |
| 3 | `PCTP/Modules/BaoCao/Application/Contracts/Repositories/IDeliveryDetailQuery.cs` | StockHistory:1, Slot:1, LUUPHIEUGIAOHANG:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Contracts/PhieuGiao/IPhieuLotRepository.cs` | IFSPHIEUGIAOHANG:2, DocQrCode:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Reports/IN_GHP_LOT_YMVN.cs` | Gear:3 |
| 3 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuLotRepository.cs` | STOCKTP:1, DocQrCode:1, Gear:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuValidationRepository.cs` | STOCKTP:2, B20Item:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Services/DeliveryStockCommandHandler.cs` | SlotLot:2, STOCKTP:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Services/DocQRService.cs` | DocQrCode:2, Gear:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Services/HangThieuCaNgayService.cs` | LUUPHIEUGIAOHANG:2, STOCKTP:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Services/IDocQRService.cs` | Gear:2, DocQrCode:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/Services/PhieuLoadService.cs` | B20Item:2, Gear:1 |
| 3 | `PCTP/Modules/GiaoHangKhach/YMVN/GIAOHANGYMN.Designer.cs` | Gear:3 |
| 3 | `PCTP/Modules/KhoCore/Application/Contracts/Stock/IStockSlotRepository.cs` | Slot:2, SlotLot:1 |
| 3 | `PCTP/Modules/KhoCore/Models/Rack.cs` | Slot:2, Rack:1 |
| 3 | `PCTP/Modules/KhoCore/Services/WarehouseService.cs` | Slot:1, Rack:1, Warehouse:1 |
| 3 | `PCTP/Modules/KhoVatLy/CommonSlotFormUI.cs` | Slot:2, Rack:1 |
| 3 | `PCTP/Modules/KhoVatLy/FormInspectionConfig.cs` | B20Item:3 |
| 3 | `PCTP/Modules/KhoVatLy/FormRegisterRack.cs` | Slot:1, Rack:1, Warehouse:1 |
| 3 | `PCTP/Modules/NhapKho/INhapKhoDashboardRepository.cs` | SlotLot:2, STOCKTP:1 |
| 3 | `PCTP/Modules/NhapKho/Services/INhapTpReceivingService.cs` | Slot:2, STOCKTP:1 |
| 3 | `PCTP/Modules/XuLyHangLoi/Models/PhieuTraHang.cs` | PhieuTraHangCT:2, PhieuTraHang:1 |
| 3 | `PCTP/Modules/XuLyHangLoi/Models/TraHangQTChungXuat.cs` | STOCKTP:1, Slot:1, PhieuXuLyBatThuong:1 |
| 3 | `PCTP/Modules/XuatKho/Interfaces/IStockExportValidationService.cs` | STOCKTP:2, Slot:1 |
| 3 | `PCTP/Shared/Models/CustomerTableConfig.cs` | IFSPHIEUGIAOHANG:1, IFSPHIEUGIAOHANG_SP:1, DocQrCode:1 |
| 3 | `PCTP/Shared/Models/RackDefinition.cs` | Slot:3 |
| 2 | `PCTP/Domain/Entities/TrangThaiBan.cs` | DocQrCode:2 |
| 2 | `PCTP/Domain/Events/DomainEvent.cs` | DocQrCode:2 |
| 2 | `PCTP/Modules/BaoCao/Application/Contracts/Models/SlotMovementRow.cs` | StockHistory:1, Slot:1 |
| 2 | `PCTP/Modules/BaoCao/Infrastructure/Repositories/ProductionLotTraceRepository.cs` | STOCKTP:2 |
| 2 | `PCTP/Modules/GiaoHangKhach/Contracts/PhieuGiao/ITableOrderRepository.cs` | LUUPHIEUGIAOHANG:1, Gear:1 |
| 2 | `PCTP/Modules/GiaoHangKhach/HVN/HVN_PGH.Designer.cs` | Gear:2 |
| 2 | `PCTP/Modules/GiaoHangKhach/Reports/IN_GHP_LOT.cs` | Gear:2 |
| 2 | `PCTP/Modules/GiaoHangKhach/Repositories/ItemFifoConfigRepository.cs` | B20Item:2 |
| 2 | `PCTP/Modules/GiaoHangKhach/Services/IDeliveryStockCommandHandler.cs` | STOCKTP:1, SlotLot:1 |
| 2 | `PCTP/Modules/GiaoHangKhach/Services/IPhieuService.cs` | Gear:2 |
| 2 | `PCTP/Modules/GiaoHangKhach/Services/PhieuService.cs` | Gear:2 |
| 2 | `PCTP/Modules/GiaoHangKhach/YMVN/YMVNDOCQRCODE.Designer.cs` | Gear:2 |
| 2 | `PCTP/Modules/KhoCore/Application/Contracts/Slot/SlotLocation.cs` | Slot:1, Warehouse:1 |
| 2 | `PCTP/Modules/KhoCore/Application/Contracts/Stock/StockMovementRequest.cs` | SlotLot:2 |
| 2 | `PCTP/Modules/KhoCore/Application/Contracts/Stock/StockSlotTakeResult.cs` | SlotLot:1, Slot:1 |
| 2 | `PCTP/Modules/KhoCore/Interfaces/IRackService.cs` | Rack:2 |
| 2 | `PCTP/Modules/KhoCore/Models/Slot.cs` | Slot:2 |
| 2 | `PCTP/Modules/KhoCore/Models/SlotLotViewInfo.cs` | SlotLot:1, Slot:1 |
| 2 | `PCTP/Modules/KhoCore/Models/Warehouse.cs` | Rack:1, Warehouse:1 |
| 2 | `PCTP/Modules/KhoCore/Repositories/IWarehouseRepository.cs` | Warehouse:2 |
| 2 | `PCTP/Modules/KhoVatLy/CanVas/RackSummaryPopup.cs` | Rack:2 |
| 2 | `PCTP/Modules/KhoVatLy/CanVas/SlotLayoutInfo.cs` | Slot:2 |
| 2 | `PCTP/Modules/NhapKho/Repository/StockTpImportGuardRepository.cs` | STOCKTP:1, StockHistory:1 |
| 2 | `PCTP/Modules/XuLyHangLoi/Reports/RpPhieuXuLyBatThuong.cs` | PhieuXuLyBatThuong:2 |
| 2 | `PCTP/Modules/XuLyHangLoi/Services/AffectedLotTraceService.cs` | PhieuXuLyBatThuong:2 |
| 2 | `PCTP/Modules/XuLyHangLoi/Services/IAffectedLotTraceService.cs` | PhieuXuLyBatThuong:2 |
| 2 | `PCTP/Modules/XuLyHangLoi/Services/ITraNoiBoService.cs` | PhieuTraHang:2 |
| 2 | `PCTP/Modules/XuatKho/Models/StockExportRequest.cs` | Slot:2 |
| 2 | `PCTP/Modules/XuatKho/Repositories/InternalTransferRepository.cs` | SlotLot:1, Slot:1 |
| 2 | `PCTP/Shared/Models/PhieuLocationInfo.cs` | STOCKTP:1, SlotLot:1 |
| 2 | `PCTP/Shared/Models/PhieuStatus.cs` | STOCKTP:1, Slot:1 |
| 2 | `PCTP/Shared/Models/SlotRenderInfo.cs` | Slot:2 |
| 2 | `PCTP/Shell/Widgets/MainAppDashboardController.cs` | STOCKTP:1, LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Acess Image/ImagesControl.cs` | B20Item:1 |
| 1 | `PCTP/Modules/BaoCao/Application/Contracts/Models/HangChoGiaoRow.cs` | Slot:1 |
| 1 | `PCTP/Modules/BaoCao/Application/Contracts/Queries/QualityHistoryRow.cs` | InspectionLog:1 |
| 1 | `PCTP/Modules/BaoCao/Infrastructure/Queries/DeliveryTraceReadService.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Modules/BaoCao/UI/FormBaoCaoTraceability.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Contracts/IComparePartRepository.cs` | B20Item:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Contracts/PhieuGiao/IPhieuLuuTruRepository.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/HVN/Controls/DocQrControl.cs` | DocQrCode:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/HVN/Controls/PhieuActionBarControl.cs` | DocQrCode:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/HVN/Controls/PhieuHeaderControl.cs` | DocQrCode:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/HVN/Edit_DH.cs` | STOCKTP:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/HVN/HVN_PGH.DocQrUi.cs` | DocQrCode:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Models/OrderSourceResult.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/OrderLoading/Category/GioRowFilter.cs` | Slot:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Reports/GHEPLOT_YMVN.Designer.cs` | Gear:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Reports/IN_GHP_LOT_YMVN.Designer.cs` | Gear:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Reports/YMVN.Designer.cs` | Gear:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Reports/YMVN_PGH.Designer.cs` | Gear:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Reports/rpPhieuGiaoHangYAM.designer.cs` | Gear:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Repositories/IFSRepository.cs` | Gear:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuCurrentQrFifoValidationRepository.cs` | STOCKTP:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Repositories/PhieuGiaoDBRepository.cs` | B20Item:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Services/DocQRTableResolver.cs` | DocQrCode:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Services/GiaoHangKhachFactory.cs` | DocQrCode:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Services/IBulkStockAdjustService.cs` | Slot:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Services/IDeliveryCommandLedger.cs` | HangChoGiao:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/Services/PhieuKhoService.cs` | SlotLot:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/SubForm/FormItemFifoConfig.cs` | B20Item:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/WorkingState/IDeliveryWorkingState.cs` | DocQrCode:1 |
| 1 | `PCTP/Modules/GiaoHangKhach/YMVN/ClsPGH.cs` | Gear:1 |
| 1 | `PCTP/Modules/KhoCore/Application/Contracts/Slot/ISlotQueryService.cs` | Slot:1 |
| 1 | `PCTP/Modules/KhoCore/Application/Contracts/Stock/IStockBalanceRepository.cs` | Slot:1 |
| 1 | `PCTP/Modules/KhoCore/Application/Contracts/Stock/StockSlotLot.cs` | STOCKTP:1 |
| 1 | `PCTP/Modules/KhoCore/Application/Contracts/Tracking/IPhieuTrackingReadRepository.cs` | SlotLot:1 |
| 1 | `PCTP/Modules/KhoCore/Repositories/StockHistoryRepository.cs` | StockHistory:1 |
| 1 | `PCTP/Modules/KhoVatLy/CanVas/RackLayoutInfo.cs` | Rack:1 |
| 1 | `PCTP/Modules/KhoVatLy/MainStockSV.Designer.cs` | Rack:1 |
| 1 | `PCTP/Modules/KhoVatLy/MainStockSV.Search.cs` | Rack:1 |
| 1 | `PCTP/Modules/KhoVatLy/MainStockSV.cs` | Slot:1 |
| 1 | `PCTP/Modules/KhoVatLy/NhapTpModuleFactory.cs` | SlotLot:1 |
| 1 | `PCTP/Modules/NhapKho/Application/Adapters/StockSlotRepositoryAdapter.cs` | Slot:1 |
| 1 | `PCTP/Modules/NhapKho/ReceivingWorklistFilter.cs` | STOCKTP:1 |
| 1 | `PCTP/Modules/NhapKho/Repository/StockTpProductionRepository.cs` | B20Item:1 |
| 1 | `PCTP/Modules/NhapKho/Services/IStockTpLookupService.cs` | STOCKTP:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Application/Adapters/ReworkStockSlotAdapter.cs` | Slot:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Application/ReworkWorkflowTransaction.cs` | PhieuXuLyBatThuong:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Enums/HuongXuLyBatThuong.cs` | PhieuXuLyBatThuong:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Enums/QTChungStatus.cs` | PhieuXuLyBatThuong:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/FormDoiChieuPhieuGiao.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/FormQCDinhHuong.cs` | PhieuXuLyBatThuong:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Models/PhieuGiaoUngVienInfo.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Models/PhieuXuLyBatThuong.cs` | PhieuXuLyBatThuong:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Models/TraHangQTChungQC.cs` | PhieuXuLyBatThuong:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Reports/RpPhieuXacNhanPhuTungLoi.cs` | PhieuTraHang:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Repository/IPhieuGiaoRepository.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Repository/ITraHangQTChungRepository.cs` | Slot:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Services/IGiaoBuNGService.cs` | HangChoGiao:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Services/IKhachTraHangService.cs` | PhieuTraHang:1 |
| 1 | `PCTP/Modules/XuLyHangLoi/Services/TraNoiBoService.cs` | PhieuTraHang:1 |
| 1 | `PCTP/Modules/XuatKho/Interfaces/IStockExportHistoryRepository.cs` | StockHistory:1 |
| 1 | `PCTP/Modules/XuatKho/Models/StockExportReferenceType.cs` | PhieuXuLyBatThuong:1 |
| 1 | `PCTP/Modules/XuatKho/Models/StockExportResult.cs` | Slot:1 |
| 1 | `PCTP/Modules/XuatKho/Models/StockExportSource.cs` | Slot:1 |
| 1 | `PCTP/Modules/XuatKho/Repositories/StockExportHistoryRepository.cs` | StockHistory:1 |
| 1 | `PCTP/Presentation/Presenters/HVNPresenterContext.cs` | Slot:1 |
| 1 | `PCTP/Presentation/Presenters/YmvnPresenter.cs` | Gear:1 |
| 1 | `PCTP/Shared/Common/ItemFifoConfig.cs` | ItemFifoConfig:1 |
| 1 | `PCTP/Shared/Lookup/StockLookupCriteria.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Shared/Models/CustomerConfig.cs` | LUUPHIEUGIAOHANG:1 |
| 1 | `PCTP/Shared/Models/QRCodeInfo.cs` | Warehouse:1 |
| 1 | `PCTP/Shell/Widgets/WarehouseDashboardBar.cs` | Warehouse:1 |

## Test files
| Refs | File |
|---|---|
| 55 | `PCTP.QC.SqlIntegration/ProductionBulkStockSlotRepositoryScenario.cs` |
| 43 | `PCTP.QC.SqlIntegration/YmvnDeliverySpecScenario.cs` |
| 37 | `PCTP.QC.Tests/YmvnGearQuantityValidatorTests.cs` |
| 31 | `PCTP.QC.SqlIntegration/DeliveryCommandSqlScenario.cs` |
| 28 | `PCTP.QC.SqlIntegration/Program.cs` |
| 27 | `PCTP.QC.SqlIntegration/ProductionBulkStockSlotConcurrencyScenario.cs` |
| 24 | `PCTP.QC.SqlIntegration/SqlEngineWorkflowScenario.cs` |
| 20 | `PCTP.QC.SqlIntegration/DeliveryQrAllocationScenario.cs` |
| 19 | `PCTP.QC.SqlIntegration/ProductionStockTpRepositoryScenario.cs` |
| 19 | `PCTP.QC.SqlIntegration/QuarantineReleaseScenario.cs` |
| 15 | `PCTP.QC.SqlIntegration/DeliveryProductionAcceptanceScenario.cs` |
| 13 | `PCTP.QC.SqlIntegration/ProductionStockHistoryRepositoryScenario.cs` |
| 13 | `PCTP.QC.Tests/DeliveryStockCommandHandlerTests.cs` |
| 12 | `PCTP.QC.Tests/AtomicityFindingsSourceContractTests.cs` |
| 11 | `PCTP.QC.Tests/YmvnDeliverySpecContractTests.cs` |
| 10 | `PCTP.QC.Tests/QrLotFormatContractTests.cs` |
| 9 | `PCTP.QC.Tests/QcFinalDomainInvariantTests.cs` |
| 8 | `PCTP.QC.Tests/DeliveryTransactionContractTests.cs` |
| 7 | `PCTP.QC.Tests/DeliveryLotServiceUnitTests.cs` |
| 7 | `PCTP.QC.Tests/OrderLotMergerTests.cs` |
| 4 | `PCTP.QC.Tests/DeliveryLegacyWriterInventoryTests.cs` |
| 3 | `PCTP.QC.Tests/DateTextCultureTests.cs` |
| 3 | `PCTP.QC.Tests/GiaoDbQrTableSourceContractTests.cs` |
| 3 | `PCTP.QC.Tests/StockOnHandAggregatorTests.cs` |
| 2 | `PCTP.QC.SqlIntegration/OrderTableDeliveryMarkingScenario.cs` |
| 2 | `PCTP.QC.SqlIntegration/ProductionReworkRepositorySqlScenario.Contracts.cs` |
| 2 | `PCTP.QC.Tests/LegacyDeliverySaveProcedureRetirementTests.cs` |
| 2 | `PCTP.QC.Tests/OrderTableSourceContractTests.cs` |
| 2 | `PCTP.QC.Tests/RackRepositoryColumnContractTests.cs` |
| 2 | `PCTP.QC.Tests/ReceivingWorklistReopenContractTests.cs` |
| 1 | `PCTP.QC.SqlIntegration/ProductionReworkRepositorySqlScenario.cs` |
| 1 | `PCTP.QC.TestRunner/MainForm.cs` |
| 1 | `PCTP.QC.Tests/DeliveryQrAllocationSourceContractTests.cs` |
| 1 | `PCTP.QC.Tests/DeliveryStockCommandContractTests.cs` |
| 1 | `PCTP.QC.Tests/LegacyTransactionBoundarySourceContractTests.cs` |
| 1 | `PCTP.QC.Tests/PerformanceContractTests.cs` |
| 1 | `PCTP.QC.Tests/RemainingQcCaseContractTests.cs` |
| 1 | `PCTP.QC.Tests/ReworkWorkflowTransactionTests.cs` |
| 1 | `PCTP.QC.Tests/StockMovementServiceTests.cs` |
