using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using PCTP.Shared.UiMd;
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
         IUnitOfWork uow,
         IWorkflowTransitionService workflow)
         : base(repo, uow, workflow)
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

                // Khách có thể chỉ báo MÃ HÀNG mà không có LOT: khi đó để trống LOT (không bắt buộc);
                // phạm vi ảnh hưởng sẽ là toàn bộ tồn của mã (xem AffectedLotTraceService.Trace).
                ct.LotNo = string.IsNullOrWhiteSpace(ct.LotNo) ? string.Empty : ct.LotNo.Trim();

                if (ct.SoLuong <= 0)
                    throw new ArgumentException(
                        "SoLuong phải lớn hơn 0.");
            }

            // Khách trả bắt buộc phải xác định khách hàng (CustomerNo hợp lệ trong CustomerTableConfig).
            if (string.IsNullOrWhiteSpace(phieu.CustomerNo))
                throw new ArgumentException(
                    "Phải xác định khách hàng trả hàng (CustomerNo).");

            // Chuẩn hoá + chặn khách chưa cấu hình (ném KeyNotFoundException).
            phieu.CustomerNo = CustomerTableConfig.Get(phieu.CustomerNo).CustomerNo;

            // Khách trả BẮT BUỘC đối chiếu được với lần giao gốc (thông tin hàng đã giao):
            // cùng mã hàng, đúng khách. SL khách trả được phép KHÁC SL đã giao
            // (trả một phần / trả gộp nhiều lần giao) nên KHÔNG chặn theo số lượng.
            foreach (var ct in phieu.ChiTiet)
                XacThucVaDienPhieuGiaoGoc(phieu, ct);

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
        // 2b. ĐỐI CHIẾU THEO KHOẢNG THỜI GIAN
        //
        // Cùng một mã hàng / LOT có thể được giao nhiều lần: trả về tất cả các
        // lần giao trong khoảng thời gian để người tiếp nhận chọn đúng phiếu.
        // ============================================================

        public List<PhieuGiaoUngVienInfo> TimPhieuGiaoTheoKhoang(
            string customerNo,
            string maHang,
            string lotNo,
            DateTime tuNgay,
            DateTime denNgay)
        {
            if (string.IsNullOrWhiteSpace(maHang))
                throw new ArgumentException(
                    "Phải có Mã hàng để tìm phiếu giao.",
                    nameof(maHang));

            if (tuNgay.Date > denNgay.Date)
                throw new ArgumentException(
                    "Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.");

            string[] patterns = null;

            if (!string.IsNullOrWhiteSpace(customerNo))
            {
                // Ném KeyNotFoundException nếu khách chưa cấu hình (giống TiepNhanPhieuKhachTra).
                var cfg = CustomerTableConfig.Get(customerNo);

                if (cfg.NhaMayMatchPatterns != null && cfg.NhaMayMatchPatterns.Length > 0)
                    patterns = cfg.NhaMayMatchPatterns;
            }

            var list = _phieuGiaoRepo.TimTheoKhoang(
                maHang, lotNo, tuNgay, denNgay, patterns)
                ?? new List<PhieuGiaoUngVienInfo>();

            foreach (var slip in list)
                slip.SoLuongLot = TinhSoLuongLot(slip, lotNo);

            return list;
        }

        public int? LaySoLuongDaGiao(string dinhDanhPhieuGiao, string lotNo)
        {
            if (string.IsNullOrWhiteSpace(dinhDanhPhieuGiao))
                return null;

            var slip = _phieuGiaoRepo.GetByDinhDanhKey(dinhDanhPhieuGiao.Trim());

            if (slip == null)
                return null;

            return TinhSoLuongLot(slip, lotNo) ?? slip.SOLUONG;
        }

        /// <summary>
        /// SL của đúng LOT trong cột LOT ghép của phiếu giao ("LOT1-100,LOT2-50").
        /// Null nếu không có LOT cần tra hoặc LOT không nằm trong phiếu.
        /// </summary>
        private static int? TinhSoLuongLot(PhieuGiaoUngVienInfo slip, string lotNo)
        {
            if (slip == null || string.IsNullOrWhiteSpace(lotNo))
                return null;

            decimal tong = 0;
            bool thay = false;

            foreach (var part in PCTP.Common.LotCodeHelper.ParseCompositeLotTolerant(
                         slip.LOT, slip.SOLUONG))
            {
                if (!part.IsValid || !part.HasQuantity)
                    continue;

                if (!PCTP.Common.LotCodeHelper.AreLotKeysEquivalent(part.LotNo, lotNo))
                    continue;

                tong += part.Quantity;
                thay = true;
            }

            return thay ? (int?)decimal.ToInt32(tong) : null;
        }

        /// <summary>
        /// Phiếu giao gốc của dòng khách trả phải tồn tại, đúng mã hàng và đúng khách;
        /// PO / Ngày giao / Nhà máy lấy từ chính phiếu giao (không tin dữ liệu form gửi lên).
        /// </summary>
        private void XacThucVaDienPhieuGiaoGoc(PhieuTraHang phieu, PhieuTraHangCT ct)
        {
            if (string.IsNullOrWhiteSpace(ct.DinhDanhPhieuGiao))
                throw new ArgumentException(
                    $"Mã hàng {ct.MaHang}: phải xác định phiếu giao gốc " +
                    "(thông tin hàng đã giao) khi tiếp nhận phiếu khách trả.");

            var slip = _phieuGiaoRepo.GetByDinhDanhKey(ct.DinhDanhPhieuGiao.Trim());

            if (slip == null)
                throw new InvalidOperationException(
                    $"Mã hàng {ct.MaHang}: phiếu giao gốc " +
                    $"'{ct.DinhDanhPhieuGiao}' không còn tồn tại.");

            if (!string.Equals(
                    (slip.MAHANG ?? string.Empty).Trim(),
                    ct.MaHang.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Phiếu giao gốc thuộc mã {slip.MAHANG}, " +
                    $"không phải mã {ct.MaHang} của dòng khách trả.");

            var khachCuaPhieuGiao = CustomerTableConfig.ResolveByNhaMay(slip.NHAMAY);

            if (khachCuaPhieuGiao != null &&
                !string.Equals(
                    khachCuaPhieuGiao.CustomerNo,
                    phieu.CustomerNo,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Phiếu giao gốc (nhà máy {slip.NHAMAY}) thuộc khách " +
                    $"{khachCuaPhieuGiao.CustomerNo}, không phải khách " +
                    $"{phieu.CustomerNo} đang trả hàng.");

            ct.DinhDanhPhieuGiao = slip.DinhDanhKey;
            // FVN_PhieuTraHangCT.PoNo nvarchar(50) / NhaMay nvarchar(100); PO_NO phiếu giao dài tới 200.
            ct.PoNo = CatChuoi(slip.PO_NO, 50);
            ct.NgayGiao = slip.NGAYGIAO;
            ct.NhaMay = CatChuoi(slip.NHAMAY, 100);
        }

        private static string CatChuoi(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value = value.Trim();

            return value.Length <= maxLen ? value : value.Substring(0, maxLen);
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

            // Ghi Note phiếu giao gốc và đổi QTChungStatus trong cùng transaction.
            // Nếu một trong hai thao tác lỗi, QTChungService rollback cả hai.
            var result = _qtChungService.DanhDauChoGiaoBu(
                phieuXuLy.Id,
                nguoiThucHien,
                () => _phieuGiaoRepo.CapNhatNotePhieuGiao(dinhDanhPhieuGiao, note));
            if (!result.IsOK)
                throw new InvalidOperationException(result.Message);
        }
    }

}
