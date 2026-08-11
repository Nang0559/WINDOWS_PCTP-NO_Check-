using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.FunctionForm
{
    /// <summary>
    /// Toàn bộ nghiệp vụ trả hàng NG:
    ///   1a. TraHangSanXuat        — trả từ Slot đang lưu kho về SX (hàng chưa hề giao)
    ///   1b. LuuThungChoGiao/HuyChoGiaoVeSanXuat — hàng đã pick "chờ giao" nhưng phát hiện NG trước khi giao
    ///   2.  LuuThungQuetTra + XacNhanNhanHangKhachTraVeKho — khách trả hàng (quét theo thùng)
    /// </summary>
    public class TraHangService
    {
        private readonly SQLPROVIDER _sql;
        private readonly IStockTpRepository _stockTpRepo;
        private readonly ITraHangRepository _traHangRepo;
        private readonly StockService _stockService;

        private const string WH_KHACH_TRA = "KHACH_TRA_NG";
        private const string RACK_KHACH_TRA = "RACK_TRA_NG";

        public TraHangService(SQLPROVIDER sql, IStockTpRepository stockTpRepo,
            ITraHangRepository traHangRepo, StockService stockService)
        {
            _sql = sql;
            _stockTpRepo = stockTpRepo;
            _traHangRepo = traHangRepo;
            _stockService = stockService;
        }

        // ════════════════════════════════════════════════════════════════
        // 1a — Trả hàng đang lưu kho (Slot thật hoặc A0 ảo) về sản xuất
        // ════════════════════════════════════════════════════════════════
        public ScanResult TraHangSanXuat(int slotId, string lot, int soLuong, string lyDo)
        {
            if (string.IsNullOrWhiteSpace(lot) || soLuong <= 0)
                return ScanResult.Fail("Thiếu LOT hoặc số lượng không hợp lệ.");

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    // 1. Đọc Lot hiện tại của Slot TRONG transaction này
                    var allLots = _traHangRepo.GetSlotLotsInTransaction(conn, tran, slotId);
                    var targetLots = allLots.Where(x => LotCodeHelper.AreLotKeysEquivalent(x.LotNo, lot)).ToList();
                    int available = targetLots.Sum(x => x.Quantity);

                    if (soLuong > available)
                    {
                        tran.Rollback();
                        return ScanResult.Fail(
                            $"LOT [{lot}] trong Slot chỉ còn {available}, không đủ {soLuong} để trả.");
                    }

                    // 2. Tách đúng LOT cần trả, giữ nguyên các LOT khác trong Slot
                    var split = LotNoHelper.SubtractLots(targetLots, soLuong);
                    var remaining = allLots.Where(x => !LotCodeHelper.AreLotKeysEquivalent(x.LotNo, lot))
                         .Concat(split.RemainingLots)
                         .ToList();

                    // 3. Ghi lại SlotLot + cập nhật Slot tổng hợp — cùng transaction
                    _traHangRepo.SaveSlotLotsInTransaction(conn, tran, slotId, remaining);

                    // 4. STOCKTP + STOCKTPTRAHANG — cùng transaction
                    _traHangRepo.TruSlConLai(conn, tran, lot, soLuong);
                    _traHangRepo.InsertTraHang(conn, tran, lot, soLuong, lyDo, "TU_KHO");

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi trả hàng về sản xuất: " + ex.Message);
                }
            }

            // Lịch sử ghi sau khi transaction đã chắc chắn thành công (giống pattern NhapTpReceivingService)
            SlotHelper.SaveHistory("RETURN_TO_PRODUCTION",
                null, new LotInfo { LotNo = lot, Quantity = soLuong }, slotId, null);

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã trả {soLuong} SP của LOT [{lot}] về sản xuất để rework."
            };
        }

        // ════════════════════════════════════════════════════════════════
        // 1b — Huỷ 1 hoặc nhiều thùng đang "chờ giao", trả về SX
        // (gộp theo LOT_GOC — trả rework cả LOT nếu tồn không đủ)
        // ════════════════════════════════════════════════════════════════
        public ScanResult HuyChoGiaoVeSanXuat(List<int> choGiaoIds, string lyDo)
        {
            if (choGiaoIds == null || choGiaoIds.Count == 0)
                return ScanResult.Fail("Chưa chọn thùng nào để huỷ giao.");

            var items = _traHangRepo.GetChoGiaoTheoDanhSach(choGiaoIds)
                .Where(x => x.TrangThai == "CHO_GIAO")
                .ToList();

            if (items.Count == 0)
                return ScanResult.Fail("Các dòng đã chọn không còn ở trạng thái chờ giao.");

            // ── Guard MỚI: nếu LOT đã được CNK (STATUS='OK' bên TMPPHIEUGIAOHANG),
            // nghĩa là SLXUAT đã bị trừ theo giấy tờ — không cho huỷ nữa vì sẽ làm
            // STOCKTP sai (đã coi là xuất nhưng giờ lại đưa về SX).
            var lotDaCnk = _traHangRepo.LocLotDaCNK(items.Select(x => x.LotGoc).Distinct());
            if (lotDaCnk.Count > 0)
                return ScanResult.Fail(
                    $"LOT [{string.Join(", ", lotDaCnk)}] đã được Cập Nhật Kho (coi như đã giao) — " +
                    "không thể huỷ chờ giao. Nếu hàng thực sự có lỗi, dùng luồng \"Trả hàng NG sau giao\" (Luồng 2).");

            var itemsHopLe = items.Where(x => !lotDaCnk.Contains(x.LotGoc)).ToList();
            if (itemsHopLe.Count == 0)
                return ScanResult.Fail("Tất cả các dòng đã chọn đều đã được CNK, không thể huỷ.");

            var nhomTheoLot = itemsHopLe.GroupBy(x => x.LotGoc)
                .Select(g => new { LotGoc = g.Key, TongSl = g.Sum(x => x.SoLuong) })
                .ToList();

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    _traHangRepo.CapNhatTrangThaiChoGiao(conn, tran,
                        itemsHopLe.Select(x => x.Id), "HUY_TRA_SX");

                    foreach (var nhom in nhomTheoLot)
                    {
                        _traHangRepo.TruSlConLai(conn, tran, nhom.LotGoc, nhom.TongSl);
                        _traHangRepo.InsertTraHang(conn, tran, nhom.LotGoc, nhom.TongSl,
                            lyDo, "TU_CHO_GIAO");
                    }

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi huỷ chờ giao: " + ex.Message);
                }
            }

            foreach (var it in itemsHopLe)
                SlotHelper.SaveHistory("RETURN_TO_PRODUCTION_FROM_STAGING", it.MaHang,
                    new LotInfo { LotNo = it.LotGoc, Quantity = it.SoLuong, TemCode = it.LotThung },
                    it.SlotIdNguon, null);

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã huỷ {itemsHopLe.Count} thùng ({nhomTheoLot.Count} LOT), trả về sản xuất."
            };
        }

        // ════════════════════════════════════════════════════════════════
        // 2 — Khách trả hàng: quét từng thùng (gọi mỗi lần scan QR)
        // ════════════════════════════════════════════════════════════════
        public ScanResult LuuThungQuetTra(int idp, string qrThung, DataTable donHangDuKien)
        {
            // qrThung dạng: LOT_THUNG:MAHANG:...:SL_THUNG (theo format QR tem thùng hiện có)
            var parts = qrThung.Trim().Split(':');
            if (parts.Length < 4)
                return ScanResult.Fail("QR không đúng định dạng tem thùng.");

            string lotThung = parts[0].Trim();
            string maHang = parts[1].Trim();
            if (!int.TryParse(parts[3].Trim(), out int slThung))
                return ScanResult.Fail("Số lượng trên tem không hợp lệ.");

            string lotGoc = LotNoHelper.GetStockTpKey(lotThung);

            // b. Mã hàng phải khớp 1 dòng trong phiếu dự kiến
            bool coTrongPhieu = donHangDuKien.Rows.Cast<DataRow>()
                .Any(r => string.Equals(r["MAHANG"]?.ToString().Trim(), maHang,
                          StringComparison.OrdinalIgnoreCase));
            if (!coTrongPhieu)
                return ScanResult.Fail($"Thùng [{maHang}] không thuộc phiếu nhận đang mở.");

            // c. Chống quét trùng
            if (_traHangRepo.ExistsThungDaQuet(idp, lotThung))
                return ScanResult.Fail("Thùng này đã được quét trước đó.");

            _traHangRepo.InsertThungQuetTra(idp, lotThung, lotGoc, maHang, slThung);

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã quét thùng {lotThung} — LOT gốc: {lotGoc}, SL: {slThung}"
            };
        }

        // ════════════════════════════════════════════════════════════════
        // 2 — Xác nhận nhận hàng: gộp theo LOT_GOC, nhập vào Slot ảo,
        //     cộng lại tồn kho, trừ SLXUAT.
        // ════════════════════════════════════════════════════════════════
        public ScanResult XacNhanNhanHangKhachTraVeKho(int idp)
        {
            var nhomLot = _traHangRepo.GetNhomLotChuaXuLy(idp);
            if (nhomLot.Count == 0)
                return ScanResult.Fail("Chưa có thùng nào được quét cho phiếu này.");

            string slotAoText = _stockService.GetOrCreateVirtualSlotText(WH_KHACH_TRA, RACK_KHACH_TRA);

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    foreach (var nhom in nhomLot)
                    {
                        int slXuatHienTai = _traHangRepo.GetSlXuatHienTai(nhom.LotGoc); // SELECT ISNULL(SLXUAT,0) FROM STOCKTP
                        if (nhom.TongSl > slXuatHienTai)
                            return ScanResult.Fail(
                                $"LOT [{nhom.LotGoc}]: SL trả ({nhom.TongSl}) vượt quá SL đã xuất ({slXuatHienTai}).");
                        _traHangRepo.NhapLaiHangKhachTra(conn, tran, nhom.LotGoc, nhom.TongSl);
                        _traHangRepo.InsertNhanTraTheoIDP(conn, tran, nhom.LotGoc, nhom.TongSl, idp);
                    }

                    _traHangRepo.DanhDauDaXuLy(conn, tran, idp);
                    _traHangRepo.DanhDauPhieuDaNhapKho(conn, tran, idp);

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi xác nhận nhận hàng: " + ex.Message);
                }
            }

            // Nhập Slot ảo — làm SAU khi transaction STOCKTP đã chắc chắn thành công
            foreach (var nhom in nhomLot)
            {
                var r = _stockService.ImportLotDirectly(slotAoText, nhom.LotGoc, nhom.MaHang, nhom.TongSl);
                if (!r.IsOK)
                    System.Diagnostics.Debug.WriteLine(
                        $"[XacNhanNhanHangKhachTraVeKho] Lỗi nhập Slot ảo LOT={nhom.LotGoc}: {r.Message}");
            }

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã nhận {nhomLot.Count} LOT khách trả, tổng SL: {nhomLot.Sum(x => x.TongSl)}."
            };
        }
        /// <summary>
        /// Xác nhận các dòng "chờ giao" đã thực sự rời kho (xe đã lấy hàng).
        /// Đây là nơi DUY NHẤT trừ vĩnh viễn STOCKTP.SLXUAT — trước đó Slot/SlotLot
        /// đã bị trừ khi pick (ExportFormSV), nhưng STOCKTP tổng vẫn coi là "còn trong kho"
        /// cho tới bước này.
        /// </summary>
        /// <summary>
        /// CHỈ dùng cho hàng "chờ giao" KHÔNG đi qua luồng HVN_PGH (không có LOT nào
        /// được ghép vào TMPPHIEUGIAOHANG để CNK) — ví dụ: chuyển kho nội bộ, xuất
        /// cho mục đích khác không phải giao khách theo phiếu.
        ///
        /// NẾU LOT này SẼ được CNK qua HVN_PGH, KHÔNG gọi hàm này — để CapNhapKho tự
        /// trừ SLXUAT và tự đóng TMPCHOGIAO (xem PhieuRepository.CapNhapKho).
        /// Gọi cả 2 cho cùng 1 LOT sẽ trừ SLXUAT 2 lần.
        /// </summary>
        // TraHangService.cs
        public ScanResult XacNhanDaGiao(List<int> choGiaoIds)
        {
            if (choGiaoIds == null || choGiaoIds.Count == 0)
                return ScanResult.Fail("Chưa chọn thùng nào để xác nhận giao.");

            var items = _traHangRepo.GetChoGiaoTheoDanhSach(choGiaoIds)
                .Where(x => x.TrangThai == "CHO_GIAO")
                .ToList();

            if (items.Count == 0)
                return ScanResult.Fail("Các dòng đã chọn không còn ở trạng thái chờ giao.");

            var lotTrungHVN = _traHangRepo.LocLotDangChoCNK(items.Select(x => x.LotGoc).Distinct());
            if (lotTrungHVN.Count > 0)
                return ScanResult.Fail(
                    $"LOT [{string.Join(", ", lotTrungHVN)}] đang chờ Cập Nhật Kho bên phiếu giao HVN — " +
                    "vui lòng xác nhận qua nút CNK bên đó, không xác nhận thủ công ở đây để tránh trừ trùng.");

            var nhomTheoLot = items.GroupBy(x => x.LotGoc)
                .Select(g => new { LotGoc = g.Key, TongSl = g.Sum(x => x.SoLuong) })
                .ToList();

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    _traHangRepo.CapNhatTrangThaiChoGiao(conn, tran, items.Select(x => x.Id), "DA_GIAO");

                    foreach (var nhom in nhomTheoLot)
                        _stockTpRepo.XuatKhoThat(nhom.LotGoc, nhom.TongSl);

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi xác nhận giao hàng: " + ex.Message);
                }
            }

            // ← THÊM: ghi lịch sử EXPORT sau khi transaction chắc chắn thành công.
            // Đây là mốc "hàng thực sự rời kho" — khớp với việc STOCKTP.SLXUAT vừa
            // bị trừ vĩnh viễn ở trên. Ghi theo TỪNG THÙNG (item), giữ SlotIdNguon
            // để biết hàng xuất phát từ Slot nào, khác với ghi theo LOT gộp.
            foreach (var it in items)
                SlotHelper.SaveHistory("EXPORT", it.MaHang,
                    new LotInfo { LotNo = it.LotGoc, Quantity = it.SoLuong, TemCode = it.LotThung },
                    it.SlotIdNguon, null, performedBy: "SYSTEM_XAC_NHAN_GIAO");

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã xác nhận giao {items.Count} thùng ({nhomTheoLot.Count} LOT) — xuất nội bộ, không qua phiếu HVN."
            };
        }
    }
}
