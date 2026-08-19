using PCTP.Modules.XuLyHangLoi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    
        /// <summary>
        /// Hành vi CHUNG cho cả 2 nhánh KhachTra/TraNoiBo — cùng thao tác trên
        /// FVN_PhieuKhachTra, chỉ khác Nguon và tập trạng thái hợp lệ (xem
        /// PhieuTraHangStatusTransition). Đặt ở đây để 2 service không lặp code.
        /// </summary>
        public interface IXuLyHangLoiService
        {
            PhieuKhachTra GetById(int id);

            /// <summary>Danh sách phiếu (đúng Nguon của service này) chưa hoàn tất QT Chung.</summary>
            List<PhieuKhachTra> GetChoXuLy();

            /// <summary>
            /// Chuyển trạng thái theo đúng state machine của Nguon tương ứng.
            /// Ném InvalidOperationException nếu bước chuyển không hợp lệ.
            /// Idempotent: gọi lại đúng trạng thái hiện tại thì không làm gì.
            /// </summary>
            void CapNhatTrangThai(int id, PhieuTraHangStatus status, string nguoiThucHien);
        }

        
    
}
