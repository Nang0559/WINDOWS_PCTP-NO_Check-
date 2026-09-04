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

  


        /// <summary>
        /// Base service dùng chung cho:
        ///
        ///     TraNoiBoService
        ///     KhachTraHangService
        ///
        /// Chịu trách nhiệm:
        ///     - kiểm tra Nguon
        ///     - GetById
        ///     - GetChoXuLy
        ///     - validate state machine Header
        ///     - cập nhật Status Header
        ///     - tạo Header + Detail trong transaction
        ///
        /// KHÔNG chịu trách nhiệm:
        ///     - QTChungStatus
        ///     - QC định hướng
        ///     - Rework
        ///     - Giao bù
        ///     - Giao lại bộ phận
        ///
        /// Các nghiệp vụ trên thuộc service chuyên trách.
        /// </summary>
        public abstract class XuLyHangLoiServiceBase
            : IXuLyHangLoiService
        {
            protected readonly IPhieuTraHangRepository Repo;
            protected readonly IUnitOfWork Uow;

            /// <summary>
            /// Nguồn xử lý của service con.
            ///
            /// TraNoiBoService:
            ///     NguonXuLyBatThuong.TraNoiBo
            ///
            /// KhachTraHangService:
            ///     NguonXuLyBatThuong.KhachTra
            /// </summary>
            protected abstract NguonXuLyBatThuong Nguon { get; }


            protected XuLyHangLoiServiceBase(
                IPhieuTraHangRepository repo,
                IUnitOfWork uow)
            {
                Repo = repo
                    ?? throw new ArgumentNullException(nameof(repo));

                Uow = uow
                    ?? throw new ArgumentNullException(nameof(uow));
            }


            // ============================================================
            // GET BY ID
            // ============================================================

            /// <summary>
            /// Lấy Header theo Id nhưng bắt buộc phải đúng Nguon
            /// của service hiện tại.
            ///
            /// Ví dụ:
            ///
            /// TraNoiBoService.GetById(10)
            ///     chỉ được trả về phiếu Nguon = TraNoiBo.
            ///
            /// Nếu Id tồn tại nhưng thuộc KhachTra:
            ///     trả về null.
            /// </summary>
            public PhieuTraHang GetById(int id)
            {
                if (id <= 0)
                    return null;

                var phieu = Repo.GetById(id);

                if (phieu == null)
                    return null;

                return phieu.Nguon == Nguon
                    ? phieu
                    : null;
            }


            // ============================================================
            // GET CHỜ XỬ LÝ
            // ============================================================

            /// <summary>
            /// Lấy danh sách Header chưa hoàn tất theo Nguon.
            ///
            /// Việc xác định "chưa hoàn tất" được Repository thực hiện
            /// dựa trên PhieuTraHang.Status.
            /// </summary>
            public List<PhieuTraHang> GetChoXuLy()
            {
                return Repo.GetChoXuLyByNguon(Nguon);
            }


            // ============================================================
            // CAP NHAT TRANG THAI HEADER
            // ============================================================

            /// <summary>
            /// Chuyển Status của PhieuTraHang theo state machine Header.
            ///
            /// Lưu ý:
            ///
            /// Đây là state machine:
            ///
            ///     PhieuTraHangStatus
            ///
            /// KHÔNG phải:
            ///
            ///     QTChungStatus
            ///
            /// Vì vậy tuyệt đối không xử lý:
            ///
            ///     DaDinhHuong
            ///     ChoGiaoBu
            ///     DaGiaoBu
            ///     DaXuatKhoRework
            ///     DaGiaoSanXuat
            ///     DaQCXacNhanCuoi
            ///     DaNhapLaiKho
            ///
            /// ở đây.
            /// </summary>
            public void CapNhatTrangThai(
                int id,
                PhieuTraHangStatus status,
                string nguoiThucHien)
            {
                var phieu = GetById(id);

                if (phieu == null)
                {
                    throw new InvalidOperationException(
                        $"Không tìm thấy phiếu xử lý hàng lỗi " +
                        $"Id={id}, Nguon={Nguon}.");
                }


                // ========================================================
                // IDEMPOTENT
                // ========================================================

                if (phieu.Status == status)
                    return;


                // ========================================================
                // VALIDATE STATE MACHINE HEADER
                // ========================================================

                if (!PhieuTraHangStatusTransition.IsValidTransition(
                        Nguon,
                        phieu.Status,
                        status))
                {
                    throw new InvalidOperationException(
                        $"Không thể chuyển trạng thái " +
                        $"{phieu.Status} → {status} " +
                        $"cho phiếu Id={id}, Nguon={Nguon}.");
                }


                // ========================================================
                // PERSISTENCE
                // ========================================================

                try
                {
                    Uow.Begin();

                    Repo.UpdateStatus(
                        id,
                        status,
                        nguoiThucHien);

                    Uow.Commit();
                }
                catch
                {
                    SafeRollback();
                    throw;
                }
            }


            // ============================================================
            // INSERT HEADER + DETAIL
            // ============================================================

            /// <summary>
            /// Helper dùng chung cho:
            ///
            ///     TraNoiBoService.TaoPhieuTraNoiBo()
            ///     KhachTraHangService.TiepNhanPhieuKhachTra()
            ///
            /// Repository chuẩn đã tách:
            ///
            ///     Insert(Header)
            ///     InsertItems(HeaderId, Items)
            ///
            /// nên Service phải thực hiện cả hai trong cùng transaction.
            ///
            /// Header được chuẩn hóa:
            ///
            ///     Nguon
            ///     CreatedBy
            ///     NgayPhatHanh
            ///     Status
            /// </summary>
            protected int InsertPhieu(
                PhieuTraHang phieu,
                string nguoiTao)
            {
                if (phieu == null)
                    throw new ArgumentNullException(nameof(phieu));


                // ========================================================
                // CHUẨN HÓA HEADER
                // ========================================================

                phieu.Nguon = Nguon;

                if (string.IsNullOrWhiteSpace(nguoiTao))
                {
                    nguoiTao = Environment.UserName;
                }

                phieu.CreatedBy = nguoiTao;

                phieu.NgayPhatHanh =
                    phieu.NgayPhatHanh ?? DateTime.Now;


                // ========================================================
                // STATUS BAN ĐẦU
                // ========================================================
                //
                // Header mới:
                //
                //     Moi
                //
                // Sau khi tạo thành công, Header sẵn sàng cho bước
                // tạo PhieuXuLyBatThuong:
                //
                //     ChoTaoPhieuBatThuong
                //
                // Theo thiết kế hiện tại, service tạo phiếu phải đặt
                // trạng thái ban đầu là ChoTaoPhieuBatThuong.
                //
                // ========================================================

                phieu.Status =
                    PhieuTraHangStatus.ChoTaoPhieuBatThuong;


                // ========================================================
                // VALIDATE DETAIL
                // ========================================================

                if (phieu.ChiTiet == null ||
                    phieu.ChiTiet.Count == 0)
                {
                    throw new ArgumentException(
                        "Phiếu phải có ít nhất một dòng chi tiết.",
                        nameof(phieu));
                }


                // ========================================================
                // TRANSACTION
                // ========================================================

                try
                {
                    Uow.Begin();


                    // ----------------------------------------------------
                    // 1. INSERT HEADER
                    // ----------------------------------------------------

                    int id = Repo.Insert(phieu);


                    // ----------------------------------------------------
                    // 2. INSERT DETAIL
                    // ----------------------------------------------------
                    //
                    // Repository chuẩn đã tách InsertItems().
                    // Không được giả định Insert() tự insert detail.
                    //
                    // ----------------------------------------------------

                    Repo.InsertItems(
                        id,
                        phieu.ChiTiet);


                    Uow.Commit();

                    return id;
                }
                catch
                {
                    SafeRollback();
                    throw;
                }
            }


            // ============================================================
            // SAFE ROLLBACK
            // ============================================================

            /// <summary>
            /// Rollback nhưng không che exception gốc.
            /// </summary>
            protected void SafeRollback()
            {
                try
                {
                    Uow.Rollback();
                }
                catch
                {
                    // Không che exception gốc.
                }
            }
        }
    

}
