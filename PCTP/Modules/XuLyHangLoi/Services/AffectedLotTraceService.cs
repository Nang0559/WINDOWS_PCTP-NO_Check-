using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public sealed class AffectedLotTraceService : SqlRepositoryBase, IAffectedLotTraceService
    {
        private readonly IReworkStockService _stockService;
        private readonly IProductionLotTraceProvider _productionProvider;
        private readonly ICustomerReturnLotTraceProvider _customerReturnProvider;
        private readonly IPhieuXuLyBatThuongRepository _phieuRepository;

        public AffectedLotTraceService(
            IReworkStockService stockService,
            IProductionLotTraceProvider productionProvider,
            ICustomerReturnLotTraceProvider customerReturnProvider,
            IPhieuXuLyBatThuongRepository phieuRepository,
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
            _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
            _productionProvider = productionProvider;
            _customerReturnProvider = customerReturnProvider;
            _phieuRepository = phieuRepository ?? throw new ArgumentNullException(nameof(phieuRepository));
        }

        public AffectedLotTraceResult TraceForPhieu(PhieuXuLyBatThuong phieu, string nguoiThucHien)
        {
            if (phieu == null) throw new ArgumentNullException(nameof(phieu));
            return Trace(phieu.MaSanPham, phieu.SoLoLoi, nguoiThucHien);
        }

        public AffectedLotTraceResult TruyVetLOT(int phieuXuLyId, string nguoiThucHien)
        {
            if (phieuXuLyId <= 0) throw new ArgumentOutOfRangeException(nameof(phieuXuLyId));
            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                throw new ArgumentException("NguoiThucHien không được rỗng.", nameof(nguoiThucHien));

            var phieu = _phieuRepository.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException("Không tìm thấy PhieuXuLyBatThuong Id=" + phieuXuLyId + ".");

            var result = TraceForPhieu(phieu, nguoiThucHien);

            if (!result.IsComplete)
                throw new InvalidOperationException(
                    "Không thể hoàn tất truy vết LOT vì còn thiếu nguồn dữ liệu: " +
                    string.Join(" | ", result.Warnings));

            if (result.TotalAffectedQuantity <= 0)
                throw new InvalidOperationException("Truy vết LOT không tìm thấy số lượng bị ảnh hưởng.");

            var snapshotAt = DateTime.Now;
            foreach (var item in result.Items)
            {
                item.PhieuXuLyBatThuongId = phieuXuLyId;
                item.SnapshotAt = snapshotAt;
                item.SnapshotBy = nguoiThucHien.Trim();
            }

            try
            {
                Uow.Begin();

                ExecuteNonQuery(
                    @"DELETE FROM FVN_PhieuXuLyBatThuongAffectedLot
                      WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId;",
                    new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId));

                foreach (var item in result.Items)
                {
                    ExecuteNonQuery(
                        @"INSERT INTO FVN_PhieuXuLyBatThuongAffectedLot
                          (PhieuXuLyBatThuongId, SourceType, SourceReference, SlotId,
                           LotNo, MaSanPham, Model, SoLuongAnhHuong, SoLuongDaKiemTra,
                           SoLuongOK, SoLuongNG, SoLuongRework, SoLuongLoaiBo,
                           SnapshotAt, SnapshotBy)
                          VALUES
                          (@PhieuXuLyBatThuongId, @SourceType, @SourceReference, @SlotId,
                           @LotNo, @MaSanPham, @Model, @SoLuongAnhHuong, 0,
                           0, 0, 0, 0, @SnapshotAt, @SnapshotBy);",
                        new SqlParameter("@PhieuXuLyBatThuongId", item.PhieuXuLyBatThuongId),
                        new SqlParameter("@SourceType", (int)item.SourceType),
                        new SqlParameter("@SourceReference", DbValueHelper.DbValue(item.SourceReference)),
                        new SqlParameter("@SlotId", DbValueHelper.DbValue(item.SlotId)),
                        new SqlParameter("@LotNo", DbValueHelper.DbValue(item.LotNo)),
                        new SqlParameter("@MaSanPham", DbValueHelper.DbValue(item.MaSanPham)),
                        new SqlParameter("@Model", DbValueHelper.DbValue(item.Model)),
                        new SqlParameter("@SoLuongAnhHuong", item.SoLuongAnhHuong),
                        new SqlParameter("@SnapshotAt", item.SnapshotAt),
                        new SqlParameter("@SnapshotBy", DbValueHelper.DbValue(item.SnapshotBy)));
                }

                Uow.Commit();
                return result;
            }
            catch
            {
                try { Uow.Rollback(); } catch { }
                throw;
            }
        }

        public IReadOnlyList<PhieuXuLyBatThuongAffectedLot> GetSnapshot(int phieuXuLyId)
        {
            if (phieuXuLyId <= 0) throw new ArgumentOutOfRangeException(nameof(phieuXuLyId));

            var table = LoadData(
                @"SELECT Id, PhieuXuLyBatThuongId, SourceType, SourceReference, SlotId,
                         LotNo, MaSanPham, Model, SoLuongAnhHuong, SoLuongDaKiemTra,
                         SoLuongOK, SoLuongNG, SoLuongRework, SoLuongLoaiBo,
                         SnapshotAt, SnapshotBy
                  FROM FVN_PhieuXuLyBatThuongAffectedLot
                  WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId
                  ORDER BY SourceType, Id;",
                new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId));

            var result = new List<PhieuXuLyBatThuongAffectedLot>();
            foreach (DataRow row in table.Rows)
            {
                result.Add(new PhieuXuLyBatThuongAffectedLot
                {
                    Id = DbValueHelper.ToInt(row["Id"]),
                    PhieuXuLyBatThuongId = DbValueHelper.ToInt(row["PhieuXuLyBatThuongId"]),
                    SourceType = (AffectedLotSourceType)DbValueHelper.ToInt(row["SourceType"]),
                    SourceReference = DbValueHelper.ToString(row["SourceReference"]),
                    SlotId = ToNullableInt(row["SlotId"]),
                    LotNo = DbValueHelper.ToString(row["LotNo"]),
                    MaSanPham = DbValueHelper.ToString(row["MaSanPham"]),
                    Model = DbValueHelper.ToString(row["Model"]),
                    SoLuongAnhHuong = DbValueHelper.ToInt(row["SoLuongAnhHuong"]),
                    SoLuongDaKiemTra = DbValueHelper.ToInt(row["SoLuongDaKiemTra"]),
                    SoLuongOK = DbValueHelper.ToInt(row["SoLuongOK"]),
                    SoLuongNG = DbValueHelper.ToInt(row["SoLuongNG"]),
                    SoLuongRework = DbValueHelper.ToInt(row["SoLuongRework"]),
                    SoLuongLoaiBo = DbValueHelper.ToInt(row["SoLuongLoaiBo"]),
                    SnapshotAt = DbValueHelper.ToDateTime(row["SnapshotAt"]) ?? DateTime.MinValue,
                    SnapshotBy = DbValueHelper.ToString(row["SnapshotBy"])
                });
            }
            return result;
        }

        public AffectedLotTraceResult Trace(string maSanPham, string lotNo, string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(maSanPham)) throw new ArgumentException("MaSanPham không được rỗng.", nameof(maSanPham));
            if (string.IsNullOrWhiteSpace(lotNo)) throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
            if (string.IsNullOrWhiteSpace(nguoiThucHien)) throw new ArgumentException("NguoiThucHien không được rỗng.", nameof(nguoiThucHien));

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
                    if (lot == null || lot.Quantity <= 0) continue;
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
                AddRows(result, _productionProvider.Trace(result.MaSanPham, result.LotNo), AffectedLotSourceType.SanXuat, nguoiThucHien);
            else
            {
                result.IsComplete = false;
                result.Warnings.Add("Chưa cấu hình provider truy vết LOT tại Sản xuất/WIP.");
            }

            if (_customerReturnProvider != null)
                AddRows(result, _customerReturnProvider.Trace(result.MaSanPham, result.LotNo), AffectedLotSourceType.KhachTra, nguoiThucHien);
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

        private static void AddRows(AffectedLotTraceResult result, IEnumerable<PhieuXuLyBatThuongAffectedLot> rows, AffectedLotSourceType sourceType, string nguoiThucHien)
        {
            if (rows == null) return;
            foreach (var row in rows)
            {
                if (row == null || row.SoLuongAnhHuong <= 0) continue;
                row.SourceType = sourceType;
                row.MaSanPham = string.IsNullOrWhiteSpace(row.MaSanPham) ? result.MaSanPham : row.MaSanPham.Trim();
                row.LotNo = NormalizeLot(row.LotNo ?? result.LotNo);
                row.SnapshotAt = row.SnapshotAt == default(DateTime) ? DateTime.Now : row.SnapshotAt;
                row.SnapshotBy = string.IsNullOrWhiteSpace(row.SnapshotBy) ? nguoiThucHien.Trim() : row.SnapshotBy;
                result.Items.Add(row);
            }
        }

        private static string NormalizeLot(string lotNo)
        {
            return string.IsNullOrWhiteSpace(lotNo) ? string.Empty : lotNo.Trim().ToUpperInvariant();
        }

        private static int? ToNullableInt(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            return DbValueHelper.ToInt(value);
        }
    }

    public sealed class CustomerReturnLotTraceProvider : ICustomerReturnLotTraceProvider
    {
        private readonly IPhieuTraHangRepository _repository;

        public CustomerReturnLotTraceProvider(IPhieuTraHangRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo)
        {
            if (string.IsNullOrWhiteSpace(maSanPham) || string.IsNullOrWhiteSpace(lotNo)) yield break;

            var maHang = maSanPham.Trim();
            var lot = NormalizeLot(lotNo);
            var headers = _repository.GetByNguon(NguonXuLyBatThuong.KhachTra);
            if (headers == null) yield break;

            foreach (var header in headers)
            {
                if (header == null || header.Id <= 0) continue;
                var items = _repository.GetItems(header.Id);
                if (items == null) continue;

                foreach (var item in items)
                {
                    if (item == null || item.SoLuong <= 0) continue;
                    if (!string.Equals(item.MaHang == null ? null : item.MaHang.Trim(), maHang, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(NormalizeLot(item.LotNo), lot, StringComparison.OrdinalIgnoreCase)) continue;

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
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
        }
    }
}
