using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Entities
{
    public enum TrangThaiXuLyBatThuong
    {
        ChoQC = 0,              // giữ nguyên — chờ QC định hướng ban đầu
        QCDaDuyet = 1,          // giữ nguyên — QC đã CHỐT CUỐI, đủ điều kiện trả về SX
        DaTraVeSX = 2,          // giữ nguyên
        Huy = 3,                // giữ nguyên
        QCDaDinhHuong = 4,      // MỚI — QC đã định hướng, đang chuyển cho SX xử lý
        ChoQCXacNhanCuoi = 5    // MỚI — SX báo xong, chờ QC chốt OK/NG cuối cùng
    }

    public enum NguonPhieuBatThuong { KhachTra = 1, NoiBo = 2 }

    public class PhieuXuLyBatThuong
    {
        public int Id { get; set; }
        public string SoPhieu { get; set; }          // tự sinh, vd "BT-260811-0001"
        public int PhieuLoiKhachTraCTId { get; set; }

        public string Model { get; set; }
        public string MaSanPham { get; set; }
        public string SoLo { get; set; }
        public string SoLoLoi { get; set; }
        public int SoLuongLoi { get; set; }
        public string PhanLoaiXuLy { get; set; }      // "Hỏng NG YMVN trả về", "Nơ lò xo khác chuẩn"...
        public string NoiDungBatThuong { get; set; }
        public string CapDoQuanTrong { get; set; }
        public string CapDoPhienBan { get; set; }

        // Phương pháp xử lý (Nắn/Vặn/Cắt gọt...) + Phương pháp sửa (Thay SPR...)
        public string PhuongPhapXuLy { get; set; }
    
        public string KetQuaXuLy { get; set; }        // OK / NG / Vứt (cải)

        public string NguoiThucHien { get; set; }
        public string BoPhanPhatHanh { get; set; }
        public string QCTiepNhan { get; set; }
        public string BoPhanPhatHanhXacNhan { get; set; }

        public TrangThaiXuLyBatThuong TrangThai { get; set; } = TrangThaiXuLyBatThuong.ChoQC;
        public DateTime NgayTao { get; set; } = DateTime.Now;
        public DateTime? NgayQCDuyet { get; set; }
        public string NguoiQCDuyet { get; set; }

        // Sau khi trả về SX thành công — lưu vết để không cho trả 2 lần
        public int? SlotIdDaTra { get; set; }
        public string LotDaTra { get; set; }
        public DateTime? NgayTraVeSX { get; set; }

        // ── Vùng "Phương pháp kiểm tra" (ô trái) ──────────────────────
        public string PhuongPhapKiemTra { get; set; }   // "Nắn", "Viết"...
        public string KetQuaKiemTra { get; set; }        // "OK" | "NG" | "Cải"
        public int? SoLuongKiemTra { get; set; }

        // ── Vùng "Phương pháp sửa" (ô phải) ────────────────────────────
        public string PhuongPhapSua { get; set; }
        public string KetQuaSua { get; set; }            // "OK" | "NG" | "Cải"
        public int? SoLuongSua { get; set; }

        // ── Vùng "Xác nhận lần cuối (phòng chất lượng)" ────────────────
        public string XacNhanCuoiKetQua { get; set; }    // "OK" | "NG"
        public string NguoiDanhGia { get; set; }          // ô "Người đánh giá"
        public string NguoiThucHienQC { get; set; }       // ô "Người thực hiện" (khác NguoiThucHien của kho!)
        public string GhiChuQC { get; set; }

        // ── Bảng chữ ký (4 cột, Ngày + Họ tên) ──────────────────────────
        public DateTime? NgayBoPhanPhatSinh { get; set; }
        public string HoTenBoPhanPhatSinh { get; set; }

        public DateTime? NgayQCTiepNhan { get; set; }
        public string HoTenQCTiepNhan { get; set; }

        public DateTime? NgayBoPhanPhatHanhXacNhan { get; set; }
        public string HoTenBoPhanPhatHanhXacNhan { get; set; }
        public string LoaiSanPham { get; set; }          // "Sản phẩm lỗi" / "Sản phẩm model cũ" / "Sản phẩm test" / "Sản phẩm không rõ ràng"
        public string BoPhanChiuTrachNhiem { get; set; }  // hiển thị ở khối "Kết luận"

        public string HoTenQCDuyet { get; set; }   // MG hoặc QM ký duyệt cuối


        public NguonPhieuBatThuong Nguon { get; set; } = NguonPhieuBatThuong.KhachTra;
        public int? SlotIdNguon { get; set; }
        public string LotNguon { get; set; }

        public string LoaiLoi { get; set; }
        public string PhuongPhapDinhHuong { get; set; }
        public string NguoiQCDinhHuong { get; set; }
        public DateTime? NgayQCDinhHuong { get; set; }

        public DateTime? NgaySXBaoXong { get; set; }
        public string NguoiSXBaoXong { get; set; }
        public string GhiChuSanXuat { get; set; }

        public bool DuDieuKienDinhHuong =>
            TrangThai == TrangThaiXuLyBatThuong.ChoQC;

        public bool DuDieuKienSanXuatBaoXong =>
            TrangThai == TrangThaiXuLyBatThuong.QCDaDinhHuong;

        public bool DuDieuKienQCXacNhanCuoi =>
            TrangThai == TrangThaiXuLyBatThuong.ChoQCXacNhanCuoi;

        // DuDieuKienTraVeSX giữ nguyên logic cũ (chỉ cần TrangThai == QCDaDuyet)
        // ── THÊM: gộp điều kiện "đủ điều kiện trả về SX" thành 1 property duy nhất,
        // tránh Form nào cũng tự viết lại logic so sánh (dễ lệch nhau) ──────────────
        public bool DuDieuKienTraVeSX =>
            TrangThai == TrangThaiXuLyBatThuong.QCDaDuyet
            && !SlotIdDaTra.HasValue
            && string.Equals(XacNhanCuoiKetQua, "OK", StringComparison.OrdinalIgnoreCase);
    }
}
