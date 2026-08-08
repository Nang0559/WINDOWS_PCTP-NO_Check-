using DevExpress.XtraRichEdit.Import.Html;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
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

            // 100003: tem tổng 6 phần
            if (parts.Length == 6 && !_cfg.CoNhieuNhaMay && !_cfg.CoGear)
                return ScanFCC_TongTem(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);

            // 100002 FCC: 4 phần + CoGear
            if (parts.Length == 4 && _cfg.CoGear)
                return ScanFCC_YMVN(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);

            // 100002 YMVN: format P...K...Q...
            if (_cfg.CoGear)
                return ScanYMVN(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan);

            if (parts.Length == 4)
            {
                // ── SP: chỉ bắn FCC, không cần HVN ─────────────────────────
                if (_isBanSP)
                    return ScanFCC_SP(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);

                // ── O TYPE: bắn FCC → HVN giống MP ──────────────────────────
                // _isBanOType chỉ ảnh hưởng bảng DB (nếu config riêng)
                // luồng bắn giống hệt MP
                return ScanFCC(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);
            }

            // HVN — bước 2 (MP + O TYPE)
            return ScanHVN(parts);
        }

        // ════════════════════════════════════════════════════════════════════
        // SP: chỉ bắn FCC — không cần HVN
        // ════════════════════════════════════════════════════════════════════
        private ScanResult ScanFCC_SP(string[] parts,
            Func<string, bool> kiemTraMa,
            Func<string, int, bool> kiemTraSl)
        {
            string maHang = parts[1].Replace(" ", "");
            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");

            string lotFcc = NormalizeLotFCC(parts[0], maHang);

            if (!kiemTraMa(maHang))
                return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");

            if (!kiemTraSl(maHang, slTem))
                return ScanResult.Fail(
                    "Tổng số lượng đã bắn vượt quá số lượng giao!\n" +
                    "Hãy kiểm tra lại phiếu.");

            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;

            var item = new DocQRCode
            {
                STT = sttBan,
                LotFCC = lotFcc,
                MaHangFCC = maHang,
                MaFCC = maHang,
                SlTemFCC = slTem,
                // SP: tự ghép HVN = FCC luôn, không đợi scan HVN
                LotHVN = lotFcc,
                MaHangHVN = maHang,
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

            string maHang = parts[1];
            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");

            string lotFcc = NormalizeLotFCC(parts[0], maHang);

            if (!kiemTraMa(maHang))
                return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");

            if (!kiemTraSl(maHang, slTem))
                return ScanResult.Fail("Số lượng bắn đang vượt quá số lượng giao!");

            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;

            var item = new DocQRCode
            {
                STT = sttBan,
                LotFCC = lotFcc,
                MaHangFCC = maHang,
                MaFCC = maHang,
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
            string sidMh = _repo.GetIdMaHangPadded(maHang);

            string[] ghep = lotSl.Split(',');
            if (ghep.Length > 1)
            {
                var resultParts = new List<string>();
                foreach (var g in ghep)
                {
                    string[] lotSlPart = g.Split('-');
                    string lot = lotSlPart[0];
                    string sl = lotSlPart.Length > 1 ? lotSlPart[1] : "0";

                    if (lot.Length < 13)
                    {
                        if (lot.Length == 12) return lotSl;
                        if (!string.IsNullOrEmpty(idRaw))
                            lot = lot.Replace(idRaw, sidMh);
                        resultParts.Add(lot + "-" + sl);
                    }
                    else
                        resultParts.Add(lot.Substring(0, 13) + "-" + sl);
                }
                return string.Join(",", resultParts);
            }
            else
            {
                string lott;
                if (lotSl.Length < 13)
                {
                    if (!string.IsNullOrEmpty(idRaw))
                        lotSl = lotSl.Replace(idRaw, sidMh);
                    lott = lotSl.Length > 13
                        ? lotSl.Substring(0, 6) + sidMh + lotSl.Substring(13, 1)
                        : lotSl;
                }
                else
                    lott = lotSl.Substring(0, 13);
                return lott;
            }
        }

        private string NormalizeLotFCC_YMVN(string lotSl, out string gear)
        {
            gear = "";
            string[] ghep = lotSl.Split(',');
            if (ghep.Length > 1) return lotSl;

            string lotFcc;
            if (lotSl.Length == 26)
            {
                lotFcc = lotSl;
            }
            else if (lotSl.Length == 27 || lotSl.Length == 28)
            {
                lotFcc = lotSl.Substring(0, 13);

                // ← ký tự thứ 12 là mã số (int) → dùng overload int
                if (int.TryParse(lotSl.Substring(12, 1), out int gearCode))
                    gear = _repo.GetGearName(gearCode);  // ← overload int
                else
                    gear = _repo.GetGearName(lotSl.Substring(12, 1)); // ← overload string fallback
            }
            else
            {
                lotFcc = lotSl.Length >= 13 ? lotSl.Substring(0, 13) : lotSl;
            }

            return lotFcc;
        }
        // ── Helper: cắt LOT cho HTN — bỏ 7 ký tự cuối ───────────────────────
        // Chỉ áp dụng cho 100003 (LoadTuBangRieng + !CoGear)
        private string NormalizeLotFCC_HTN(string lotRaw)
        {
            // HTN: bỏ 8 ký tự cuối
            // VD: "260721031241010540001226000" (27 ký tự)
            //   → "2607210312410105400"         (19 ký tự)
            const int BoKyTuCuoi = 8;
            if (lotRaw.Length > BoKyTuCuoi)
                return lotRaw.Substring(0, lotRaw.Length - BoKyTuCuoi);
            return lotRaw;
        }

    }


        // ── Result type ─────────────────────────────────────────────────────────────
        public class ScanResult
    {
        public bool Success { get; private set; }
        public bool SlKhacBiet { get; private set; }
        public string Message { get; private set; } = "";
        public DocQRCode Item { get; private set; }

        public static ScanResult OK(DocQRCode item)
            => new ScanResult { Success = true, Item = item };

        public static ScanResult Fail(string message)
            => new ScanResult { Success = false, Message = message };

        public static ScanResult SlKhongKhop(DocQRCode item)
            => new ScanResult { Success = false, SlKhacBiet = true, Item = item };
    }
}