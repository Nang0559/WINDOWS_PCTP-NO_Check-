using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.UiMd;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{

    public sealed class TraNoiBoService
     : XuLyHangLoiServiceBase, ITraNoiBoService
    {
        private readonly WorkflowEngine _workflow;
        protected override NguonXuLyBatThuong Nguon
            => NguonXuLyBatThuong.TraNoiBo;

        public TraNoiBoService(
            IPhieuTraHangRepository repo,
            WorkflowEngine workflow,
            IUnitOfWork uow)
            : base(repo, uow)
        {
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        }

        // ============================================================
        // TẠO PHIẾU TRẢ NỘI BỘ
        //
        // Header:
        //     Moi
        //       ↓
        //     ChoTaoPhieuBatThuong
        //
        // Base.InsertPhieu() chịu trách nhiệm:
        //     - Nguon = TraNoiBo
        //     - CreatedBy
        //     - NgayPhatHanh
        //     - Status = ChoTaoPhieuBatThuong
        //     - Insert Header + Detail trong transaction
        // ============================================================
        public int TaoPhieuTraNoiBo(PhieuTraHang phieu)
        {
            if (phieu == null)
                throw new ArgumentNullException(nameof(phieu));

            if (phieu.ChiTiet == null || phieu.ChiTiet.Count == 0)
                throw new ArgumentException(
                    "Phiếu trả nội bộ phải có ít nhất một dòng chi tiết.",
                    nameof(phieu));

            foreach (var ct in phieu.ChiTiet)
            {
                if (ct == null)
                    throw new ArgumentException(
                        "Chi tiết phiếu không được null.",
                        nameof(phieu));

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

            // ------------------------------------------------------------
            // Tra nội bộ không có chứng từ khách
            // ------------------------------------------------------------
            phieu.NguonKhachTra = null;
            phieu.SoPhieuKhach = null;

            // ------------------------------------------------------------
            // Tổng số lượng Header
            // ------------------------------------------------------------
            phieu.TongSoLuongNhan =
                phieu.ChiTiet.Sum(x => x.SoLuong);

            // ------------------------------------------------------------
            // Người tạo
            // ------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(phieu.CreatedBy))
                phieu.CreatedBy = Environment.UserName;

            // ------------------------------------------------------------
            // Ngày phát hành
            // ------------------------------------------------------------
            phieu.NgayPhatHanh =
                phieu.NgayPhatHanh ?? DateTime.Now;

            // ------------------------------------------------------------
            // Số phiếu
            // ------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(phieu.SoPhieu))
                phieu.SoPhieu = GenerateSoPhieuTraNoiBo();

            // ------------------------------------------------------------
            // InsertPhieu() của Base sẽ:
            //
            //     phieu.Nguon = TraNoiBo
            //     phieu.Status = ChoTaoPhieuBatThuong
            //     Repo.Insert(phieu)
            //
            // ------------------------------------------------------------
            return InsertPhieu(
                phieu,
                phieu.CreatedBy);
        }


        // ============================================================
        // GIAO LẠI BỘ PHẬN PHÁT HIỆN LỖI
        //
        // Header transition:
        //
        //     ChoGiaoLaiBoPhan
        //             ↓
        //     DaGiaoLaiBoPhan
        //
        // Có thể cho phép gọi từ DangXuLyQTChung nếu nghiệp vụ muốn
        // đi thẳng sau khi QTChung hoàn tất? KHÔNG.
        //
        // Theo state machine Header hiện tại:
        //     DangXuLyQTChung
        //             ↓
        //     ChoGiaoLaiBoPhan
        //
        // nên phải có bước CapNhatTrangThai(...ChoGiaoLaiBoPhan...)
        // trước khi thực hiện giao lại.
        // ============================================================
        public void GiaoLaiBoPhanPhatHien(
            int phieuTraHangId,
            string boPhanNhan,
            int soLuongGiaoLai,
            string nguoiThucHien)
        {
            if (phieuTraHangId <= 0)
                throw new ArgumentException(
                    "PhieuTraHangId không hợp lệ.",
                    nameof(phieuTraHangId));

            if (string.IsNullOrWhiteSpace(boPhanNhan))
                throw new ArgumentException(
                    "Bộ phận nhận không được rỗng.",
                    nameof(boPhanNhan));

            if (soLuongGiaoLai <= 0)
                throw new ArgumentException(
                    "Số lượng giao lại phải lớn hơn 0.",
                    nameof(soLuongGiaoLai));

            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                throw new ArgumentException(
                    "Người thực hiện không được rỗng.",
                    nameof(nguoiThucHien));

            var phieu = GetById(phieuTraHangId);

            if (phieu == null)
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu trả nội bộ Id={phieuTraHangId}.");

            // ------------------------------------------------------------
            // Phải đúng nguồn TraNoiBo
            //
            // GetById() của Base đã lọc Nguon, nhưng giữ check rõ ràng
            // ở đây để nghiệp vụ đặc thù dễ đọc.
            // ------------------------------------------------------------
            if (phieu.Nguon != NguonXuLyBatThuong.TraNoiBo)
                throw new InvalidOperationException(
                    $"Phiếu Id={phieuTraHangId} không thuộc nguồn TraNoiBo.");

            // ------------------------------------------------------------
            // Giao lại phải thực hiện từ:
            //
            //     ChoGiaoLaiBoPhan
            //
            // Không dùng DaNhapLaiKho ở Header nữa.
            //
            // DaNhapLaiKho thuộc QTChungStatus.
            // ------------------------------------------------------------
            if (phieu.Status != PhieuTraHangStatus.ChoGiaoLaiBoPhan)
                throw new InvalidOperationException(
                    $"Phiếu Id={phieuTraHangId} đang ở trạng thái " +
                    $"{phieu.Status}. Chỉ được giao lại khi " +
                    $"Status = {PhieuTraHangStatus.ChoGiaoLaiBoPhan}.");

            // ------------------------------------------------------------
            // Kiểm tra số lượng
            //
            // SoLuongGiaoLai phải <= số lượng OK có thể giao lại.
            // ------------------------------------------------------------
            int soLuongOK =
                phieu.SoLuongGiaoLai
                ?? phieu.TongSoLuongNhan;

            if (soLuongGiaoLai > soLuongOK)
                throw new InvalidOperationException(
                    $"Số lượng giao lại ({soLuongGiaoLai}) vượt quá " +
                    $"số lượng được phép giao lại ({soLuongOK}) " +
                    $"của phiếu Id={phieuTraHangId}.");

            // ------------------------------------------------------------
            // Validate Header state machine
            //
            // ChoGiaoLaiBoPhan
            //          ↓
            // DaGiaoLaiBoPhan
            // ------------------------------------------------------------
            // 1. Kiểm tra luồng trạng thái động qua WorkflowEngine
            string processCode = Nguon.ToProcessCode();
            int fromStatus = (int)phieu.Status;
            int toStatus = (int)PhieuTraHangStatus.DaGiaoLaiBoPhan;

            if (!_workflow.IsValidTransition(processCode, fromStatus, toStatus))
            {
                throw new InvalidOperationException(
                    $"Không thể chuyển trạng thái từ {phieu.Status} sang DaGiaoLaiBoPhan theo quy trình {processCode}.");
            }

            // ------------------------------------------------------------
            // Transaction:
            //
            // 1. Ghi thông tin giao lại
            // 2. Chuyển Header Status
            //
            // Hai thao tác phải atomic.
            // ------------------------------------------------------------
            try
            {
                Uow.Begin();

                Repo.UpdateThongTinGiaoLaiBoPhan(
                    phieuTraHangId,
                    boPhanNhan,
                    soLuongGiaoLai,
                    DateTime.Now,
                    nguoiThucHien);

                bool ok = Repo.UpdateStatusIfCurrentIs(
                phieuTraHangId,
                phieu.Status,
                PhieuTraHangStatus.DaGiaoLaiBoPhan,
                nguoiThucHien);
                if (!ok)
                {
                    throw new InvalidOperationException(
                        $"Trạng thái của phiếu {phieuTraHangId} đã bị thay đổi bởi người khác trong lúc xử lý — vui lòng tải lại.");
                }
                Uow.Commit();
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }


        // ============================================================
        // ĐƯA HEADER VÀO TRẠNG THÁI CHỜ GIAO LẠI
        //
        // DangXuLyQTChung
        //        ↓
        // ChoGiaoLaiBoPhan
        //
        // Đây là transition Header.
        // Không trộn với QTChungStatus.
        // ============================================================
        public void ChoGiaoLaiBoPhan(
            int phieuTraHangId,
            string nguoiThucHien)
        {
            var phieu = GetById(phieuTraHangId);

            if (phieu == null)
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu trả nội bộ Id={phieuTraHangId}.");

            if (phieu.Status ==
                PhieuTraHangStatus.ChoGiaoLaiBoPhan)
                return;
            // 1. Kiểm tra luồng trạng thái động qua WorkflowEngine
            string processCode = Nguon.ToProcessCode();
            int fromStatus = (int)phieu.Status;
            int toStatus = (int)PhieuTraHangStatus.ChoGiaoLaiBoPhan;

            if (!_workflow.IsValidTransition(processCode, fromStatus, toStatus))
            {
                throw new InvalidOperationException(
                    $"Không thể chuyển trạng thái từ {phieu.Status} sang ChoGiaoLaiBoPhan theo quy trình {processCode}.");
            }
            

            try
            {
                Uow.Begin();

                bool ok = Repo.UpdateStatusIfCurrentIs(
                phieuTraHangId,
                phieu.Status,
                PhieuTraHangStatus.ChoGiaoLaiBoPhan,
                nguoiThucHien);
                if (!ok)
                {
                    throw new InvalidOperationException(
                        $"Trạng thái của phiếu {phieuTraHangId} đã bị thay đổi bởi người khác trong lúc xử lý — vui lòng tải lại.");
                }
                Uow.Commit();
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }


        // ============================================================
        // SINH SỐ PHIẾU
        // ============================================================
        private static string GenerateSoPhieuTraNoiBo()
            => $"TNB{DateTime.Now:yyyyMMddHHmmssfff}";
    }

}
