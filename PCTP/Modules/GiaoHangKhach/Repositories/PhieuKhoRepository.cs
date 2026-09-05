using PCTP.Common;
using PCTP.FuctionMain;
using PCTP.Models;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.Services;
using PCTP.YMN;
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
    /// Kế thừa SqlRepositoryBase — mọi truy vấn (Db.*/inherited ExecuteX) đều tự động
    /// chạy trong transaction Uow nếu Uow.Begin() đang mở (ambient), ngược lại tự
    /// mở/đóng connection riêng qua PhieuSqlExecutor — KHÔNG còn dùng SQLPROVIDER trực tiếp.
    /// </summary>
    public sealed class PhieuKhoRepository : SqlRepositoryBase, IPhieuKhoRepository
    {
        private readonly CustomerConfig _cfg;

        /// <summary>
        /// Repo của phân khu Xuất Kho, sở hữu bảng FVN_HangChoGiao (TMPCHOGIAO).
        /// Dùng để đóng các dòng "chờ giao" theo LOT sau khi CNK xác nhận xuất kho
        /// thành công (xem WORKFLOW_XUATKHO.md — không thuộc XuLyHangLoi/QTChung).
        /// PHẢI được khởi tạo bằng CÙNG 1 IUnitOfWork instance đã truyền cho
        /// constructor của chính PhieuKhoRepository này (VD: new HangChoGiaoRepository(db, uow))
        /// — nhờ vậy Uow.Begin() ở đây và Uow bên trong _hangChoGiaoRepo trỏ chung 1 transaction.
        /// </summary>
        private readonly IHangChoGiaoRepository _hangChoGiaoRepo;

        public PhieuKhoRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            CustomerConfig cfg = null,
            IHangChoGiaoRepository hangChoGiaoRepo = null)
            : base(db, uow)
        {
            _cfg = cfg;
            _hangChoGiaoRepo = hangChoGiaoRepo;
        }

        // ============================================================
        // IPhieuKhoRepository
        // ============================================================

        #region CapNhapKho

        public int CapNhapKho(
            string gioGiaoFcc,
            string nhaMay,
            PhieuTableSet tables,
            out DataTable errors)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return CapNhapKho(
                gioGiaoFcc,
                nhaMay,
                tables.TmpTable,
                tables.DocQRTable,
                out errors);
        }

        public int CapNhapKho(
            string gioGiaoFcc,
            string nhaMay,
            string tmpTable,
            string docQRTable,
            out DataTable errors)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            DataSet ds = ExecuteStoredProcedureDataSet(
                "Usp_Qrcode_Update_Stock2405",

                new SqlParameter(
                    "@GIOGIAOFCC",
                    SqlDbType.NVarChar,
                    200)
                {
                    Value = (object)(gioGiaoFcc ?? "")
                },

                new SqlParameter(
                    "@NHAMAY",
                    SqlDbType.NVarChar,
                    200)
                {
                    Value = (object)(nhaMay ?? "")
                },

                new SqlParameter(
                    "@TMPTABLE",
                    SqlDbType.NVarChar,
                    128)
                {
                    Value = tmpTable
                },

                new SqlParameter(
                    "@DOCQRTABLE",
                    SqlDbType.NVarChar,
                    128)
                {
                    Value = docQRTable
                },

                new SqlParameter(
                    "@LOT_KEY_LEN",
                    SqlDbType.Int)
                {
                    Value = PCTP.Common.LotCodeHelper.LEN_HEAD_FIXED
                }
            );

            // --------------------------------------------------------
            // Kết quả SP
            // --------------------------------------------------------

            DataTable stok =
                ds != null &&
                ds.Tables.Count > 0
                    ? ds.Tables[0]
                    : new DataTable();

            errors =
                ds != null &&
                ds.Tables.Count > 1
                    ? ds.Tables[1]
                    : new DataTable();

            // --------------------------------------------------------
            // (1) Trừ SlotLot của kho ảo A0
            // --------------------------------------------------------

            bool coAnhHuongA0 = false;

            var lotsDaXuatThanhCong =
                new List<string>();

            if (stok.Rows.Count > 0 &&
                stok.Columns.Contains("LOT") &&
                stok.Columns.Contains("SOLUONG"))
            {
                var bulkService =
                    new BulkStockAdjustService();

                foreach (DataRow row in stok.Rows)
                {
                    string lotRaw =
                        row["LOT"]?.ToString();

                    string lot =
                        LotCodeHelper.TrimTo(
                            lotRaw,
                            LotCodeHelper.LEN_HEAD_FIXED);

                    int sl =
                        row["SOLUONG"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(
                                row["SOLUONG"]);

                    if (string.IsNullOrWhiteSpace(lot) ||
                        sl <= 0)
                    {
                        continue;
                    }

                    bool anhHuong =
                        bulkService.TruKhoAoTheoLot(
                            lot,
                            sl);

                    if (anhHuong)
                        coAnhHuongA0 = true;

                    lotsDaXuatThanhCong.Add(lot);
                }
            }

            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();

            // --------------------------------------------------------
            // (2) Đóng TMPCHOGIAO tương ứng
            // Best-effort: lỗi không làm fail xuất kho
            // --------------------------------------------------------

            if (lotsDaXuatThanhCong.Count > 0 &&
                _hangChoGiaoRepo != null)
            {
                try
                {
                    List<HangChoGiao> closedItems;

                    Uow.Begin();
                    try
                    {
                        closedItems =
                            _hangChoGiaoRepo.CloseChoGiaoTheoLotAndReturn(
                                lotsDaXuatThanhCong,
                                nguoiGiao: "SYSTEM_HVN_CNK");

                        Uow.Commit();
                    }
                    catch
                    {
                        Uow.Rollback();
                        throw;
                    }

                    if (closedItems != null)
                    {
                        foreach (
                            var it in closedItems.Where(
                                x => x.SlotIdNguon.HasValue))
                        {
                            SlotHelper.SaveHistory(
                                "EXPORT_CONFIRMED_HVN",
                                it.MaHang,

                                new LotInfo
                                {
                                    LotNo = it.LotGoc,
                                    Quantity = it.SoLuong,
                                    TemCode = it.LotThung
                                },

                                it.SlotIdNguon,
                                null,

                                performedBy:
                                    "SYSTEM_HVN_CNK");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[CapNhapKho] Không đóng được TMPCHOGIAO: {ex.Message}");
                }
            }

            // --------------------------------------------------------
            // (3) Đánh dấu IsDelivered
            // cho YMVN / HTN có OrderTable
            // --------------------------------------------------------

            if (_cfg != null &&
                _cfg.LoadTuBangRieng &&
                !string.IsNullOrEmpty(_cfg.OrderTable) &&
                stok.Rows.Count > 0)
            {
                foreach (DataRow row in stok.Rows)
                {
                    string maHang =
                        row["MH"]?.ToString() ?? "";

                    int stt = 0;

                    if (row.Table.Columns.Contains("STT") &&
                        row["STT"] != DBNull.Value)
                    {
                        int.TryParse(
                            row["STT"].ToString(),
                            out stt);
                    }

                    if (string.IsNullOrEmpty(maHang))
                        continue;

                    string whereClause;

                    if (stt > 0)
                    {
                        whereClause =
                            $"STT={stt}";
                    }
                    else
                    {
                        whereClause =
                            $"MAHANG='{SqlHelper.Esc(maHang)}' " +
                            "AND STATUS='OK'";
                    }

                    string ngayGiao =
                        Convert.ToString(
                            ExecuteScalar(
                                $"SELECT CONVERT(varchar, NGAYGIAO, 23) " +
                                $"FROM [{tmpTable}] " +
                                $"WHERE {whereClause}"))
                        ?.Trim() ?? "";

                    string poNo =
                        Convert.ToString(
                            ExecuteScalar(
                                $"SELECT ISNULL(PO_NO,'') " +
                                $"FROM [{tmpTable}] " +
                                $"WHERE {whereClause}"))
                        ?.Trim() ?? "";

                    if (!string.IsNullOrEmpty(poNo) &&
                        !string.IsNullOrEmpty(ngayGiao))
                    {
                        DanhDauDaGiao(
                            poNo,
                            maHang,
                            ngayGiao,
                            _cfg);
                    }
                }
            }

            return stok.Rows.Count;
        }

        #endregion

        // ============================================================
        // CapNhapKhoHTN
        // ============================================================

        #region CapNhapKhoHTN

        public int CapNhapKhoHTN(
            string nhaMay,
            PhieuTableSet tables,
            out DataTable errors)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return CapNhapKho(
                "",
                nhaMay,
                tables.TmpTable,
                tables.DocQRTable,
                out errors);
        }

        public int CapNhapKhoHTN(
            string nhaMay,
            string tmpTable,
            string docQRTable,
            out DataTable errors)
        {
            return CapNhapKho(
                "",
                nhaMay,
                tmpTable,
                docQRTable,
                out errors);
        }

        #endregion

        // ============================================================
        // CapNhapKhoSP
        // ============================================================

        #region CapNhapKhoSP

        public int CapNhapKhoSP(
            string gioGiaoFcc,
            string nhaMay,
            out DataTable errors)
        {
            DataSet ds =
                ExecuteStoredProcedureDataSet(
                    "Usp_Qrcode_Update_Stock_SP",

                    new SqlParameter(
                        "@GIOGIAOFCC",
                        SqlDbType.NVarChar,
                        200)
                    {
                        Value = (object)(
                            gioGiaoFcc ?? "")
                    },

                    new SqlParameter(
                        "@NHAMAY",
                        SqlDbType.NVarChar,
                        200)
                    {
                        Value = (object)(
                            nhaMay ?? "")
                    }
                );

            DataTable stok =
                ds != null &&
                ds.Tables.Count > 0
                    ? ds.Tables[0]
                    : new DataTable();

            errors =
                ds != null &&
                ds.Tables.Count > 1
                    ? ds.Tables[1]
                    : new DataTable();

            // --------------------------------------------------------
            // Trừ kho ảo A0
            // --------------------------------------------------------

            if (stok.Rows.Count > 0 &&
                stok.Columns.Contains("LOT") &&
                stok.Columns.Contains("SOLUONG"))
            {
                var bulkService =
                    new BulkStockAdjustService();

                bool coAnhHuongA0 = false;

                foreach (DataRow row in stok.Rows)
                {
                    string lotRaw =
                        row["LOT"]?.ToString();

                    string lot =
                        LotCodeHelper.TrimTo(
                            lotRaw,
                            LotCodeHelper.LEN_HEAD_FIXED);

                    int sl =
                        row["SOLUONG"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(
                                row["SOLUONG"]);

                    if (string.IsNullOrWhiteSpace(lot) ||
                        sl <= 0)
                    {
                        continue;
                    }

                    if (bulkService.TruKhoAoTheoLot(
                            lot,
                            sl))
                    {
                        coAnhHuongA0 = true;
                    }
                }

                if (coAnhHuongA0)
                    StockChangedNotifier.RaiseStockChanged();
            }

            return stok.Rows.Count;
        }

        #endregion

        // ============================================================
        // CapNhapKhoYMVN
        // ============================================================

        #region CapNhapKhoYMVN

        public bool CapNhapKhoYMVN(
            int stt,
            string lotSl,
            string maHang,
            string ngayGiao,
            string gioGiao,
            string nhaMay,
            out DS_ERR_CNK error)
        {
            error = null;

            if (_cfg == null)
            {
                throw new InvalidOperationException(
                    "PhieuKhoRepository cần CustomerConfig " +
                    "để thực hiện CapNhapKhoYMVN.");
            }

            string tmpTable =
                _cfg.TmpTable;

            string docQRTable =
                _cfg.DocQRTable;

            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            if (string.IsNullOrWhiteSpace(lotSl))
                return false;

            var bulkService =
                new BulkStockAdjustService();

            bool coAnhHuongA0 = false;

            string[] lotParts =
                lotSl.Split(',');

            foreach (string part in lotParts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                string[] tach =
                    part.Trim().Split('-');

                if (tach.Length < 2)
                    continue;

                string lot =
                    LotCodeHelper.TrimTo(
                        tach[0],
                        LotCodeHelper.LEN_HEAD_FIXED);

                if (!int.TryParse(
                        tach[1],
                        out int sl))
                {
                    continue;
                }

                if (sl <= 0)
                    continue;

                string matchCondition =
                    LotCodeHelper.BuildLotMatchSql(
                        "LOT",
                        $"'{SqlHelper.Esc(lot)}'");

                // ----------------------------------------------------
                // Kiểm tra tồn kho
                // ----------------------------------------------------

                object slConlaiRaw =
                    ExecuteScalar(
                        "SELECT ISNULL(slconlai,0) " +
                        "FROM STOCKTP " +
                        $"WHERE {matchCondition}");

                int slConlai =
                    slConlaiRaw == null ||
                    slConlaiRaw == DBNull.Value
                        ? 0
                        : Convert.ToInt32(
                            slConlaiRaw);

                if (slConlai < sl)
                {
                    error = new DS_ERR_CNK
                    {
                        MH = maHang,
                        LOT = lot,
                        SLC = sl,
                        SLTK = slConlai,
                        SLT = sl - slConlai,
                        Ms = "Không đủ tồn kho"
                    };

                    return false;
                }

                // ----------------------------------------------------
                // Trừ STOCKTP
                // ----------------------------------------------------

                string gg =
                    (ngayGiao ?? "") +
                    " " +
                    (gioGiao ?? "") +
                    ":00";

                ExecuteNonQuery(
                    $"UPDATE t " +
                    $"SET t.ngayxuat = @ngayxuat, " +
                    $"    t.slxuat = slxuat + @sl, " +
                    $"    t.slconlai = slconlai - @sl " +
                    $"FROM " +
                    $"(" +
                    $"    SELECT TOP 1 * " +
                    $"    FROM STOCKTP " +
                    $"    WHERE {matchCondition}" +
                    $") t",

                    new SqlParameter(
                        "@ngayxuat",
                        SqlDbType.NVarChar,
                        50)
                    {
                        Value = gg
                    },

                    new SqlParameter(
                        "@sl",
                        SqlDbType.Int)
                    {
                        Value = sl
                    }
                );

                // ----------------------------------------------------
                // Trừ kho ảo A0
                // ----------------------------------------------------

                if (bulkService.TruKhoAoTheoLot(
                        lot,
                        sl))
                {
                    coAnhHuongA0 = true;
                }
            }

            // ========================================================
            // Lưu DOCQRCODE
            // ========================================================

            ExecuteNonQuery(
                $@"
                    INSERT INTO LUUDOCQRCODE
                    (
                        LOTFCC,
                        MAHANGFCC,
                        SLTEMFCC,
                        LOTHVN,
                        MAHANGHVN,
                        SLTEMHVN,
                        STATUS,
                        MAFCC,
                        STT,
                        KETQUA,
                        NGAYXUAT,
                        GIOXUAT,
                        NHAMAY
                    )
                    SELECT
                        LEFT(LOTFCC, 500),
                        LEFT(MAHANGFCC, 60),
                        SLTEMFCC,
                        LEFT(LOTHVN, 500),
                        LEFT(MAHANGHVN, 60),
                        SLTEMHVN,
                        STATUS,
                        LEFT(MAFCC, 50),
                        STT,
                        KETQUA,
                        @ngayGiao,
                        @gioGiao,
                        @nhaMay
                    FROM [{docQRTable}]
                    WHERE MAHANGFCC = @maHang
                      AND KETQUA = 'DG'",

                new SqlParameter(
                    "@ngayGiao",
                    SqlDbType.NVarChar,
                    50)
                {
                    Value = ngayGiao ?? ""
                },

                new SqlParameter(
                    "@gioGiao",
                    SqlDbType.NVarChar,
                    50)
                {
                    Value = gioGiao ?? ""
                },

                new SqlParameter(
                    "@nhaMay",
                    SqlDbType.NVarChar,
                    200)
                {
                    Value = nhaMay ?? ""
                },

                new SqlParameter(
                    "@maHang",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = maHang ?? ""
                }
            );

            // ========================================================
            // Lưu phiếu giao hàng
            // ========================================================

            ExecuteNonQuery(
                $@"
                    INSERT INTO LUUPHIEUGIAOHANG
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
                        GIOGIAO,
                        STATUS,
                        GearYMVN,
                        NHAMAY,
                        GIOGIAOFCC,
                        PO_NO,
                        TTPHIEU
                    )
                    SELECT
                        STT,
                        CUA,
                        TRUYEN,
                        MAHANG,
                        TENHANG,
                        LOT,
                        DV,
                        SOLUONG,
                        NGAYGIAO,
                        GIOGIAO,
                        'OK',
                        ISNULL(GEAR,''),
                        @nhaMay,
                        CONVERT(VARCHAR(8), GETDATE(), 108),
                        ISNULL(PO_NO,''),
                        ISNULL(TTPHIEU,'')
                    FROM [{tmpTable}]
                    WHERE STT = @stt
                      AND STATUS = 'NG'",

                new SqlParameter(
                    "@nhaMay",
                    SqlDbType.NVarChar,
                    200)
                {
                    Value = nhaMay ?? ""
                },

                new SqlParameter(
                    "@stt",
                    SqlDbType.Int)
                {
                    Value = stt
                }
            );

            // ========================================================
            // Đánh dấu phiếu OK
            // ========================================================

            ExecuteNonQuery(
                $"UPDATE [{tmpTable}] " +
                "SET STATUS = 'OK' " +
                "WHERE STT = @stt",

                new SqlParameter(
                    "@stt",
                    SqlDbType.Int)
                {
                    Value = stt
                }
            );

            // ========================================================
            // Xóa QR đã giao
            // ========================================================

            ExecuteNonQuery(
                $"DELETE FROM [{docQRTable}] " +
                "WHERE MAHANGFCC = @maHang " +
                "AND KETQUA = 'DG'",

                new SqlParameter(
                    "@maHang",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = maHang ?? ""
                }
            );

            // ========================================================
            // Notify kho
            // ========================================================

            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();

            // ========================================================
            // Lấy PO_NO
            // ========================================================

            string poNo =
                Convert.ToString(
                    ExecuteScalar(
                        $"SELECT ISNULL(PO_NO,'') " +
                        $"FROM [{tmpTable}] " +
                        "WHERE STT = @stt",

                        new SqlParameter(
                            "@stt",
                            SqlDbType.Int)
                        {
                            Value = stt
                        }))
                ?.Trim() ?? "";

            // ========================================================
            // Đánh dấu OrderTable đã giao
            // ========================================================

            if (!string.IsNullOrEmpty(poNo) &&
                !string.IsNullOrEmpty(_cfg.OrderTable))
            {
                DanhDauDaGiao(
                    poNo,
                    maHang,
                    ngayGiao,
                    _cfg);
            }

            return true;
        }

        #endregion

        // ============================================================
        // DanhDauDaGiao
        // ============================================================

        #region DanhDauDaGiao

        public void DanhDauDaGiao(
            string poNo,
            string maHang,
            string ngayGiao,
            CustomerConfig cfg)
        {
            if (cfg == null)
                return;

            if (string.IsNullOrEmpty(
                    cfg.OrderTable))
            {
                return;
            }

            Db.ValidateTableName(
                cfg.OrderTable);

            ExecuteNonQuery(
                $"UPDATE [{cfg.OrderTable}] " +
                "SET IsDelivered = 1, " +
                "    DeliveredDate = GETDATE() " +
                "WHERE Oder_no = @po " +
                "  AND Part_no = @pno " +
                "  AND CAST(NgayGiao AS DATE) = @ng " +
                "  AND IsDelivered = 0",

                new SqlParameter(
                    "@po",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = poNo ?? ""
                },

                new SqlParameter(
                    "@pno",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = maHang ?? ""
                },

                new SqlParameter(
                    "@ng",
                    SqlDbType.Date)
                {
                    Value = ngayGiao ?? ""
                }
            );
        }

        #endregion
        public DataTable LoadHangThieu(
         bool isMayBanQR,
         string tenBan)
        {
            if (isMayBanQR)
            {
                return ExecuteStoredProcedure(
                    "Usp_Qrcode_LOAD_HANGTHIEU");
            }

            if (string.IsNullOrWhiteSpace(tenBan))
            {
                throw new ArgumentException(
                    "Tên bảng không được rỗng.",
                    nameof(tenBan));
            }

            Db.ValidateTableName(tenBan);

            return ExecuteStoredProcedure(
                "Usp_Qrcode_LOAD_HANGTHIEUView",
                new SqlParameter("@TENBAN", tenBan));
        }
    }
}