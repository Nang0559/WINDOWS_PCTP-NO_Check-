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

    public sealed class ProductionLotTraceProvider : SqlRepositoryBase, IProductionLotTraceProvider
    {
        public ProductionLotTraceProvider(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow) { }

        public IEnumerable<PhieuXuLyBatThuongAffectedLot> Trace(string maSanPham, string lotNo)
        {
            var result = new List<PhieuXuLyBatThuongAffectedLot>();
            if (string.IsNullOrWhiteSpace(maSanPham) || string.IsNullOrWhiteSpace(lotNo))
                return result;

            const string sql = @"
SELECT FIND, LOT_NO, MODEL, MA_SAN_PHAM,
       SL_DA_SAN_XUAT, SL_DA_NHAP, SL_DA_TRA
FROM vNhapTP
WHERE MA_SAN_PHAM = @MaSanPham
  AND LOT_NO = @LotNo;";

            var table = LoadData(
                sql,
                new SqlParameter("@MaSanPham", SqlDbType.NVarChar, 100) { Value = maSanPham.Trim() },
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 100) { Value = NormalizeLot(lotNo) });

            foreach (DataRow row in table.Rows)
            {
                int produced = ToInt(row["SL_DA_SAN_XUAT"]);
                int received = ToInt(row["SL_DA_NHAP"]);
                int returned = ToInt(row["SL_DA_TRA"]);
                int wip = produced - received - returned;
                if (wip <= 0) continue;

                result.Add(new PhieuXuLyBatThuongAffectedLot
                {
                    SourceType = AffectedLotSourceType.SanXuat,
                    SourceReference = "V_NHAP_TP:" + ToText(row["FIND"]),
                    SlotId = null,
                    LotNo = ToText(row["LOT_NO"]),
                    MaSanPham = ToText(row["MA_SAN_PHAM"]),
                    Model = ToText(row["MODEL"]),
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

        private static string ToText(object value)
        {
            return value == null || value == DBNull.Value ? null : value.ToString();
        }
    }
}
