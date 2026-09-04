using PCTP.Common;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public sealed class ReworkStockService : IReworkStockService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISlotService _slotService;
        private readonly IStockExportRepository _stockTpRepo;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly ITraHangQTChungRepository _qtChungRepo;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public ReworkStockService(
            IUnitOfWork uow,
            ISlotService slotService,
            IStockExportRepository stockTpRepo,
            IStockHistoryRepository historyRepo,
            ITraHangQTChungRepository qtChungRepo,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo)
        {
            _uow = uow
                ?? throw new ArgumentNullException(nameof(uow));

            _slotService = slotService
                ?? throw new ArgumentNullException(nameof(slotService));

            _stockTpRepo = stockTpRepo
                ?? throw new ArgumentNullException(nameof(stockTpRepo));

            _historyRepo = historyRepo
                ?? throw new ArgumentNullException(nameof(historyRepo));

            _qtChungRepo = qtChungRepo
                ?? throw new ArgumentNullException(nameof(qtChungRepo));

            _phieuXuLyRepo = phieuXuLyRepo
                ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
        }


        // ============================================================
        // 1. TRA CỨU LOT CÓ THỂ REWORK
        // ============================================================

        public List<LotInfo> GetLotsCanRework(
            string maHang,
            string lotNo)
        {
            if (string.IsNullOrWhiteSpace(maHang))
                throw new ArgumentException(
                    "MaHang không được rỗng.",
                    nameof(maHang));

            string lotChuan = null;

            if (!string.IsNullOrWhiteSpace(lotNo))
            {
                lotChuan =
                    LotNoHelper.GetStockTpKey(lotNo);
            }

            var rows =
                _stockTpRepo.FindLotsWithStock(
                    maHang,
                    lotChuan);

            if (rows == null)
                return new List<LotInfo>();

            return rows
                .Where(x => x.SlConLai > 0)
                .Select(x => new LotInfo
                {
                    LotNo = x.LotNo,
                    Quantity = x.SlConLai,
                    ItemCode = x.ItemCode
                })
                .ToList();
        }


        // ============================================================
        // 2. TRA CỨU LOT THEO PHIẾU XỬ LÝ
        // ============================================================

        public List<LotInfo> GetLotsCanReworkByPhieuXuLy(
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentException(
                    "phieuXuLyId không hợp lệ.",
                    nameof(phieuXuLyId));

            var phieu =
                _phieuXuLyRepo.GetById(phieuXuLyId);

            if (phieu == null)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu xử lý bất thường " +
                    $"Id={phieuXuLyId}.");
            }

            if (string.IsNullOrWhiteSpace(phieu.MaSanPham))
            {
                throw new InvalidOperationException(
                    $"Phiếu xử lý Id={phieuXuLyId} " +
                    "chưa có MaSanPham.");
            }

            return GetLotsCanRework(
                phieu.MaSanPham,
                phieu.SoLoLoi);
        }


        // ============================================================
        // 3. XUẤT KHO ĐI REWORK
        //
        // Service này chỉ xử lý:
        //
        //   STOCKTP
        //   SLOT
        //   AUDIT QT CHUNG
        //   STOCK HISTORY
        //
        // KHÔNG tự chuyển:
        //
        //   QTChungStatus.DaDinhHuong
        //       ->
        //   QTChungStatus.DaXuatKhoRework
        //
        // Transition do QTChungService thực hiện.
        // ============================================================

        public ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotLotId,
            string lotNo,
            int soLuong,
            string nguoiXuat)
        {
            if (phieuXuLyId <= 0)
            {
                return ScanResult.Fail(
                    "phieuXuLyId không hợp lệ.");
            }

            if (slotLotId <= 0)
            {
                return ScanResult.Fail(
                    "slotLotId không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(lotNo))
            {
                return ScanResult.Fail(
                    "LotNo không được rỗng.");
            }

            if (soLuong <= 0)
            {
                return ScanResult.Fail(
                    "Số lượng xuất phải lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(nguoiXuat))
            {
                return ScanResult.Fail(
                    "Chưa xác định người xuất.");
            }

            string lotChuan;

            try
            {
                lotChuan =
                    LotNoHelper.GetStockTpKey(lotNo);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail(
                    $"LOT không hợp lệ: {ex.Message}");
            }


            _uow.Begin();

            try
            {
                // ========================================================
                // 1. LẤY PHIẾU XỬ LÝ
                // ========================================================

                var phieu =
                    _phieuXuLyRepo.GetById(
                        phieuXuLyId);

                if (phieu == null)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"Không tìm thấy phiếu xử lý bất thường " +
                        $"Id={phieuXuLyId}.");
                }


                // ========================================================
                // 2. KIỂM TRA HƯỚNG REWORK
                //
                // Chỉ CanRework mới được xuất kho rework.
                // ========================================================

                if (phieu.HuongXuLy !=
                    HuongXuLyBatThuong.CanRework)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"Phiếu Id={phieuXuLyId} không có " +
                        "hướng xử lý CanRework.");
                }


                // ========================================================
                // 3. LẤY SLOT
                // ========================================================

                var slotLot =
                    _slotService.GetLotsBySlotLotId(
                        slotLotId);

                if (slotLot == null)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"Không tìm thấy SlotLot " +
                        $"Id={slotLotId}.");
                }


                // ========================================================
                // 4. KIỂM TRA LOT
                // ========================================================

                if (!LotCodeHelper.AreLotKeysEquivalent(
                        slotLot.LotNo,
                        lotChuan))
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"SlotLot {slotLotId} chứa LOT " +
                        $"[{slotLot.LotNo}], không khớp " +
                        $"[{lotChuan}].");
                }


                // ========================================================
                // 5. KIỂM TRA SLOT ĐỦ HÀNG
                // ========================================================

                if (slotLot.Quantity < soLuong)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"SlotLot {slotLotId} chỉ còn " +
                        $"{slotLot.Quantity}, không đủ " +
                        $"{soLuong}.");
                }


                // ========================================================
                // 6. KIỂM TRA STOCKTP
                // ========================================================

                int tonTruocStockTp =
                    _stockTpRepo.GetSlConLai(
                        lotChuan);

                if (tonTruocStockTp < soLuong)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"STOCKTP LOT [{lotChuan}] không đủ " +
                        $"tồn để xuất {soLuong} " +
                        $"(hiện có: {tonTruocStockTp}).");
                }


                // ========================================================
                // 7. TRỪ STOCKTP ATOMIC
                // ========================================================

                bool daTruStockTp =
                    _stockTpRepo.TryDecreaseSlConLai(
                        lotChuan,
                        soLuong);

                if (!daTruStockTp)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"Không thể trừ tồn STOCKTP LOT " +
                        $"[{lotChuan}]. Có thể tồn đã thay đổi.");
                }


                // ========================================================
                // 8. TRỪ SLOT
                // ========================================================

                _slotService.DecreaseSlotLotQuantity(
                    slotLotId,
                    soLuong);


                int tonSauStockTp =
                    tonTruocStockTp - soLuong;


                // ========================================================
                // 9. GHI AUDIT XUẤT REWORK
                // ========================================================

                int xuatId =
                    _qtChungRepo.InsertXuat(
                        new TraHangQTChungXuat
                        {
                            PhieuXuLyBatThuongId =
                                phieuXuLyId,

                            SlotIdNguon =
                                slotLot.SlotVatLyId,

                            LotXuat =
                                lotChuan,

                            LoaiXuat =
                                "Rework",

                            MaHang =
                                slotLot.ItemCode,

                            SoLuongXuat =
                                soLuong,

                            TonTruoc =
                                tonTruocStockTp,

                            TonSau =
                                tonSauStockTp,

                            NguoiXuat =
                                nguoiXuat,

                            LyDo =
                                "Xuất kho đi rework"
                        });


                // ========================================================
                // 10. GHI STOCK HISTORY
                // ========================================================

                _historyRepo.SaveHistory(
                    actionType:
                        "REWORK_EXPORT",

                    itemCode:
                        slotLot.ItemCode,

                    lot:
                        new LotInfo
                        {
                            LotNo =
                                lotChuan,

                            Quantity =
                                soLuong,

                            TemCode =
                                StockExportReferenceFormatter.Format(
                                    StockExportReferenceType
                                        .PhieuXuLyBatThuong,
                                    phieuXuLyId)
                        },

                    fromSlotId:
                        slotLot.SlotVatLyId,

                    toSlotId:
                        null,

                    performedBy:
                        nguoiXuat);


                // ========================================================
                // 11. COMMIT
                // ========================================================

                _uow.Commit();

                return ScanResult.OK(
                    $"Đã xuất {soLuong} LOT [{lotChuan}] " +
                    $"đi rework (XuatId={xuatId}).");
            }
            catch (Exception ex)
            {
                SafeRollback();

                return ScanResult.Fail(
                    "Lỗi xuất kho rework: " +
                    ex.Message);
            }
        }


        // ============================================================
        // 4. NHẬP LẠI HÀNG NG
        //
        // Không cộng STOCKTP.
        //
        // Chỉ:
        //
        //   SLOT NG
        //   AUDIT
        //   HISTORY
        //
        // Transition:
        //
        //   DaQCXacNhanCuoi
        //          ↓
        //   DaNhapLaiKho
        //
        // do QTChungService xử lý.
        // ============================================================

        public ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuongNG,
            int? slotIdOK,
            int? slotIdNG,
            string nguoiNhap)
        {
            if (phieuXuLyId <= 0)
            {
                return ScanResult.Fail(
                    "phieuXuLyId không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(lotNo))
            {
                return ScanResult.Fail(
                    "LotNo không được rỗng.");
            }

            if (soLuongNG <= 0)
            {
                return ScanResult.Fail(
                    "Số lượng nhập hàng NG phải lớn hơn 0.");
            }

            if (!slotIdNG.HasValue ||
                slotIdNG.Value <= 0)
            {
                return ScanResult.Fail(
                    "Chưa chọn Slot NG để nhập hàng.");
            }

            if (string.IsNullOrWhiteSpace(nguoiNhap))
            {
                return ScanResult.Fail(
                    "Chưa xác định người nhập.");
            }

            string lotChuan;

            try
            {
                lotChuan =
                    LotNoHelper.GetStockTpKey(lotNo);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail(
                    $"LOT không hợp lệ: {ex.Message}");
            }


            _uow.Begin();

            try
            {
                // ========================================================
                // 1. LẤY PHIẾU
                // ========================================================

                var phieu =
                    _phieuXuLyRepo.GetById(
                        phieuXuLyId);

                if (phieu == null)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        $"Không tìm thấy phiếu xử lý bất thường " +
                        $"Id={phieuXuLyId}.");
                }
                // ── THÊM: phần OK (nếu có slotIdOK và số lượng OK > 0) ──────────────
                // Lấy SoLuongOK từ bảng QC đã ghi trước đó (TraHangQTChungQC)
                var qc = _qtChungRepo.GetQC(phieuXuLyId);
                int soLuongOK = qc?.SoLuongOK ?? 0;
                if (slotIdOK.HasValue && soLuongOK > 0)
                {
                    _slotService.AddQuantity(slotIdOK.Value, soLuongOK, phieu.MaSanPham, DateTime.Now);
                    _stockTpRepo.AdjustSlConLai(lotChuan, soLuongOK); // cộng lại STOCKTP khả dụng

                    _historyRepo.SaveHistory(
                        actionType: "NHAP_LAI_SAU_REWORK",   // KHÁC "IMPORT" của hàng nhập mới
                        itemCode: phieu.MaSanPham,
                        lot: new LotInfo
                        {
                            LotNo = lotChuan,
                            Quantity = soLuongOK,
                            TemCode = StockExportReferenceFormatter.Format(
                                StockExportReferenceType.PhieuXuLyBatThuong, phieuXuLyId)
                        },
                        fromSlotId: null,
                        toSlotId: slotIdOK.Value,
                        performedBy: nguoiNhap);
                }

                // ========================================================
                // 2. NHẬP VÀO SLOT NG
                //
                // Hàng NG không cộng STOCKTP khả dụng.
                // ========================================================

                _slotService.AddQuantity(
                    slotIdNG.Value,
                    soLuongNG,
                    phieu.MaSanPham,
                    DateTime.Now);


                // ========================================================
                // 3. GHI AUDIT NHẬP NG
                // ========================================================

                int nhapId =
                    _qtChungRepo.InsertNhapNG(
                        new TraHangQTChungNhapNG
                        {
                            PhieuXuLyBatThuongId =
                                phieuXuLyId,

                            SlotIdOK =
                                slotIdOK,

                            SlotIdNG =
                                slotIdNG,

                            SlotIdNhap =
                                slotIdNG,

                            LotNhapLai =
                                lotChuan,

                            MaHang =
                                phieu.MaSanPham,

                            SoLuongNG =
                                soLuongNG,

                            NgayNhap =
                                DateTime.Now,

                            NguoiNhap =
                                nguoiNhap,

                            LyDo =
                                "Nhập lại hàng NG sau rework"
                        });


                // ========================================================
                // 4. GHI HISTORY
                // ========================================================

                _historyRepo.SaveHistory(
                    actionType:
                        "REWORK_NG_IMPORT",

                    itemCode:
                        phieu.MaSanPham,

                    lot:
                        new LotInfo
                        {
                            LotNo =
                                lotChuan,

                            Quantity =
                                soLuongNG,

                            TemCode =
                                StockExportReferenceFormatter.Format(
                                    StockExportReferenceType
                                        .PhieuXuLyBatThuong,
                                    phieuXuLyId)
                        },

                    fromSlotId:
                        null,

                    toSlotId:
                        slotIdNG.Value,

                    performedBy:
                        nguoiNhap);


                // ========================================================
                // 5. COMMIT
                // ========================================================

                _uow.Commit();

                return ScanResult.OK($"Đã nhập {soLuongOK} OK + {soLuongNG} NG cho LOT [{lotChuan}].");
            }
            catch (Exception ex)
            {
                SafeRollback();

                return ScanResult.Fail(
                    "Lỗi nhập lại hàng NG: " +
                    ex.Message);
            }
        }
        public ScanResult NhapLaiHangOK(
    int phieuXuLyId,
    string lotNo,
    int soLuongOK,
    int slotIdOK,
    string nguoiNhap)
        {
            if (phieuXuLyId <= 0)
                return ScanResult.Fail("phieuXuLyId không hợp lệ.");
            if (soLuongOK <= 0)
                return ScanResult.Fail("SoLuongOK phải lớn hơn 0.");
            if (slotIdOK <= 0)
                return ScanResult.Fail("SlotIdOK không hợp lệ.");
            if (string.IsNullOrWhiteSpace(lotNo))
                return ScanResult.Fail("LotNo không được rỗng.");
            if (string.IsNullOrWhiteSpace(nguoiNhap))
                return ScanResult.Fail("Chưa xác định người nhập.");

            string lotChuan;
            try
            {
                // ✅ FIX: chuẩn hoá LOT trước khi động vào STOCKTP — đồng nhất với
                // XuatKhoRework / NhapLaiHangNG trong cùng class, tránh lệch dữ liệu
                // khi lotNo truyền vào còn dư Counter/Qty ở đuôi.
                lotChuan = LotNoHelper.GetStockTpKey(lotNo);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail($"LOT không hợp lệ: {ex.Message}");
            }

            _uow.Begin();
            try
            {
                // ✅ FIX: lấy phiếu 1 lần duy nhất thay vì gọi GetById() 3 lần rải rác
                // và không kiểm tra null (NullReferenceException tiềm ẩn ở bản cũ).
                var phieu = _phieuXuLyRepo.GetById(phieuXuLyId);
                if (phieu == null)
                {
                    SafeRollback();
                    return ScanResult.Fail(
                        $"Không tìm thấy phiếu xử lý bất thường Id={phieuXuLyId}.");
                }

                // ✅ FIX: GetSlConLai chỉ nhận (lotNo) — IStockExportRepository không có
                // overload (maHang, lotNo). Chỉ dùng để log tồn trước khi cộng.
                int tonTruoc = _stockTpRepo.GetSlConLai(lotChuan);

                // Nhập vào Slot OK
                _slotService.AddQuantity(
                    slotIdOK,
                    soLuongOK,
                    phieu.MaSanPham,
                    DateTime.Now);

                // ✅ FIX: TangSlConLai(maHang, lotNo, soLuong) KHÔNG tồn tại trong
                // IStockExportRepository. Cộng lại tồn khả dụng bằng đúng method có sẵn:
                // AdjustSlConLai(lotNo, delta) — delta dương = cộng thêm.
                // Method này tự throw nếu LOT chưa từng tồn tại trong STOCKTP, sẽ được
                // bắt ở catch bên dưới và rollback đúng.
                _stockTpRepo.AdjustSlConLai(lotChuan, soLuongOK);

                // Ghi lịch sử — dùng lotChuan (đã chuẩn hoá) thay vì lotNo thô
                _historyRepo.SaveHistory(
                    actionType: "NHAP_LAI_SAU_REWORK",
                    itemCode: phieu.MaSanPham,
                    lot: new LotInfo
                    {
                        LotNo = lotChuan,
                        Quantity = soLuongOK,
                        TemCode = StockExportReferenceFormatter.Format(
                            StockExportReferenceType.PhieuXuLyBatThuong, phieuXuLyId)
                    },
                    fromSlotId: null,
                    toSlotId: slotIdOK,
                    performedBy: nguoiNhap);

                _uow.Commit();

                return ScanResult.OK(
                    $"Đã nhập lại {soLuongOK} hàng OK vào Slot {slotIdOK} " +
                    $"(LOT [{lotChuan}], tồn trước: {tonTruoc}, tồn sau: {tonTruoc + soLuongOK}).");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi nhập lại hàng OK: " + ex.Message);
            }
        }

        // ============================================================
        // 5. HOÀN TRẢ KHO KHI HUỶ QT CHUNG
        //
        // Dùng khi:
        //
        //   QTChungService.HuyQTChung()
        //
        // KHÔNG tự đổi Status.
        // ============================================================

        public ScanResult HoanTraKhoKhiHuy(
            int phieuXuLyId,
            string nguoiThucHien)
        {
            if (phieuXuLyId <= 0)
            {
                return ScanResult.Fail(
                    "phieuXuLyId không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(nguoiThucHien))
            {
                return ScanResult.Fail(
                    "Chưa xác định người thực hiện.");
            }


            _uow.Begin();

            try
            {
                // ========================================================
                // 1. LẤY TỔNG HÀNG ĐÃ XUẤT
                // ========================================================

                var xuat =
                    _qtChungRepo
                        .GetXuat(phieuXuLyId);

                var tongXuat =
                    xuat
                        .GroupBy(x => new
                        {
                            x.LotXuat,
                            x.SlotIdNguon,
                            x.MaHang
                        })
                        .Select(g => new
                        {
                            LotNo =
                                g.Key.LotXuat,

                            SlotId =
                                g.Key.SlotIdNguon,

                            MaHang =
                                g.Key.MaHang,

                            TongXuat =
                                g.Sum(x =>
                                    x.SoLuongXuat)
                        })
                        .ToList();


                if (tongXuat.Count == 0)
                {
                    _uow.Commit();

                    return ScanResult.OK(
                        "Không có gì để hoàn trả — " +
                        "phiếu chưa từng xuất kho.");
                }


                // ========================================================
                // 2. LẤY TỔNG NG ĐÃ NHẬP
                // ========================================================

                var nhapNG =
                    _qtChungRepo
                        .GetNhapNG(phieuXuLyId);

                var tongNhapNG =
                    nhapNG
                        .GroupBy(x => x.LotNhapLai)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Sum(
                                x => x.SoLuongNG));


                var ketQua =
                    new List<string>();


                // ========================================================
                // 3. TÍNH PHẦN HÀNG CÒN TREO
                // ========================================================

                foreach (var nhom in tongXuat)
                {
                    string lotChuan =
                        LotNoHelper.GetStockTpKey(
                            nhom.LotNo);

                    int daNhapNG = 0;

                    if (!string.IsNullOrWhiteSpace(
                            nhom.LotNo))
                    {
                        tongNhapNG.TryGetValue(
                            nhom.LotNo,
                            out daNhapNG);
                    }

                    int conTreo =
                        nhom.TongXuat -
                        daNhapNG;

                    if (conTreo <= 0)
                        continue;


                    // ====================================================
                    // 4. HOÀN STOCKTP
                    // ====================================================

                    _stockTpRepo.AdjustSlConLai(
                        lotChuan,
                        conTreo);


                    // ====================================================
                    // 5. HOÀN SLOT NGUỒN
                    // ====================================================

                    _slotService.AddQuantity(
                        nhom.SlotId,
                        conTreo,
                        nhom.MaHang,
                        DateTime.Now);


                    // ====================================================
                    // 6. GHI HISTORY
                    // ====================================================

                    _historyRepo.SaveHistory(
                        actionType:
                            "REWORK_CANCEL_RETURN",

                        itemCode:
                            nhom.MaHang,

                        lot:
                            new LotInfo
                            {
                                LotNo =
                                    lotChuan,

                                Quantity =
                                    conTreo,

                                TemCode =
                                    StockExportReferenceFormatter.Format(
                                        StockExportReferenceType
                                            .PhieuXuLyBatThuong,
                                        phieuXuLyId)
                            },

                        fromSlotId:
                            null,

                        toSlotId:
                            nhom.SlotId,

                        performedBy:
                            nguoiThucHien);


                    ketQua.Add(
                        $"LOT [{lotChuan}]: " +
                        $"hoàn trả {conTreo} " +
                        $"về Slot {nhom.SlotId}");
                }


                // ========================================================
                // 7. COMMIT
                // ========================================================

                _uow.Commit();


                if (ketQua.Count == 0)
                {
                    return ScanResult.OK(
                        "Toàn bộ hàng xuất đã được xử lý — " +
                        "không còn số lượng nào cần hoàn trả.");
                }

                return ScanResult.OK(
                    "Đã hoàn trả kho do huỷ QT chung:\n" +
                    string.Join("\n", ketQua));
            }
            catch (Exception ex)
            {
                SafeRollback();

                return ScanResult.Fail(
                    "Lỗi hoàn trả kho khi huỷ: " +
                    ex.Message);
            }
        }


        // ============================================================
        // ROLLBACK AN TOÀN
        // ============================================================

        private void SafeRollback()
        {
            try
            {
                _uow.Rollback();
            }
            catch
            {
                // Không che lỗi nghiệp vụ ban đầu.
            }
        }
    }
}
