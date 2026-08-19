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
   
        public sealed class TraNoiBoService : XuLyHangLoiServiceBase, ITraNoiBoService
        {
            protected override NguonXuLyBatThuong Nguon => NguonXuLyBatThuong.TraNoiBo;

            public TraNoiBoService(IPhieuKhachTraRepository repo, IUnitOfWork uow)
                : base(repo, uow) { }

            // ============================================================
            // TẠO PHIẾU TRẢ NỘI BỘ
            // Không có "chứng từ khách" -> tự sinh SoPhieu, tạo thẳng 1 item duy nhất.
            // ============================================================

            public int TaoPhieuTraNoiBo(
                string maHang, string lotNo, int soLuong, string noiDung, string nguoiTao)
            {
                if (string.IsNullOrWhiteSpace(maHang))
                    throw new ArgumentException("MaHang không được rỗng.", nameof(maHang));
                if (string.IsNullOrWhiteSpace(lotNo))
                    throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
                if (soLuong <= 0)
                    throw new ArgumentException("SoLuong phải lớn hơn 0.", nameof(soLuong));

                var phieu = new PhieuKhachTra
                {
                    SoPhieu = GenerateSoPhieuTraNoiBo(),
                    TongSoLuongNhan = soLuong,
                    Note = noiDung,
                    Items = new List<PhieuKhachTraItem>
                {
                    new PhieuKhachTraItem
                    {
                        MaHang = maHang,
                        LotNo = lotNo,
                        SoLuong = soLuong,
                        NoiDungLoi = noiDung
                    }
                }
                };

                return InsertPhieu(phieu, nguoiTao);
            }

            // ⚠ Tạm sinh theo timestamp — nếu hệ thống đã có PhieuNoHelper chuyên
            // dụng (như PhieuNoHelper.NewMaPhieuNhap trong NhapTpReceivingService),
            // thay thế dòng này để đồng bộ format số phiếu toàn hệ thống.
            private static string GenerateSoPhieuTraNoiBo()
                => $"TNB{DateTime.Now:yyyyMMddHHmmss}";
        }
    
}
