using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Một vị trí (Slot) đang chứa MaHang + LOT: tồn kho thực, phần đang bị các phiếu chưa hoàn tất giữ,
    /// và phần còn khả dụng để tạo phiếu mới.
    /// </summary>
    public sealed class LotKhaDungInfo
    {
        public int SlotLotId { get; set; }
        public int SlotId { get; set; }
        public string ViTri { get; set; }
        public string MaHang { get; set; }
        public string LotNo { get; set; }
        public int TonKho { get; set; }
        public int DaGiu { get; set; }
        public string PhieuDangGiu { get; set; }

        public int KhaDung
        {
            get { return Math.Max(0, TonKho - DaGiu); }
        }
    }

    public interface ILotClaimGuard
    {
        /// <summary>Rà toàn bộ Slot đang chứa MaHang + LOT (lotNo rỗng = mọi LOT của mã) kèm số lượng đang bị giữ.</summary>
        List<LotKhaDungInfo> TraCuuLotTrongKho(string maHang, string lotNo, int? boQuaPhieuTraHangId);

        /// <summary>Ném InvalidOperationException nếu có dòng vượt tồn khả dụng của Slot. Gọi trong transaction đã khóa LOT.</summary>
        void KiemTraTonKhaDung(IEnumerable<PhieuTraHangCT> items, int? boQuaPhieuTraHangId);
    }

    public sealed class LotClaimGuard : ILotClaimGuard
    {
        private readonly IPhieuTraHangRepository _repo;
        private readonly ISlotService _slot;
        private readonly ITraHangQTChungRepository _qtRepo;

        public LotClaimGuard(
            IPhieuTraHangRepository repo,
            ISlotService slot,
            ITraHangQTChungRepository qtRepo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _slot = slot ?? throw new ArgumentNullException(nameof(slot));
            _qtRepo = qtRepo;
        }

        public List<LotKhaDungInfo> TraCuuLotTrongKho(string maHang, string lotNo, int? boQuaPhieuTraHangId)
        {
            string ma = Chuan(maHang);
            if (ma.Length == 0)
                throw new ArgumentException("MaHang không được rỗng.", nameof(maHang));

            string lot = Chuan(lotNo);

            var rows = (_slot.GetAllActiveSlotLots() ?? new List<SlotLotViewInfo>())
                .Where(x => x != null
                    && x.SlotId > 0
                    && x.Quantity > 0
                    && string.Equals(Chuan(x.ItemCode), ma, StringComparison.OrdinalIgnoreCase)
                    && (lot.Length == 0 || LotKhop(x.LotNo, lot)))
                .ToList();

            // Một (Slot, LOT) có thể gồm nhiều dòng SlotLot (nhiều tem) → gộp thành MỘT vị trí.
            var result = new List<LotKhaDungInfo>();
            foreach (var g in rows.GroupBy(x => new { x.SlotId, Lot = Chuan(x.LotNo).ToUpperInvariant() }))
            {
                var first = g.OrderBy(x => x.SlotLotId).First();
                result.Add(new LotKhaDungInfo
                {
                    SlotLotId = first.SlotLotId,
                    SlotId = first.SlotId,
                    ViTri = ViTri(first),
                    MaHang = Chuan(first.ItemCode),
                    LotNo = Chuan(first.LotNo),
                    TonKho = g.Sum(x => x.Quantity),
                    DaGiu = 0,
                    PhieuDangGiu = string.Empty
                });
            }

            // Phần đang bị phiếu NỘI BỘ chưa hoàn tất giữ (chưa xuất khỏi Slot).
            foreach (var lotKey in result.Select(x => x.LotNo).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            {
                var claims = _repo.TimLotDangXuLy(ma, lotKey) ?? new List<LotDangXuLyInfo>();
                foreach (var c in claims)
                {
                    if (c.Nguon != NguonXuLyBatThuong.TraNoiBo || !c.SlotIdNguon.HasValue)
                        continue;
                    if (boQuaPhieuTraHangId.HasValue && c.PhieuTraHangId == boQuaPhieuTraHangId.Value)
                        continue;

                    int conGiu = ConGiu(c);
                    if (conGiu <= 0)
                        continue;

                    var target = result.FirstOrDefault(x =>
                        x.SlotId == c.SlotIdNguon.Value
                        && string.Equals(x.LotNo, lotKey, StringComparison.OrdinalIgnoreCase));
                    if (target == null)
                        continue;

                    target.DaGiu += conGiu;
                    string soPhieu = string.IsNullOrWhiteSpace(c.SoPhieu) ? "Id=" + c.PhieuTraHangId : c.SoPhieu.Trim();
                    if (target.PhieuDangGiu.IndexOf(soPhieu, StringComparison.OrdinalIgnoreCase) < 0)
                        target.PhieuDangGiu = target.PhieuDangGiu.Length == 0 ? soPhieu : target.PhieuDangGiu + ", " + soPhieu;
                }
            }

            return result
                .OrderBy(x => x.ViTri, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.LotNo, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void KiemTraTonKhaDung(IEnumerable<PhieuTraHangCT> items, int? boQuaPhieuTraHangId)
        {
            if (items == null)
                return;

            var loi = new List<string>();

            var nhom = items
                .Where(x => x != null)
                .GroupBy(x => new
                {
                    Ma = Chuan(x.MaHang).ToUpperInvariant(),
                    Lot = Chuan(x.LotNo).ToUpperInvariant()
                });

            foreach (var g in nhom)
            {
                var dong = g.First();
                string ma = Chuan(dong.MaHang);
                string lot = Chuan(dong.LotNo);

                var khaDung = TraCuuLotTrongKho(ma, lot, boQuaPhieuTraHangId)
                    .Where(x => string.Equals(x.LotNo, lot, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var line in g)
                {
                    if (!line.SlotIdNguon.HasValue || line.SlotIdNguon.Value <= 0)
                    {
                        loi.Add("- " + ma + " / LOT " + lot + ": dòng chưa có Slot nguồn.");
                        continue;
                    }

                    var row = khaDung.FirstOrDefault(x => x.SlotId == line.SlotIdNguon.Value);
                    if (row == null)
                    {
                        loi.Add("- " + ma + " / LOT " + lot + ": Slot nguồn không còn chứa LOT này.");
                        continue;
                    }

                    if (row.KhaDung <= 0)
                        loi.Add("- " + ma + " / LOT " + lot + " (" + row.ViTri + "): tồn "
                            + row.TonKho.ToString("n0") + " đã được phiếu " + row.PhieuDangGiu
                            + " lấy hết, không thể tạo phiếu tiếp.");
                    else if (line.SoLuong > row.KhaDung)
                        loi.Add("- " + ma + " / LOT " + lot + " (" + row.ViTri + "): SL "
                            + line.SoLuong.ToString("n0") + " vượt số còn khả dụng " + row.KhaDung.ToString("n0")
                            + " (tồn " + row.TonKho.ToString("n0") + ", phiếu " + row.PhieuDangGiu
                            + " đang giữ " + row.DaGiu.ToString("n0") + ").");
                }
            }

            if (loi.Count > 0)
                throw new InvalidOperationException(
                    "Số lượng vượt tồn khả dụng trong kho:\n" + string.Join("\n", loi));
        }

        // Phần của dòng phiếu chưa xuất khỏi Slot = SL dòng − tổng SL đã xuất của phiếu xử lý gắn với dòng.
        private int ConGiu(LotDangXuLyInfo c)
        {
            int xuat = 0;

            if (c.PhieuXuLyId.HasValue && _qtRepo != null)
            {
                foreach (var x in _qtRepo.GetXuat(c.PhieuXuLyId.Value) ?? new List<TraHangQTChungXuat>())
                    if (x != null)
                        xuat += Math.Max(0, x.SoLuongXuat);
            }

            return Math.Max(0, c.SoLuong - xuat);
        }

        private static bool LotKhop(string lotTrongKho, string lotTim)
        {
            if (QrItemParser.LotKhop(lotTrongKho, lotTim))
                return true;

            try
            {
                return PCTP.Common.LotCodeHelper.AreLotKeysEquivalent(lotTrongKho, lotTim);
            }
            catch
            {
                return false;
            }
        }

        private static string ViTri(SlotLotViewInfo x)
        {
            return Chuan(x.WarehouseName) + " / " + Chuan(x.RackName) + " / Slot " + x.SlotNumber;
        }

        private static string Chuan(string s)
        {
            return (s ?? string.Empty).Trim();
        }
    }
}
