using PCTP.Modules.XuLyHangLoi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public interface IPhieuGiaoRepository
    {
        // ============================================================
        // TÌM PHIẾU GIAO THEO LOT
        // ============================================================

        List<PhieuGiaoUngVienInfo> TimTheoLot(
            string lotNo);

        // ============================================================
        // TÌM THEO MÃ HÀNG + NGÀY GIAO
        // ============================================================

        List<PhieuGiaoUngVienInfo> TimTheoMaHangNgayGiao(
            string maHang,
            DateTime ngayGiao);

        // ============================================================
        // TÌM PHIẾU CỤ THỂ
        // ============================================================

        PhieuGiaoUngVienInfo GetByDinhDanhKey(
            string dinhDanhKey);

        // ============================================================
        // TRẠNG THÁI GIAO BÙ
        // ============================================================

        List<PhieuGiaoUngVienInfo> GetPhieuChoGiaoBu(
            string maHang);
        // ── THÊM: nhận từ GiaoBuNGRepository cũ — cùng bảng LUUPHIEUGIAOHANG ──
        void CapNhatNotePhieuGiao(string dinhDanhKey, string note);
        //void DanhDauChoGiaoBu(string dinhDanhKey, int phieuKhachTraId);
    }
}
