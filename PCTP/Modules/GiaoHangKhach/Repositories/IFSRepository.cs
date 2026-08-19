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

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Infrastructure.Repositories
{
    public interface IIFSProviderAdapter
    {
        DataTable ExecuteQuery(string sql);
    }

    internal sealed class IFSProviderAdapter : IIFSProviderAdapter
    {
        private readonly IFSPROVIDER _inner;

        public IFSProviderAdapter(IFSPROVIDER inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public DataTable ExecuteQuery(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException(
                    "SQL không được rỗng.",
                    nameof(sql));

            return _inner.ExecuteQuery(sql);
        }
    }


    public class IFSRepository : IIFSRepository
    {
        private readonly IIFSProviderAdapter _ifs;


        // ================================================================
        // Constructor
        // ================================================================

        public IFSRepository(IIFSProviderAdapter ifs)
        {
            _ifs = ifs ?? throw new ArgumentNullException(nameof(ifs));
        }


        // ================================================================
        // Factory
        // ================================================================

        public static IFSRepository Create()
        {
            return new IFSRepository(
                new IFSProviderAdapter(
                    new IFSPROVIDER()));
        }


        // ================================================================
        // CUSTOMER ORDER
        // ================================================================

        /// <summary>
        /// Load phiếu thường.
        /// Mặc định hinhThucIn = 1.
        /// </summary>
        public DataTable GetCustomerOrderJoin(
            string ngayXuat,
            string gioXuat,
            string gioXuatH,
            string nhaMay,
            int addNm,
            CustomerConfig cfg)
        {
            return GetCustomerOrderJoin(
                ngayXuat,
                gioXuat,
                gioXuatH,
                nhaMay,
                addNm,
                1,
                cfg);
        }


        /// <summary>
        /// Load CUSTOMER_ORDER_JOIN từ IFS.
        ///
        /// hinhThucIn:
        /// 1 = theo nhà máy + giờ
        /// 2 = theo nhà máy, không lọc giờ
        /// 3 = theo giờ, không lọc nhà máy
        /// </summary>
        public DataTable GetCustomerOrderJoin(
            string ngayXuat,
            string gioXuat,
            string gioXuatH,
            string nhaMay,
            int addNm,
            int hinhThucIn,
            CustomerConfig cfg)
        {
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            string inClause =
                BuildOracleInClause(gioXuat);

            string customerNo =
                EscapeOracle(cfg.CustomerNo);

            /*
             * NhaMayCase là SQL fragment.
             *
             * Không EscapeOracle().
             *
             * Ví dụ:
             *
             * CASE
             *     WHEN ...
             *     THEN ...
             * END
             */
            string nhaMayCase =
                cfg.NhaMayCase;


            if (string.IsNullOrWhiteSpace(nhaMayCase))
            {
                nhaMayCase = "''";
            }


            string sql =
                "SELECT " +
                "    '' AS STT, " +

                "    (col.CUSTOMER_PART_NO || " +
                "     TO_CHAR(col.BUY_QTY_DUE)) AS FIND, " +

                "    col.CUSTOMER_NO, " +

                "    TO_CHAR(" +
                "        col.WANTED_DELIVERY_DATE," +
                "        'HH24') AS GIOGIAO, " +

                "    col.BUY_QTY_DUE AS SOLUONG, " +

                "    col.CUSTOMER_PART_UNIT_MEAS AS DV, " +

                "    col.SHIP_ADDR_NO AS ADDNM, " +

                "    col.SUB_DOCK_CODE AS CUA, " +

                "    col.CUSTOMER_PART_NO AS MAHANG, " +

                "    col.CATALOG_DESC AS TENHANG, " +

                "    col.CUSTOMER_PO_REL_NO, " +

                "    col.DOCK_CODE AS TRUYEN, " +

                "    col.ORDER_NO, " +

                "    col.CATALOG_NO, " +

                "    TO_CHAR(" +
                "        col.WANTED_DELIVERY_DATE," +
                "        'YYYY-MM-DD') AS NGAYGIAO, " +

                "    col.CUSTOMER_PO_NO, " +

                "    '' AS HOP, " +
                "    '' AS LOT, " +
                "    '' AS DIA_CHI, " +
                "    '' AS KGX, " +

                "    'NG' AS STATUS, " +
                "    'NG' AS STATUSDOC, " +
                "    '' AS TTPHIEU, " +

                $"    '{EscapeOracle(gioXuatH)}' AS GIOGIAOFCC, " +

                "    col.SHIP_ADDR_NO AS SHIP_ADDR_NO, " +

                $"    {nhaMayCase} AS NHAMAY, " +

                "    '' AS NOTE, " +

                "    csl.MANUFACTURING_DEPARTMENT AS PO_NO, " +

                "    csl.PATTERN_DESCRIPTION AS PO_ITEM " +

                "FROM CUSTOMER_ORDER_JOIN col " +

                "LEFT JOIN " +
                "( " +

                "    SELECT " +
                "        CUSTOMER_NO, " +
                "        SHIP_ADDR_NO, " +
                "        CUSTOMER_PART_NO, " +
                "        CUSTOMER_PO_NO, " +

                "        MAX(MANUFACTURING_DEPARTMENT) " +
                "            AS MANUFACTURING_DEPARTMENT, " +

                "        MAX(PATTERN_DESCRIPTION) " +
                "            AS PATTERN_DESCRIPTION " +

                "    FROM CUST_SCHED_LINE_TAB " +

                "    WHERE MANUFACTURING_DEPARTMENT IS NOT NULL " +

                "      AND " +
                "      ( " +
                "          DOC_NO, " +
                "          CUSTOMER_NO, " +
                "          SHIP_ADDR_NO, " +
                "          CUSTOMER_PART_NO, " +
                "          CUSTOMER_PO_NO " +
                "      ) IN " +
                "      ( " +

                "          SELECT " +
                "              MAX(s.DOC_NO), " +
                "              s.CUSTOMER_NO, " +
                "              s.SHIP_ADDR_NO, " +
                "              s.CUSTOMER_PART_NO, " +
                "              s.CUSTOMER_PO_NO " +

                "          FROM CUST_SCHED_LINE_TAB s " +

                "          WHERE s.MANUFACTURING_DEPARTMENT " +
                "                IS NOT NULL " +

                "          GROUP BY " +
                "              s.CUSTOMER_NO, " +
                "              s.SHIP_ADDR_NO, " +
                "              s.CUSTOMER_PART_NO, " +
                "              s.CUSTOMER_PO_NO " +

                "      ) " +

                "    GROUP BY " +
                "        CUSTOMER_NO, " +
                "        SHIP_ADDR_NO, " +
                "        CUSTOMER_PART_NO, " +
                "        CUSTOMER_PO_NO " +

                ") csl " +

                "ON  csl.CUSTOMER_NO = col.CUSTOMER_NO " +
                "AND csl.SHIP_ADDR_NO = col.SHIP_ADDR_NO " +
                "AND csl.CUSTOMER_PART_NO = col.CUSTOMER_PART_NO " +
                "AND csl.CUSTOMER_PO_NO = col.CUSTOMER_PO_NO " +

                $"WHERE col.CUSTOMER_NO = '{customerNo}' " +

                "AND " +
                "( " +
                "    col.OBJSTATE = " +
                "    ( " +
                "        SELECT " +
                "            CUSTOMER_ORDER_LINE_API." +
                "            FINITE_STATE_ENCODE__('Released') " +
                "        FROM DUAL " +
                "    ) " +

                "    OR " +

                "    col.OBJSTATE = " +
                "    ( " +
                "        SELECT " +
                "            CUSTOMER_ORDER_LINE_API." +
                "            FINITE_STATE_ENCODE__('Partially Delivered') " +
                "        FROM DUAL " +
                "    ) " +
                ") " +

                $"AND TO_CHAR(" +
                $"    col.WANTED_DELIVERY_DATE," +
                $"'ddmmyyyy') = '{EscapeOracle(ngayXuat)}' ";


            // ============================================================
            // Require PO Release
            // ============================================================

            if (cfg.RequirePoRelNo)
            {
                sql +=
                    "AND col.CUSTOMER_PO_REL_NO IS NOT NULL ";
            }


            // ============================================================
            // Hình thức in
            // ============================================================

            switch (hinhThucIn)
            {
                case 1:

                    // Nhà máy
                    sql +=
                        $"AND col.SHIP_ADDR_NO = {addNm} ";

                    // Giờ nếu có
                    if (!string.IsNullOrWhiteSpace(inClause))
                    {
                        sql +=
                            "AND TO_CHAR(" +
                            "    col.WANTED_DELIVERY_DATE," +
                            "    'HH24') " +
                            $"IN ({inClause}) ";
                    }

                    break;


                case 2:

                    // Nhà máy
                    sql +=
                        $"AND col.SHIP_ADDR_NO = {addNm} ";

                    break;


                case 3:

                    // Không lọc nhà máy
                    // Chỉ lọc giờ

                    if (!string.IsNullOrWhiteSpace(inClause))
                    {
                        sql +=
                            "AND TO_CHAR(" +
                            "    col.WANTED_DELIVERY_DATE," +
                            "    'HH24') " +
                            $"IN ({inClause}) ";
                    }

                    break;


                default:

                    sql +=
                        $"AND col.SHIP_ADDR_NO = {addNm} ";

                    if (!string.IsNullOrWhiteSpace(inClause))
                    {
                        sql +=
                            "AND TO_CHAR(" +
                            "    col.WANTED_DELIVERY_DATE," +
                            "    'HH24') " +
                            $"IN ({inClause}) ";
                    }

                    break;
            }


            // ============================================================
            // ORDER
            // ============================================================

            sql +=
                "ORDER BY " +
                "    col.WANTED_DELIVERY_DATE, " +
                "    col.SUB_DOCK_CODE, " +
                "    col.CUSTOMER_PART_NO, " +
                "    col.BUY_QTY_DUE";


            return _ifs.ExecuteQuery(sql);
        }


        // ================================================================
        // YMVN
        // ================================================================

        public DataTable GetCustomerOrderJoinYMVN(
            string ngayXuat,
            string customerNo,
            string dockFilter)
        {
            string safeCustomerNo =
                EscapeOracle(customerNo);

            /*
             * dockFilter là SQL fragment.
             *
             * Ví dụ:
             * AND SUB_DOCK_CODE = 'VSP1'
             *
             * Vì vậy KHÔNG EscapeOracle(dockFilter).
             */
            string safeDockFilter =
                dockFilter ?? "";


            string sql =
                "SELECT " +

                "    '' AS STT, " +

                "    TO_CHAR(" +
                "        WANTED_DELIVERY_DATE," +
                "        'HH24') AS GIOGIAO, " +

                "    SUB_DOCK_CODE AS CUA, " +

                "    DOCK_CODE AS TRUYEN, " +

                "    CUSTOMER_PART_NO AS MAHANG, " +

                "    CATALOG_DESC AS TENHANG, " +

                "    BUY_QTY_DUE AS SOLUONG, " +

                "    CUSTOMER_PART_UNIT_MEAS AS DV, " +

                "    SHIP_ADDR_NO AS ADDNM, " +

                "    CUSTOMER_PO_NO, " +

                "    TO_CHAR(" +
                "        WANTED_DELIVERY_DATE," +
                "        'YYYY-MM-DD') AS NGAYGIAO, " +

                "    '' AS HOP, " +
                "    '' AS LOT, " +
                "    '' AS Gear, " +
                "    '' AS XE, " +

                "    'NG' AS STATUS, " +
                "    'NG' AS STATUSDOC, " +

                "    '' AS TTPHIEU, " +
                "    '' AS NOTE, " +
                "    '' AS PO_NO, " +
                "    '' AS PO_ITEM, " +
                "    '' AS KGX, " +
                "    '' AS DIA_CHI " +

                "FROM CUSTOMER_ORDER_JOIN " +

                $"WHERE CUSTOMER_NO = '{safeCustomerNo}' " +

                "AND " +
                "( " +
                "    OBJSTATE <> " +
                "    ( " +
                "        SELECT " +
                "            CUSTOMER_ORDER_LINE_API." +
                "            FINITE_STATE_ENCODE__('Cancelled') " +
                "        FROM dual " +
                "    ) " +
                ") " +

                $"AND TO_CHAR(" +
                $"    WANTED_DELIVERY_DATE," +
                $"'ddmmyyyy') = '{EscapeOracle(ngayXuat)}' " +

                safeDockFilter +

                "ORDER BY " +
                "    SUB_DOCK_CODE, " +
                "    CUSTOMER_PART_NO";


            return _ifs.ExecuteQuery(sql);
        }


        // ================================================================
        // CUSTOMER ADDRESS
        // ================================================================

        public DataTable GetCustomerAddress(
            string customerNo)
        {
            string sql =
                "SELECT " +
                "    IDENTITY, " +
                "    IDENTITY_NAME, " +
                "    ADDRESS_ID, " +
                "    ADDRESS1, " +
                "    ADDRESS2 " +

                "FROM CUSTOMER_ADDRESS_AV " +

                $"WHERE IDENTITY = '{EscapeOracle(customerNo)}'";


            return _ifs.ExecuteQuery(sql);
        }


        // ================================================================
        // CUST SCHED LINE
        // ================================================================

        public DataTable GetCustSchedLine(
            string customerNo,
            string shipAddrNo,
            string customerPartNo,
            string customerPoNo)
        {
            string sql =
                "SELECT " +
                "    MANUFACTURING_DEPARTMENT, " +
                "    PATTERN_DESCRIPTION " +

                "FROM CUST_SCHED_LINE_TAB " +

                $"WHERE CUSTOMER_NO = " +
                $"'{EscapeOracle(customerNo)}' " +

                $"AND SHIP_ADDR_NO = " +
                $"'{EscapeOracle(shipAddrNo)}' " +

                $"AND CUSTOMER_PART_NO = " +
                $"'{EscapeOracle(customerPartNo)}' " +

                $"AND CUSTOMER_PO_NO = " +
                $"'{EscapeOracle(customerPoNo)}' " +

                "AND MANUFACTURING_DEPARTMENT IS NOT NULL " +

                "ORDER BY DOC_NO DESC " +

                "FETCH FIRST 1 ROWS ONLY";


            return _ifs.ExecuteQuery(sql);
        }


        // ================================================================
        // YMVN - DOCK CODE / DV
        // ================================================================

        public DataTable GetDockCodeDv(
            string po,
            string pno,
            string customerNo,
            string dockFilter)
        {
            string safeCustomerNo =
                EscapeOracle(customerNo);

            string safePo =
                EscapeOracle(po);

            string safePno =
                EscapeOracle(pno);

            string safeDockFilter =
                dockFilter ?? "";


            string sql =
                "SELECT " +
                "    DOCK_CODE, " +
                "    CUSTOMER_PART_UNIT_MEAS AS DV " +

                "FROM CUSTOMER_ORDER_JOIN " +

                $"WHERE CUSTOMER_NO = '{safeCustomerNo}' " +

                "AND " +
                "( " +
                "    OBJSTATE <> " +
                "    ( " +
                "        SELECT " +
                "            CUSTOMER_ORDER_LINE_API." +
                "            FINITE_STATE_ENCODE__('Cancelled') " +
                "        FROM dual " +
                "    ) " +
                ") " +

                $"AND SUB_DOCK_CODE = '{safePo}' " +

                $"AND CUSTOMER_PART_NO = '{safePno}' " +

                safeDockFilter;


            return _ifs.ExecuteQuery(sql);
        }


        // ================================================================
        // Helper: Oracle IN
        // ================================================================

        /// <summary>
        /// Chuyển:
        ///
        /// "'14','15'"
        ///
        /// thành:
        ///
        /// '14','15'
        ///
        /// hoặc:
        ///
        /// "14,15"
        ///
        /// thành:
        ///
        /// '14','15'
        ///
        /// Empty => ""
        /// </summary>
        private static string BuildOracleInClause(
            string gioXuat)
        {
            if (string.IsNullOrWhiteSpace(gioXuat))
                return "";


            var parts =
                gioXuat
                    .Split(',')
                    .Select(x =>
                        x.Trim()
                         .Trim('\'')
                         .Trim())
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .ToArray();


            if (parts.Length == 0)
                return "";


            return string.Join(
                ",",
                parts.Select(x =>
                    $"'{EscapeOracle(x)}'"));
        }


        // ================================================================
        // Helper: Escape Oracle value
        // ================================================================

        /// <summary>
        /// Chỉ dùng cho VALUE.
        ///
        /// Không dùng cho SQL fragment.
        /// </summary>
        private static string EscapeOracle(
            string value)
        {
            return (value ?? "")
                .Replace("'", "''");
        }
    }
}


