using PCTP.ClassSQL;
using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// Implementation duy nhất của ITableOrderRepository. Toàn bộ SQL đọc/ghi
    /// Purchase_Order_* — tách nguyên khối từ region "IPhieuOrderTableRepository"
    /// cũ trong PhieuRepository. Cần IIFSRepository để SoSanhDonHangVoiIFS đối chiếu.
    ///
    /// Được <see cref="PCTP.Modules.GiaoHangKhach.TableOrderLoad.OrderTableLoadStrategy"/>
    /// và <see cref="PCTP.Modules.GiaoHangKhach.Services.PhieuService"/> tiêu thụ như một
    /// dependency RIÊNG (không đi qua IPhieuRepository) — xem WORKFLOW_GIAOHANGKHACH.md
    /// mục 3 (Order Load Strategy) để biết vì sao interface này bị tách khỏi PhieuRepository.
    /// </summary>
    public sealed class TableOrderRepo : ITableOrderRepository
    {
        private readonly PhieuSqlExecutor _db;
        private readonly IIFSRepository _ifsRepo;

        public TableOrderRepo(
            PhieuSqlExecutor db,
            IIFSRepository ifsRepo = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ifsRepo = ifsRepo ?? IFSRepository.Create();
        }

        // ============================================================
        // ITableOrderRepository
        // ============================================================

        public DataTable LoadPhieuTuBangRieng(
            string ngayGiao,
            string gioFilter,
            bool isLoaiSP,
            string dockCodeSP,
            CustomerConfig cfg,
            string tenBangOverride = null)
        {
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            string tenBang =
                !string.IsNullOrWhiteSpace(tenBangOverride)
                    ? tenBangOverride
                    : cfg.OrderTable;

            _db.ValidateTableName(tenBang);

            // --------------------------------------------------------
            // Filter giờ
            // --------------------------------------------------------

            string whereGio = "";

            if (!string.IsNullOrWhiteSpace(gioFilter))
            {
                var gioList = gioFilter
                    .Split(',')
                    .Select(g => g.Trim().Trim('\''))
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Select(g =>
                    {
                        if (!int.TryParse(g, out int gio))
                            throw new ArgumentException(
                                $"Giờ giao không hợp lệ: '{g}'");

                        if (gio < 0 || gio > 23)
                            throw new ArgumentException(
                                $"Giờ giao phải nằm trong khoảng 0-23: '{g}'");

                        return gio.ToString();
                    })
                    .Distinct()
                    .ToList();

                if (gioList.Count > 0)
                {
                    whereGio =
                        $"AND DATEPART(HH, o.NgayGiao) IN ({string.Join(",", gioList)}) ";
                }
            }

            // --------------------------------------------------------
            // Filter loại sản phẩm
            // --------------------------------------------------------

            string whereSP = "";

            if (cfg.CoLoaiSP)
            {
                string safeDockCode =
                    (dockCodeSP ?? "").Replace("'", "''");

                whereSP = isLoaiSP
                    ? $"AND RTRIM(o.CUA) = '{safeDockCode}' "
                    : $"AND RTRIM(o.CUA) <> '{safeDockCode}' ";
            }

            // --------------------------------------------------------
            // Query
            // --------------------------------------------------------

            string safeNgayGiao =
                (ngayGiao ?? "").Replace("'", "''");

            string sql =
                "SELECT " +
                "    o.Oder_no                            AS PO_NO, " +
                "    CONVERT(VARCHAR(5), o.NgayGiao, 108) AS GIO, " +
                "    MAX(o.CUA)                           AS CUA, " +
                "    MAX(o.CUA)                           AS TRUYEN, " +
                "    o.Part_no                            AS MAHANG, " +
                "    MAX(o.Part_name)                     AS TENHANG, " +
                "    ''                                   AS LOT, " +
                "    'PCS'                                AS DV, " +
                "    SUM(o.Slgiao)                        AS SOLUONG, " +
                "    MAX(o.QCDG)                          AS HOP, " +
                "    MAX(ISNULL(o.Gear, ''))              AS GEAR, " +
                "    'NG'                                 AS STATUS, " +
                "    'NG'                                 AS STATUSDOC, " +
                "    ''                                   AS TTPHIEU, " +
                "    MIN(o.NgayGiao)                      AS NGAYGIAO, " +
                "    o.Oder_no                            AS ORDER_NO " +
                $"FROM [{tenBang}] o " +
                $"WHERE CAST(o.NgayGiao AS DATE) = '{safeNgayGiao}' " +
                whereGio +
                whereSP +
                "GROUP BY " +
                "    o.Oder_no, " +
                "    o.Part_no, " +
                "    CONVERT(VARCHAR(5), o.NgayGiao, 108) " +
                "ORDER BY " +
                "    GIO, " +
                "    MAX(o.CUA), " +
                "    o.Part_no";

            DataTable dt = _db.LoadData(sql);

            // --------------------------------------------------------
            // STT
            // --------------------------------------------------------

            if (!dt.Columns.Contains("STT"))
                dt.Columns.Add("STT", typeof(int));

            for (int i = 0; i < dt.Rows.Count; i++)
                dt.Rows[i]["STT"] = i + 1;

            // --------------------------------------------------------
            // Merge LOT đã lưu
            // --------------------------------------------------------

            MergeLotTuBangRieng(
                dt,
                ngayGiao,
                cfg.TenNhaMay);

            return dt;
        }

        // ============================================================
        // Load phiếu đang đọc YMVN
        // ============================================================

        public DataTable LoadPhieuDangDocYMVN(
            string tmpTable,
            bool isLoaiSP = false)
        {
            _db.ValidateTableName(tmpTable);

            if (isLoaiSP)
            {
                string sql = $@"
            SELECT
                STT,
                NGAYGIAO AS WANTED_DELIVERY_DATE,
                SLGIAO AS BUY_QTY_DUE,
                'pcs' AS DV,
                '' AS Gear,
                NHAMAY,
                PO_NO AS PO_NO,
                MAHANG AS CUSTOMER_PART_NO,
                TENHANG AS CATALOG_DESC,
                CUA AS DOCK_CODE,
                '' AS HOP,
                '' AS XE,
                LOTNO AS LOT,
                STATUS
            FROM [{tmpTable}]";

                return _db.LoadData(sql);
            }

            string sqlNormal = $@"
            SELECT
                STT,
                NGAYGIAO AS WANTED_DELIVERY_DATE,
                SOLUONG AS BUY_QTY_DUE,
                DV,
                NHAMAY,
                CUA AS PO,
                MAHANG AS CUSTOMER_PART_NO,
                TENHANG AS CATALOG_DESC,
                TRUYEN AS DOCK_CODE,
                '' AS HOP,
                '' AS XE,
                LOT,
                STATUS,
                TTPHIEU AS Gear
            FROM [{tmpTable}]";

            return _db.LoadData(sqlNormal);
        }

        // ============================================================
        // Hàng thiếu YMVN
        // ============================================================

        public DataTable LoadHangThieuYMVN(
            string ngayXuatMDY,
            bool isLoaiSP)
        {
            var dt = new DataTable();

            dt.Columns.Add("MAHANG");
            dt.Columns.Add("SLGIAO", typeof(int));
            dt.Columns.Add("SLTONKHO", typeof(int));
            dt.Columns.Add("SLTHIEU", typeof(int));

            string dockWhere =
                isLoaiSP
                    ? "AND CUA = 'VSP1'"
                    : "AND CUA <> 'VSP1'";

            string safeDate =
                (ngayXuatMDY ?? "").Replace("'", "''");

            string sqlGiao =
                "SELECT " +
                "    Part_no AS MAHANG, " +
                "    SUM(Slgiao) AS SLGIAO " +
                "FROM Purchase_Order_YMVN " +
                $"WHERE CONVERT(VARCHAR(10), NgayGiao, 101) = '{safeDate}' " +
                dockWhere +
                " GROUP BY Part_no";

            DataTable giao = _db.LoadData(sqlGiao);

            foreach (DataRow r in giao.Rows)
            {
                string ma = r["MAHANG"]?.ToString() ?? "";

                int slGiao =
                    DbValueHelper.SafeInt(r["SLGIAO"]);

                object result = _db.ExecuteScalar(
                    @"SELECT ISNULL(SUM(slconlai), 0)
                  FROM STOCKTP
                  WHERE part = @MaHang
                    AND slconlai > 0",
                    new SqlParameter(
                        "@MaHang",
                        SqlDbType.NVarChar,
                        100)
                    {
                        Value = ma
                    });

                int tonKho =
                    result == null || result == DBNull.Value
                        ? 0
                        : Convert.ToInt32(result);

                if (slGiao > tonKho)
                {
                    dt.Rows.Add(
                        ma,
                        slGiao,
                        tonKho,
                        slGiao - tonKho);
                }
            }

            return dt;
        }

        // ============================================================
        // Danh sách giờ YMVN
        // ============================================================

        public IReadOnlyList<string> GetDanhSachGioYMVN(
            string ngayXuatMDY)
        {
            string safeDate =
                (ngayXuatMDY ?? "").Replace("'", "''");

            string sql =
                "SELECT " +
                "    CONVERT(VARCHAR, NgayGiao, 108) AS TIMES " +
                "FROM Purchase_Order_YMVN " +
                $"WHERE CONVERT(VARCHAR(10), NgayGiao, 101) = '{safeDate}' " +
                "GROUP BY NgayGiao " +
                "ORDER BY NgayGiao";

            DataTable dt = _db.LoadData(sql);

            return dt.Rows
                .Cast<DataRow>()
                .Select(r => r["TIMES"]?.ToString() ?? "")
                .ToList();
        }

        // ============================================================
        // Giờ giao YMVN
        // ============================================================

        public IReadOnlyList<string> GetGioGiaoYMVN(
            string ngayGiao)
        {
            string safeDate =
                (ngayGiao ?? "").Replace("'", "''");

            string sql =
                "SELECT DISTINCT " +
                "    RIGHT('0' + CAST(DATEPART(HH, NgayGiao) AS VARCHAR), 2) " +
                "    + ':' + " +
                "    RIGHT('0' + CAST(DATEPART(MI, NgayGiao) AS VARCHAR), 2) " +
                "    AS GIO " +
                "FROM Purchase_Order_YMVN " +
                $"WHERE CAST(NgayGiao AS DATE) = '{safeDate}' " +
                "ORDER BY GIO";

            DataTable dt = _db.LoadData(sql);

            return dt.AsEnumerable()
                .Select(r => r["GIO"]?.ToString() ?? "")
                .ToList();
        }

        // ============================================================
        // Upload MilkRun SP
        // ============================================================

        public void UploadMilkrunSP(
            DataTable donHang,
            string ngayGiao)
        {
            if (donHang == null)
                throw new ArgumentNullException(nameof(donHang));

            foreach (DataRow row in donHang.Rows)
            {
                string dockCode =
                    row["DOCK_CODE"]?.ToString() ?? "";

                if (!dockCode.Contains("VSP"))
                    continue;

                string po =
                    row["PO"]?.ToString() ?? "";

                string pno =
                    row["CUSTOMER_PART_NO"]?.ToString() ?? "";

                string name =
                    row["CATALOG_DESC"]?.ToString() ?? "";

                int sl =
                    DbValueHelper.SafeInt(
                        row["BUY_QTY_DUE"]);

                object checkObj = _db.ExecuteScalar(
                    @"SELECT COUNT(*)
                  FROM Purchase_Order_YMVN
                  WHERE Oder_no = @po
                    AND Part_no = @pno",
                    new SqlParameter(
                        "@po",
                        po),
                    new SqlParameter(
                        "@pno",
                        pno));

                int exists =
                    checkObj == null || checkObj == DBNull.Value
                        ? 0
                        : Convert.ToInt32(checkObj);

                if (exists != 0)
                    continue;

                _db.ExecuteNonQuery(
                    @"INSERT INTO Purchase_Order_YMVN
                (
                    Oder_no,
                    Part_no,
                    Part_name,
                    NgayGiao,
                    Slgiao,
                    QCDG,
                    CUA
                )
                VALUES
                (
                    @po,
                    @pno,
                    @name,
                    @ng,
                    @sl,
                    0,
                    'VSP1'
                )",
                    new SqlParameter("@po", po),
                    new SqlParameter("@pno", pno),
                    new SqlParameter("@name", name),
                    new SqlParameter("@ng", ngayGiao),
                    new SqlParameter("@sl", sl));
            }
        }

        // ============================================================
        // Insert TMP YMVN
        // ============================================================

        public void InsertTmpYMVN(
            string stt,
            string cua,
            string truyen,
            string maHang,
            string tenHang,
            string lot,
            string dv,
            int slXuat,
            string ngayGiao,
            string gear,
            string gioXuat,
            string tmpTable,
            string poNo = "",
            string cusPoNo = "")
        {
            _db.ValidateTableName(tmpTable);

            string sql = $@"
                INSERT INTO [{tmpTable}]
                (
                    STT,
                    CUA,
                    TRUYEN,
                    MAHANG,
                    TENHANG,
                    LOT,
                    DV,
                    SOLUONG,
                    NGAYGIAO,
                    GEAR,
                    GIOGIAO,
                    STATUS,
                    PO_NO,
                    TTPHIEU
                )
                VALUES
                (
                    @STT,
                    @CUA,
                    @TRUYEN,
                    @MAHANG,
                    @TENHANG,
                    @LOT,
                    @DV,
                    @SOLUONG,
                    @NGAYGIAO,
                    @GEAR,
                    @GIOGIAO,
                    'NG',
                    @PO_NO,
                    @CUSPO
                )";

            _db.ExecuteNonQuery(
                sql,
                new SqlParameter("@STT", stt),
                new SqlParameter("@CUA", cua),
                new SqlParameter("@TRUYEN", truyen),
                new SqlParameter("@MAHANG", maHang),
                new SqlParameter("@TENHANG", tenHang),
                new SqlParameter("@LOT", lot),
                new SqlParameter("@DV", dv),
                new SqlParameter("@SOLUONG", slXuat),
                new SqlParameter("@NGAYGIAO", ngayGiao),
                new SqlParameter("@GEAR", gear),
                new SqlParameter("@GIOGIAO", gioXuat),
                new SqlParameter("@PO_NO", poNo),
                new SqlParameter("@CUSPO", cusPoNo));
        }

        // ============================================================
        // So sánh đơn hàng bảng riêng với IFS
        // ============================================================

        public DataTable SoSanhDonHangVoiIFS(
            DataTable donHangBangRieng,
            string ngayGiao,
            CustomerConfig cfg)
        {
            if (donHangBangRieng == null)
                throw new ArgumentNullException(
                    nameof(donHangBangRieng));

            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            var result = new DataTable();

            result.Columns.Add(
                "MAHANG",
                typeof(string));

            result.Columns.Add(
                "SL_BANG_RIENG",
                typeof(int));

            result.Columns.Add(
                "SL_IFS",
                typeof(int));

            result.Columns.Add(
                "CHENH_LECH",
                typeof(int));

            result.Columns.Add(
                "GHI_CHU",
                typeof(string));

            string customerNoIFS =
                !string.IsNullOrEmpty(cfg.CustomerNoIFS)
                    ? cfg.CustomerNoIFS
                    : cfg.CustomerNo;

            if (string.IsNullOrEmpty(customerNoIFS))
                return result;

            // --------------------------------------------------------
            // Tổng số lượng bảng riêng
            // --------------------------------------------------------

            var slBangRieng =
                donHangBangRieng
                    .AsEnumerable()
                    .GroupBy(r =>
                        r["MAHANG"]?.ToString()?.Trim() ?? "")
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(r =>
                            DbValueHelper.SafeInt(
                                r["SOLUONG"])),
                        StringComparer.OrdinalIgnoreCase);

            // --------------------------------------------------------
            // Query IFS
            // --------------------------------------------------------

            DataTable ifsData;

            try
            {
                ifsData =
                    _ifsRepo.GetCustomerOrderJoinYMVN(
                        DateTime.Parse(ngayGiao)
                            .ToString("ddMMyyyy"),
                        customerNoIFS,
                        "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SoSanhDonHangVoiIFS] " +
                    $"Lỗi truy vấn IFS: {ex.Message}");

                return result;
            }

            // --------------------------------------------------------
            // Tổng số lượng IFS
            // --------------------------------------------------------

            var slIfs =
                ifsData
                    .AsEnumerable()
                    .GroupBy(r =>
                        r["MAHANG"]?.ToString()?.Trim() ?? "")
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(r =>
                            DbValueHelper.SafeInt(
                                r["SOLUONG"])),
                        StringComparer.OrdinalIgnoreCase);

            // --------------------------------------------------------
            // Merge danh sách mã
            // --------------------------------------------------------

            var tatCaMaHang =
                slBangRieng.Keys
                    .Union(
                        slIfs.Keys,
                        StringComparer.OrdinalIgnoreCase);

            foreach (string ma in tatCaMaHang)
            {
                int slBR =
                    slBangRieng.TryGetValue(
                        ma,
                        out int a)
                        ? a
                        : 0;

                int slIF =
                    slIfs.TryGetValue(
                        ma,
                        out int b)
                        ? b
                        : 0;

                int chenh =
                    slBR - slIF;

                string ghiChu = "";

                if (slBR > 0 && slIF == 0)
                {
                    ghiChu =
                        "THIẾU_IFS — không thấy PO trên IFS";
                }
                else if (slBR == 0 && slIF > 0)
                {
                    ghiChu =
                        "THIẾU_BANG_RIENG — " +
                        "IFS có đơn nhưng chưa nhập bảng riêng";
                }
                else if (chenh != 0)
                {
                    ghiChu =
                        $"CHÊNH SỐ LƯỢNG: {chenh:+0;-0}";
                }

                if (!string.IsNullOrEmpty(ghiChu))
                {
                    result.Rows.Add(
                        ma,
                        slBR,
                        slIF,
                        chenh,
                        ghiChu);
                }
            }

            return result;
        }

        // ============================================================
        // Private: Merge LOT từ LUUPHIEUGIAOHANG
        // ============================================================

        private void MergeLotTuBangRieng(
            DataTable dt,
            string ngayGiao,
            string tenNhaMay)
        {
            if (dt == null || dt.Rows.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(tenNhaMay))
                return;

            string safeNgay =
                (ngayGiao ?? "").Replace("'", "''");

            string safeNhaMay =
                tenNhaMay.Replace("'", "''");

            string sql =
                "SELECT " +
                "    MAHANG, " +
                "    GIOGIAO, " +
                "    LOT, " +
                "    STATUS, " +
                "    STATUSDOC, " +
                "    ISNULL(PO_NO,'') AS PO_NO, " +
                "    ISNULL(SOLUONG,0) AS SOLUONG, " +
                "    ISNULL(GearYMVN,'') AS GEAR " +
                "FROM LUUPHIEUGIAOHANG " +
                $"WHERE CAST(NGAYGIAO AS DATE) = '{safeNgay}' " +
                $"AND NHAMAY = '{safeNhaMay}'";

            DataTable luuDt =
                _db.LoadData(sql);

            if (luuDt.Rows.Count == 0)
                return;

            var lookup =
                new Dictionary<string, DataRow>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in luuDt.Rows)
            {
                string gioChuan =
                    NormalizeGio(
                        row["GIOGIAO"]?.ToString()?.Trim());

                string maHang =
                    row["MAHANG"]?.ToString()?.Trim() ?? "";

                string poNo =
                    row["PO_NO"]?.ToString()?.Trim() ?? "";

                string keyFull =
                    maHang +
                    "|" +
                    gioChuan +
                    "|" +
                    poNo;

                string keyShort =
                    maHang +
                    "|" +
                    gioChuan;

                if (!lookup.ContainsKey(keyFull))
                    lookup[keyFull] = row;

                if (!lookup.ContainsKey(keyShort))
                    lookup[keyShort] = row;
            }

            if (!dt.Columns.Contains("GearSuDung"))
                dt.Columns.Add(
                    "GearSuDung",
                    typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                string gioChuan =
                    NormalizeGio(
                        row["GIO"]?.ToString()?.Trim());

                string maHang =
                    row["MAHANG"]?.ToString()?.Trim() ?? "";

                string poNo =
                    dt.Columns.Contains("PO_NO")
                        ? row["PO_NO"]?.ToString()?.Trim() ?? ""
                        : "";

                string keyFull =
                    maHang +
                    "|" +
                    gioChuan +
                    "|" +
                    poNo;

                string keyShort =
                    maHang +
                    "|" +
                    gioChuan;

                DataRow luuRow = null;

                if (!lookup.TryGetValue(
                        keyFull,
                        out luuRow))
                {
                    lookup.TryGetValue(
                        keyShort,
                        out luuRow);
                }

                if (luuRow == null)
                    continue;

                string lot =
                    luuRow["LOT"]?.ToString()?.Trim() ?? "";

                if (string.IsNullOrEmpty(lot))
                    continue;

                row["LOT"] =
                    lot;

                row["STATUS"] =
                    luuRow["STATUS"]?.ToString() ?? "NG";

                row["STATUSDOC"] =
                    luuRow["STATUSDOC"]?.ToString() ?? "NG";

                if (dt.Columns.Contains("GEAR"))
                {
                    string gearHienTai =
                        row["GEAR"]?.ToString()?.Trim() ?? "";

                    string gearLuu =
                        luuRow["GEAR"]?.ToString()?.Trim() ?? "";

                    if (string.IsNullOrEmpty(gearHienTai) &&
                        !string.IsNullOrEmpty(gearLuu))
                    {
                        row["GEAR"] =
                            gearLuu;
                    }
                }
            }
        }
        public Dictionary<string, int> GetQcDongGoiBatch(
         List<string> maHangList)
        {
            var result = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            if (maHangList == null || maHangList.Count == 0)
                return result;

            var maHangs = maHangList
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (maHangs.Count == 0)
                return result;

            var parameters = new List<SqlParameter>();

            var placeholders = new List<string>();

            for (int i = 0; i < maHangs.Count; i++)
            {
                string parameterName = "@MAHANG" + i;

                placeholders.Add(parameterName);

                parameters.Add(
                    new SqlParameter(
                        parameterName,
                        SqlDbType.NVarChar,
                        100)
                    {
                        Value = maHangs[i]
                    });
            }

            string sql = $@"
                    SELECT
                        PART_NO AS MAHANG,
                        QCDG
                    FROM QCDONGGOI
                    WHERE PART_NO IN ({string.Join(",", placeholders)})
                ";

            DataTable dt = _db.LoadData(
                sql,
                parameters.ToArray());

            foreach (DataRow row in dt.Rows)
            {
                string maHang =
                    row["MAHANG"]?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(maHang))
                    continue;

                int qc = DbValueHelper.SafeInt(
                    row["QCDG"]);

                result[maHang] = qc;
            }

            return result;
        }
        // ============================================================
        // Private helper
        // ============================================================

        private static string NormalizeGio(string gio)
        {
            if (string.IsNullOrWhiteSpace(gio))
                return "00";

            gio = gio
                .Replace("H", "")
                .Trim();

            int colonIdx =
                gio.IndexOf(':');

            if (colonIdx >= 0)
            {
                gio =
                    gio.Substring(0, colonIdx)
                        .Trim();
            }

            return int.TryParse(
                    gio,
                    out int gioInt)
                ? gioInt.ToString("00")
                : "00";
        }
    }
}
