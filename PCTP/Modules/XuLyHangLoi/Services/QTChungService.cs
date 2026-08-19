using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Enum;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public sealed class QTChungService : IQTChungService
    {
        private readonly IUnitOfWork _uow;
        private readonly IReworkStockService _reworkStockService;
        private readonly ITraHangQTChungRepository _qtChungRepo;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;

        public QTChungService(
            IUnitOfWork uow,
            IReworkStockService reworkStockService,
            ITraHangQTChungRepository qtChungRepo,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _reworkStockService =
                reworkStockService ?? throw new ArgumentNullException(nameof(reworkStockService));
            _qtChungRepo =
                qtChungRepo ?? throw new ArgumentNullException(nameof(qtChungRepo));
            _phieuXuLyRepo =
                phieuXuLyRepo ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
        }

        // ════════════════════════════════════════════════════════════════
        // 1. TẠO PHIẾU XỬ LÝ BẤT THƯỜNG
        // ════════════════════════════════════════════════════════════════
        public int TaoPhieuXuLyBatThuong(PhieuXuLyBatThuong phieu)
        {
            if (phieu == null)
                throw new ArgumentNullException(nameof(phieu));

            if (phieu.PhieuKhachTraId <= 0)
                throw new ArgumentException(
                    "Phiếu xử lý bất thường phải gắn với 1 phiếu cha (KhachTra/TraNoiBo).");

            phieu.Status = QTChungStatus.Moi;

            try
            {
                _uow.Begin();

                int id = _phieuXuLyRepo.Insert(phieu);

                // Mới chỉ là trạng thái tức thời.
                // Tạo xong -> Chờ QC định hướng.
                _phieuXuLyRepo.UpdateStatus(
                    id,
                    QTChungStatus.ChoQCDinhHuong,
                    phieu.CreatedBy);

                _uow.Commit();

                return id;
            }
            catch (Exception)
            {
                SafeRollback();
                throw;
            }
        }


        // ════════════════════════════════════════════════════════════════
        // 2. QC ĐỊNH HƯỚNG REWORK
        //
        // Moi
        //   ↓
        // ChoQCDinhHuong
        //   ↓
        // ChoXuatKhoRework
        // ════════════════════════════════════════════════════════════════
        public void QCDinhHuongRework(
            int phieuXuLyId,
            string huongXuLy,
            string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(huongXuLy))
                throw new ArgumentException(
                    "Hướng xử lý không được rỗng.",
                    nameof(huongXuLy));

            var phieu = RequirePhieu(phieuXuLyId);

            RequireTransition(
                phieu.Status,
                QTChungStatus.ChoXuatKhoRework);

            try
            {
                _uow.Begin();

                _phieuXuLyRepo.UpdateHuongXuLy(
                    phieuXuLyId,
                    huongXuLy,
                    nguoiThucHien);

                // Sau khi QC định hướng xong:
                // Chờ xuất kho rework.
                _phieuXuLyRepo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.ChoXuatKhoRework,
                    nguoiThucHien);

                _uow.Commit();
            }
            catch (Exception)
            {
                SafeRollback();
                throw;
            }
        }


        // ════════════════════════════════════════════════════════════════
        // 3. TÌM TOÀN BỘ LOT NG CÒN TRONG KHO
        // ════════════════════════════════════════════════════════════════
        public List<LotInfo> GetLotsCanRework(int phieuXuLyId)
            => _reworkStockService.GetLotsCanReworkByPhieuXuLy(phieuXuLyId);


        // ════════════════════════════════════════════════════════════════
        // 4. XUẤT KHO ĐI REWORK
        //
        // ChoXuatKhoRework
        //       ↓
        // DaXuatKhoRework
        //
        // Cho phép xuất nhiều lần / nhiều LOT.
        // ════════════════════════════════════════════════════════════════
        public ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotId,
            string lotNo,
            int soLuong,
            string nguoiXuat)
        {
            var phieu = RequirePhieu(phieuXuLyId);

            if (phieu.Status != QTChungStatus.ChoXuatKhoRework &&
                phieu.Status != QTChungStatus.DaXuatKhoRework)
            {
                return ScanResult.Fail(
                    $"Phiếu đang ở trạng thái {phieu.Status} — " +
                    "phải QC định hướng REWORK trước khi xuất kho.");
            }

            if (soLuong <= 0)
                return ScanResult.Fail("Số lượng xuất kho phải lớn hơn 0.");

            if (string.IsNullOrWhiteSpace(lotNo))
                return ScanResult.Fail("LOT không được rỗng.");

            // ReworkStockService tự transaction phần:
            // Slot / STOCKTP / History / InsertXuat.
            var result = _reworkStockService.XuatKhoRework(
                phieuXuLyId,
                slotId,
                lotNo,
                soLuong,
                nguoiXuat);

            if (!result.IsOK)
                return result;

            // Nếu lần đầu xuất:
            //
            // ChoXuatKhoRework -> DaXuatKhoRework
            //
            // Nếu đã xuất trước đó:
            //
            // DaXuatKhoRework -> DaXuatKhoRework
            //
            // là idempotent.
            TryUpdateStatus(
                phieuXuLyId,
                QTChungStatus.DaXuatKhoRework,
                nguoiXuat);

            return result;
        }


        // ════════════════════════════════════════════════════════════════
        // 5. GIAO HÀNG CHO SẢN XUẤT
        //
        // DaXuatKhoRework
        //       ↓
        // DaGiaoSanXuat
        //
        // Sau khi giao xong, sản xuất có thể bắt đầu rework.
        // ════════════════════════════════════════════════════════════════
        public ScanResult GiaoHangRework(
            int phieuXuLyId,
            List<LotInfo> lots,
            string nguoiGiao)
        {
            if (lots == null || lots.Count == 0)
                return ScanResult.Fail(
                    "Danh sách LOT giao cho sản xuất đang rỗng.");

            var phieu = RequirePhieu(phieuXuLyId);

            if (phieu.Status != QTChungStatus.DaXuatKhoRework)
            {
                return ScanResult.Fail(
                    $"Phiếu đang ở trạng thái {phieu.Status} — " +
                    "phải xuất kho rework trước khi giao cho sản xuất.");
            }

            // Kiểm tra thêm bằng dữ liệu thực tế trong DB.
            if (!_qtChungRepo.DaXuatKho(phieuXuLyId))
            {
                return ScanResult.Fail(
                    "Phiếu chưa xuất kho — không thể giao cho sản xuất.");
            }

            int tongDaXuat =
                _qtChungRepo.GetTongSoLuongDaXuat(phieuXuLyId);

            int tongDaGiaoTruoc =
                _qtChungRepo.GetTongSoLuongDaGiao(phieuXuLyId);

            int tongGiaoLanNay =
                lots.Sum(l => l.Quantity);

            if (tongGiaoLanNay <= 0)
            {
                return ScanResult.Fail(
                    "Tổng số lượng giao phải lớn hơn 0.");
            }

            if (tongDaGiaoTruoc + tongGiaoLanNay > tongDaXuat)
            {
                return ScanResult.Fail(
                    $"Tổng SL giao ({tongDaGiaoTruoc + tongGiaoLanNay}) " +
                    $"sẽ vượt quá SL đã xuất kho ({tongDaXuat}).");
            }

            try
            {
                _uow.Begin();

                var giaoIds = new List<int>();

                foreach (var lot in lots)
                {
                    if (lot == null)
                        continue;

                    if (lot.Quantity <= 0)
                        continue;

                    if (string.IsNullOrWhiteSpace(lot.LotNo))
                        continue;

                    giaoIds.Add(
                        _qtChungRepo.InsertGiao(
                            new TraHangQTChungGiao
                            {
                                PhieuXuLyId = phieuXuLyId,
                                LotNo = lot.LotNo,
                                MaHang = lot.QRInfo?.ItemCode
                                         ?? phieu.MaSanPham,
                                SoLuong = lot.Quantity,
                                NguoiGiao = nguoiGiao
                            }));
                }

                if (giaoIds.Count == 0)
                {
                    SafeRollback();

                    return ScanResult.Fail(
                        "Không có LOT hợp lệ để giao cho sản xuất.");
                }

                // Giao hàng thành công.
                _phieuXuLyRepo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.DaGiaoSanXuat,
                    nguoiGiao);

                _uow.Commit();

                return ScanResult.OK(
                    $"Đã giao {tongGiaoLanNay} SP " +
                    $"({giaoIds.Count} LOT) cho sản xuất.");
            }
            catch (Exception ex)
            {
                SafeRollback();

                return ScanResult.Fail(
                    "Lỗi giao hàng cho sản xuất: " + ex.Message);
            }
        }


        // ════════════════════════════════════════════════════════════════
        // 6. SẢN XUẤT BẮT ĐẦU / ĐANG REWORK
        //
        // DaGiaoSanXuat
        //       ↓
        // DangRework
        //
        // Vì enum có DangRework nên dùng trạng thái này làm mốc
        // sản xuất đang xử lý.
        // ════════════════════════════════════════════════════════════════
        public void BatDauRework(
            int phieuXuLyId,
            string nguoiThucHien)
        {
            var phieu = RequirePhieu(phieuXuLyId);

            RequireTransition(
                phieu.Status,
                QTChungStatus.DangRework);

            try
            {
                _uow.Begin();

                _phieuXuLyRepo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.DangRework,
                    nguoiThucHien);

                _uow.Commit();
            }
            catch (Exception)
            {
                SafeRollback();
                throw;
            }
        }


        // ════════════════════════════════════════════════════════════════
        // 7. SẢN XUẤT BÁO REWORK XONG
        //
        // DangRework
        //       ↓
        // ChoQCXacNhanCuoi
        // ════════════════════════════════════════════════════════════════
        public void SanXuatBaoReworkXong(
            int phieuXuLyId,
            string ghiChu,
            string nguoiThucHien)
        {
            var phieu = RequirePhieu(phieuXuLyId);

            RequireTransition(
                phieu.Status,
                QTChungStatus.ChoQCXacNhanCuoi);

            try
            {
                _uow.Begin();

                _phieuXuLyRepo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.ChoQCXacNhanCuoi,
                    nguoiThucHien);

                _uow.Commit();
            }
            catch (Exception)
            {
                SafeRollback();
                throw;
            }
        }


        // ════════════════════════════════════════════════════════════════
        // 8. QC XÁC NHẬN CUỐI
        //
        // ChoQCXacNhanCuoi
        //       ↓
        // QCDaXacNhan
        //
        // Nếu:
        //   OK = toàn bộ -> HoanTat
        //
        // Nếu:
        //   Có NG -> DaNhapNG
        // ════════════════════════════════════════════════════════════════
        public ScanResult QCXacNhanCuoi(
            int phieuXuLyId,
            int soLuongOK,
            int soLuongNG,
            string nguoiQC)
        {
            if (soLuongOK < 0 || soLuongNG < 0)
            {
                return ScanResult.Fail(
                    "Số lượng OK/NG không được âm.");
            }

            if (soLuongOK == 0 && soLuongNG == 0)
            {
                return ScanResult.Fail(
                    "Số lượng OK và NG không được đồng thời bằng 0.");
            }

            var phieu = RequirePhieu(phieuXuLyId);

            if (phieu.Status != QTChungStatus.ChoQCXacNhanCuoi)
            {
                return ScanResult.Fail(
                    $"Phiếu đang ở trạng thái {phieu.Status} — " +
                    "SX phải báo rework xong trước khi QC xác nhận cuối.");
            }

            if (_qtChungRepo.DaQCXacNhan(phieuXuLyId))
            {
                return ScanResult.Fail(
                    "Phiếu đã có QC xác nhận trước đó.");
            }

            try
            {
                _uow.Begin();

                int qcId = _qtChungRepo.InsertQC(
                    new TraHangQTChungQC
                    {
                        PhieuXuLyId = phieuXuLyId,
                        SoLuongDaRework = soLuongOK + soLuongNG,
                        SoLuongOK = soLuongOK,
                        SoLuongNG = soLuongNG,
                        NguoiQC = nguoiQC
                    });

                // Luôn ghi nhận mốc QC đã xác nhận.
                _phieuXuLyRepo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.QCDaXacNhan,
                    nguoiQC);

                if (soLuongNG > 0)
                {
                    // Có hàng NG -> chờ nhập NG.
                    _phieuXuLyRepo.UpdateStatus(
                        phieuXuLyId,
                        QTChungStatus.DaNhapNG,
                        nguoiQC);
                }
                else
                {
                    // Không có NG -> hoàn tất ngay.
                    _phieuXuLyRepo.UpdateStatus(
                        phieuXuLyId,
                        QTChungStatus.HoanTat,
                        nguoiQC);
                }

                _uow.Commit();

                return ScanResult.OK(
                    $"QC xác nhận (QcId={qcId}): " +
                    $"OK={soLuongOK}, NG={soLuongNG}." +
                    (soLuongNG == 0
                        ? " Không có hàng NG — QT chung đã hoàn tất."
                        : " Còn hàng NG cần nhập lại kho."));
            }
            catch (Exception ex)
            {
                SafeRollback();

                return ScanResult.Fail(
                    "Lỗi QC xác nhận cuối: " + ex.Message);
            }
        }


        // ════════════════════════════════════════════════════════════════
        // 9. NHẬP LẠI HÀNG NG
        //
        // QCDaXacNhan
        //       ↓
        // DaNhapNG
        //       ↓
        // HoanTat
        //
        // Lưu ý:
        // DaNhapNG là trạng thái "đang/đã nhập NG".
        // Cho phép nhập nhiều lần cho đến khi đủ số lượng NG.
        // ════════════════════════════════════════════════════════════════
        public ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuongNG,
            string nguoiNhap)
        {
            var phieu = RequirePhieu(phieuXuLyId);

            if (phieu.Status != QTChungStatus.DaNhapNG)
            {
                return ScanResult.Fail(
                    $"Phiếu đang ở trạng thái {phieu.Status} — " +
                    "không cần/không thể nhập lại NG.");
            }

            if (soLuongNG <= 0)
            {
                return ScanResult.Fail(
                    "Số lượng NG nhập lại phải lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(lotNo))
            {
                return ScanResult.Fail(
                    "LOT nhập lại NG không được rỗng.");
            }

            var qc = _qtChungRepo.GetQC(phieuXuLyId);

            if (qc == null)
            {
                return ScanResult.Fail(
                    "Phiếu chưa có QC xác nhận — " +
                    "không thể nhập lại hàng NG.");
            }

            if (qc.SoLuongNG <= 0)
            {
                return ScanResult.Fail(
                    "Phiếu không có số lượng NG theo kết quả QC.");
            }

            int daNhapTruoc =
                _qtChungRepo.GetTongSoLuongDaNhapNG(phieuXuLyId);

            if (daNhapTruoc + soLuongNG > qc.SoLuongNG)
            {
                return ScanResult.Fail(
                    $"Tổng SL nhập lại NG ({daNhapTruoc + soLuongNG}) " +
                    $"sẽ vượt quá SL NG theo QC ({qc.SoLuongNG}).");
            }

            // ReworkStockService tự transaction cho:
            // Slot / History / InsertNhapNG.
            //
            // slotIdDich = null:
            // service tự chọn slot mặc định theo implementation hiện tại.
            var result = _reworkStockService.NhapLaiHangNG(
                phieuXuLyId,
                lotNo,
                soLuongNG,
                slotIdDich: null,
                nguoiNhap);

            if (!result.IsOK)
                return result;

            int daNhapSauKhiThem =
                _qtChungRepo.GetTongSoLuongDaNhapNG(phieuXuLyId);

            // Chưa nhập đủ NG:
            // giữ nguyên DaNhapNG.
            if (daNhapSauKhiThem < qc.SoLuongNG)
            {
                return result;
            }

            // Đã nhập đủ toàn bộ NG -> hoàn tất.
            TryUpdateStatus(
                phieuXuLyId,
                QTChungStatus.HoanTat,
                nguoiNhap);

            return result;
        }


        // ════════════════════════════════════════════════════════════════
        // 10. HUỶ QT CHUNG
        //
        // Tất cả trạng thái chưa HoanTat/Huy đều có thể -> Huy.
        // Nếu đã xuất kho thì hoàn trả kho trước khi đánh dấu Huy.
        // ════════════════════════════════════════════════════════════════
        public ScanResult HuyQTChung(
            int phieuXuLyId,
            string lyDoHuy,
            string nguoiThucHien)
        {
            var phieu = RequirePhieu(phieuXuLyId);

            if (phieu.Status == QTChungStatus.HoanTat)
            {
                return ScanResult.Fail(
                    "Phiếu đã hoàn tất — không thể huỷ.");
            }

            if (phieu.Status == QTChungStatus.Huy)
            {
                return ScanResult.Fail(
                    "Phiếu đã huỷ trước đó.");
            }

            // Hoàn trả kho — chỉ cần thiết nếu đã từng xuất kho.
            ScanResult stockResult =
                ScanResult.OK("Chưa xuất kho — không cần hoàn trả.");

            if (_qtChungRepo.DaXuatKho(phieuXuLyId))
            {
                stockResult =
                    _reworkStockService.HoanTraKhoKhiHuy(
                        phieuXuLyId,
                        nguoiThucHien);

                if (!stockResult.IsOK)
                    return stockResult;
            }

            try
            {
                _uow.Begin();

                _phieuXuLyRepo.MarkHuy(
                    phieuXuLyId,
                    lyDoHuy,
                    nguoiThucHien);

                _uow.Commit();
            }
            catch (Exception ex)
            {
                SafeRollback();

                return ScanResult.Fail(
                    "Lỗi cập nhật trạng thái huỷ: " + ex.Message);
            }

            return ScanResult.OK(
                $"Đã huỷ QT chung (Id={phieuXuLyId}). " +
                stockResult.Message);
        }


        // ════════════════════════════════════════════════════════════════
        // 11. TRA CỨU
        // ════════════════════════════════════════════════════════════════
        public PhieuXuLyBatThuong GetById(int phieuXuLyId)
            => _phieuXuLyRepo.GetById(phieuXuLyId);

        public QTChungStatus GetTrangThai(int phieuXuLyId)
            => RequirePhieu(phieuXuLyId).Status;

        public List<QTChungTimelineItem> GetTimeline(int phieuXuLyId)
            => _qtChungRepo.GetTimeline(phieuXuLyId);


        // ════════════════════════════════════════════════════════════════
        // 12. HELPERS
        // ════════════════════════════════════════════════════════════════

        private PhieuXuLyBatThuong RequirePhieu(int phieuXuLyId)
        {
            var phieu = _phieuXuLyRepo.GetById(phieuXuLyId);

            if (phieu == null)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu xử lý bất thường Id={phieuXuLyId}.");
            }

            return phieu;
        }


        private static void RequireTransition(
            QTChungStatus from,
            QTChungStatus to)
        {
            if (!QTChungStatusTransition.IsValid(from, to))
            {
                throw new InvalidOperationException(
                    $"Không thể chuyển trạng thái {from} → {to}.");
            }
        }


        /// <summary>
        /// Cập nhật trạng thái best-effort.
        ///
        /// Không throw nếu:
        /// - Không tìm thấy phiếu
        /// - Trạng thái đã là target
        /// - Transition không hợp lệ
        ///
        /// Dùng cho các mốc có thể được gọi nhiều lần,
        /// ví dụ xuất kho nhiều LOT hoặc nhập NG nhiều lần.
        /// </summary>
        private void TryUpdateStatus(
            int phieuXuLyId,
            QTChungStatus target,
            string nguoiThucHien)
        {
            var phieu = _phieuXuLyRepo.GetById(phieuXuLyId);

            if (phieu == null)
                return;

            if (phieu.Status == target)
                return;

            if (!QTChungStatusTransition.IsValid(
                    phieu.Status,
                    target))
            {
                return;
            }

            try
            {
                _uow.Begin();

                _phieuXuLyRepo.UpdateStatus(
                    phieuXuLyId,
                    target,
                    nguoiThucHien);

                _uow.Commit();
            }
            catch
            {
                SafeRollback();
            }
        }


        private void SafeRollback()
        {
            try
            {
                _uow.Rollback();
            }
            catch
            {
                // Không throw lỗi rollback đè lên lỗi gốc.
            }
        }
    }
}
