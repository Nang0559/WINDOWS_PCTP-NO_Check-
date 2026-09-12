using DevExpress.XtraRichEdit.Import.Html;
using PCTP.Common;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Core QR parsing and scan business rules.
    /// This class intentionally preserves the existing QR behavior; the facade
    /// only owns the public service boundary and session state.
    /// </summary>
    public sealed class DocQRScanEngine
    {
        private readonly IDocQRRepository _repo;
        private readonly IEventBus _bus;
        private readonly CustomerConfig _cfg;
        private readonly DocQRSessionState _session;

        private string DocQRTable => _session.DocQrTable;
        private string TmpTable => _session.TmpTable;
        private bool IsBanSP => _session.IsBanSP;
        private bool IsBanOType => _session.IsBanOType;

        public DocQRScanEngine(
            IDocQRRepository repo,
            IEventBus bus,
            CustomerConfig cfg,
            DocQRSessionState session)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void SetCategory(bool isSp, bool isOType)
        {
            _session.SetCategory(isSp, isOType);
        }

        public int CountChuaDG() => _repo.CountChuaDG(DocQRTable);
        public bool CoDocQRNao() => _repo.Count(DocQRTable) > 0;
        public DataTable LoadAll() => _repo.GetAllAsTable(DocQRTable);
        public void XoaDong(int stt) => _repo.Delete(stt, DocQRTable);
        public void XoaToanBo() => _repo.DeleteAll(DocQRTable);
        public void CapNhapSlHvn(int stt, int slMoi) => _repo.UpdateSlHvn(stt, slMoi, DocQRTable);

        public ScanResult ProcessScan(string rawQr, Func<string, bool> kiemTraMaTrongPhieu, Func<string, int, bool> kiemTraSlDaBan)
        {
            rawQr = rawQr.Trim().ToUpper();
            string[] parts = rawQr.Split(':');
            if (parts.Length == 6 && !_cfg.Delivery.CoNhieuNhaMay && !_cfg.Delivery.CoGear)
                return ScanFCC_TongTem(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);
            if (parts.Length == 4 && _cfg.Delivery.CoGear)
                return ScanFCC_YMVN(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);
            if (_cfg.Delivery.CoGear)
                return ScanYMVN(rawQr, kiemTraMaTrongPhieu, kiemTraSlDaBan);
            if (parts.Length == 4)
            {
                if (IsBanSP)
                    return ScanFCC_SP(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);
                return ScanFCC(parts, kiemTraMaTrongPhieu, kiemTraSlDaBan);
            }
            return ScanHVN(parts);
        }

        private string MapMaHang(string maHang)
        {
            string mapped = _repo.GetMaHangMapped(maHang);
            return string.IsNullOrEmpty(mapped) ? maHang : mapped;
        }

        private ScanResult ScanFCC_SP(string[] parts, Func<string, bool> kiemTraMa, Func<string, int, bool> kiemTraSl)
        {
            string maFcc = parts[1].Trim();
            string maHangThuc = MapMaHang(maFcc);
            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");
            string lotFcc = NormalizeLotFCC(parts[0], maHangThuc);
            if (!kiemTraMa(maHangThuc))
                return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");
            if (!kiemTraSl(maHangThuc, slTem))
                return ScanResult.Fail("Tổng số lượng đã bắn vượt quá số lượng giao!\nHãy kiểm tra lại phiếu.");
            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;
            var item = new DocQRCode { STT = sttBan, LotFCC = lotFcc, MaHangFCC = maHangThuc, MaFCC = maHangThuc, SlTemFCC = slTem, LotHVN = lotFcc, MaHangHVN = maHangThuc, SlTemHVN = slTem, KetQua = "OK", Gio = "" };
            _repo.InsertFCC(item, DocQRTable);
            _repo.UpdateHVN(item, DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "FCC_SP"));
            return ScanResult.OK(item);
        }

        private ScanResult ScanFCC(string[] parts, Func<string, bool> kiemTraMa, Func<string, int, bool> kiemTraSl)
        {
            if (!KiemTraThuTuFCC()) return ScanResult.Fail("Sai Thứ tự bắn!");
            string maFcc = parts[1].Trim();
            string maHangThuc = MapMaHang(maFcc);
            if (!int.TryParse(parts[3], out int slTem))
                return ScanResult.Fail("Số lượng tem không hợp lệ.");
            string lotFcc = NormalizeLotFCC(parts[0], maHangThuc);
            if (!kiemTraMa(maHangThuc)) return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");
            if (!kiemTraSl(maHangThuc, slTem)) return ScanResult.Fail("Số lượng bắn đang vượt quá số lượng giao!");
            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;
            var item = new DocQRCode { STT = sttBan, LotFCC = lotFcc, MaHangFCC = maHangThuc, MaFCC = maHangThuc, SlTemFCC = slTem, Gio = "" };
            _repo.InsertFCC(item, DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "FCC"));
            return ScanResult.OK(item);
        }

        public ScanResult ProcessScanYMVN(string rawQr, Func<string, bool> kiemTraMaTrongPhieu, Func<string, int, bool> kiemTraSlDaBan)
        {
            var parts = rawQr.Split(':');
            if (parts.Length < 6) return ScanResult.Fail("Mã QR không đúng định dạng YMVN!");
            string lotFcc = parts[0].Trim();
            string maHang = parts[1].Trim();
            string ngay = parts[2].Trim();
            string slStr = parts[3].Trim();
            string soTT = parts[4].Trim();
            string temCode = parts[5].Trim();
            string gear = parts.Length > 6 ? parts[6].Trim() : "";
            if (!int.TryParse(slStr, out int slTem)) return ScanResult.Fail("Số lượng TEM không hợp lệ!");
            if (!kiemTraMaTrongPhieu(maHang)) return ScanResult.Fail($"Mã hàng [{maHang}] không có trong phiếu!");
            string gearName = parts.Length > 6 ? _repo.GetGearName(parts[6].Trim()) : "";
            string docQRTable = _cfg.Delivery.GetDocQRTable(IsBanSP);
            bool trung = _repo.KiemTraTrungTemTong(lotFcc, soTT, docQRTable);
            if (trung) return ScanResult.Fail($"TEM [{temCode}] đã được quét!");
            int maxStt = _repo.GetMaxStt(docQRTable);
            var item = new DocQRCode { STT = maxStt + 1, LotFCC = lotFcc, MaHangFCC = maHang, MaFCC = temCode, SlTemFCC = slTem, Gio = soTT, Gear = gearName, SoPhieu = soTT };
            if (!kiemTraSlDaBan(maHang, slTem)) return ScanResult.SlKhongKhop(item);
            _repo.InsertFCC(item, docQRTable, _cfg.Delivery.CoGear);
            _bus.Publish(new QRScannedEvent(item, _cfg.CustomerNo));
            return ScanResult.OK(item);
        }

        private DocQRCode BuildItemYMVN(string lotFcc, string maHang, int slTem, string temCode, string gear)
        {
            return new DocQRCode { LotFCC = lotFcc, MaHangFCC = maHang, SlTemFCC = slTem, MaFCC = temCode, Gear = gear };
        }

        private ScanResult ScanHVN(string[] parts)
        {
            if (!KiemTraThuTuHVN()) return ScanResult.Fail("Sai Thứ tự bắn!");
            if (parts.Length < 4) return ScanResult.Fail("Dữ liệu QR HVN không hợp lệ.");
            string lotHvn = parts[0];
            string maHvn = parts[1].Replace(" ", "");
            if (!int.TryParse(parts[3], out int slTem)) return ScanResult.Fail("Số lượng tem không hợp lệ.");
            if (!KiemTraTrungTem(lotHvn)) return ScanResult.Fail("Trùng Tem!");
            if (!_repo.KiemTraTemMa(maHvn)) return ScanResult.Fail("Mã Hàng HVN không khớp với FCC!");
            int sttBan = _repo.GetMaxStt(DocQRTable);
            if (!_repo.KiemTraTemSoLuong(maHvn, slTem))
                return ScanResult.SlKhongKhop(new DocQRCode { STT = sttBan, LotHVN = lotHvn, MaHangHVN = maHvn, SlTemHVN = slTem });
            var item = new DocQRCode { STT = sttBan, LotHVN = lotHvn, MaHangHVN = maHvn, SlTemHVN = slTem, KetQua = "OK" };
            _repo.UpdateHVN(item, DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "HVN"));
            return ScanResult.OK(item);
        }

        private ScanResult ScanFCC_YMVN(string[] parts, Func<string, bool> kiemTraMa, Func<string, int, bool> kiemTraSl)
        {
            string lotSl = parts[0];
            string maHang = parts[1];
            if (!int.TryParse(parts[3], out int slTem)) return ScanResult.Fail("Số lượng tem không hợp lệ.");
            string lotFcc = NormalizeLotFCC_YMVN(lotSl, out string gear);
            if (!kiemTraMa(maHang)) return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");
            if (!kiemTraSl(maHang, slTem)) return ScanResult.Fail("Số lượng bắn vượt quá số lượng giao!");
            int sttBan = _repo.GetMaxStt(DocQRTable) + 1;
            var item = new DocQRCode { STT = sttBan, LotFCC = lotFcc, MaHangFCC = maHang, MaFCC = maHang.Replace("-", ""), SlTemFCC = slTem, Gear = gear, Gio = "" };
            _repo.InsertFCC(item, DocQRTable, _cfg.Delivery.CoGear);
            _bus.Publish(new QRScannedEvent(item, "FCC_YMVN"));
            return ScanResult.OK(item);
        }

        private ScanResult ScanYMVN(string rawQr, Func<string, bool> kiemTraMa, Func<string, int, bool> kiemTraSl)
        {
            string partNo = "", oderNo = "";
            int slTem = 0, vtp = 0, vtor = 0;
            for (int i = 0; i < rawQr.Length; i++) if (rawQr[i] == 'P') { partNo = rawQr.Substring(i + 1, 14); vtp = i + 15; break; }
            for (int j = vtp; j < rawQr.Length; j++) if (rawQr[j] == 'K') { oderNo = rawQr.Substring(j + 1, 5); vtor = j + 6; break; }
            for (int j = vtor; j < rawQr.Length; j++) if (rawQr[j] == 'Q') { slTem = int.Parse(rawQr.Substring(j + 1, 6)); break; }
            if (!kiemTraMa(partNo)) return ScanResult.Fail($"Mã hàng YMVN {partNo} không có trong phiếu!");
            if (!kiemTraSl(partNo, slTem)) return ScanResult.Fail("Số lượng bắn vượt quá số lượng giao!");
            int sttBan = _repo.GetMaxStt(_cfg.Delivery.DocQRTable);
            var item = new DocQRCode { STT = sttBan, LotHVN = oderNo, MaHangHVN = partNo, SlTemHVN = slTem, KetQua = "OK" };
            _repo.UpdateHVN(item, _cfg.Delivery.DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "YMVN"));
            return ScanResult.OK(item);
        }

        private ScanResult ScanFCC_TongTem(string[] parts, Func<string, bool> kiemTraMa, Func<string, int, bool> kiemTraSl)
        {
            if (parts.Length < 6) return ScanResult.Fail("Mã vạch không đúng định dạng (cần 6 phần).");
            string lotRaw = parts[0];
            string maHang = parts[1].Replace(" ", "");
            string soPhieu = parts[4];
            if (!int.TryParse(parts[3], out int slTem)) return ScanResult.Fail("Số lượng tem không hợp lệ.");
            string lotFcc = (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear) ? NormalizeLotFCC_HTN(lotRaw) : lotRaw;
            if (!kiemTraMa(maHang)) return ScanResult.Fail("Không tồn tại mã trong phiếu giao!");
            if (KiemTraTrungTemTong(lotFcc, soPhieu)) return ScanResult.Fail($"Trùng phiếu!\nLot: [{lotFcc}]\nSố phiếu: [{soPhieu}]");
            if (!kiemTraSl(maHang, slTem)) return ScanResult.Fail("Tổng số lượng đã bắn vượt quá số lượng giao!");
            int sttBan = _repo.GetMaxStt(_cfg.Delivery.DocQRTable) + 1;
            var item = new DocQRCode { STT = sttBan, LotFCC = lotFcc, MaHangFCC = maHang, MaFCC = maHang, SlTemFCC = slTem, LotHVN = lotFcc, MaHangHVN = maHang, SlTemHVN = slTem, KetQua = "OK", Gio = "" };
            _repo.InsertFCC(item, _cfg.Delivery.DocQRTable);
            _repo.UpdateHVN(item, _cfg.Delivery.DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "FCC_TONG"));
            return ScanResult.OK(item);
        }

        public ScanResult ConfirmSlKhacBiet(DocQRCode pending)
        {
            var item = new DocQRCode { STT = pending.STT, LotHVN = pending.LotHVN, MaHangHVN = pending.MaHangHVN, SlTemHVN = pending.SlTemHVN, KetQua = "KHAC SLTEM" };
            _repo.UpdateHVN(item, DocQRTable);
            _bus.Publish(new QRScannedEvent(item, "HVN"));
            return ScanResult.OK(item);
        }

        public bool KiemTraSlDaBan(string maHang, int slBan)
        {
            int ttSlDaBan = _repo.GetTongSlDaBan(maHang, DocQRTable);
            int slGiao = _repo.GetSoLuongGiaoTheoMa(maHang, TmpTable);
            return ttSlDaBan + slBan <= slGiao;
        }

        private bool KiemTraThuTuFCC()
        {
            if (!_cfg.Delivery.CoNhieuNhaMay || IsBanSP || IsBanOType) return true;
            var all = _repo.GetAll(DocQRTable);
            if (all.Count == 0) return true;
            foreach (var item in all) if (string.IsNullOrEmpty(item.LotHVN)) return false;
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
                if (!string.IsNullOrEmpty(item.LotHVN) && item.LotHVN.Trim() == lotHvn.Trim()) return false;
            return true;
        }

        private bool KiemTraTrungTemTong(string lotFcc, string soPhieu)
            => _repo.KiemTraTrungTemTong(lotFcc, soPhieu, _cfg.Delivery.DocQRTable);

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
                        if (lot.Length == 12) { resultParts.Add(lot + "-" + sl); continue; }
                        lot = LotCodeHelper.BuildLegacyShortLot(lot, idPadded);
                        resultParts.Add(lot + "-" + sl);
                    }
                    else resultParts.Add(LotCodeHelper.StripCounterAndQty(lot) + "-" + sl);
                }
                return string.Join(",", resultParts);
            }
            if (lotSl.Length < LotCodeHelper.LEN_HEAD_FIXED) return LotCodeHelper.BuildLegacyShortLot(lotSl, idPadded);
            return LotCodeHelper.StripCounterAndQty(lotSl);
        }

        private string NormalizeLotFCC_YMVN(string lotSl, out string gear)
        {
            gear = "";
            string[] ghep = lotSl.Split(',');
            if (ghep.Length > 1) return lotSl;
            string gearRaw = LotCodeHelper.GetGearPart(lotSl);
            if (!string.IsNullOrEmpty(gearRaw))
                gear = int.TryParse(gearRaw, out int gearCode) ? _repo.GetGearName(gearCode) : _repo.GetGearName(gearRaw);
            return lotSl.Length >= LotCodeHelper.LEN_HEAD_FIXED ? LotCodeHelper.StripCounterAndQty(lotSl) : lotSl;
        }

        private string NormalizeLotFCC_HTN(string lotRaw)
        {
            return LotCodeHelper.StripCounterAndQty(lotRaw);
        }
    }
}