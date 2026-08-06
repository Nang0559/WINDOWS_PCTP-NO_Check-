using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public class NhapTpReceivingService
    {
        private readonly IStockTpRepository _repo;
        private static readonly HashSet<string> _sessionCases = new HashSet<string>(); // thay SQLPROVIDER.c_Ns

        public NhapTpReceivingService(IStockTpRepository repo) => _repo = repo;

        /// <summary>Bước 1: chỉ validate, KHÔNG ghi DB</summary>
        public ScanResult TiepNhanTemTong(QRCodeInfo qr, string loaiNhap)
        {
            if (qr == null) return ScanResult.Fail("Không đọc được QR.");

            if (!qr.IsTongPhieu && loaiNhap != "NG")
                return ScanResult.Fail("Bạn đang bắn tem thùng (tem thùng chỉ cho phép nhập lại NG).");

            if (string.IsNullOrEmpty(qr.CaseNo))
                return ScanResult.Fail("QR thiếu mã Case.");

            // Dedup: session + DB (port KTTRUNGLIST + check NHAP_TP_HIS)
            if (_sessionCases.Contains(qr.CaseNo))
                return ScanResult.Trung($"Case [{qr.CaseNo}] đã quét trong phiên này!");

            if (_repo.ExistsCaseHistory(qr.CaseNo))
                return ScanResult.Trung($"Case [{qr.CaseNo}] đã được nhập trước đó!");

            return loaiNhap == "N" ? XuLyNhapThuong(qr) : XuLyNhapNG(qr);
        }

        private ScanResult XuLyNhapThuong(QRCodeInfo qr)
        {
            var phieu = _repo.GetPhieuByFind(qr.LotNo);
            if (phieu == null)
                return ScanResult.Fail("Không tồn tại phiếu nhập tương ứng LOT này.");

            if (phieu.KetThucLot)
                return ScanResult.Fail($"LOT [{phieu.LotNo}] đã kết thúc, không thể nhập thêm.");

            var item = NhapKhoItem.FromPhieu(phieu, "N");
            item.SlNhap = qr.Quantity;

            var result = ScanResult.OKNhapKho(item);
            result.CaseNo = qr.CaseNo;
            // Port điều kiện: SLDANHAP + SLSENHAP > SLSX → cảnh báo, KHÔNG chặn cứng (giống code cũ dùng YesNo)
            result.CanhBaoVuotSanLuong = (phieu.SlDaNhap + qr.Quantity) > phieu.SlSanXuat;
            return result;
        }

        private ScanResult XuLyNhapNG(QRCodeInfo qr)
        {
            var dsTra = _repo.GetTraHangConLai(qr.LotNo);
            if (dsTra == null || dsTra.Count == 0)
                return ScanResult.Fail("Không tồn tại phiếu nhập NG cho LOT này.");

            var r = ScanResult.OKNgList(dsTra);
            r.CaseNo = qr.CaseNo;
            return r;
        }

        /// <summary>Bước 3: GHI DB — chỉ gọi sau khi qua Inspection (nếu có) và user xác nhận</summary>
        public (bool ok, string error) XacNhanGhiNhan(NhapKhoItem item, string caseNo)
        {
            try
            {
                int status = (item.SlDaNhap + item.SlNhap >= item.SlSanXuat) ? 1 : 0;

                if (_repo.ExistsStockTp(item.Lot))
                    _repo.UpdateStockTp(item.Lot, item.SlNhap, status);
                else
                    _repo.InsertStockTp(item, status);

                _repo.InsertCaseHistory(caseNo);
                _sessionCases.Add(caseNo);
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string error) XacNhanNhanLaiNG(
            string lot, string part, string name, string lyDoNg, int slNhanLai, string caseNo)
        {
            try
            {
                var traInfo = _repo.GetTraHangConLai(lot).FirstOrDefault(x => x.LyDoNg == lyDoNg);
                if (traInfo == null) return (false, "Không tìm thấy dòng trả hàng tương ứng.");

                if (!_repo.ExistsStockTp(lot))
                    return (false, $"LOT [{lot}] chưa tồn tại trong STOCKTP để nhận lại NG.");

                int slConLaiSauNhan = traInfo.SlConLai - slNhanLai;
                int status = slConLaiSauNhan <= 0 ? 0 : 1;

                _repo.UpdateStockTp(lot, slNhanLai, 0);
                _repo.InsertNhanTra(lot, part, name, slNhanLai, lyDoNg);
                _repo.UpdateTraHangSauNhanLai(lot, lyDoNg, slNhanLai, status);
                _repo.InsertCaseHistory(caseNo);
                _sessionCases.Add(caseNo);
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
