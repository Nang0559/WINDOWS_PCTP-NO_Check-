using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.FunctionForm
{
    public class GiaoBuNGService
    {
        private readonly SQLPROVIDER _sql;
        private readonly IGiaoBuNGRepository _repo;
        private readonly ITraHangRepository _slotRepo; // chỉ dùng GetSlotLotsInTransaction/SaveSlotLotsInTransaction

        public GiaoBuNGService(SQLPROVIDER sql, IGiaoBuNGRepository repo, ITraHangRepository slotRepo)
        {
            _sql = sql;
            _repo = repo;
            _slotRepo = slotRepo;
        }

        public List<PhieuGiaoGocInfo> TimPhieuGocTheoLot(string lot) => _repo.TimPhieuGocTheoLot(lot);
        public List<PhieuGiaoGocInfo> TimPhieuGocTheoMaHangNgay(string maHang, DateTime tu, DateTime den)
            => _repo.TimPhieuGocTheoMaHangNgay(maHang, tu, den);

        // ════════════════════════════════════════════════════════════════
        // ResolveTemFcc — gọi NGAY khi quét (FormQuetQRGiaoBuNG.TxtQr_KeyDown)
        // Kiểm tra: mã hàng khớp phiếu gốc → LOT đã nhập kho (STOCKTP) → đủ
        // tồn (SLCONLAI) → tìm được Slot vật lý đang chứa LOT này để phân bổ.
        // Đối xử THỐNG NHẤT cho cả tem thùng lẫn tem tổng — khác biệt duy nhất
        // là tem tổng còn phải chống trùng SoPhieu qua ExistsGiaoBuTem.
        // ════════════════════════════════════════════════════════════════
        public ScanResult ResolveTemFcc(string maHangPhieuGoc, TemFccQuetInfo tem)
        {
            if (tem == null)
                return ScanResult.Fail("Không có dữ liệu tem.");

            // ── 1. Mã hàng phải khớp phiếu gốc ──────────────────────────
            if (!string.Equals(tem.MaHangFcc, maHangPhieuGoc, StringComparison.OrdinalIgnoreCase))
                return ScanResult.Fail(
                    $"Sai mã hàng!\nTem quét: {tem.MaHangFcc}\nPhiếu gốc cần bù: {maHangPhieuGoc}");

            // ── 2. Tem tổng: chống quét lại tem đã dùng giao bù trước đó ─
            if (tem.IsTongPhieu && _repo.ExistsGiaoBuTem(tem.LotFcc, tem.SoPhieu))
                return ScanResult.Fail(
                    $"Tem tổng [Số phiếu: {tem.SoPhieu}] đã được dùng để giao bù trước đó!");

            // ── 3. LOT phải đã nhập kho thật (STOCKTP) ───────────────────
            var stockItem = _repo.TraCuuLotDaNhapKho(tem.LotFcc);
            if (stockItem == null)
                return ScanResult.Fail(
                    $"LOT [{tem.LotFcc}] chưa từng nhập kho (không có trong STOCKTP).\n" +
                    "Kiểm tra lại: hàng rework đã được nhập lại kho (Nhập TP) chưa?");

            int slConLai = stockItem.SlConLai ?? 0;
            if (slConLai < tem.SlTemFcc)
                return ScanResult.Fail(
                    $"LOT [{tem.LotFcc}] chỉ còn tồn {slConLai}, không đủ {tem.SlTemFcc} để giao bù.");

            // ── 4. Tìm Slot vật lý đang giữ LOT này — phân bổ FIFO theo
            //      ImportDate, giống cách BulkStockAdjustService đang làm ──
            var slots = _repo.GetSlotsChuaLot(tem.LotFcc)
                .OrderBy(s => s.ImportDate ?? DateTime.MaxValue)
                .ToList();

            if (slots.Count == 0)
                return ScanResult.Fail(
                    $"LOT [{tem.LotFcc}] có tồn trong STOCKTP ({slConLai}) nhưng không tìm thấy " +
                    "trong bất kỳ Slot nào (SlotLot) — dữ liệu kho vật lý bị lệch với STOCKTP. " +
                    "Cần đối soát trước khi giao bù.");

            var phanBo = new List<SlotAllocation>();
            int conLaiCanPhanBo = tem.SlTemFcc;

            foreach (var s in slots)
            {
                if (conLaiCanPhanBo <= 0) break;
                if (s.Quantity <= 0) continue;

                int lay = Math.Min(conLaiCanPhanBo, s.Quantity);
                phanBo.Add(new SlotAllocation
                {
                    SlotId = s.SlotId,
                    WarehouseName = s.WarehouseName,
                    RackName = s.RackName,
                    SlotNumber = s.SlotNumber,
                    SoLuong = lay
                });
                conLaiCanPhanBo -= lay;
            }

            if (conLaiCanPhanBo > 0)
                return ScanResult.Fail(
                    $"LOT [{tem.LotFcc}]: các Slot đang chứa chỉ cộng lại được " +
                    $"{tem.SlTemFcc - conLaiCanPhanBo}/{tem.SlTemFcc} — thiếu {conLaiCanPhanBo}. " +
                    "Tồn kho vật lý (SlotLot) không đủ so với STOCKTP.SLCONLAI, cần đối soát.");

            tem.PhanBoSlot = phanBo;
            tem.DaResolve = true;

            return new ScanResult { IsOK = true };
        }

        // ════════════════════════════════════════════════════════════════
        // XacNhanGiaoBu — 1 transaction duy nhất: trừ SlotLot theo từng
        // phân bổ → trừ STOCKTP (SLXUAT/SLCONLAI) → lưu LUUDOCQRCODE từng
        // tem → lưu 1 dòng LUUPHIEUGIAOHANG gộp tất cả tem.
        // ════════════════════════════════════════════════════════════════
        public ScanResult XacNhanGiaoBu(PhieuGiaoGocInfo phieuGoc,
            List<TemFccQuetInfo> temDaQuet, string nguoiThucHien)
        {
            if (phieuGoc == null)
                return ScanResult.Fail("Chưa chọn phiếu giao gốc.");

            if (temDaQuet == null || temDaQuet.Count == 0)
                return ScanResult.Fail("Chưa quét tem FCC nào.");

            // Re-resolve phòng trường hợp UI đưa vào tem chưa qua ResolveTemFcc
            foreach (var tem in temDaQuet)
            {
                if (tem.DaResolve) continue;
                var rr = ResolveTemFcc(phieuGoc.MaHang, tem);
                if (!rr.IsOK) return rr;
            }

            int tongSl = temDaQuet.Sum(t => t.SlTemFcc);
            if (tongSl < phieuGoc.SoLuong)
                return ScanResult.Fail(
                    $"Tổng SL đã quét ({tongSl}) chưa đủ SL cần bù ({phieuGoc.SoLuong}).");

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    foreach (var tem in temDaQuet)
                    {
                        // ── 1. Trừ SlotLot cho từng phần đã phân bổ ─────────
                        foreach (var alloc in tem.PhanBoSlot)
                            TruSlotLotTrongTransaction(conn, tran, alloc.SlotId, tem.LotFcc, alloc.SoLuong);

                        // ── 2. Trừ STOCKTP (chỉ SLXUAT/SLCONLAI — KHÔNG đụng SLSX) ─
                        _repo.XuatKhoGiaoBu(conn, tran, tem.LotFcc, tem.SlTemFcc);

                        // ── 3. Lưu lịch sử quét (LUUDOCQRCODE) ──────────────
                        _repo.InsertLuuDocQRCodeGiaoBu(conn, tran,
                            tem.LotFcc, tem.MaHangFcc, tem.SlTemFcc,
                            phieuGoc.NhaMay, phieuGoc.DinhDanhKey);
                    }

                    // ── 4. Gộp LOT-SL thành 1 chuỗi, lưu 1 dòng LUUPHIEUGIAOHANG ─
                    string lotFccGop = string.Join(",",
                        temDaQuet.Select(t => $"{t.LotFcc}-{t.SlTemFcc}"));

                    _repo.InsertLuuPhieuGiaoBu(conn, tran, phieuGoc, lotFccGop, tongSl, nguoiThucHien);

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi khi lưu giao bù: " + ex.Message);
                }
            }

            // Báo Canvas kho (nếu đang mở) vẽ lại — dùng lại notifier có sẵn
            StockChangedNotifier.RaiseStockChanged();

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã giao bù {temDaQuet.Count} tem, tổng SL {tongSl} " +
                          $"cho phiếu gốc LOT [{phieuGoc.Lot}] (Mã hàng {phieuGoc.MaHang})."
            };
        }

        // ── Helper: trừ đúng 1 dòng SlotLot (theo LotNo) trong Slot, giữ
        //    nguyên các LOT khác — cùng pattern với BulkStockAdjustService ──
        private void TruSlotLotTrongTransaction(SqlConnection conn, SqlTransaction tran,
            int slotId, string lotNo, int soLuongTru)
        {
            var lots = _slotRepo.GetSlotLotsInTransaction(conn, tran, slotId);

            string keyTarget = LotCodeHelper.TrimTo(lotNo, LotCodeHelper.LEN_HEAD_FIXED);
            var target = lots.FirstOrDefault(l =>
                LotCodeHelper.TrimTo(l.LotNo, LotCodeHelper.LEN_HEAD_FIXED) == keyTarget
                && l.Quantity > 0);

            if (target == null)
                throw new InvalidOperationException(
                    $"Không tìm thấy LOT [{lotNo}] trong Slot #{slotId} (dữ liệu đã thay đổi " +
                    "giữa lúc quét và lúc xác nhận — vui lòng quét lại).");

            if (target.Quantity < soLuongTru)
                throw new InvalidOperationException(
                    $"LOT [{lotNo}] trong Slot #{slotId} chỉ còn {target.Quantity}, " +
                    $"không đủ {soLuongTru} (có thể đã bị người khác xuất trước).");

            target.Quantity -= soLuongTru;

            var remaining = lots.Where(l => l.Quantity > 0).ToList();
            _slotRepo.SaveSlotLotsInTransaction(conn, tran, slotId, remaining);
        }
    
    }
}
