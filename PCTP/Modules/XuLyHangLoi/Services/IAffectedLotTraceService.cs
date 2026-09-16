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

    /// <summary>
    /// Phase 3: ket qua QC ban dau theo tung dong LOT snapshot.
    /// Bat buoc kiem tra het TotalAffectedQuantity truoc khi confirm.
    /// </summary>
    public sealed class InitialQCLotResult
    {
        public int AffectedLotId { get; set; }
        public int SoLuongDaKiemTra { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongLoaiBo { get; set; }
    }

    public sealed class InitialQCResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongAnhHuong { get; set; }
        public int SoLuongDaKiemTra { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongLoaiBoBanDau { get; set; }
        public string NoiDungKiemTra { get; set; }
        public string KetLuan { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }
    }

    public interface IInitialQCService
    {
        InitialQCResult Confirm(
            int phieuXuLyId,
            IReadOnlyList<InitialQCLotResult> lotResults,
            string noiDungKiemTra,
            string ketLuan,
            string nguoiQC);

        InitialQCResult Get(int phieuXuLyId);
    }

    public sealed class InitialQCService : SqlRepositoryBase, IInitialQCService
    {
        private readonly IPhieuXuLyBatThuongRepository _phieuRepository;
        private readonly IAffectedLotTraceService _traceService;

        public InitialQCService(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            IPhieuXuLyBatThuongRepository phieuRepository,
            IAffectedLotTraceService traceService)
            : base(db, uow)
        {
            _phieuRepository = phieuRepository ?? throw new ArgumentNullException(nameof(phieuRepository));
            _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
        }

        public InitialQCResult Confirm(
            int phieuXuLyId,
            IReadOnlyList<InitialQCLotResult> lotResults,
            string noiDungKiemTra,
            string ketLuan,
            string nguoiQC)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentException("phieuXuLyId không hợp lệ.", nameof(phieuXuLyId));
            if (string.IsNullOrWhiteSpace(nguoiQC))
                throw new ArgumentException("NguoiQC không được rỗng.", nameof(nguoiQC));
            if (lotResults == null || lotResults.Count == 0)
                throw new ArgumentException("Phải có kết quả QC theo từng LOT.", nameof(lotResults));

            var phieu = _phieuRepository.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException($"Không tìm thấy PhieuXuLyBatThuong Id={phieuXuLyId}.");

            if (phieu.Status != QTChungStatus.DaDinhHuong)
                throw new InvalidOperationException($"Chỉ được QC ban đầu khi phiếu đang DaDinhHuong. Hiện tại: {phieu.Status}.");

            var snapshot = _traceService.GetSnapshot(phieuXuLyId);
            if (snapshot == null || snapshot.Count == 0)
                throw new InvalidOperationException("Chưa có snapshot LOT để thực hiện QC ban đầu.");

            int totalAffected = 0;
            var snapshotIds = new HashSet<int>();
            foreach (var lot in snapshot)
            {
                if (lot == null || lot.Id <= 0 || lot.SoLuongAnhHuong <= 0)
                    continue;
                totalAffected += lot.SoLuongAnhHuong;
                snapshotIds.Add(lot.Id);
            }

            if (totalAffected <= 0)
                throw new InvalidOperationException("Tổng số lượng LOT bị ảnh hưởng phải lớn hơn 0.");

            int inspected = 0;
            int ok = 0;
            int ng = 0;
            int rework = 0;
            int scrap = 0;
            var seen = new HashSet<int>();

            foreach (var item in lotResults)
            {
                if (item == null)
                    throw new InvalidOperationException("Kết quả QC LOT không được null.");
                if (!snapshotIds.Contains(item.AffectedLotId))
                    throw new InvalidOperationException($"AffectedLotId={item.AffectedLotId} không thuộc snapshot của phiếu.");
                if (!seen.Add(item.AffectedLotId))
                    throw new InvalidOperationException($"LOT Id={item.AffectedLotId} bị gửi QC trùng.");
                if (item.SoLuongDaKiemTra < 0 || item.SoLuongOK < 0 || item.SoLuongNG < 0 ||
                    item.SoLuongRework < 0 || item.SoLuongLoaiBo < 0)
                    throw new InvalidOperationException("Số lượng QC không được âm.");
                if (item.SoLuongDaKiemTra != item.SoLuongOK + item.SoLuongNG)
                    throw new InvalidOperationException($"LOT Id={item.AffectedLotId}: QC phải bằng OK + NG.");
                if (item.SoLuongNG != item.SoLuongRework + item.SoLuongLoaiBo)
                    throw new InvalidOperationException($"LOT Id={item.AffectedLotId}: NG phải bằng Rework + Loại bỏ ban đầu.");

                var source = FindSnapshot(snapshot, item.AffectedLotId);
                if (item.SoLuongDaKiemTra > source.SoLuongAnhHuong)
                    throw new InvalidOperationException($"LOT Id={item.AffectedLotId}: số lượng QC vượt số lượng ảnh hưởng.");

                inspected += item.SoLuongDaKiemTra;
                ok += item.SoLuongOK;
                ng += item.SoLuongNG;
                rework += item.SoLuongRework;
                scrap += item.SoLuongLoaiBo;
            }

            if (inspected != totalAffected)
                throw new InvalidOperationException($"QC ban đầu phải kiểm tra đủ {totalAffected:n0}. Hiện mới {inspected:n0}.");
            if (ok + ng != inspected)
                throw new InvalidOperationException("Tổng QC phải bằng OK + NG.");
            if (ng != rework + scrap)
                throw new InvalidOperationException("NG phải bằng Rework + Loại bỏ ban đầu.");

            if (phieu.HuongXuLy == HuongXuLyBatThuong.TuChoiGiaoBu && ng > 0)
                throw new InvalidOperationException("Hướng Từ chối giao bù không thể có NG sau QC ban đầu.");
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework && rework > 0)
                throw new InvalidOperationException("Chỉ hướng CanRework mới được phân bổ số lượng Rework.");

            var existing = Get(phieuXuLyId);
            if (existing != null)
                throw new InvalidOperationException("Phiếu đã có kết quả QC ban đầu. Không ghi đè kết quả đã xác nhận.");

            try
            {
                Uow.Begin();

                int qcId = Convert.ToInt32(ExecuteScalar(@"
INSERT INTO FVN_PhieuXuLyBatThuongQCInitial
(
    PhieuXuLyBatThuongId,
    SoLuongAnhHuong,
    SoLuongDaKiemTra,
    SoLuongOK,
    SoLuongNG,
    SoLuongRework,
    SoLuongLoaiBoBanDau,
    NoiDungKiemTra,
    KetLuan,
    ConfirmedAt,
    ConfirmedBy
)
OUTPUT INSERTED.Id
VALUES
(
    @PhieuXuLyBatThuongId,
    @SoLuongAnhHuong,
    @SoLuongDaKiemTra,
    @SoLuongOK,
    @SoLuongNG,
    @SoLuongRework,
    @SoLuongLoaiBoBanDau,
    @NoiDungKiemTra,
    @KetLuan,
    GETDATE(),
    @ConfirmedBy
);",
                    new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId),
                    new SqlParameter("@SoLuongAnhHuong", totalAffected),
                    new SqlParameter("@SoLuongDaKiemTra", inspected),
                    new SqlParameter("@SoLuongOK", ok),
                    new SqlParameter("@SoLuongNG", ng),
                    new SqlParameter("@SoLuongRework", rework),
                    new SqlParameter("@SoLuongLoaiBoBanDau", scrap),
                    new SqlParameter("@NoiDungKiemTra", DbValueHelper.DbValue(noiDungKiemTra)),
                    new SqlParameter("@KetLuan", DbValueHelper.DbValue(ketLuan)),
                    new SqlParameter("@ConfirmedBy", DbValueHelper.DbValue(nguoiQC))));

                foreach (var item in lotResults)
                {
                    ExecuteNonQuery(@"
UPDATE FVN_PhieuXuLyBatThuongAffectedLot
SET
    SoLuongDaKiemTra = @SoLuongDaKiemTra,
    SoLuongOK = @SoLuongOK,
    SoLuongNG = @SoLuongNG,
    SoLuongRework = @SoLuongRework,
    SoLuongLoaiBo = @SoLuongLoaiBo
WHERE Id = @Id
  AND PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId;",
                        new SqlParameter("@SoLuongDaKiemTra", item.SoLuongDaKiemTra),
                        new SqlParameter("@SoLuongOK", item.SoLuongOK),
                        new SqlParameter("@SoLuongNG", item.SoLuongNG),
                        new SqlParameter("@SoLuongRework", item.SoLuongRework),
                        new SqlParameter("@SoLuongLoaiBo", item.SoLuongLoaiBo),
                        new SqlParameter("@Id", item.AffectedLotId),
                        new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId));
                }

                Uow.Commit();
                return Get(phieuXuLyId) ?? throw new InvalidOperationException("Không đọc lại được kết quả QC ban đầu sau khi lưu.");
            }
            catch
            {
                try { Uow.Rollback(); } catch { }
                throw;
            }
        }

        public InitialQCResult Get(int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                return null;

            var table = LoadData(@"
SELECT TOP 1
    Id,
    PhieuXuLyBatThuongId,
    SoLuongAnhHuong,
    SoLuongDaKiemTra,
    SoLuongOK,
    SoLuongNG,
    SoLuongRework,
    SoLuongLoaiBoBanDau,
    NoiDungKiemTra,
    KetLuan,
    ConfirmedAt,
    ConfirmedBy
FROM FVN_PhieuXuLyBatThuongQCInitial
WHERE PhieuXuLyBatThuongId = @PhieuXuLyBatThuongId;",
                new SqlParameter("@PhieuXuLyBatThuongId", phieuXuLyId));

            if (table.Rows.Count == 0)
                return null;

            var row = table.Rows[0];
            return new InitialQCResult
            {
                Id = DbValueHelper.ToInt(row["Id"]),
                PhieuXuLyBatThuongId = DbValueHelper.ToInt(row["PhieuXuLyBatThuongId"]),
                SoLuongAnhHuong = DbValueHelper.ToInt(row["SoLuongAnhHuong"]),
                SoLuongDaKiemTra = DbValueHelper.ToInt(row["SoLuongDaKiemTra"]),
                SoLuongOK = DbValueHelper.ToInt(row["SoLuongOK"]),
                SoLuongNG = DbValueHelper.ToInt(row["SoLuongNG"]),
                SoLuongRework = DbValueHelper.ToInt(row["SoLuongRework"]),
                SoLuongLoaiBoBanDau = DbValueHelper.ToInt(row["SoLuongLoaiBoBanDau"]),
                NoiDungKiemTra = DbValueHelper.ToString(row["NoiDungKiemTra"]),
                KetLuan = DbValueHelper.ToString(row["KetLuan"]),
                ConfirmedAt = DbValueHelper.ToDateTime(row["ConfirmedAt"]) ?? DateTime.MinValue,
                ConfirmedBy = DbValueHelper.ToString(row["ConfirmedBy"])
            };
        }

        private static PhieuXuLyBatThuongAffectedLot FindSnapshot(
            IReadOnlyList<PhieuXuLyBatThuongAffectedLot> snapshot,
            int id)
        {
            foreach (var item in snapshot)
                if (item != null && item.Id == id)
                    return item;
            throw new InvalidOperationException($"Không tìm thấy AffectedLot Id={id}.");
        }
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
