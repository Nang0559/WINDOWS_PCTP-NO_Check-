using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
   
        public sealed class KhachTraHangService : XuLyHangLoiServiceBase, IKhachTraHangService
        {
            private readonly IPhieuGiaoRepository _phieuGiaoRepo;

            protected override NguonXuLyBatThuong Nguon => NguonXuLyBatThuong.KhachTra;

            public KhachTraHangService(
                IPhieuKhachTraRepository repo,
                IPhieuGiaoRepository phieuGiaoRepo,
                IUnitOfWork uow)
                : base(repo, uow)
            {
                _phieuGiaoRepo = phieuGiaoRepo ?? throw new ArgumentNullException(nameof(phieuGiaoRepo));
            }

            // ============================================================
            // 1. TIẾP NHẬN PHIẾU KHÁCH TRẢ
            // ============================================================

            public int TiepNhanPhieuKhachTra(PhieuKhachTra phieu)
            {
                if (phieu == null) throw new ArgumentNullException(nameof(phieu));
                if (phieu.Items == null || phieu.Items.Count == 0)
                    throw new ArgumentException("Phiếu khách trả phải có ít nhất 1 item.", nameof(phieu));

                // InsertPhieu tự set Nguon=KhachTra/Status=ChoTaoPhieuBatThuong,
                // nhưng CreatedBy ở đây lấy từ chính phiếu (do UI đã điền sẵn).
                return InsertPhieu(phieu, phieu.CreatedBy);
            }

            // ============================================================
            // 2. TÌM PHIẾU GIAO CŨ
            // ============================================================

            public List<PhieuGiaoUngVienInfo> TimPhieuGiaoUngVien(
                string maHang, DateTime? ngayGiao, string lotNo)
            {
                if (!string.IsNullOrWhiteSpace(lotNo))
                    return _phieuGiaoRepo.TimTheoLot(lotNo);

                if (!string.IsNullOrWhiteSpace(maHang) && ngayGiao.HasValue)
                    return _phieuGiaoRepo.TimTheoMaHangNgayGiao(maHang, ngayGiao.Value);

                throw new ArgumentException(
                    "Phải cung cấp LotNo, hoặc cả MaHang và NgayGiao để tìm phiếu giao ứng viên.");
            }

            // ============================================================
            // 3. GẮN PHIẾU GIAO GỐC CHO ITEM TRẢ
            // Xác thực item thuộc đúng phiếu Nguon=KhachTra, và DinhDanhKey
            // khớp 1 phiếu giao có thật trước khi lưu.
            // ============================================================

            public void GanPhieuGiaoGoc(int phieuKhachTraItemId, string dinhDanhPhieuGiao)
            {
                var item = Repo.GetItemById(phieuKhachTraItemId);
                if (item == null)
                    throw new InvalidOperationException($"Không tìm thấy item Id={phieuKhachTraItemId}.");

                var phieuChaCua = GetById(item.PhieuKhachTraId);
                if (phieuChaCua == null)
                    throw new InvalidOperationException(
                        $"Item {phieuKhachTraItemId} không thuộc phiếu Khách trả nào (hoặc không tìm thấy phiếu cha).");

                var phieuGiao = _phieuGiaoRepo.GetByDinhDanhKey(dinhDanhPhieuGiao);
                if (phieuGiao == null)
                    throw new InvalidOperationException($"DinhDanhKey '{dinhDanhPhieuGiao}' không khớp phiếu giao nào.");

                Repo.UpdateItemDinhDanhPhieuGiao(
                    phieuKhachTraItemId, dinhDanhPhieuGiao, phieuGiao.PO_NO, phieuGiao.NGAYGIAO, phieuGiao.NHAMAY);
            }

            // ============================================================
            // 6. ĐÁNH DẤU PHIẾU GIAO CŨ — chờ giao bù
            // Đồng bộ 2 phía trong CÙNG 1 transaction:
            //   - Note trên LUUPHIEUGIAOHANG (phía phiếu giao gốc)
            //   - Status = ChoGiaoBu trên FVN_PhieuKhachTra (qua state machine chung)
            // ============================================================

            public void DanhDauPhieuGiaoChoGiaoBu(
                string dinhDanhPhieuGiao, string soPhieuKhachTra, string nguoiThucHien)
            {
                var phieuKhachTra = Repo.GetBySoPhieu(soPhieuKhachTra);
                if (phieuKhachTra == null || phieuKhachTra.Nguon != Nguon)
                    throw new InvalidOperationException(
                        $"Không tìm thấy phiếu Khách trả với SoPhieu='{soPhieuKhachTra}'.");

                try
                {
                    Uow.Begin();

                    _phieuGiaoRepo.CapNhatNotePhieuGiao(
                        dinhDanhPhieuGiao,
                        $"CHO_GIAO_BU:{phieuKhachTra.Id}:{nguoiThucHien}:{DateTime.Now:yyyy-MM-dd HH:mm}");

                    // Dùng lại state machine chung — vẫn đúng vì CapNhatTrangThai tự
                    // Begin/Commit riêng; ở đây nối tiếp trong cùng Uow instance nên
                    // gọi trực tiếp Repo.DanhDauChoGiaoBu thay vì gọi lại public
                    // CapNhatTrangThai (tránh nested transaction).
                    if (!PhieuTraHangStatusTransition.IsValidTransition(
                            Nguon, phieuKhachTra.Status, PhieuTraHangStatus.ChoGiaoBu))
                        throw new InvalidOperationException(
                            $"Không thể chuyển trạng thái {phieuKhachTra.Status} → ChoGiaoBu cho phiếu Id={phieuKhachTra.Id}.");

                    Repo.DanhDauChoGiaoBu(phieuKhachTra.Id);

                    Uow.Commit();
                }
                catch
                {
                    SafeRollback();
                    throw;
                }
            }
        }
    
}
