using DevExpress.XtraRichEdit.Import.Html;
using PCTP.Common;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Logic nghiệp vụ đọc QR Code — tách hoàn toàn khỏi KeyPress handler.
    /// Test được độc lập, không cần form.
    /// </summary>
    public class DocQRService
    {
        private readonly IDocQRRepository _repo;
        private readonly IEventBus _bus;
        private readonly CustomerConfig _cfg;

        // ── Trạng thái chế độ bắn ────────────────────────────────────────────
        private bool _isBanSP = false;
        private bool _isBanOType = false;

        public bool IsBanSP => _isBanSP;
        public bool IsBanOType => _isBanOType;

        // ── Backward compat ──────────────────────────────────────────────────
        public void SetCheDoBanSP(bool isSP) => _isBanSP = isSP;

        // ── FIX: Set chế độ từ gioMoTa — Presenter gọi thay SetCheDoBanSP ───
        public void SetCheDoBan(string gioMoTa)
        {
            _isBanSP = PhieuService.IsLoaiSP(gioMoTa);
            _isBanOType = PhieuService.IsLoaiOType(gioMoTa);
            // MP = không phải SP và không phải O TYPE
        }

        // ── Helper lấy bảng đúng theo chế độ ────────────────────────────────
        private string DocQRTable
            => _isBanSP
                ? _cfg.GetDocQRTable(true)   // DOCQRCODE_SP
                : _cfg.GetDocQRTable(false); // DOCQRCODE (MP + O TYPE dùng chung)

        private string TmpTable
            => _isBanSP
                ? _cfg.GetTmpTable(true)
                : _cfg.GetTmpTable(false);

        public DocQRService(IDocQRRepository repo, IEventBus bus, CustomerConfig cfg)
        {
            _repo = repo;
            _bus = bus;
            _cfg = cfg;
        }

        // ════════════════════════════════════════════════════════════════════
        // Public helpers — dùng DocQRTable đúng theo chế độ
        // ════════════════════════════════════════════════════════════════════
        public int CountChuaDG() => _repo.CountChuaDG(DocQRTable);
        public bool CoDocQRNao() => _repo.Count(DocQRTable) > 0;
        public DataTable LoadAll() => _repo.GetAllAsTable(DocQRTable);
        public void XoaDong(int stt) => _repo.Delete(stt, DocQRTable);
        public void XoaToanBo() => _repo.DeleteAll(DocQRTable);
        public void CapNhapSlHvn(int stt, int slMoi)
                                                  => _repo.UpdateSlHvn(stt, slMoi, DocQRTable);

        // ════════════════════════════════════════════════════════════════════
        // ProcessScan — phân luồng theo loại
        // ════════════════════════════════════════════════════════════════════
        public ScanResult ProcessScan(string rawQr,
    Func<string, bool> kiemTraMaTrongPhieu,
    Func<string, int, bool> kiemTraSlDaBan)
        {
            rawQr = rawQr.Trim().ToUpper();
            string[] parts = rawQr.Split(':');

            if (parts.Length == 6 && !_cfg.CoNhieuNhaMay && !_cfg.CoGear)
                return ScanFCC_TongTem(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);

            if (parts.Length == 4 && _cfg.CoGear)
                return ScanFCC_YMVN(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);

            if (_cfg.CoGear)
                return ScanYMVN(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan);

            if (parts.Length == 4)
            {
                if (_isBanSP)
                    return ScanFCC_SP(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);
                return ScanFCC(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);
            }

            return ScanHVN(parts);
        }

        // ✅ Helper dùng trong các ScanXXX — map mã hàng nếu có trong ComparePart
        private string MapMaHang(string maHang)
        {
            // ✅ Tra bảng tbl_QR_ComparePart — nếu có thì trả mã đích
            string mapped = _repo.GetMaHangMapped(maHang);
            return string.IsNullOrEmpty(mapped) ? maHang : mapped;
        }

        // ════════════════════════════════════════════════════════════════════
        // SP: chỉ bắn FCC — không cần HVN
        // ════════════════════════════════════════════════════════════════════
        private ScanResult ScanFCC_SP(string[] parts,
            Func<string, bool> kiemTraMa,
            Func<string, int, bool> kiemTraSl)
        {
            string maFcc = parts[1].Trim();
            string maHangThuc = MapMaHang(maFcc);

            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");

            string lotFcc = NormalizeLotFCC(parts[0], maHangThuc);

            if (!kiemTraMa(maHangThuc))
                return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");

            if (!kiemTraSl(maHangThuc, slTem))
                return ScanResult.Fail(
                    "Tổng số lượng đã bắn vượt quá số lượng giao!\n" +
                    "Hãy kiểm tra lại phiếu.");

            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;

            var item = new DocQRCode
            {
                STT = sttBan,
                LotFCC = lotFcc,
                MaHangFCC = maHangThuc,
                MaFCC = maHangThuc,
                SlTemFCC = slTem,
                // SP: tự ghép HVN = FCC luôn, không đợi scan HVN
                LotHVN = lotFcc,
                MaHangHVN = maHangThuc,
                SlTemHVN = slTem,
                KetQua = "OK",
                Gio = ""
            };

            _repo.InsertFCC(item, DocQRTable);
            _repo.UpdateHVN(item, DocQRTable);

            _bus.Publish(new QRScannedEvent(item, "FCC_SP"));
            return ScanResult.OK(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // MP + O TYPE: bắn FCC (bước 1)
        // ════════════════════════════════════════════════════════════════════
        private ScanResult ScanFCC(string[] parts,
        Func<string, bool> kiemTraMa,
        Func<string, int, bool> kiemTraSl)
        {
            if (!KiemTraThuTuFCC()) return ScanResult.Fail("Sai Thứ tự bắn!");

            string maFcc = parts[1].Trim();
            string maHangThuc = MapMaHang(maFcc);
            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");

            string lotFcc = NormalizeLotFCC(parts[0], maHangThuc);

            if (!kiemTraMa(maHangThuc))
                return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");

            if (!kiemTraSl(maHangThuc, slTem))
                return ScanResult.Fail("Số lượng bắn đang vượt quá số lượng giao!");

            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;

            var item = new DocQRCode
            {
                STT = sttBan,
                LotFCC = lotFcc,
                MaHangFCC = maHangThuc,
                MaFCC = maHangThuc,
                SlTemFCC = slTem,
                Gio = ""
            };

            _repo.InsertFCC(item, DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "FCC"));
            return ScanResult.OK(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // MP + O TYPE: bắn HVN (bước 2)
        // ════════════════════════════════════════════════════════════════════

        public ScanResult ProcessScanYMVN(
    string rawQr,
    Func<string, bool> kiemTraMaTrongPhieu,
    Func<string, int, bool> kiemTraSlDaBan)
        {
            var parts = rawQr.Split(':');
            if (parts.Length < 6)
                return ScanResult.Fail("Mã QR không đúng định dạng YMVN!");

            string lotFcc = parts[0].Trim();
            string maHang = parts[1].Trim();
            string ngay = parts[2].Trim();
            string slStr = parts[3].Trim();
            string soTT = parts[4].Trim();
            string temCode = parts[5].Trim();
            string gear = parts.Length > 6 ? parts[6].Trim() : "";

            if (!int.TryParse(slStr, out int slTem))
                return ScanResult.Fail("Số lượng TEM không hợp lệ!");

            if (!kiemTraMaTrongPhieu(maHang))
                return ScanResult.Fail($"Mã hàng [{maHang}] không có trong phiếu!");

            // ── Build item dùng chung ────────────────────────────────────────────
            string gearName = parts.Length > 6
                 ? _repo.GetGearName(parts[6].Trim())
                 : "";

            string docQRTable = _cfg.GetDocQRTable(_isBanSP);

            // ── Kiểm tra trùng tem ───────────────────────────────────────────────
            bool trung = _repo.KiemTraTrungTemTong(lotFcc, soTT, docQRTable);
            if (trung)
                return ScanResult.Fail($"TEM [{temCode}] đã được quét!");

            // ── Kiểm tra SL ─────────────────────────────────────────────────────
            int maxStt = _repo.GetMaxStt(docQRTable);
            var item = new DocQRCode
            {
                STT = maxStt + 1,
                LotFCC = lotFcc,
                MaHangFCC = maHang,
                MaFCC = temCode,
                SlTemFCC = slTem,
                Gio = soTT,
                Gear = gearName,
                SoPhieu = soTT
            };

            if (!kiemTraSlDaBan(maHang, slTem))
                return ScanResult.SlKhongKhop(item);  // ← dùng factory method đúng

            // ── Insert ───────────────────────────────────────────────────────────
            _repo.InsertFCC(item, docQRTable, _cfg.CoGear);

            // ── Publish — truyền đúng tham số ────────────────────────────────────
            _bus.Publish(new QRScannedEvent(item, _cfg.CustomerNo));

            return ScanResult.OK(item);  // ← truyền item
        }

        // ── Helper build item khi SL khác biệt ──────────────────────────────────
        private DocQRCode BuildItemYMVN(string lotFcc, string maHang,
            int slTem, string temCode, string gear)
        {
            return new DocQRCode
            {
                LotFCC = lotFcc,
                MaHangFCC = maHang,
                SlTemFCC = slTem,
                MaFCC = temCode,
                Gear = gear
            };
        }
        private ScanResult ScanHVN(string[] parts)
        {
            if (!KiemTraThuTuHVN()) return ScanResult.Fail("Sai Thứ tự bắn!");
            if (parts.Length < 4) return ScanResult.Fail("Dữ liệu QR HVN không hợp lệ.");

            string lotHvn = parts[0];
            string maHvn = parts[1].Replace(" ", "");

            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");

            if (!KiemTraTrungTem(lotHvn))
                return ScanResult.Fail("Trùng Tem!");

            if (!_repo.KiemTraTemMa(maHvn))
                return ScanResult.Fail("Mã Hàng HVN không khớp với FCC!");

            int sttBan = _repo.GetMaxStt(DocQRTable);

            if (!_repo.KiemTraTemSoLuong(maHvn, slTem))
                return ScanResult.SlKhongKhop(new DocQRCode
                {
                    STT = sttBan,
                    LotHVN = lotHvn,
                    MaHangHVN = maHvn,
                    SlTemHVN = slTem
                });

            var item = new DocQRCode
            {
                STT = sttBan,
                LotHVN = lotHvn,
                MaHangHVN = maHvn,
                SlTemHVN = slTem,
                KetQua = "OK"
            };

            _repo.UpdateHVN(item, DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "HVN"));
            return ScanResult.OK(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // YMVN FCC
        // ════════════════════════════════════════════════════════════════════
        private ScanResult ScanFCC_YMVN(string[] parts,
            Func<string, bool> kiemTraMa,
            Func<string, int, bool> kiemTraSl)
        {
            string lotSl = parts[0];
            string maHang = parts[1];
            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");

            string lotFcc = NormalizeLotFCC_YMVN(lotSl, out string gear);

            if (!kiemTraMa(maHang))
                return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");

            if (!kiemTraSl(maHang, slTem))
                return ScanResult.Fail("Số lượng bắn vượt quá số lượng giao!");

            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;

            var item = new DocQRCode
            {
                STT = sttBan,
                LotFCC = lotFcc,
                MaHangFCC = maHang,
                MaFCC = maHang.Replace("-", ""),
                SlTemFCC = slTem,
                Gear = gear,
                Gio = ""
            };

            _repo.InsertFCC(item, DocQRTable, _cfg.CoGear);
            _bus.Publish(new QRScannedEvent(item, "FCC_YMVN"));
            return ScanResult.OK(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // YMVN HVN
        // ════════════════════════════════════════════════════════════════════
        private ScanResult ScanYMVN(string rawQr,
            Func<string, bool> kiemTraMa,
            Func<string, int, bool> kiemTraSl)
        {
            string partNo = "", oderNo = "";
            int slTem = 0, vtp = 0, vtor = 0;

            for (int i = 0; i < rawQr.Length; i++)
                if (rawQr[i] == 'P') { partNo = rawQr.Substring(i + 1, 14); vtp = i + 15; break; }
            for (int j = vtp; j < rawQr.Length; j++)
                if (rawQr[j] == 'K') { oderNo = rawQr.Substring(j + 1, 5); vtor = j + 6; break; }
            for (int j = vtor; j < rawQr.Length; j++)
                if (rawQr[j] == 'Q') { slTem = int.Parse(rawQr.Substring(j + 1, 6)); break; }

            if (!kiemTraMa(partNo))
                return ScanResult.Fail($"Mã hàng YMVN {partNo} không có trong phiếu!");

            if (!kiemTraSl(partNo, slTem))
                return ScanResult.Fail("Số lượng bắn vượt quá số lượng giao!");

            int sttBan = _repo.GetMaxStt(_cfg.DocQRTable);

            var item = new DocQRCode
            {
                STT = sttBan,
                LotHVN = oderNo,
                MaHangHVN = partNo,
                SlTemHVN = slTem,
                KetQua = "OK"
            };

            _repo.UpdateHVN(item, _cfg.DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "YMVN"));
            return ScanResult.OK(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // Tem tổng — 100003
        // ════════════════════════════════════════════════════════════════════
        private ScanResult ScanFCC_TongTem(string[] parts,
     Func<string, bool> kiemTraMa,
     Func<string, int, bool> kiemTraSl)
        {
            if (parts.Length < 6)
                return ScanResult.Fail("Mã vạch không đúng định dạng (cần 6 phần).");

            string lotRaw = parts[0];
            string maHang = parts[1].Replace(" ", "");
            string soPhieu = parts[4];

            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");

            // ── FIX: normalize LOT đúng theo customer ───────────────────────
            // HTN (100003): bỏ 7 ký tự cuối
            // Các customer khác: giữ nguyên (ScanFCC_TongTem chỉ dùng cho 100003
            //                    nhưng để an toàn vẫn check)
            string lotFcc = (_cfg.LoadTuBangRieng && !_cfg.CoGear)
                ? NormalizeLotFCC_HTN(lotRaw)
                : lotRaw;

            if (!kiemTraMa(maHang))
                return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");

            if (KiemTraTrungTemTong(lotFcc, soPhieu))
                return ScanResult.Fail(
                    $"Trùng phiếu!\nLot: [{lotFcc}]\nSố phiếu: [{soPhieu}]");

            if (!kiemTraSl(maHang, slTem))
                return ScanResult.Fail(
                    "Tổng số lượng đã bắn vượt quá số lượng giao!");

            int sttBan = _repo.GetMaxStt(_cfg.DocQRTable) + 1;

            var item = new DocQRCode
            {
                STT = sttBan,
                LotFCC = lotFcc,
                MaHangFCC = maHang,
                MaFCC = maHang,
                SlTemFCC = slTem,
                LotHVN = lotFcc,
                MaHangHVN = maHang,
                SlTemHVN = slTem,
                KetQua = "OK",
                Gio = ""
            };

            _repo.InsertFCC(item, _cfg.DocQRTable);
            _repo.UpdateHVN(item, _cfg.DocQRTable);

            _bus.Publish(new QRScannedEvent(item, "FCC_TONG"));
            return ScanResult.OK(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // Xác nhận SL khác biệt
        // ════════════════════════════════════════════════════════════════════
        public ScanResult ConfirmSlKhacBiet(DocQRCode pending)
        {
            var item = new DocQRCode
            {
                STT = pending.STT,
                LotHVN = pending.LotHVN,
                MaHangHVN = pending.MaHangHVN,
                SlTemHVN = pending.SlTemHVN,
                KetQua = "KHAC SLTEM"
            };
            _repo.UpdateHVN(item, DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "HVN"));
            return ScanResult.OK(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // KiemTraSlDaBan
        // ════════════════════════════════════════════════════════════════════
        public bool KiemTraSlDaBan(string maHang, int slBan)
        {
            int ttSlDaBan = _repo.GetTongSlDaBan(maHang, DocQRTable);
            int slGiao = _repo.GetSoLuongGiaoTheoMa(maHang, TmpTable);
            return ttSlDaBan + slBan <= slGiao;
        }

        // ════════════════════════════════════════════════════════════════════
        // Validation helpers
        // ════════════════════════════════════════════════════════════════════
        private bool KiemTraThuTuFCC()
        {
            // SP và O TYPE không cần thứ tự xen kẽ
            if (!_cfg.CoNhieuNhaMay || _isBanSP || _isBanOType) return true;
            var all = _repo.GetAll(DocQRTable);
            if (all.Count == 0) return true;
            foreach (var item in all)
                if (string.IsNullOrEmpty(item.LotHVN)) return false;
            return true;
        }

        private bool KiemTraThuTuHVN()
        {
            var all = _repo.GetAll(DocQRTable);
            if (all.Count == 0) return false;
            return !string.IsNullOrEmpty(all[all.Count - 1].LotFCC);
        }

        private bool KiemTraTrungTem(string lotHvn)
        {
            var all = _repo.GetAll(DocQRTable);
            foreach (var item in all)
                if (!string.IsNullOrEmpty(item.LotHVN) &&
                    item.LotHVN.Trim() == lotHvn.Trim())
                    return false;
            return true;
        }

        private bool KiemTraTrungTemTong(string lotFcc, string soPhieu)
            => _repo.KiemTraTrungTemTong(lotFcc, soPhieu, _cfg.DocQRTable);

        // ════════════════════════════════════════════════════════════════════
        // Normalize LOT helpers — giữ nguyên
        // ════════════════════════════════════════════════════════════════════
        private string NormalizeLotFCC(string lotSl, string maHang)
        {
            string idPadded = _repo.GetIdMaHangPadded(maHang);

            string[] ghep = lotSl.Split(',');
            if (ghep.Length > 1)
            {
                var resultParts = new List<string>();
                foreach (var g in ghep)
                {
                    string[] lotSlPart = g.Split('-');
                    string lot = lotSlPart[0];
                    string sl = lotSlPart.Length > 1 ? lotSlPart[1] : "0";

                    if (lot.Length < LotCodeHelper.LEN_HEAD_FIXED)
                    {
                        if (lot.Length == 12)
                        {
                            resultParts.Add(lot + "-" + sl);
                            continue;
                        }
                        // ✅ Fallback tra cứu tương thích ngược — KHÔNG phải chuẩn ghi mới
                        lot = LotCodeHelper.BuildLegacyShortLot(lot, idPadded);
                        resultParts.Add(lot + "-" + sl);
                    }
                    else
                    {
                        // ✅ Chuẩn ghi mới — luôn strip theo 20 ký tự head
                        resultParts.Add(LotCodeHelper.StripCounterAndQty(lot) + "-" + sl);
                    }
                }
                return string.Join(",", resultParts);
            }
            else
            {
                if (lotSl.Length < LotCodeHelper.LEN_HEAD_FIXED)
                {
                    // ✅ Fallback tra cứu tương thích ngược — KHÔNG phải chuẩn ghi mới
                    return LotCodeHelper.BuildLegacyShortLot(lotSl, idPadded);
                }

                // ✅ Chuẩn ghi mới — luôn strip theo 20 ký tự head
                return LotCodeHelper.StripCounterAndQty(lotSl);
            }
        }
        private string NormalizeLotFCC_YMVN(string lotSl, out string gear)
        {
            gear = "";
            string[] ghep = lotSl.Split(',');
            if (ghep.Length > 1) return lotSl; // LOT ghép nhiều — xử lý ở luồng khác

            // Gear nằm ở vị trí cố định theo cấu trúc field (sau Date+Id+Shift), đọc TRƯỚC
            // khi strip đuôi — vị trí này không đổi bất kể độ dài tổng chuỗi.
            string gearRaw = LotCodeHelper.GetGearPart(lotSl);
            if (!string.IsNullOrEmpty(gearRaw))
            {
                gear = int.TryParse(gearRaw, out int gearCode)
                    ? _repo.GetGearName(gearCode)
                    : _repo.GetGearName(gearRaw);
            }

            // ✅ Chỉ strip khi đủ 20 ký tự head — theo đúng cấu trúc field thật,
            // không cắt cơ học 19+4 như GetStockTpKey cũ.
            return lotSl.Length >= LotCodeHelper.LEN_HEAD_FIXED
                ? LotCodeHelper.StripCounterAndQty(lotSl)
                : lotSl;
        }
        // ── Helper: cắt LOT cho HTN — bỏ 7 ký tự cuối ───────────────────────
        // Chỉ áp dụng cho 100003 (LoadTuBangRieng + !CoGear)
        private string NormalizeLotFCC_HTN(string lotRaw)
        {
            // ✅ Cắt theo cấu trúc field thật (bỏ 8 ký tự Counter+Qty), khớp đúng cách
            // vNhapTP.LOT_NO / STOCKTP.LOT được build — thay vì cắt cơ học 19+4.
            return LotCodeHelper.StripCounterAndQty(lotRaw);
        }

    }


}