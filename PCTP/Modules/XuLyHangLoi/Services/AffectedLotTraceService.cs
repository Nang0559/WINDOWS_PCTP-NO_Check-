using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Phase 2: hợp nhất snapshot LOT từ Kho + Sản xuất/WIP + Khách trả.
    /// Service này chỉ đọc; việc lưu snapshot xuống DB sẽ được nối ở repository phase kế tiếp.
    /// Không được coi riêng tồn kho là toàn bộ LOT bị ảnh hưởng.
    /// </summary>
    public sealed class AffectedLotTraceService : IAffectedLotTraceService
    {
        private readonly IReworkStockService _stockService;
        private readonly IProductionLotTraceProvider _productionProvider;
        private readonly ICustomerReturnLotTraceProvider _customerReturnProvider;

        public AffectedLotTraceService(
            IReworkStockService stockService,
            IProductionLotTraceProvider productionProvider = null,
            ICustomerReturnLotTraceProvider customerReturnProvider = null)
        {
            _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
            _productionProvider = productionProvider;
            _customerReturnProvider = customerReturnProvider;
        }

        public AffectedLotTraceResult TraceForPhieu(
            PhieuXuLyBatThuong phieu,
            string nguoiThucHien)
        {
            if (phieu == null)
                throw new ArgumentNullException(nameof(phieu));

            return Trace(phieu.MaSanPham, phieu.SoLoLoi, nguoiThucHien);
        }

        public AffectedLotTraceResult Trace(
            string maSanPham,
            string lotNo,
            string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(maSanPham))
                throw new ArgumentException("MaSanPham không được rỗng.", nameof(maSanPham));
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                throw new ArgumentException("NguoiThucHien không được rỗng.", nameof(nguoiThucHien));

            var result = new AffectedLotTraceResult
            {
                MaSanPham = maSanPham.Trim(),
                LotNo = NormalizeLot(lotNo),
                IsComplete = true
            };

            var stockLots = _stockService.GetLotsCanRework(result.MaSanPham, result.LotNo);
            if (stockLots != null)
            {
                foreach (var lot in stockLots)
                {
                    if (lot == null || lot.Quantity <= 0)
                        continue;

                    result.Items.Add(new PhieuXuLyBatThuongAffectedLot
                    {
                        SourceType = AffectedLotSourceType.Kho,
                        SourceReference = lot.TemCode,
                        SlotId = null,
                        LotNo = lot.LotNo,
                        MaSanPham = lot.ItemCode,
                        SoLuongAnhHuong = lot.Quantity,
                        SnapshotAt = DateTime.Now,
                        SnapshotBy = nguoiThucHien.Trim()
                    });
                }
            }

            if (_productionProvider != null)
            {
                var rows = _productionProvider.Trace(result.MaSanPham, result.LotNo);
                AddRows(result, rows, AffectedLotSourceType.SanXuat, nguoiThucHien);
            }
            else
            {
                result.IsComplete = false;
                result.Warnings.Add("Chưa cấu hình provider truy vết LOT tại Sản xuất/WIP.");
            }

            if (_customerReturnProvider != null)
            {
                var rows = _customerReturnProvider.Trace(result.MaSanPham, result.LotNo);
                AddRows(result, rows, AffectedLotSourceType.KhachTra, nguoiThucHien);
            }
            else
            {
                result.IsComplete = false;
                result.Warnings.Add("Chưa cấu hình provider truy vết LOT tại nguồn Khách trả.");
            }

            result.Items = result.Items
                .Where(x => x != null && x.SoLuongAnhHuong > 0)
                .GroupBy(x => new
                {
                    x.SourceType,
                    SourceReference = (x.SourceReference ?? string.Empty).Trim(),
                    x.SlotId,
                    LotNo = NormalizeLot(x.LotNo),
                    MaSanPham = (x.MaSanPham ?? result.MaSanPham).Trim()
                })
                .Select(g =>
                {
                    var first = g.First();
                    first.SoLuongAnhHuong = g.Sum(x => x.SoLuongAnhHuong);
                    first.LotNo = NormalizeLot(first.LotNo);
                    return first;
                })
                .ToList();

            return result;
        }

        private static void AddRows(
            AffectedLotTraceResult result,
            IEnumerable<PhieuXuLyBatThuongAffectedLot> rows,
            AffectedLotSourceType sourceType,
            string nguoiThucHien)
        {
            if (rows == null)
                return;

            foreach (var row in rows)
            {
                if (row == null || row.SoLuongAnhHuong <= 0)
                    continue;

                row.SourceType = sourceType;
                row.MaSanPham = string.IsNullOrWhiteSpace(row.MaSanPham)
                    ? result.MaSanPham
                    : row.MaSanPham.Trim();
                row.LotNo = NormalizeLot(row.LotNo ?? result.LotNo);
                row.SnapshotAt = row.SnapshotAt == default(DateTime)
                    ? DateTime.Now
                    : row.SnapshotAt;
                row.SnapshotBy = string.IsNullOrWhiteSpace(row.SnapshotBy)
                    ? nguoiThucHien.Trim()
                    : row.SnapshotBy;
                result.Items.Add(row);
            }
        }

        private static string NormalizeLot(string lotNo)
        {
            return string.IsNullOrWhiteSpace(lotNo)
                ? string.Empty
                : lotNo.Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// Truy vết các dòng hàng Khách trả có cùng Mã hàng + LOT.
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
