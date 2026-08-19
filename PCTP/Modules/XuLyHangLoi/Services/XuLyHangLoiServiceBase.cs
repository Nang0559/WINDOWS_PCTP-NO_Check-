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
        /// Base dùng chung cho TraNoiBoService/KhachTraHangService. Toàn bộ logic
        /// KHÔNG phụ thuộc Nguon (CRUD header, kiểm tra state machine, insert phiếu)
        /// nằm ở đây — subclass chỉ khai báo Nguon và thêm nghiệp vụ đặc thù.
        /// </summary>
        public abstract class XuLyHangLoiServiceBase : IXuLyHangLoiService
        {
            protected readonly IPhieuKhachTraRepository Repo;
            protected readonly IUnitOfWork Uow;

            protected abstract NguonXuLyBatThuong Nguon { get; }

            protected XuLyHangLoiServiceBase(IPhieuKhachTraRepository repo, IUnitOfWork uow)
            {
                Repo = repo ?? throw new ArgumentNullException(nameof(repo));
                Uow = uow ?? throw new ArgumentNullException(nameof(uow));
            }

            // ============================================================
            // GetById — chỉ trả về nếu đúng Nguon của service này (tránh
            // KhachTraHangService.GetById lỡ trả về 1 phiếu TraNoiBo).
            // ============================================================

            public PhieuKhachTra GetById(int id)
            {
                var phieu = Repo.GetById(id);
                return phieu != null && phieu.Nguon == Nguon ? phieu : null;
            }

            // ============================================================
            // GetChoXuLy — lọc thẳng tại DB theo Nguon
            // ============================================================

            public List<PhieuKhachTra> GetChoXuLy() => Repo.GetChoXuLyByNguon(Nguon);

            // ============================================================
            // CapNhatTrangThai — state machine dùng chung, delegate xuống
            // đúng method repo chuyên biệt cho từng đích đến (vì 1 vài trạng
            // thái còn kèm set thêm cờ boolean: DaGiaoBu, DaHoanTatQTChung).
            // ============================================================

            public void CapNhatTrangThai(int id, PhieuTraHangStatus status, string nguoiThucHien)
            {
                var phieu = GetById(id);
                if (phieu == null)
                    throw new InvalidOperationException($"Không tìm thấy phiếu (Nguon={Nguon}) Id={id}.");

                if (phieu.Status == status)
                    return; // idempotent — gọi lại đúng trạng thái hiện tại không phải lỗi

                if (!PhieuTraHangStatusTransition.IsValidTransition(Nguon, phieu.Status, status))
                    throw new InvalidOperationException(
                        $"Không thể chuyển trạng thái {phieu.Status} → {status} " +
                        $"cho phiếu (Nguon={Nguon}) Id={id}.");

                try
                {
                    Uow.Begin();

                    switch (status)
                    {
                        case PhieuTraHangStatus.ChoGiaoBu:
                            Repo.DanhDauChoGiaoBu(id);
                            break;

                        case PhieuTraHangStatus.DaGiaoBu:
                            Repo.DanhDauDaGiaoBu(id, nguoiThucHien);
                            break;

                        case PhieuTraHangStatus.HoanTat:
                            Repo.MarkHoanTat(id, nguoiThucHien);
                            break;

                        default:
                            Repo.UpdateStatus(id, status, nguoiThucHien);
                            break;
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
            // Helper insert dùng chung cho TiepNhanPhieuKhachTra/TaoPhieuTraNoiBo
            // ============================================================

            protected int InsertPhieu(PhieuKhachTra phieu, string nguoiTao)
            {
                phieu.Nguon = Nguon;
                phieu.CreatedBy = nguoiTao;
                phieu.NgayPhatHanh = phieu.NgayPhatHanh ?? DateTime.Now;
                phieu.Status = PhieuTraHangStatus.ChoTaoPhieuBatThuong;

                try
                {
                    Uow.Begin();
                    int id = Repo.Insert(phieu); // Insert() đã tự InsertItems bên trong
                    Uow.Commit();
                    return id;
                }
                catch
                {
                    SafeRollback();
                    throw;
                }
            }

            protected void SafeRollback()
            {
                try { Uow.Rollback(); } catch { /* không che exception gốc */ }
            }
        }
    
}
