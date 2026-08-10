using PCTP.ClassSQL;
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

            int slConLai = _stockTpRepo.GetSlConLai(lot);
            if (soLuong > slConLai)
                return ScanResult.Fail($"Số lượng trả ({soLuong}) vượt quá tồn kho hiện tại của LOT ({slConLai}).");

            try
            {
                // 1. Xuất khỏi Slot — dùng chung logic tách LOT có sẵn
                var splitResult = _stockService.ExportFromSlot(slotId, soLuong, null);

                // 2+3. STOCKTP + STOCKTPTRAHANG trong 1 transaction
                using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
                {
                    try
                    {
                        _traHangRepo.TruSlConLai(conn, tran, lot, soLuong);
                        _traHangRepo.InsertTraHang(conn, tran, lot, soLuong, lyDo, "TU_KHO");
                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }

                return new ScanResult
                {
                    IsOK = true,
                    Message = $"Đã trả {soLuong} SP của LOT [{lot}] về sản xuất để rework."
                };
            }
            catch (Exception ex)
            {
                return ScanResult.Fail("Lỗi trả hàng về sản xuất: " + ex.Message);
            }
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

            var nhomTheoLot = items
                .GroupBy(x => x.LotGoc)
                .Select(g => new { LotGoc = g.Key, TongSl = g.Sum(x => x.SoLuong) })
                .ToList();

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    // 1. Đổi trạng thái các thùng đã chọn
                    _traHangRepo.CapNhatTrangThaiChoGiao(conn, tran,
                        items.Select(x => x.Id), "HUY_TRA_SX");

                    // 2-4. Với từng LOT_GOC: trừ SLCONLAI + ghi STOCKTPTRAHANG
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

            foreach (var it in items)
                SlotHelper.SaveHistory("RETURN_TO_PRODUCTION_FROM_STAGING", it.MaHang,
                    new VIEWSTOCK.Models.LotInfo { LotNo = it.LotGoc, Quantity = it.SoLuong, TemCode = it.LotThung },
                    it.SlotIdNguon, null);

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã huỷ {items.Count} thùng ({nhomTheoLot.Count} LOT), trả về sản xuất."
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

            string lotGoc = LotNoHelper.NormalizeLot(lotThung);

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
        public ScanResult XacNhanDaGiao(List<int> choGiaoIds)
        {
            if (choGiaoIds == null || choGiaoIds.Count == 0)
                return ScanResult.Fail("Chưa chọn thùng nào để xác nhận giao.");

            var items = _traHangRepo.GetChoGiaoTheoDanhSach(choGiaoIds)
                .Where(x => x.TrangThai == "CHO_GIAO")
                .ToList();

            if (items.Count == 0)
                return ScanResult.Fail("Các dòng đã chọn không còn ở trạng thái chờ giao.");

            var nhomTheoLot = items.GroupBy(x => x.LotGoc)
                .Select(g => new { LotGoc = g.Key, TongSl = g.Sum(x => x.SoLuong) })
                .ToList();

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    _traHangRepo.CapNhatTrangThaiChoGiao(conn, tran, items.Select(x => x.Id), "DA_GIAO");

                    foreach (var nhom in nhomTheoLot)
                        _stockTpRepo.XuatKhoThat(nhom.LotGoc, nhom.TongSl);   // trừ SLCONLAI + cộng SLXUAT

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi xác nhận giao hàng: " + ex.Message);
                }
            }

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã xác nhận giao {items.Count} thùng ({nhomTheoLot.Count} LOT)."
            };
        }
    }
}
