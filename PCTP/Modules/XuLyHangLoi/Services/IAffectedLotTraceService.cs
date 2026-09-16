using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Truy vết toàn bộ nguồn hàng có cùng LOT bị ảnh hưởng.
    /// Phase 2: đọc -> kiểm tra completeness -> snapshot bền vững.
    /// </summary>
    public interface IAffectedLotTraceService
    {
        AffectedLotTraceResult Trace(string maSanPham, string lotNo, string nguoiThucHien);
        AffectedLotTraceResult TraceForPhieu(PhieuXuLyBatThuong phieu, string nguoiThucHien);
        AffectedLotTraceResult TruyVetLOT(int phieuXuLyId, string nguoiThucHien);
        IReadOnlyList<PhieuXuLyBatThuongAffectedLot> GetSnapshot(int phieuXuLyId);
    }

    public sealed class AffectedLotTraceResult
    {
        public string MaSanPham { get; set; }
        public string LotNo { get; set; }
        public bool IsComplete { get; set; }
        public List<PhieuXuLyBatThuongAffectedLot> Items { get; set; } = new List<PhieuXuLyBatThuongAffectedLot>();
        public List<string> Warnings { get; set; } = new List<string>();

        public int TotalAffectedQuantity
        {
            get
            {
                int total = 0;
                foreach (var item in Items)
                    if (item != null) total += item.SoLuongAnhHuong;
                return total;
            }
        }
    }

    public interface IProductionLotTraceProvider
    {
        IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo);
    }

    public interface ICustomerReturnLotTraceProvider
    {
        IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo);
    }

    /// <summary>
    /// Nguồn LOT tại Sản xuất/WIP của hệ thống hiện tại.
    /// Dữ liệu lấy từ view vNhapTP đã được StockTpProductionRepository sử dụng.
    /// WIP = SL_DA_SAN_XUAT - SL_DA_NHAP - SL_DA_TRA.
    /// Không coi TON_KHO_TP là WIP vì phần đó thuộc nguồn Kho.
    /// </summary>
    public sealed class ProductionLotTraceProvider : IProductionLotTraceProvider
    {
        private readonly PhieuSqlExecutor _db;
        private readonly IUnitOfWork _uow;

        public ProductionLotTraceProvider(PhieuSqlExecutor db, IUnitOfWork uow)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        public IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo)
        {
            if (string.IsNullOrWhiteSpace(maSanPham) || string.IsNullOrWhiteSpace(lotNo))
                return new List<PhieuXuLyBatThuongAffectedLot>();

            const string sql = @"
SELECT
    STT,
    FIND,
    LOT_NO,
    MODEL,
    MA_SAN_PHAM,
    SL_DA_SAN_XUAT,
    SL_DA_NHAP,
    SL_DA_TRA
FROM vNhapTP
WHERE MA_SAN_PHAM = @MaSanPham
  AND LOT_NO = @LotNo;";

            var table = new SqlRepositoryBaseAdapter(_db, _uow).Load(
                sql,
                new SqlParameter("@MaSanPham", SqlDbType.NVarChar, 100) { Value = maSanPham.Trim() },
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 100) { Value = NormalizeLot(lotNo) });

            var result = new List<PhieuXuLyBatThuongAffectedLot>();
            if (table == null)
                return result;

            foreach (DataRow row in table.Rows)
            {
                int produced = ToInt(row["SL_DA_SAN_XUAT"]);
                int received = ToInt(row["SL_DA_NHAP"]);
                int returned = ToInt(row["SL_DA_TRA"]);
                int wip = produced - received - returned;

                if (wip <= 0)
                    continue;

                result.Add(new PhieuXuLyBatThuongAffectedLot
                {
                    SourceType = AffectedLotSourceType.SanXuat,
                    SourceReference = "V_NHAP_TP:" + ToString(row["FIND"]),
                    SlotId = null,
                    LotNo = ToString(row["LOT_NO"]),
                    MaSanPham = ToString(row["MA_SAN_PHAM"]),
                    Model = ToString(row["MODEL"]),
                    SoLuongAnhHuong = wip
                });
            }

            return result;
        }

        private static string NormalizeLot(string lotNo)
        {
            return (lotNo ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            int result;
            return int.TryParse(value.ToString(), out result) ? result : 0;
        }

        private static string ToString(object value)
        {
            return value == null || value == DBNull.Value ? null : value.ToString();
        }

        /// <summary>
        /// Chỉ dùng để tái sử dụng SqlRepositoryBase.LoadData mà không tạo thêm
        /// một file source mới phải đăng ký vào csproj cũ.
        /// </summary>
        private sealed class SqlRepositoryBaseAdapter : SqlRepositoryBase
        {
            public SqlRepositoryBaseAdapter(PhieuSqlExecutor db, IUnitOfWork uow)
                : base(db, uow) { }

            public DataTable Load(string sql, params SqlParameter[] parameters)
            {
                return LoadData(sql, parameters);
            }
        }
    }
}
