using PCTP.Modules.XuLyHangLoi.Enum;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public interface IPhieuXuLyBatThuongRepository
    {
        // ============================================================
        // CRUD
        // ============================================================

        int Insert(PhieuXuLyBatThuong entity);

        PhieuXuLyBatThuong GetById(int id);

        List<PhieuXuLyBatThuong> GetByNguon(
            NguonXuLyBatThuong nguon);

        // ============================================================
        // STATE MACHINE QT CHUNG
        // ============================================================

        void UpdateTrangThai(
            int id,
            Enum.QTChungStatus trangThai,
            string nguoiThucHien);

        Enum.QTChungStatus GetTrangThai(int id);

        // ============================================================
        // QC ĐỊNH HƯỚNG
        // ============================================================

        void UpdateDinhHuong(
            int id,
            string huongXuLy,
            string nguoiThucHien);

        // ============================================================
        // LIÊN KẾT CHỨNG TỪ GỐC
        // ============================================================

        void GanPhieuKhachTra(
            int phieuXuLyId,
            int phieuKhachTraId);

        int? GetPhieuKhachTraId(int phieuXuLyId);
    }
}
