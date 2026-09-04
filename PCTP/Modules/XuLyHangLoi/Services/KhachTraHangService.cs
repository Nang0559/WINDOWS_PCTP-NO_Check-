using PCTP.Modules.XuLyHangLoi.Enums;
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

    public sealed class KhachTraHangService
    : XuLyHangLoiServiceBase, IKhachTraHangService
    {
        private readonly IQTChungService _qtChungService;
        private readonly IPhieuGiaoRepository _phieuGiaoRepo;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;

        protected override NguonXuLyBatThuong Nguon
            => NguonXuLyBatThuong.KhachTra;

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public KhachTraHangService(
             IQTChungService qtChungService,
            IPhieuTraHangRepository repo,
            IPhieuGiaoRepository phieuGiaoRepo,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            IUnitOfWork uow)
            : base(repo, uow)
        {
            _qtChungService = qtChungService
                ?? throw new ArgumentNullException(nameof(qtChungService));
            _phieuGiaoRepo = phieuGiaoRepo
                ?? throw new ArgumentNullException(nameof(phieuGiaoRepo));

            _phieuXuLyRepo = phieuXuLyRepo
                ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
        }

        // ============================================================
        // 1. TIẾP NHẬN PHIẾU KHÁCH TRẢ
        //
        // Header:
        //     Nguon  = KhachTra
        //     Status = ChoTaoPhieuBatThuong
        //
        // InsertPhieu() của Base chịu trách nhiệm:
        //     - Nguon
        //     - CreatedBy
        //     - NgayPhatHanh
        //     - Status = ChoTaoPhieuBatThuong
        //     - transaction Insert Header + Detail
        // ============================================================

        public int TiepNhanPhieuKhachTra(PhieuTraHang phieu)
        {
            if (phieu == null)
                throw new ArgumentNullException(nameof(phieu));

            if (phieu.Nguon != NguonXuLyBatThuong.KhachTra)
                throw new InvalidOperationException(
                    "Phiếu không phải nguồn KhachTra.");

            if (phieu.ChiTiet == null ||
                phieu.ChiTiet.Count == 0)
            {
                throw new ArgumentException(
                    "Phiếu khách trả phải có ít nhất một dòng chi tiết.",
                    nameof(phieu));
            }

            foreach (var ct in phieu.ChiTiet)
            {
                if (string.IsNullOrWhiteSpace(ct.MaHang))
                    throw new ArgumentException(
                        "MaHang không được rỗng.");

                if (string.IsNullOrWhiteSpace(ct.LotNo))
                    throw new ArgumentException(
                        "LotNo không được rỗng.");

                if (ct.SoLuong <= 0)
                    throw new ArgumentException(
                        "SoLuong phải lớn hơn 0.");
            }

            // Khách trả bắt buộc phải xác định nguồn:
            // HVN / YMVN
            if (phieu.NguonKhachTra == null)
                throw new ArgumentException(
                    "Phải xác định nguồn khách trả hàng (HVN/YMVN).");

            phieu.TongSoLuongNhan =
                phieu.ChiTiet.Sum(x => x.SoLuong);

            if (string.IsNullOrWhiteSpace(phieu.CreatedBy))
                phieu.CreatedBy = Environment.UserName;

            return InsertPhieu(
                phieu,
                phieu.CreatedBy);
        }

        // ============================================================
        // 2. TÌM PHIẾU GIAO ỨNG VIÊN
        //
        // Ưu tiên:
        //     LOT
        //
        // Nếu không có LOT:
        //     MAHANG + NGAYGIAO
        // ============================================================

        public List<PhieuGiaoUngVienInfo> TimPhieuGiaoUngVien(
            string maHang,
            DateTime? ngayGiao,
            string lotNo)
        {
            if (!string.IsNullOrWhiteSpace(lotNo))
            {
                return _phieuGiaoRepo.TimTheoLot(lotNo);
            }

            if (!string.IsNullOrWhiteSpace(maHang) &&
                ngayGiao.HasValue)
            {
                return _phieuGiaoRepo.TimTheoMaHangNgayGiao(
                    maHang,
                    ngayGiao.Value);
            }

            throw new ArgumentException(
                "Phải cung cấp LotNo, hoặc cả MaHang và NgayGiao " +
                "để tìm phiếu giao ứng viên.");
        }

        // ============================================================
        // 3. GẮN PHIẾU GIAO GỐC CHO ITEM
        //
        // Không thay đổi PhieuTraHang.Status.
        //
        // Chỉ cập nhật thông tin đối chiếu trên Detail:
        //     DinhDanhPhieuGiao
        //     PO_NO
        //     NGAYGIAO
        //     NHAMAY
        // ============================================================

        public void GanPhieuGiaoGoc(
            int phieuKhachTraItemId,
            string dinhDanhPhieuGiao)
        {
            if (phieuKhachTraItemId <= 0)
                throw new ArgumentException(
                    "phieuKhachTraItemId không hợp lệ.",
                    nameof(phieuKhachTraItemId));

            if (string.IsNullOrWhiteSpace(dinhDanhPhieuGiao))
                throw new ArgumentException(
                    "DinhDanhPhieuGiao không được rỗng.",
                    nameof(dinhDanhPhieuGiao));

            var item =
                Repo.GetItemById(phieuKhachTraItemId);

            if (item == null)
                throw new InvalidOperationException(
                    $"Không tìm thấy item Id={phieuKhachTraItemId}.");

            var phieuKhachTra =
                GetById(item.PhieuTraHangId);

            if (phieuKhachTra == null)
                throw new InvalidOperationException(
                    $"Item {phieuKhachTraItemId} không thuộc " +
                    "phiếu KhachTra.");

            var phieuGiao =
                _phieuGiaoRepo.GetByDinhDanhKey(
                    dinhDanhPhieuGiao);

            if (phieuGiao == null)
                throw new InvalidOperationException(
                    $"DinhDanhKey '{dinhDanhPhieuGiao}' " +
                    "không khớp phiếu giao nào.");

            Repo.UpdateItemDinhDanhPhieuGiao(
                phieuKhachTraItemId,
                dinhDanhPhieuGiao,
                phieuGiao.PO_NO,
                phieuGiao.NGAYGIAO,
                phieuGiao.NHAMAY);
        }

        // ============================================================
        // 4. ĐÁNH DẤU PHIẾU GIAO GỐC → CHỜ GIAO BÙ
        //
        // STATE MACHINE:
        //
        // PhieuTraHang:
        //
        //     DangXuLyQTChung
        //             │
        //             │  không đổi
        //             ▼
        //
        // QTChung:
        //
        //     DaDinhHuong
        //          ↓
        //     ChoGiaoBu
        //
        // Đồng thời:
        //
        // LUUPHIEUGIAOHANG.Note
        //     = CHO_GIAO_BU:...
        //
        // Hai thao tác cùng transaction.
        // ============================================================

        public void DanhDauPhieuGiaoGocChoGiaoBu(
            string dinhDanhPhieuGiao,
            string soPhieuKhachTra,
            string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(dinhDanhPhieuGiao))
                throw new ArgumentException(
                    "DinhDanhPhieuGiao không được rỗng.",
                    nameof(dinhDanhPhieuGiao));

            if (string.IsNullOrWhiteSpace(soPhieuKhachTra))
                throw new ArgumentException(
                    "SoPhieuKhachTra không được rỗng.",
                    nameof(soPhieuKhachTra));

            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                throw new ArgumentException(
                    "NguoiThucHien không được rỗng.",
                    nameof(nguoiThucHien));

            // ========================================================
            // 1. LẤY HEADER
            // ========================================================

            var phieuKhachTra =
                Repo.GetBySoPhieu(soPhieuKhachTra);

            if (phieuKhachTra == null ||
                phieuKhachTra.Nguon != Nguon)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu KhachTra " +
                    $"với SoPhieu='{soPhieuKhachTra}'.");
            }

            // ========================================================
            // 2. HEADER PHẢI ĐANG TRONG QT CHUNG
            //
            // PhieuTraHangStatus KHÔNG có ChoGiaoBu.
            // ========================================================

            if (phieuKhachTra.Status !=
                PhieuTraHangStatus.DangXuLyQTChung)
            {
                throw new InvalidOperationException(
                    $"Phiếu KhachTra Id={phieuKhachTra.Id} " +
                    $"đang ở trạng thái {phieuKhachTra.Status}. " +
                    "Chỉ được đánh dấu giao bù khi Header đang " +
                    "DangXuLyQTChung.");
            }

            // ========================================================
            // 3. KIỂM TRA PHIẾU GIAO GỐC
            // ========================================================

            var phieuGiao =
                _phieuGiaoRepo.GetByDinhDanhKey(
                    dinhDanhPhieuGiao);

            if (phieuGiao == null)
            {
                throw new InvalidOperationException(
                    $"DinhDanhKey '{dinhDanhPhieuGiao}' " +
                    "không khớp phiếu giao nào.");
            }

            // ========================================================
            // 4. LẤY PHIẾU XỬ LÝ BẤT THƯỜNG
            //
            // Quan hệ:
            //
            // PhieuTraHang
            //       ↓
            // PhieuXuLyBatThuong
            //
            // Repository trả về phiếu mới nhất.
            // ========================================================

            var phieuXuLy =
                _phieuXuLyRepo.GetByPhieuTraHangId(
                    phieuKhachTra.Id);

            if (phieuXuLy == null)
            {
                throw new InvalidOperationException(
                    $"Phiếu KhachTra Id={phieuKhachTra.Id} " +
                    "chưa có PhieuXuLyBatThuong.");
            }

            // ========================================================
            // 5. KIỂM TRA NGUỒN
            // ========================================================

            if (phieuXuLy.Nguon != Nguon)
            {
                throw new InvalidOperationException(
                    $"PhieuXuLyBatThuong Id={phieuXuLy.Id} " +
                    $"không thuộc nguồn {Nguon}.");
            }

            // ========================================================
            // 6. KIỂM TRA HƯỚNG XỬ LÝ
            //
            // QTChungStatusTransition dùng:
            //
            //     HuongXuLyBatThuong
            //
            // KHÔNG dùng NguonXuLyBatThuong.
            //
            // Với nhánh giao bù phải là:
            //
            //     HuongXuLyBatThuong.ChiGiaoBu
            // ========================================================

            if (phieuXuLy.HuongXuLy !=
                HuongXuLyBatThuong.ChiGiaoBu)
            {
                throw new InvalidOperationException(
                    $"PhieuXuLyBatThuong Id={phieuXuLy.Id} " +
                    $"không thuộc hướng ChiGiaoBu. " +
                    $"Hướng hiện tại: {phieuXuLy.HuongXuLy}.");
            }

            // ========================================================
            // 7. IDEMPOTENT
            //
            // Nếu đã ChoGiaoBu thì không làm lại.
            // ========================================================

            if (phieuXuLy.Status == QTChungStatus.ChoGiaoBu)
                return;
           
            // ========================================================
            // 8. VALIDATE STATE MACHINE QT CHUNG
            //
            // ChiGiaoBu:
            //
            //     DaDinhHuong
            //          ↓
            //     ChoGiaoBu
            // ========================================================

            //if (!QTChungStatusTransition.IsValidTransition(
            //        HuongXuLyBatThuong.ChiGiaoBu,
            //        phieuXuLy.Status,
            //        QTChungStatus.ChoGiaoBu))
            //{
            //    throw new InvalidOperationException(
            //        $"Không thể chuyển QT Chung " +
            //        $"{phieuXuLy.Status} → " +
            //        $"{QTChungStatus.ChoGiaoBu} " +
            //        $"cho PhieuXuLyBatThuong Id={phieuXuLy.Id}.");
            //}

            // ========================================================
            // 9. NOTE ĐỒNG BỘ PHIẾU GIAO
            // ========================================================

            string note =
                $"CHO_GIAO_BU:" +
                $"{phieuKhachTra.Id}:" +
                $"{nguoiThucHien}:" +
                $"{DateTime.Now:yyyy-MM-dd HH:mm}";

            // ========================================================
            // 10. TRANSACTION
            //
            // A. LUUPHIEUGIAOHANG.Note
            // B. FVN_PhieuXuLyBatThuong.Status
            //
            // Không thay đổi PhieuTraHang.Status.
            // ========================================================

            try
            {
                Uow.Begin();

                // A. Note phiếu giao gốc
                _phieuGiaoRepo.CapNhatNotePhieuGiao(
                    dinhDanhPhieuGiao,
                    note);

                Uow.Commit();

            }
            catch
            {
                SafeRollback();
                throw;
            }
            // Đẩy việc đổi QTChungStatus sang đúng chủ sở hữu — QTChungService
            // (Cần thêm 1 method QTChungService.DanhDauChoGiaoBu tương tự XacNhanChoGiaoBu ở mục 7
            //  nhưng chỉ set status ChoGiaoBu, không gọi GiaoBuNGService)
            var result = _qtChungService.DanhDauChoGiaoBu(phieuXuLy.Id, nguoiThucHien);
            if (!result.IsOK)
                throw new InvalidOperationException(result.Message);
        }
    }

}
