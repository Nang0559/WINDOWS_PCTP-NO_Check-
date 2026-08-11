using PCTP.ClassSQL;
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

        /// <summary>
        /// Gọi ngay sau khi user quét 1 tem FCC trong dialog — resolve xem LOT này có thực sự
        /// đang nằm trong kho (đã qua nhập kho rework bình thường) hay không, và nằm ở Slot nào.
        /// Nếu LOT nằm rải nhiều Slot, ưu tiên Slot có đủ số lượng lớn nhất; nếu không Slot nào
        /// đủ riêng lẻ, trả lỗi — KHÔNG tự gộp nhiều Slot cho 1 tem (tem vật lý chỉ nằm ở 1 chỗ).
        /// </summary>
        public ScanResult ResolveTemFcc(string maHangPhieuGoc, TemFccQuetInfo tem)
        {
            if (!string.Equals(tem.MaHangFcc, maHangPhieuGoc, StringComparison.OrdinalIgnoreCase))
                return ScanResult.Fail($"Mã hàng trên tem [{tem.MaHangFcc}] không khớp phiếu gốc [{maHangPhieuGoc}].");

            var stock = _repo.TraCuuLotDaNhapKho(tem.LotFcc);
            if (stock == null)
                return ScanResult.Fail(
                    $"LOT [{tem.LotFcc}] chưa được nhập kho. Vui lòng nhập kho hàng rework qua màn hình " +
                    "Nhập kho (FormEnterItemSV) trước khi giao bù.");

            int slConLai = stock.SlConLai ?? 0;
            if (slConLai < tem.SlTemFcc)
                return ScanResult.Fail(
                    $"LOT [{tem.LotFcc}] chỉ còn tồn {slConLai}, không đủ {tem.SlTemFcc} để giao bù.");

            var slots = _repo.GetSlotsChuaLot(tem.LotFcc);
            var slotDu = slots.FirstOrDefault(s => s.Quantity >= tem.SlTemFcc);
            if (slotDu == null)
                return ScanResult.Fail(
                    $"LOT [{tem.LotFcc}] đang rải rác ở {slots.Count} Slot, không có Slot nào đủ " +
                    $"{tem.SlTemFcc} để xuất trực tiếp. Cần gộp Slot thủ công trước khi giao bù.");

            tem.SlotIdNguon = slotDu.SlotId;
            tem.SlConLaiTaiSlot = slotDu.Quantity;
            return new ScanResult { IsOK = true };
        }

        /// <summary>
        /// Xác nhận giao bù — transaction gồm: xuất từng tem khỏi đúng Slot của nó (SlotLot),
        /// trừ SLCONLAI/cộng SLXUAT ở STOCKTP (KHÔNG đụng SLSX), ghi LUUPHIEUGIAOHANG + LUUDOCQRCODE.
        /// </summary>
        public ScanResult XacNhanGiaoBu(PhieuGiaoGocInfo phieuGoc, List<TemFccQuetInfo> temDaQuet, string nguoiThucHien)
        {
            if (phieuGoc == null)
                return ScanResult.Fail("Chưa chọn phiếu giao gốc cần bù.");
            if (temDaQuet == null || temDaQuet.Count == 0)
                return ScanResult.Fail("Chưa quét tem FCC nào.");
            if (temDaQuet.Any(t => t.SlotIdNguon <= 0))
                return ScanResult.Fail("Có tem chưa được xác định Slot nguồn — không thể giao bù.");

            int tongSl = temDaQuet.Sum(t => t.SlTemFcc);
            string lotFccGop = string.Join(",", temDaQuet.Select(t => $"{t.LotFcc}-{t.SlTemFcc}"));

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    foreach (var tem in temDaQuet)
                    {
                        // ── Xuất khỏi SlotLot (giảm/xoá đúng LOT trong đúng Slot) ──────────
                        var allLots = _slotRepo.GetSlotLotsInTransaction(conn, tran, tem.SlotIdNguon);
                        var targetLots = allLots.Where(x => x.LotNo == tem.LotFcc).ToList();
                        int available = targetLots.Sum(x => x.Quantity);

                        if (tem.SlTemFcc > available)
                            throw new InvalidOperationException(
                                $"LOT [{tem.LotFcc}] tại Slot chỉ còn {available}, không đủ {tem.SlTemFcc}.");

                        var split = LotNoHelper.SubtractLots(targetLots, tem.SlTemFcc);
                        var remaining = allLots.Where(x => x.LotNo != tem.LotFcc)
                                                .Concat(split.RemainingLots)
                                                .ToList();

                        _slotRepo.SaveSlotLotsInTransaction(conn, tran, tem.SlotIdNguon, remaining);

                        // ── STOCKTP: chỉ xuất — KHÔNG đụng SLSX ─────────────────────────
                        _repo.XuatKhoGiaoBu(conn, tran, tem.LotFcc, tem.SlTemFcc);
                    }

                    _repo.InsertLuuPhieuGiaoBu(conn, tran, phieuGoc, lotFccGop, tongSl, nguoiThucHien);

                    foreach (var tem in temDaQuet)
                        _repo.InsertLuuDocQRCodeGiaoBu(conn, tran,
                            tem.LotFcc, tem.MaHangFcc, tem.SlTemFcc, phieuGoc.NhaMay, phieuGoc.DinhDanhKey);

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return ScanResult.Fail("Lỗi giao bù NG: " + ex.Message);
                }
            }

            // ── Ghi lịch sử Slot sau khi transaction chắc chắn thành công (giống pattern NhapTpReceivingService) ──
            foreach (var tem in temDaQuet)
                SlotHelper.SaveHistory("GIAO_BU_NG", tem.MaHangFcc,
                    new LotInfo { LotNo = tem.LotFcc, Quantity = tem.SlTemFcc }, tem.SlotIdNguon, null);

            return new ScanResult
            {
                IsOK = true,
                Message = $"Đã giao bù {temDaQuet.Count} tem ({tongSl} SP) cho phiếu gốc [{phieuGoc.DinhDanhKey}]."
            };
        }
    }
}
