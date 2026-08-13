using PCTP.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public interface IPhieuTrackingRepository
    {
        // ── Đọc (không cần transaction) ─────────────────────────────────
        List<PhieuLocationInfo> GetPhieuTheoLot(string lotNo);
        int GetTongSlActiveTheoLot(string lotNo);
        bool ExistsQrData(string qrData);

        // ── Ghi (transaction-aware — PHẢI dùng chung conn/tran với STOCKTP) ─
        void InsertPhieuMoi(SqlConnection conn, SqlTransaction tran,
            int slotId, string itemCode, string lotNo, int quantity,
            string temCode, string qrData, DateTime? importDate,
            string ngaySX, string soPhieuTong, string maPhieuMoi,
            string parentSoPhieu, PhieuStatus status);

        void CapNhatTrangThai(SqlConnection conn, SqlTransaction tran,
            string maPhieu, PhieuStatus status);

        void CapNhatQuantity(SqlConnection conn, SqlTransaction tran,
            string maPhieu, int quantityMoi);
    }
}
