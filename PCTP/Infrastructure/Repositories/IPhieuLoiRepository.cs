using PCTP.Domain.Entities;
using PCTP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public interface IPhieuLoiRepository
    {
        int InsertPhieuLoiKhachTra(PhieuLoiKhachTra header);   // trả về Id header
        PhieuLoiKhachTra GetPhieuLoiKhachTra(int id);
        List<PhieuLoiKhachTra> GetDanhSachChuaXuLy();           // header còn dòng CT chưa gán PhieuXuLyBatThuongId

        int InsertPhieuXuLyBatThuong(PhieuXuLyBatThuong pht);   // gán luôn SoPhieu tự sinh
        void CapNhatQCDuyet(QCDuyetInput input);
        void DanhDauDaTraVeSX(int id, int slotId, string lot);

        // <summary>
        /// Tra phiếu Xử Lý Bất Thường đang ở trạng thái mới nhất khớp với LOT này.
        /// Join theo MaSanPham + SoLo/SoLoLoi — nhưng LOT trong hệ thống có thể là
        /// khoá cũ 13 ký tự hoặc khoá mới 20 ký tự, nên bước so khớp cuối PHẢI dùng
        /// LotCodeHelper.AreLotKeysEquivalent (không so bằng '=' cứng).
        /// Trả null nếu LOT chưa từng có phiếu xử lý bất thường nào.
        /// </summary>
        PhieuXuLyBatThuong GetPhieuXuLyBatThuongTheoLot(string lot);

        /// <summary>
        /// Đánh dấu phiếu đã trả về SX thành công — gọi NGAY SAU KHI trừ kho xong,
        /// trong CÙNG transaction với thao tác trừ SlotLot/STOCKTP để đảm bảo không
        /// bao giờ có trạng thái "đã trừ kho nhưng phiếu vẫn hiện CHO_QC/QCDaDuyet".
        /// </summary>
        void CapNhatDaTraVeSX(SqlConnection conn, SqlTransaction tran,
            int phieuId, int slotId, string lot);
        PhieuXuLyBatThuong GetPhieuXuLyBatThuong(int id);
        List<PhieuXuLyBatThuong> GetDanhSachChoQC();
        List<PhieuXuLyBatThuong> GetDanhSachDaDuyetChuaTra(); // TrangThai = QCDaDuyet
                                                              // ── Đếm cho Badge Timeline ──────────────────────────────────────────
        int DemChuaNhapLieu();          // Bước 1: chứng từ khách gửi tới nhưng CT chưa gán PhieuXuLyBatThuongId
        int DemChoBanHanhPhieuBatThuong(); // Bước 2: dòng CT đã nhập nhưng chưa sinh PhieuXuLyBatThuong
        int DemChoQC();                  // Bước 3: PhieuXuLyBatThuong.TrangThai = ChoQC
        int DemSanSangTra();             // Bước 4: TrangThai = QCDaDuyet

        // ── Dữ liệu Grid cho từng bước ──────────────────────────────────────
        DataTable GetGridBuoc1_ChungTuMoi();
        DataTable GetGridBuoc2_ChoSinhPhieuBatThuong();
        DataTable GetGridBuoc3_ChoQC();
        DataTable GetGridBuoc4_SanSangTra();

        // Nhánh nội bộ — tạo thẳng phiếu bất thường từ Slot, bỏ qua bước 1
        int InsertPhieuXuLyBatThuongNoiBo(PhieuXuLyBatThuong p);

        // Bước 3a: QC định hướng — ChoQC -> QCDaDinhHuong
        void CapNhatQCDinhHuong(int id, string loaiLoi, string phuongPhapDinhHuong, string nguoiQC);

        // Bước 4: SX báo xong — QCDaDinhHuong -> ChoQCXacNhanCuoi
        void DanhDauSanXuatBaoXong(int id, string ghiChu, string nguoiThucHien);

        // Bước 5: giữ nguyên CapNhatQCDuyet(QCDuyetInput) NHƯNG thêm guard
        //   chỉ cho phép khi TrangThai == ChoQCXacNhanCuoi, nếu không throw

        // Grid/đếm cho 3 trạng thái mới (tách nhỏ khỏi "GetDanhSachChoQC" cũ)
        List<PhieuXuLyBatThuong> GetDanhSachChoQCDinhHuong();     // TrangThai = ChoQC
        List<PhieuXuLyBatThuong> GetDanhSachDangSanXuat();        // TrangThai = QCDaDinhHuong
        List<PhieuXuLyBatThuong> GetDanhSachChoQCXacNhanCuoi();   // TrangThai = ChoQCXacNhanCuoi

        int DemChoQCDinhHuong();
        int DemDangSanXuat();
        int DemChoXacNhanCuoi();

        DataTable GetGridDinhHuong();
        DataTable GetGridDangSanXuat();
        DataTable GetGridXacNhanCuoi();
    }
}
