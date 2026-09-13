using PCTP.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
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

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Điều phối nghiệp vụ stock của Rework.
    ///
    /// Quy tắc quan trọng:
    /// - ISlotService chỉ còn dùng cho read/query legacy.
    /// - IStockExportRepository chỉ dùng để tra cứu STOCKTP.
    /// - Mọi mutation Slot/SlotLot/STOCKTP đi qua IStockMovementService.
    /// - Audit/history vẫn nằm trong cùng UnitOfWork của workflow.
    /// </summary>
    public sealed class ReworkStockService : IReworkStockService
    {
        private const string MovementReworkNgReceive = "REWORK_NG_RECEIVE";

        private readonly IUnitOfWork _uow;
        private readonly ISlotService _slotService;
        private readonly IStockExportRepository _stockTpRepo;
        private readonly IStockMovementService _stockMovement;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly ITraHangQTChungRepository _qtChungRepo;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;

        public ReworkStockService(
            IUnitOfWork uow,
            ISlotService slotService,
            IStockExportRepository stockTpRepo,
            IStockMovementService stockMovement,
            IStockHistoryRepository historyRepo,
            ITraHangQTChungRepository qtChungRepo,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _stockTpRepo = stockTpRepo ?? throw new ArgumentNullException(nameof(stockTpRepo));
            _stockMovement = stockMovement ?? throw new ArgumentNullException(nameof(stockMovement));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _qtChungRepo = qtChungRepo ?? throw new ArgumentNullException(nameof(qtChungRepo));
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
        }

        public List<LotInfo> GetLotsCanRework(string maHang, string lotNo)
        {
            if (string.IsNullOrWhiteSpace(maHang))
                throw new ArgumentException("MaHang không được rỗng.", nameof(maHang));

            string lotChuan = null;
            if (!string.IsNullOrWhiteSpace(lotNo))
                lotChuan = LotNoHelper.GetStockTpKey(lotNo);

            var rows = _stockTpRepo.FindLotsWithStock(maHang, lotChuan);
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

        public List<LotInfo> GetLotsCanReworkByPhieuXuLy(int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentException("phieuXuLyId không hợp lệ.", nameof(phieuXuLyId));

            var phieu = _phieuXuLyRepo.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException(
                    string.Format("Không tìm thấy phiếu xử lý bất thường Id={0}.", phieuXuLyId));

            if (string.IsNullOrWhiteSpace(phieu.MaSanPham))
                throw new InvalidOperationException(
                    string.Format("Phiếu xử lý Id={0} chưa có MaSanPham.", phieuXuLyId));

            return GetLotsCanRework(phieu.MaSanPham, phieu.SoLoLoi);
        }

        public ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotLotId,
            string lotNo,
            int soLuong,
            string nguoiXuat)
        {
            if (phieuXuLyId <= 0)
                return ScanResult.Fail("phieuXuLyId không hợp lệ.");
            if (slotLotId <= 0)
                return ScanResult.Fail("slotLotId không hợp lệ.");
            if (string.IsNullOrWhiteSpace(lotNo))
                return ScanResult.Fail("LotNo không được rỗng.");
            if (soLuong <= 0)
                return ScanResult.Fail("Số lượng xuất phải lớn hơn 0.");
            if (string.IsNullOrWhiteSpace(nguoiXuat))
                return ScanResult.Fail("Chưa xác định người xuất.");

            string lotChuan;
            try
            {
                lotChuan = LotNoHelper.GetStockTpKey(lotNo);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail("LOT không hợp lệ: " + ex.Message);
            }

            _uow.Begin();
            try
            {
                var phieu = _phieuXuLyRepo.GetById(phieuXuLyId);
                if (phieu == null)
                    return FailAndRollback(
                        string.Format("Không tìm thấy phiếu xử lý bất thường Id={0}.", phieuXuLyId));

                if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                    return FailAndRollback(
                        string.Format("Phiếu Id={0} không có hướng xử lý CanRework.", phieuXuLyId));

                var slotLot = _slotService.GetLotsBySlotLotId(slotLotId);
                if (slotLot == null)
                    return FailAndRollback(
                        string.Format("Không tìm thấy SlotLot Id={0}.", slotLotId));

                if (!LotCodeHelper.AreLotKeysEquivalent(slotLot.LotNo, lotChuan))
                    return FailAndRollback(
                        string.Format("SlotLot {0} chứa LOT [{1}], không khớp [{2}].",
                            slotLotId, slotLot.LotNo, lotChuan));

                if (slotLot.Quantity < soLuong)
                    return FailAndRollback(
                        string.Format("SlotLot {0} chỉ còn {1}, không đủ {2}.",
                            slotLotId, slotLot.Quantity, soLuong));

                int tonTruocStockTp = _stockTpRepo.GetSlConLai(lotChuan);
                if (tonTruocStockTp < soLuong)
                    return FailAndRollback(
                        string.Format("STOCKTP LOT [{0}] không đủ tồn để xuất {1} (hiện có: {2}).",
                            lotChuan, soLuong, tonTruocStockTp));

                var movement = _stockMovement.Export(new StockMovementRequest
                {
                    MovementType = "REWORK_EXPORT",
                    SlotLotId = slotLotId,
                    LotNo = lotChuan,
                    ItemCode = slotLot.ItemCode,
                    Quantity = soLuong,
                    ReferenceType = "PHIEU_XU_LY_BAT_THUONG",
                    ReferenceId = phieuXuLyId.ToString(),
                    PerformedBy = nguoiXuat,
                    Reason = "Xuất kho đi rework"
                });

                if (!movement.Success)
                    return FailAndRollback(movement.Message);

                int tonSauStockTp = tonTruocStockTp - soLuong;

                int xuatId = _qtChungRepo.InsertXuat(new TraHangQTChungXuat
                {
                    PhieuXuLyBatThuongId = phieuXuLyId,
                    SlotIdNguon = slotLot.SlotVatLyId,
                    LotXuat = lotChuan,
                    LoaiXuat = "Rework",
                    MaHang = slotLot.ItemCode,
                    SoLuongXuat = soLuong,
                    TonTruoc = tonTruocStockTp,
                    TonSau = tonSauStockTp,
                    NguoiXuat = nguoiXuat,
                    LyDo = "Xuất kho đi rework"
                });

                _historyRepo.SaveHistory(
                    actionType: "REWORK_EXPORT",
                    itemCode: slotLot.ItemCode,
                    lot: new LotInfo
                    {
                        LotNo = lotChuan,
                        Quantity = soLuong,
                        TemCode = StockExportReferenceFormatter.Format(
                            StockExportReferenceType.PhieuXuLyBatThuong,
                            phieuXuLyId)
                    },
                    fromSlotId: slotLot.SlotVatLyId,
                    toSlotId: null,
                    performedBy: nguoiXuat);

                _uow.Commit();
                return ScanResult.OK(
                    string.Format("Đã xuất {0} LOT [{1}] đi rework (XuatId={2}).",
                        soLuong, lotChuan, xuatId));
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi xuất kho rework: " + ex.Message);
            }
        }

        public ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuongNG,
            int? slotIdOK,
            int? slotIdNG,
            string nguoiNhap)
        {
            if (phieuXuLyId <= 0)
                return ScanResult.Fail("phieuXuLyId không hợp lệ.");
            if (string.IsNullOrWhiteSpace(lotNo))
                return ScanResult.Fail("LotNo không được rỗng.");
            if (soLuongNG <= 0)
                return ScanResult.Fail("Số lượng nhập hàng NG phải lớn hơn 0.");
            if (!slotIdNG.HasValue || slotIdNG.Value <= 0)
                return ScanResult.Fail("Chưa chọn Slot NG để nhập hàng.");
            if (string.IsNullOrWhiteSpace(nguoiNhap))
                return ScanResult.Fail("Chưa xác định người nhập.");

            string lotChuan;
            try
            {
                lotChuan = LotNoHelper.GetStockTpKey(lotNo);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail("LOT không hợp lệ: " + ex.Message);
            }

            _uow.Begin();
            try
            {
                var phieu = _phieuXuLyRepo.GetById(phieuXuLyId);
                if (phieu == null)
                    return FailAndRollback(
                        string.Format("Không tìm thấy phiếu xử lý bất thường Id={0}.", phieuXuLyId));

                var qc = _qtChungRepo.GetQC(phieuXuLyId);
                int soLuongOK = qc == null ? 0 : qc.SoLuongOK;

                if (slotIdOK.HasValue && soLuongOK > 0)
                {
                    var okMovement = _stockMovement.ReturnFromRework(new StockMovementRequest
                    {
                        MovementType = "REWORK_OK_RECEIVE",
                        TargetSlotId = slotIdOK.Value,
                        LotNo = lotChuan,
                        ItemCode = phieu.MaSanPham,
                        Quantity = soLuongOK,
                        ReferenceType = "PHIEU_XU_LY_BAT_THUONG",
                        ReferenceId = phieuXuLyId.ToString(),
                        PerformedBy = nguoiNhap,
                        Reason = "Nhập lại hàng OK sau rework"
                    });

                    if (!okMovement.Success)
                        return FailAndRollback(okMovement.Message);

                    _historyRepo.SaveHistory(
                        actionType: "NHAP_LAI_SAU_REWORK",
                        itemCode: phieu.MaSanPham,
                        lot: new LotInfo
                        {
                            LotNo = lotChuan,
                            Quantity = soLuongOK,
                            TemCode = StockExportReferenceFormatter.Format(
                                StockExportReferenceType.PhieuXuLyBatThuong,
                                phieuXuLyId)
                        },
                        fromSlotId: null,
                        toSlotId: slotIdOK.Value,
                        performedBy: nguoiNhap);
                }

                // NG được nhập vào quarantine/NG slot nhưng KHÔNG tăng STOCKTP.
                var ngMovement = _stockMovement.Receive(new StockMovementRequest
                {
                    MovementType = MovementReworkNgReceive,
                    TargetSlotId = slotIdNG.Value,
                    LotNo = lotChuan,
                    ItemCode = phieu.MaSanPham,
                    Quantity = soLuongNG,
                    ReferenceType = "PHIEU_XU_LY_BAT_THUONG",
                    ReferenceId = phieuXuLyId.ToString(),
                    PerformedBy = nguoiNhap,
                    Reason = "Nhập lại hàng NG sau rework"
                });

                if (!ngMovement.Success)
                    return FailAndRollback(ngMovement.Message);

                int nhapId = _qtChungRepo.InsertNhapNG(new TraHangQTChungNhapNG
                {
                    PhieuXuLyBatThuongId = phieuXuLyId,
                    SlotIdOK = slotIdOK,
                    SlotIdNG = slotIdNG,
                    SlotIdNhap = slotIdNG,
                    LotNhapLai = lotChuan,
                    MaHang = phieu.MaSanPham,
                    SoLuongNG = soLuongNG,
                    NgayNhap = DateTime.Now,
                    NguoiNhap = nguoiNhap,
                    LyDo = "Nhập lại hàng NG sau rework"
                });

                _historyRepo.SaveHistory(
                    actionType: "REWORK_NG_IMPORT",
                    itemCode: phieu.MaSanPham,
                    lot: new LotInfo
                    {
                        LotNo = lotChuan,
                        Quantity = soLuongNG,
                        TemCode = StockExportReferenceFormatter.Format(
                            StockExportReferenceType.PhieuXuLyBatThuong,
                            phieuXuLyId)
                    },
                    fromSlotId: null,
                    toSlotId: slotIdNG.Value,
                    performedBy: nguoiNhap);

                _uow.Commit();
                return ScanResult.OK(
                    string.Format("Đã nhập {0} OK + {1} NG cho LOT [{2}] (NhapId={3}).",
                        soLuongOK, soLuongNG, lotChuan, nhapId));
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi nhập lại hàng NG: " + ex.Message);
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
                lotChuan = LotNoHelper.GetStockTpKey(lotNo);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail("LOT không hợp lệ: " + ex.Message);
            }

            _uow.Begin();
            try
            {
                var phieu = _phieuXuLyRepo.GetById(phieuXuLyId);
                if (phieu == null)
                    return FailAndRollback(
                        string.Format("Không tìm thấy phiếu xử lý bất thường Id={0}.", phieuXuLyId));

                int tonTruoc = _stockTpRepo.GetSlConLai(lotChuan);

                var movement = _stockMovement.ReturnFromRework(new StockMovementRequest
                {
                    MovementType = "REWORK_OK_RECEIVE",
                    TargetSlotId = slotIdOK,
                    LotNo = lotChuan,
                    ItemCode = phieu.MaSanPham,
                    Quantity = soLuongOK,
                    ReferenceType = "PHIEU_XU_LY_BAT_THUONG",
                    ReferenceId = phieuXuLyId.ToString(),
                    PerformedBy = nguoiNhap,
                    Reason = "Nhập lại hàng OK sau rework"
                });

                if (!movement.Success)
                    return FailAndRollback(movement.Message);

                _historyRepo.SaveHistory(
                    actionType: "NHAP_LAI_SAU_REWORK",
                    itemCode: phieu.MaSanPham,
                    lot: new LotInfo
                    {
                        LotNo = lotChuan,
                        Quantity = soLuongOK,
                        TemCode = StockExportReferenceFormatter.Format(
                            StockExportReferenceType.PhieuXuLyBatThuong,
                            phieuXuLyId)
                    },
                    fromSlotId: null,
                    toSlotId: slotIdOK,
                    performedBy: nguoiNhap);

                _uow.Commit();
                return ScanResult.OK(
                    string.Format("Đã nhập lại {0} hàng OK vào Slot {1} (LOT [{2}], tồn trước: {3}, tồn sau: {4}).",
                        soLuongOK, slotIdOK, lotChuan, tonTruoc, tonTruoc + soLuongOK));
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi nhập lại hàng OK: " + ex.Message);
            }
        }

        public ScanResult HoanTraKhoKhiHuy(
            int phieuXuLyId,
            string nguoiThucHien)
        {
            if (phieuXuLyId <= 0)
                return ScanResult.Fail("phieuXuLyId không hợp lệ.");
            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                return ScanResult.Fail("Chưa xác định người thực hiện.");

            _uow.Begin();
            try
            {
                var xuat = _qtChungRepo.GetXuat(phieuXuLyId);
                var tongXuat = xuat
                    .GroupBy(x => new { x.LotXuat, x.SlotIdNguon, x.MaHang })
                    .Select(g => new
                    {
                        LotNo = g.Key.LotXuat,
                        SlotId = g.Key.SlotIdNguon,
                        MaHang = g.Key.MaHang,
                        TongXuat = g.Sum(x => x.SoLuongXuat)
                    })
                    .ToList();

                if (tongXuat.Count == 0)
                {
                    _uow.Commit();
                    return ScanResult.OK("Không có gì để hoàn trả — phiếu chưa từng xuất kho.");
                }

                var nhapNG = _qtChungRepo.GetNhapNG(phieuXuLyId);
                var tongNhapNG = nhapNG
                    .GroupBy(x => x.LotNhapLai)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuongNG));

                var ketQua = new List<string>();

                foreach (var nhom in tongXuat)
                {
                    string lotChuan = LotNoHelper.GetStockTpKey(nhom.LotNo);
                    int daNhapNG = 0;
                    if (!string.IsNullOrWhiteSpace(nhom.LotNo))
                        tongNhapNG.TryGetValue(nhom.LotNo, out daNhapNG);

                    int conTreo = nhom.TongXuat - daNhapNG;
                    if (conTreo <= 0)
                        continue;

                    var movement = _stockMovement.ReturnFromRework(new StockMovementRequest
                    {
                        MovementType = "REWORK_CANCEL_RETURN",
                        TargetSlotId = nhom.SlotId,
                        LotNo = lotChuan,
                        ItemCode = nhom.MaHang,
                        Quantity = conTreo,
                        ReferenceType = "PHIEU_XU_LY_BAT_THUONG",
                        ReferenceId = phieuXuLyId.ToString(),
                        PerformedBy = nguoiThucHien,
                        Reason = "Hoàn trả kho do huỷ QT chung"
                    });

                    if (!movement.Success)
                        return FailAndRollback(movement.Message);

                    _historyRepo.SaveHistory(
                        actionType: "REWORK_CANCEL_RETURN",
                        itemCode: nhom.MaHang,
                        lot: new LotInfo
                        {
                            LotNo = lotChuan,
                            Quantity = conTreo,
                            TemCode = StockExportReferenceFormatter.Format(
                                StockExportReferenceType.PhieuXuLyBatThuong,
                                phieuXuLyId)
                        },
                        fromSlotId: null,
                        toSlotId: nhom.SlotId,
                        performedBy: nguoiThucHien);

                    ketQua.Add(
                        string.Format("LOT [{0}]: hoàn trả {1} về Slot {2}",
                            lotChuan, conTreo, nhom.SlotId));
                }

                _uow.Commit();

                if (ketQua.Count == 0)
                    return ScanResult.OK(
                        "Toàn bộ hàng xuất đã được xử lý — không còn số lượng nào cần hoàn trả.");

                return ScanResult.OK(
                    "Đã hoàn trả kho do huỷ QT chung:\n" + string.Join("\n", ketQua));
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi hoàn trả kho khi huỷ: " + ex.Message);
            }
        }

        private ScanResult FailAndRollback(string message)
        {
            SafeRollback();
            return ScanResult.Fail(message);
        }

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
