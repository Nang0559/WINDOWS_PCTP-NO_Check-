using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.NhapKho.Repository
{
   

    public sealed class StockTpProductionRepository
        : SqlRepositoryBase,
          IStockTpProductionRepository
    {
        public StockTpProductionRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // PHIẾU SẢN XUẤT
        // ============================================================

        public PhieuNhapInfo GetPhieuByFind(
            string find)
        {
            if (string.IsNullOrWhiteSpace(find))
                return null;

            const string sql = @"
SELECT
    STT,
    FIND,
    LOT_NO,
    MODEL,
    TEN_SAN_PHAM,
    MA_SAN_PHAM,
    CA_SAN_XUAT,
    NGAY_SAN_XUAT,
    SL_DA_SAN_XUAT,
    SL_DA_NHAP,
    SL_DA_TRA,
    LY_DO_TRA,
    TON_KHO_TP,
    KET_THUC_LOT
FROM vNhapTP
WHERE FIND = @Find;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter(
                    "@Find",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = find.Trim()
                });

            if (dt == null || dt.Rows.Count == 0)
                return null;

            return MapPhieu(dt.Rows[0]);
        }


        // ============================================================
        // TOÀN BỘ PHIẾU
        // ============================================================

        public List<PhieuNhapInfo> GetPhieuTong()
        {
            const string sql = @"
SELECT
    STT,
    FIND,
    LOT_NO,
    MODEL,
    TEN_SAN_PHAM,
    MA_SAN_PHAM,
    CA_SAN_XUAT,
    NGAY_SAN_XUAT,
    SL_DA_SAN_XUAT,
    SL_DA_NHAP,
    SL_DA_TRA,
    LY_DO_TRA,
    TON_KHO_TP,
    KET_THUC_LOT
FROM vNhapTP
ORDER BY NGAY_SAN_XUAT DESC;";

            DataTable dt = LoadData(sql);

            if (dt == null || dt.Rows.Count == 0)
                return new List<PhieuNhapInfo>();

            return dt.Rows
                .Cast<DataRow>()
                .Select(MapPhieu)
                .ToList();
        }


        // ============================================================
        // PHIẾU ĐANG SẢN XUẤT
        // ============================================================

        public List<PhieuNhapInfo> GetPhieuDangSanXuat(
            int soNgayGanDay = 30)
        {
            if (soNgayGanDay < 0)
                soNgayGanDay = 30;

            const string sql = @"
SELECT
    STT,
    FIND,
    LOT_NO,
    MODEL,
    TEN_SAN_PHAM,
    MA_SAN_PHAM,
    CA_SAN_XUAT,
    NGAY_SAN_XUAT,
    SL_DA_SAN_XUAT,
    SL_DA_NHAP,
    SL_DA_TRA,
    LY_DO_TRA,
    TON_KHO_TP,
    KET_THUC_LOT
FROM vNhapTP
WHERE NGAY_SAN_XUAT >=
      DATEADD(
          DAY,
          -@SoNgay,
          CAST(GETDATE() AS DATE)
      )
ORDER BY
    NGAY_SAN_XUAT DESC,
    STT DESC;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter(
                    "@SoNgay",
                    SqlDbType.Int)
                {
                    Value = soNgayGanDay
                });

            if (dt == null || dt.Rows.Count == 0)
                return new List<PhieuNhapInfo>();

            return dt.Rows
                .Cast<DataRow>()
                .Select(MapPhieu)
                .ToList();
        }


        // ============================================================
        // TÌM PHIẾU TỪ QR
        // ============================================================

        public PhieuNhapInfo TimPhieuTheoLotQR(
            string rawLotNoSL,
            string maHang)
        {
            if (string.IsNullOrWhiteSpace(rawLotNoSL))
                return null;

            if (string.IsNullOrWhiteSpace(maHang))
                return null;

            // --------------------------------------------------------
            // BƯỚC 1:
            // Lấy ID của mã hàng và chuẩn hóa thành 5 ký tự.
            // --------------------------------------------------------

            const string sqlGetId = @"
SELECT
    STUFF(
        '00000',
        5 - LEN(ID) + 1,
        LEN(ID),
        ID
    )
FROM B20Item
WHERE CODE = @MaHang;";

            object idResult = ExecuteScalar(
                sqlGetId,
                new SqlParameter(
                    "@MaHang",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = maHang.Trim()
                });

            string idPadded = idResult == null ||
                              idResult == DBNull.Value
                ? null
                : idResult.ToString();

            if (string.IsNullOrWhiteSpace(idPadded))
                return null;

            // --------------------------------------------------------
            // BƯỚC 2:
            // Build các FIND candidate từ QR.
            //
            // Giữ nguyên logic cũ:
            // LotCodeHelper.BuildCandidateFinds(...)
            // --------------------------------------------------------

            var finds =
                LotCodeHelper.BuildCandidateFinds(
                    rawLotNoSL,
                    idPadded);

            var candidates = finds
                .Select(GetPhieuByFind)
                .Where(p =>
                    p != null &&
                    string.Equals(
                        p.MaSP,
                        maHang,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Khớp duy nhất
            if (candidates.Count == 1)
                return candidates[0];

            // Có nhiều phiếu cùng khớp
            if (candidates.Count > 1)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TimPhieuTheoLotQR] " +
                    $"Khớp {candidates.Count} phiếu " +
                    $"cho QR={rawLotNoSL}.");

                return null;
            }

            // --------------------------------------------------------
            // BƯỚC 3:
            // FALLBACK bằng LOT prefix.
            // --------------------------------------------------------

            if (rawLotNoSL.Length < 11)
                return null;

            string prefix11 =
                LotCodeHelper.GetReliablePrefix(
                    rawLotNoSL,
                    out string ca);

            if (string.IsNullOrWhiteSpace(prefix11))
                return null;

            PhieuNhapInfo phieu = null;

            // Ưu tiên prefix + ca
            if (!string.IsNullOrWhiteSpace(ca))
            {
                phieu = GetPhieuByLotPrefix(
                    prefix11 + ca,
                    maHang);
            }

            // Không có -> thử prefix
            if (phieu == null)
            {
                phieu = GetPhieuByLotPrefix(
                    prefix11,
                    maHang);
            }

            return phieu;
        }


        // ============================================================
        // PRIVATE:
        // TÌM PHIẾU THEO LOT PREFIX
        // ============================================================

        private PhieuNhapInfo GetPhieuByLotPrefix(
            string lotPrefix,
            string maHang)
        {
            if (string.IsNullOrWhiteSpace(lotPrefix))
                return null;

            if (string.IsNullOrWhiteSpace(maHang))
                return null;

            const string sql = @"
SELECT
    STT,
    FIND,
    LOT_NO,
    MODEL,
    TEN_SAN_PHAM,
    MA_SAN_PHAM,
    CA_SAN_XUAT,
    NGAY_SAN_XUAT,
    SL_DA_SAN_XUAT,
    SL_DA_NHAP,
    SL_DA_TRA,
    LY_DO_TRA,
    TON_KHO_TP,
    KET_THUC_LOT
FROM vNhapTP
WHERE LOT_NO LIKE @Prefix + '%'
  AND MA_SAN_PHAM = @MaHang;";

            DataTable dt = LoadData(
                sql,

                new SqlParameter(
                    "@Prefix",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = lotPrefix.Trim()
                },

                new SqlParameter(
                    "@MaHang",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = maHang.Trim()
                });

            if (dt == null || dt.Rows.Count == 0)
                return null;

            // --------------------------------------------------------
            // QUAN TRỌNG:
            // Prefix phải khớp DUY NHẤT.
            //
            // Nếu > 1 thì không được tự chọn.
            // Tránh nhập nhầm LOT.
            // --------------------------------------------------------

            if (dt.Rows.Count > 1)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[GetPhieuByLotPrefix] " +
                    $"Prefix [{lotPrefix}] khớp {dt.Rows.Count} phiếu.");

                return null;
            }

            return MapPhieu(dt.Rows[0]);
        }


        // ============================================================
        // MAPPER
        // ============================================================

        private static PhieuNhapInfo MapPhieu(
            DataRow r)
        {
            if (r == null)
                return null;

            return new PhieuNhapInfo
            {
                Stt = DbValueHelper.SafeInt(r["STT"]),

                Find = DbValueHelper.SafeString(
                    r["FIND"]),

                LotNo = DbValueHelper.SafeString(
                    r["LOT_NO"]),
                    
                Model = DbValueHelper.SafeString(
                    r["MODEL"]),

                TenSP = DbValueHelper.SafeString(
                    r["TEN_SAN_PHAM"]),

                MaSP = DbValueHelper.SafeString(
                    r["MA_SAN_PHAM"]),

                CaSX = DbValueHelper.SafeInt(
                    r["CA_SAN_XUAT"]),

                NgaySX = DbValueHelper.SafeDate(
                    r["NGAY_SAN_XUAT"]),

                SlSanXuat = DbValueHelper.SafeInt(
                    r["SL_DA_SAN_XUAT"]),

                SlDaNhap = DbValueHelper.SafeInt(
                    r["SL_DA_NHAP"]),

                SlDaTra = DbValueHelper.SafeInt(
                    r["SL_DA_TRA"]),

                LyDoTra = DbValueHelper.SafeString(
                    r["LY_DO_TRA"]),

                TonKhoTP = DbValueHelper.SafeInt(
                    r["TON_KHO_TP"]),

                KetThucLot =
                   DbValueHelper.SafeInt(r["KET_THUC_LOT"]) == 1
            };
        }


    }
}
