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
        protected readonly IWorkflowTransitionService Workflow;

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

        /// <summary>
        /// ProcessCode của state machine PhieuTraHangStatus.
        ///
        /// Khác với QTChung:
        ///     QT_CHUNG
        ///
        /// PhieuTraHang:
        ///     PHIEU_TRA_HANG
        /// </summary>
        protected const string ProcessCode =
            "PHIEU_TRA_HANG";


        protected XuLyHangLoiServiceBase(
            IPhieuTraHangRepository repo,
            IUnitOfWork uow,
            IWorkflowTransitionService workflow)
        {
            Repo = repo
                ?? throw new ArgumentNullException(nameof(repo));

            Uow = uow
                ?? throw new ArgumentNullException(nameof(uow));

            Workflow = workflow
                ?? throw new ArgumentNullException(nameof(workflow));
        }


        // ============================================================
        // GET BY ID
        // ============================================================

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

        public List<PhieuTraHang> GetChoXuLy()
        {
            return Repo.GetChoXuLyByNguon(Nguon);
        }


        // ============================================================
        // CẬP NHẬT TRẠNG THÁI HEADER
        // ============================================================

        /// <summary>
        /// Chuyển trạng thái PhieuTraHangStatus.
        ///
        /// Không chứa transition map trong C#.
        /// Luật chuyển trạng thái được đọc từ:
        ///
        ///     sys_WorkflowTransitions
        ///
        /// thông qua:
        ///
        ///     IWorkflowTransitionService
        ///
        /// ProcessCode:
        ///
        ///     PHIEU_TRA_HANG
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
            // VALIDATE WORKFLOW
            // ========================================================
            //
            // Workflow service sử dụng int.
            // Model nghiệp vụ sử dụng PhieuTraHangStatus.
            //
            // Chuyển enum -> int tại boundary.
            //
            // ========================================================

            Workflow.EnsureCanTransition(
                ProcessCode,
                (int)phieu.Status,
                (int)status);


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
