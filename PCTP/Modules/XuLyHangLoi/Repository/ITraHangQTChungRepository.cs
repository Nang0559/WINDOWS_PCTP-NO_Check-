using PCTP.Modules.XuLyHangLoi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public interface ITraHangQTChungRepository
    {
        // ============================================================
        // XUẤT KHO REWORK
        // ============================================================

        int InsertXuat(TraHangQTChungXuat entity);

        List<TraHangQTChungXuat> GetXuat(int phieuXuLyId);

        TraHangQTChungXuat GetXuatById(int id);


        // ============================================================
        // GIAO CHO SẢN XUẤT
        // ============================================================

        int InsertGiao(TraHangQTChungGiao entity);

        List<TraHangQTChungGiao> GetGiao(int phieuXuLyId);

        TraHangQTChungGiao GetGiaoById(int id);


        // ============================================================
        // QC XÁC NHẬN CUỐI
        // ============================================================

        int InsertQC(TraHangQTChungQC entity);

        TraHangQTChungQC GetQC(int phieuXuLyId);


        // ============================================================
        // NHẬP NG
        // ============================================================

        int InsertNhapNG(TraHangQTChungNhapNG entity);

        List<TraHangQTChungNhapNG> GetNhapNG(int phieuXuLyId);

        TraHangQTChungNhapNG GetNhapNGById(int id);


        // ============================================================
        // TIMELINE
        // ============================================================

        List<QTChungTimelineItem> GetTimeline(
            int phieuXuLyId);


        // ============================================================
        // KIỂM TRA TIẾN ĐỘ
        // ============================================================

        bool DaXuatKho(int phieuXuLyId);

        bool DaGiaoSanXuat(int phieuXuLyId);

        bool DaQCXacNhan(int phieuXuLyId);

        bool DaNhapNG(int phieuXuLyId);


        // ============================================================
        // TỔNG HỢP
        // ============================================================

        int GetTongSoLuongDaXuat(
            int phieuXuLyId);

        int GetTongSoLuongDaGiao(
            int phieuXuLyId);

        int GetTongSoLuongOK(
            int phieuXuLyId);

        int GetTongSoLuongNG(
            int phieuXuLyId);

        int GetTongSoLuongDaNhapNG(
            int phieuXuLyId);
    }
}
