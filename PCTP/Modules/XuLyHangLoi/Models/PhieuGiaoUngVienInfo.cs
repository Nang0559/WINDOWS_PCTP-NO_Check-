using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public class PhieuGiaoUngVienInfo
    {
        // ============================================================
        // ĐỊNH DANH PHIẾU
        // ============================================================

        /// <summary>
        /// STT của dòng/phiếu giao trong LUUPHIEUGIAOHANG.
        /// </summary>
        public short STT { get; set; }

        /// <summary>
        /// Khoá định danh duy nhất của phiếu giao.
        /// Nhà máy + Ngày + Giờ + PO + STT
        /// </summary>
        public string DinhDanhKey { get; set; }


        // ============================================================
        // THÔNG TIN HÀNG GIAO
        // ============================================================

        /// <summary>
        /// Số lot đã giao.
        /// </summary>
        public string LOT { get; set; }

        /// <summary>
        /// Mã hàng.
        /// </summary>
        public string MAHANG { get; set; }

        /// <summary>
        /// Tên hàng.
        /// </summary>
        public string TENHANG { get; set; }

        /// <summary>
        /// Số lượng đã giao.
        /// </summary>
        public int SOLUONG { get; set; }


        // ============================================================
        // THỜI GIAN GIAO
        // ============================================================

        /// <summary>
        /// Ngày giao.
        /// </summary>
        public DateTime? NGAYGIAO { get; set; }

        /// <summary>
        /// Giờ giao.
        /// </summary>
        public string GIOGIAO { get; set; }

        /// <summary>
        /// Giờ giao tại FCC.
        /// </summary>
        public string GIOGIAOFCC { get; set; }


        // ============================================================
        // THÔNG TIN NHÀ MÁY / CỬA / TUYẾN
        // ============================================================

        /// <summary>
        /// Nhà máy nhận hàng.
        /// </summary>
        public string NHAMAY { get; set; }

        /// <summary>
        /// Cửa giao hàng.
        /// </summary>
        public string CUA { get; set; }

        /// <summary>
        /// Tuyến/truyền giao hàng.
        /// </summary>
        public string TRUYEN { get; set; }


        // ============================================================
        // PO
        // ============================================================

        /// <summary>
        /// Số PO.
        /// </summary>
        public string PO_NO { get; set; }


        // ============================================================
        // GHI CHÚ / TRẠNG THÁI ĐỐI CHIẾU
        // ============================================================

        /// <summary>
        /// Note hiện tại của phiếu giao.
        /// Dùng để lưu trạng thái nghiệp vụ như:
        /// - Đã huỷ
        /// - Chờ giao bù
        /// - Đã giao bù
        /// - Giao bù cho phiếu nào
        /// </summary>
        public string Note { get; set; }
    }
}
