using PCTP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Interfaces
{
    /// <summary>
    /// Các query SQL Server dùng chung cho nhiều service.
    /// Tách riêng để InPhieuService không phụ thuộc IPhieuRepository.
    /// </summary>
    public interface ISqlRepository
    {
        // ── Đã có trong IPhieuRepository → KHÔNG đưa vào đây ────────────────
        // CountTrungMaSl, GetDanhSachTrungMaSl, GetDonHangChuaLot,
        // LoadGhepLot, LayLaiLotNo, CapNhapKho, GetLotNo → dùng IPhieuRepository

        // ── Chỉ dùng trong InPhieuService → cần ở đây ────────────────────────

        /// <summary>
        /// Lấy MinCloseQty từ B20Item — dùng tính số hộp khi in phiếu.
        /// Trả về 0 nếu không tìm thấy mã.
        /// </summary>
        int GetMinCloseQty(string maHang);

        /// <summary>
        /// Lấy LOT đã lưu trong LUUPHIEUGIAOHANG khớp với dòng phiếu.
        /// Trả về "" nếu chưa có.
        /// </summary>
        string GetSavedLot(string cua, string truyen, string maHang,
                           int soLuong, string ngayGiao, string gioGiao,
                           string nhaMayLike);

        // ── Dùng trong InGhepLot ─────────────────────────────────────────────

        /// <summary>
        /// Xóa TMPLOTGHEP rồi insert các dòng user chọn.
        /// Form gốc: INGHEPLOT() — delete + insert TMPLOTGHEP
        /// </summary>
        void XoaVaInsertTmpLotGhep(IEnumerable<GhepLotItem> items);

        /// <summary>
        /// Gọi Usp_gheplotPrint → DataTable cho report ghép lot.
        /// </summary>
        DataTable GetGhepLotPrint();

        // ── Dùng trong Presenter (SET_PHIEU) ─────────────────────────────────

        /// <summary>
        /// Tên máy được phép bắn QR.
        /// Form gốc: "select TenMay from tbl_QR_MAY_DOCQR where TT = 1"
        /// </summary>
        string GetTenMayBanQR();

        /// <summary>
        /// Kiểm tra có hiện nút CNK + KiemTraMaNG không.
        /// Form gốc: "select dbo.ufn_QRcode_ADD_CMD_MANG() gt" → 1 hoặc 2
        /// </summary>
        int GetAddCmdMang();

        /// <summary>
        /// Metadata phiếu đang dở khi DOCQRCODE đã có data.
        /// Form gốc LoadDL(): "select top(1) ADDNM,NGAYGIAO,GIOGIAOFCC,NHAMAY
        ///                      from IFSPHIEUGIAOHANG"
        /// </summary>
        PhieuMeta GetPhieuMeta();
    }
}
