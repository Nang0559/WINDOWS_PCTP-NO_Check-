using DevExpress.CodeParser;
using DevExpress.Utils.Gesture;
using DevExpress.XtraRichEdit.Import.Html;
using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.DHRepository;
using PCTP.Domain.Interfaces;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Infrastructure.Repositories
{
    public interface IIFSProviderAdapter
    {
        DataTable ExecuteQuery(string sql);
    }

    internal sealed class IFSProviderAdapter : IIFSProviderAdapter
    {
        private readonly IFSPROVIDER _inner;
        public IFSProviderAdapter(IFSPROVIDER inner) => _inner = inner;
        public DataTable ExecuteQuery(string sql) => _inner.ExecuteQuery(sql);
    }
    public class IFSRepository : IIFSRepository
    {
        private readonly IIFSProviderAdapter _ifs;

        public IFSRepository(IIFSProviderAdapter ifs) => _ifs = ifs;

        public static IFSRepository Create() =>
            new IFSRepository(new IFSProviderAdapter(new IFSPROVIDER()));

        // ── Overload 1: load phiếu thường, hinhThucIn = 1 cố định ───────────
        public DataTable GetCustomerOrderJoin(
            string ngayXuat, string gioXuat, string gioXuatH,
            string nhaMay, int addNm,
            CustomerConfig cfg)
            => GetCustomerOrderJoin(ngayXuat, gioXuat, gioXuatH,
                                    nhaMay, addNm, hinhThucIn: 1, cfg);

        // ── Overload 2: overload chính, nhận hinhThucIn + cfg ────────────────
        public DataTable GetCustomerOrderJoin(
            string ngayXuat, string gioXuat, string gioXuatH,
            string nhaMay, int addNm, int hinhThucIn,
            CustomerConfig cfg)
        {
            string inClause = BuildOracleInClause(gioXuat);
            string customerNo = EscapeOracle(cfg.CustomerNo); // ← cfg, không hardcode
            string nhaMayCase = cfg.NhaMayCase;               // ← SQL fragment, KHÔNG EscapeOracle

            string sql =
     "SELECT '' AS STT, " +
     "  (col.CUSTOMER_PART_NO || TO_CHAR(col.BUY_QTY_DUE)) AS FIND, " +
     "  col.CUSTOMER_NO, " +
     "  TO_CHAR(col.WANTED_DELIVERY_DATE,'HH24') AS GIOGIAO, " +
     "  col.BUY_QTY_DUE             AS SOLUONG, " +
     "  col.CUSTOMER_PART_UNIT_MEAS  AS DV, " +
     "  col.SHIP_ADDR_NO             AS ADDNM, " +
     "  col.SUB_DOCK_CODE            AS CUA, " +
     "  col.CUSTOMER_PART_NO         AS MAHANG, " +
     "  col.CATALOG_DESC             AS TENHANG, " +
     "  col.CUSTOMER_PO_REL_NO, " +
     "  col.DOCK_CODE                AS TRUYEN, " +
     "  col.ORDER_NO, " +
     "  col.CATALOG_NO, " +
     "  TO_CHAR(col.WANTED_DELIVERY_DATE,'YYYY-MM-DD') AS NGAYGIAO, " +
     "  col.CUSTOMER_PO_NO, " +
     "  '' AS HOP, '' AS LOT, '' AS DIA_CHI, '' AS KGX, " +
     "  'NG' AS STATUS, 'NG' AS STATUSDOC, '' AS TTPHIEU, " +
     $"  '{EscapeOracle(gioXuatH)}' AS GIOGIAOFCC, " +
     "  col.SHIP_ADDR_NO             AS SHIP_ADDR_NO, " +
     $"  {nhaMayCase} AS NHAMAY, " +
     "  '' AS NOTE, " +
     "  csl.MANUFACTURING_DEPARTMENT AS PO_NO, " +
     "  csl.PATTERN_DESCRIPTION      AS PO_ITEM " +
     "FROM CUSTOMER_ORDER_JOIN col " +
     "LEFT JOIN ( " +
     "  SELECT CUSTOMER_NO, SHIP_ADDR_NO, CUSTOMER_PART_NO, CUSTOMER_PO_NO, " +
     "         MAX(MANUFACTURING_DEPARTMENT) AS MANUFACTURING_DEPARTMENT, " +
     "         MAX(PATTERN_DESCRIPTION) AS PATTERN_DESCRIPTION " +
     "  FROM CUST_SCHED_LINE_TAB " +
     "  WHERE MANUFACTURING_DEPARTMENT IS NOT NULL " +
     "    AND (DOC_NO, CUSTOMER_NO, SHIP_ADDR_NO, CUSTOMER_PART_NO, CUSTOMER_PO_NO) IN ( " +
     "        SELECT MAX(s.DOC_NO), s.CUSTOMER_NO, s.SHIP_ADDR_NO, s.CUSTOMER_PART_NO, s.CUSTOMER_PO_NO " +
     "        FROM CUST_SCHED_LINE_TAB s " +
     "        WHERE s.MANUFACTURING_DEPARTMENT IS NOT NULL " +
     "        GROUP BY s.CUSTOMER_NO, s.SHIP_ADDR_NO, s.CUSTOMER_PART_NO, s.CUSTOMER_PO_NO " +
     "    ) " +
     "  GROUP BY CUSTOMER_NO, SHIP_ADDR_NO, CUSTOMER_PART_NO, CUSTOMER_PO_NO " +
     ") csl " +
     "  ON  csl.CUSTOMER_NO      = col.CUSTOMER_NO " +
     "  AND csl.SHIP_ADDR_NO     = col.SHIP_ADDR_NO " +
     "  AND csl.CUSTOMER_PART_NO = col.CUSTOMER_PART_NO " +
     "  AND csl.CUSTOMER_PO_NO   = col.CUSTOMER_PO_NO " +
     $"WHERE col.CUSTOMER_NO = '{customerNo}' " +
     "AND (col.OBJSTATE = (SELECT CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') FROM DUAL) " +
     "  OR col.OBJSTATE = (SELECT CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') FROM DUAL)) " +
     $"AND TO_CHAR(col.WANTED_DELIVERY_DATE,'ddmmyyyy') = '{ngayXuat}' " +
     (cfg.RequirePoRelNo ? "AND col.CUSTOMER_PO_REL_NO IS NOT NULL " : "");

            switch (hinhThucIn)
            {
                case 1: // load phiếu thường + in theo nhà máy + giờ
                    sql += $"AND col.SHIP_ADDR_NO = {addNm} ";
                    // ✅ Chỉ filter giờ nếu có inClause — rỗng = LoadTheoNgay
                    if (!string.IsNullOrEmpty(inClause))
                        sql += $"AND TO_CHAR(col.WANTED_DELIVERY_DATE,'HH24') IN ({inClause}) ";
                    break;

                case 2: // in toàn bộ nhà máy, không lọc giờ
                    sql += $"AND col.SHIP_ADDR_NO = {addNm} ";
                    break;

                case 3: // in theo giờ, không lọc nhà máy
                    if (!string.IsNullOrEmpty(inClause))
                        sql += $"AND TO_CHAR(col.WANTED_DELIVERY_DATE,'HH24') IN ({inClause}) ";
                    break;

                default:
                    sql += $"AND col.SHIP_ADDR_NO = {addNm} ";
                    if (!string.IsNullOrEmpty(inClause))
                        sql += $"AND TO_CHAR(col.WANTED_DELIVERY_DATE,'HH24') IN ({inClause}) ";
                    break;
            }



            sql += "ORDER BY col.WANTED_DELIVERY_DATE, col.SUB_DOCK_CODE, " +
                   "col.CUSTOMER_PART_NO, col.BUY_QTY_DUE";

            return _ifs.ExecuteQuery(sql);
        }

        public DataTable GetCustomerOrderJoinYMVN(string ngayXuat,
                                            string customerNo,
                                            string dockFilter)
        {
            string sql =
                "SELECT " +
                "  '' AS STT, " +
                // ── Các cột dùng chung với grid 100001 ──────────────────────
                "  TO_CHAR(WANTED_DELIVERY_DATE,'HH24') AS GIOGIAO, " +  // ← thêm
                "  SUB_DOCK_CODE                        AS CUA, " +       // ← đổi PO → CUA
                "  DOCK_CODE                            AS TRUYEN, " +    // ← thêm alias
                "  CUSTOMER_PART_NO                     AS MAHANG, " +    // ← thêm alias
                "  CATALOG_DESC                         AS TENHANG, " +   // ← thêm alias
                "  BUY_QTY_DUE                          AS SOLUONG, " +   // ← thêm alias
                "  CUSTOMER_PART_UNIT_MEAS              AS DV, " +
                "  SHIP_ADDR_NO                         AS ADDNM, " +
                "  CUSTOMER_PO_NO, " +
                "  TO_CHAR(WANTED_DELIVERY_DATE,'YYYY-MM-DD') AS NGAYGIAO, " +
                // ── Cột bổ sung của YMVN ────────────────────────────────────
                "  '' AS HOP, " +
                "  '' AS LOT, " +
                "  '' AS Gear, " +
                "  '' AS XE, " +
                "  'NG' AS STATUS, " +
                "  'NG' AS STATUSDOC, " +
                "  '' AS TTPHIEU, " +
                "  '' AS NOTE, " +
                "  '' AS PO_NO, " +
                "  '' AS PO_ITEM, " +
                "  '' AS KGX, " +
                "  '' AS DIA_CHI " +
               $"FROM CUSTOMER_ORDER_JOIN " +
               $"WHERE CUSTOMER_NO = '{EscapeOracle(customerNo)}' " +
                "AND (OBJSTATE <> (SELECT CUSTOMER_ORDER_LINE_API." +
                "FINITE_STATE_ENCODE__('Cancelled') FROM DUAL)) " +
               $"AND TO_CHAR(WANTED_DELIVERY_DATE,'ddmmyyyy') = '{EscapeOracle(ngayXuat)}' " +
               $"{dockFilter} " +
                "ORDER BY SUB_DOCK_CODE, CUSTOMER_PART_NO";

            return _ifs.ExecuteQuery(sql);
        }

        // ════════════════════════════════════════════════════════════════════
        // Địa chỉ — nhận customerNo tường minh, caller truyền _cfg.CustomerNo
        // ════════════════════════════════════════════════════════════════════
        public DataTable GetCustomerAddress(string customerNo)
        {
            string sql =
                "SELECT IDENTITY, IDENTITY_NAME, ADDRESS_ID, ADDRESS1, ADDRESS2 " +
                "FROM CUSTOMER_ADDRESS_AV " +
               $"WHERE IDENTITY = '{EscapeOracle(customerNo)}'";
            return _ifs.ExecuteQuery(sql);
        }

        // ════════════════════════════════════════════════════════════════════
        // Cust sched line — không liên quan CustomerConfig, giữ nguyên
        // ════════════════════════════════════════════════════════════════════
        public DataTable GetCustSchedLine(string customerNo, string shipAddrNo,
                                           string customerPartNo, string customerPoNo)
        {
            string sql =
                "SELECT MANUFACTURING_DEPARTMENT, PATTERN_DESCRIPTION " +
                "FROM CUST_SCHED_LINE_TAB " +
               $"WHERE CUSTOMER_NO      = '{EscapeOracle(customerNo)}' " +
               $"AND   SHIP_ADDR_NO     = '{EscapeOracle(shipAddrNo)}' " +
               $"AND   CUSTOMER_PART_NO = '{EscapeOracle(customerPartNo)}' " +
               $"AND   CUSTOMER_PO_NO   = '{EscapeOracle(customerPoNo)}' " +
                "AND   MANUFACTURING_DEPARTMENT IS NOT NULL " +
                "ORDER BY DOC_NO DESC FETCH FIRST 1 ROWS ONLY";
            return _ifs.ExecuteQuery(sql);
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parse gioXuat từ format SQL Server "'14','15'" sang Oracle IN clause.
        /// "'14','15'" → '14','15' | "14,15" → '14','15' | "" → '00'
        /// </summary>
        private static string BuildOracleInClause(string gioXuat)
        {
            if (string.IsNullOrWhiteSpace(gioXuat))
                return "";  // ← THAY vì "'00'" — rỗng = load theo ngày

            var parts = gioXuat
                .Split(',')
                .Select(p => p.Trim().Trim('\'').Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => $"'{p}'")
                .ToArray();

            return parts.Length > 0
                ? string.Join(",", parts)
                : "";  // ← THAY vì "'00'"
        }
        ///
        /// YMVN

        /// <summary>
        public DataTable GetDockCodeDv(string po, string pno,
                                string customerNo, string dockFilter)
        {
            string sql =
                $"SELECT DOCK_CODE, customer_part_unit_meas AS DV " +
                $"FROM CUSTOMER_ORDER_JOIN " +
                $"WHERE CUSTOMER_NO = '{customerNo}' " +
                $"AND (OBJSTATE <> (SELECT CUSTOMER_ORDER_LINE_API" +
                $"    .FINITE_STATE_ENCODE__('Cancelled') FROM dual)) " +
                $"AND SUB_DOCK_CODE = '{EscapeOracle(po)}' " +
                $"AND CUSTOMER_PART_NO = '{EscapeOracle(pno)}' " +
                $"{dockFilter}";

            return _ifs.ExecuteQuery(sql);
        }
        /// Escape nháy đơn Oracle — chỉ dùng cho VALUE, KHÔNG dùng cho SQL fragment (NhaMayCase).
        /// </summary>
        private static string EscapeOracle(string value)
            => (value ?? "").Replace("'", "''");
    }
}

    
