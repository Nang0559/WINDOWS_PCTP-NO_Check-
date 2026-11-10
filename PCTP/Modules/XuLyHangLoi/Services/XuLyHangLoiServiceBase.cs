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

        /// <summary>Gác tồn kho / số lượng đang bị phiếu khác giữ (gán ở nơi dựng service).</summary>
        public ILotClaimGuard LotGuard { get; set; }

        public List<LotKhaDungInfo> TraCuuLotTrongKho(string maHang, string lotNo, int? boQuaPhieuTraHangId)
        {
            if (LotGuard == null)
                return new List<LotKhaDungInfo>();

            return LotGuard.TraCuuLotTrongKho(maHang, lotNo, boQuaPhieuTraHangId);
        }

        /// <summary>
        /// Khóa chặt + kiểm tra trước khi ghi dòng chi tiết. Gọi NGAY SAU Uow.Begin():
        /// 1) khóa applock theo từng MaHang+LOT (thứ tự cố định, tránh deadlock);
        /// 2) mỗi LOT chỉ một dòng cho mỗi Slot (nội bộ) / mỗi lần giao (khách trả);
        /// 3) nội bộ: SL không vượt tồn khả dụng của Slot (tồn − phần phiếu khác đang giữ);
        /// 4) khách trả: SL không vượt phần còn lại của lần giao (SL giao − các phiếu trước đã nhận).
        /// </summary>
        protected void KiemTraLotTruocKhiLuu(PhieuTraHang phieu, int? boQuaPhieuTraHangId)
        {
            var items = phieu == null ? null : phieu.ChiTiet;
            if (items == null || items.Count == 0)
                return;

            var keys = items
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.MaHang))
                .Select(x => Tuple.Create(
                    x.MaHang.Trim().ToUpperInvariant(),
                    (x.LotNo ?? string.Empty).Trim().ToUpperInvariant()))
                .Distinct()
                .OrderBy(t => t.Item1, StringComparer.Ordinal)
                .ThenBy(t => t.Item2, StringComparer.Ordinal)
                .ToList();

            foreach (var k in keys)
                Repo.KhoaLot(k.Item1, k.Item2);

            if (Nguon == NguonXuLyBatThuong.TraNoiBo)
            {
                var trungSlot = items
                    .Where(x => x != null)
                    .GroupBy(x => new
                    {
                        Ma = (x.MaHang ?? string.Empty).Trim().ToUpperInvariant(),
                        Lot = (x.LotNo ?? string.Empty).Trim().ToUpperInvariant(),
                        Slot = x.SlotIdNguon ?? 0
                    })
                    .Where(g => g.Count() > 1)
                    .Select(g => "- " + g.Key.Ma + " / LOT " + g.Key.Lot + " (SlotId " + g.Key.Slot + ")")
                    .ToList();

                if (trungSlot.Count > 0)
                    throw new InvalidOperationException(
                        "Cùng một LOT chỉ được có nhiều dòng khi KHÁC Slot. Dòng trùng LOT + Slot:\n"
                        + string.Join("\n", trungSlot));

                if (LotGuard == null)
                    throw new InvalidOperationException(
                        "Chưa cấu hình LotGuard cho nghiệp vụ trả nội bộ nên không kiểm tra được tồn khả dụng.");

                LotGuard.KiemTraTonKhaDung(items, boQuaPhieuTraHangId);
            }
            else
            {
                var trungGiao = items
                    .Where(x => x != null)
                    .GroupBy(x => new
                    {
                        Ma = (x.MaHang ?? string.Empty).Trim().ToUpperInvariant(),
                        Lot = (x.LotNo ?? string.Empty).Trim().ToUpperInvariant(),
                        Giao = (x.DinhDanhPhieuGiao ?? string.Empty).Trim()
                    })
                    .Where(g => g.Count() > 1)
                    .Select(g => "- " + g.Key.Ma + " / LOT " + g.Key.Lot)
                    .ToList();

                if (trungGiao.Count > 0)
                    throw new InvalidOperationException(
                        "Cùng một LOT chỉ được có nhiều dòng khi KHÁC lần giao. Dòng trùng LOT + lần giao:\n"
                        + string.Join("\n", trungGiao));

                KiemTraSoLuongTheoPhieuGiao(phieu, boQuaPhieuTraHangId);
            }
        }

        /// <summary>Khách trả override: chặn tổng SL trả vượt SL đã giao của từng lần giao.</summary>
        protected virtual void KiemTraSoLuongTheoPhieuGiao(PhieuTraHang phieu, int? boQuaPhieuTraHangId)
        {
        }

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

                KiemTraLotTruocKhiLuu(phieu, null);


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
        // MỞ LẠI PHIẾU: SỬA / XÓA
        // ============================================================

        public bool LaPhieuDaDong(PhieuTraHang phieu)
        {
            return phieu != null && phieu.Status == PhieuTraHangStatus.HoanTat;
        }

        public bool CoPhieuXuLy(int phieuTraHangId)
        {
            return phieuTraHangId > 0 && Repo.CoPhieuXuLy(phieuTraHangId);
        }

        public void CapNhatPhieu(PhieuTraHang phieu, string nguoiThucHien)
        {
            if (phieu == null)
                throw new ArgumentNullException(nameof(phieu));

            var current = GetById(phieu.Id);

            if (current == null)
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu Id={phieu.Id}, Nguon={Nguon}.");

            if (LaPhieuDaDong(current))
                throw new InvalidOperationException(
                    "Phiếu đã đóng (hoàn tất), không thể sửa.");

            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                nguoiThucHien = Environment.UserName;

            bool thayChiTiet =
                phieu.ChiTiet != null && !Repo.CoPhieuXuLy(phieu.Id);

            if (thayChiTiet && phieu.ChiTiet.Count == 0)
                throw new ArgumentException(
                    "Phiếu phải có ít nhất một dòng chi tiết.",
                    nameof(phieu));

            // Giữ nguyên các trường nghiệp vụ không nằm trên form.
            phieu.Nguon = current.Nguon;
            phieu.Status = current.Status;
            phieu.SoPhieu = current.SoPhieu;
            phieu.CreatedAt = current.CreatedAt;
            phieu.CreatedBy = current.CreatedBy;
            phieu.UpdatedBy = nguoiThucHien;

            if (thayChiTiet)
                phieu.TongSoLuongNhan = phieu.ChiTiet.Sum(x => x.SoLuong);
            else
                phieu.TongSoLuongNhan = current.TongSoLuongNhan;

            try
            {
                Uow.Begin();

                if (thayChiTiet)
                    KiemTraLotTruocKhiLuu(phieu, phieu.Id);

                Repo.Update(phieu);

                if (thayChiTiet)
                {
                    Repo.DeleteItems(phieu.Id);

                    foreach (var ct in phieu.ChiTiet)
                    {
                        ct.Id = 0;
                        ct.PhieuTraHangId = phieu.Id;
                    }

                    Repo.InsertItems(phieu.Id, phieu.ChiTiet);
                }

                Uow.Commit();
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }

        public void XoaPhieu(int id, string nguoiThucHien)
        {
            var current = GetById(id);

            if (current == null)
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu Id={id}, Nguon={Nguon}.");

            if (LaPhieuDaDong(current))
                throw new InvalidOperationException(
                    "Phiếu đã đóng (hoàn tất), không thể xóa.");

            if (Repo.CoPhieuXuLy(id))
                throw new InvalidOperationException(
                    "Phiếu đã phát sinh Phiếu xử lý bất thường, không thể xóa.");

            try
            {
                Uow.Begin();

                Repo.DeleteItems(id);
                Repo.Delete(id);

                Uow.Commit();
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
