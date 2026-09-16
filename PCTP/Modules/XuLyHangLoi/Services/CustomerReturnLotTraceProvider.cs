using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using System;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Phase 2: truy vết các dòng hàng Khách trả có cùng Mã hàng + LOT.
    /// Chỉ đọc dữ liệu từ PhieuTraHang/PhieuTraHangCT, không mutate stock.
    /// </summary>
    public sealed class CustomerReturnLotTraceProvider : ICustomerReturnLotTraceProvider
    {
        private readonly IPhieuTraHangRepository _repository;

        public CustomerReturnLotTraceProvider(IPhieuTraHangRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo)
        {
            if (string.IsNullOrWhiteSpace(maSanPham) || string.IsNullOrWhiteSpace(lotNo))
                yield break;

            var maHang = maSanPham.Trim();
            var lot = NormalizeLot(lotNo);
            var headers = _repository.GetByNguon(NguonXuLyBatThuong.KhachTra);

            if (headers == null)
                yield break;

            foreach (var header in headers)
            {
                if (header == null || header.Id <= 0)
                    continue;

                var items = _repository.GetItems(header.Id);
                if (items == null)
                    continue;

                foreach (var item in items)
                {
                    if (item == null || item.SoLuong <= 0)
                        continue;
                    if (!string.Equals(item.MaHang == null ? null : item.MaHang.Trim(), maHang, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.Equals(NormalizeLot(item.LotNo), lot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    yield return new PhieuXuLyBatThuongAffectedLot
                    {
                        SourceType = AffectedLotSourceType.KhachTra,
                        SourceReference = string.Format("PHIEU_TRA_HANG:{0}/CT:{1}", header.Id, item.Id),
                        SlotId = item.SlotIdNguon,
                        LotNo = item.LotNo,
                        MaSanPham = item.MaHang,
                        SoLuongAnhHuong = item.SoLuong,
                        SnapshotAt = DateTime.Now
                    };
                }
            }
        }

        private static string NormalizeLot(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }
    }
}
