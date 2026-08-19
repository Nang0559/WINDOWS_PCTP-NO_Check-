using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public interface IPhieuKhachTraRepository
    {
        // ============================================================
        // HEADER
        // ============================================================

        int Insert(PhieuKhachTra entity);

        PhieuKhachTra GetById(int id);

        PhieuKhachTra GetBySoPhieu(string soPhieu);

        List<PhieuKhachTra> GetByNguon(NguonXuLyBatThuong nguon);

        List<PhieuKhachTra> GetChoXuLy();

        void Update(PhieuKhachTra entity);

        void UpdateStatus(
            int id,
            PhieuTraHangStatus status,
            string nguoiThucHien);

        // ============================================================
        // ITEM
        // ============================================================

        int InsertItem(PhieuKhachTraItem item);

        void InsertItems(
            int phieuKhachTraId,
            IEnumerable<PhieuKhachTraItem> items);

        List<PhieuKhachTraItem> GetItems(int phieuKhachTraId);

        PhieuKhachTraItem GetItemById(int itemId);

        // ============================================================
        // LIÊN KẾT PHIẾU BẤT THƯỜNG
        // ============================================================

        void GanPhieuXuLyBatThuong(
            int phieuKhachTraId,
            int phieuXuLyId);

        int? GetPhieuXuLyBatThuongId(int phieuKhachTraId);

        // ============================================================
        // GIAO BÙ
        // ============================================================

        void DanhDauChoGiaoBu(int phieuKhachTraId);

        void DanhDauDaGiaoBu(
            int phieuKhachTraId,
            string nguoiThucHien);

        // ============================================================
        // NOTE / AUDIT
        // ============================================================

        void UpdateNote(
            int id,
            string note,
            string nguoiThucHien);
        /// <summary>Đánh dấu hoàn tất toàn bộ quy trình — set Status=HoanTat VÀ DaHoanTatQTChung=1 cùng lúc.</summary>
        void MarkHoanTat(int id, string nguoiThucHien);

        /// <summary>Lấy danh sách đang xử lý (chưa hoàn tất) lọc thẳng theo Nguon tại DB, tránh load hết rồi filter ở memory.</summary>
        List<PhieuKhachTra> GetChoXuLyByNguon(NguonXuLyBatThuong nguon);

        /// <summary>Gắn DinhDanhPhieuGiao (+ đồng bộ PoNo/NgayGiao/NhaMay) cho 1 item cụ thể.</summary>
        void UpdateItemDinhDanhPhieuGiao(
            int itemId, string dinhDanhPhieuGiao, string poNo, DateTime? ngayGiao, string nhaMay);
    }
}
