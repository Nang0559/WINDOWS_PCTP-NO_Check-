using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    /// <summary>
    /// Interface thao tác với bảng STOCKTP
    /// </summary>
    public interface IStockTpRepository
    {
        // ── Đọc phiếu sản xuất (vNhapTP) ──────────────────────────
        PhieuNhapInfo GetPhieuByFind(string find);
        List<PhieuNhapInfo> GetPhieuTong();

        // ── STOCKTP ────────────────────────────────────────────────
        bool ExistsStockTp(string lot);
        StockItem GetByLot(string lot);
        int GetSlConLai(string lot);
        void InsertStockTp(NhapKhoItem item, int status);
        void UpdateStockTp(string lot, int slSeNhap, int status);

        // ── Xuất kho thật (giao hàng) ─────────────────────────────
        void XuatKhoThat(string lot, int slXuat);

        // ── Đối chiếu tồn kho ──────────────────────────────────────
        List<(string Lot, int SlConLai)> GetDanhSachLotConTon();
        Dictionary<string, int> GetSlConLaiBatch(IEnumerable<string> lots);

        // ── Case dedup (NHAP_TP_HIS) ──────────────────────────────
        bool ExistsCaseHistory(string caseNo);
        void InsertCaseHistory(string caseNo);

        // ── NG (STOCKTPTRAHANG / STOCKTPNHANTRA) ──────────────────
        List<StockTraHangInfo> GetTraHangConLai(string lot);
        void InsertNhanTra(string lot, string part, string name, int slNhanLai, string lyDoNg);
        void UpdateTraHangSauNhanLai(string lot, string lyDoNg, int slNhanLai, int status);
        // ── THÊM: overload transaction-aware ─────────────────────────────
        bool ExistsStockTp(SqlConnection conn, SqlTransaction tran, string lot);
        void InsertStockTp(SqlConnection conn, SqlTransaction tran, NhapKhoItem item, int status);
        void UpdateStockTp(SqlConnection conn, SqlTransaction tran, string lot, int slSeNhap, int status);
        bool ExistsCaseHistory(SqlConnection conn, SqlTransaction tran, string caseNo);
        void InsertCaseHistory(SqlConnection conn, SqlTransaction tran, string caseNo);
    }
}
