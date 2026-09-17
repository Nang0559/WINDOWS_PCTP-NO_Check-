using PCTP.Applications.Services;
using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Presentation.Views;
using PCTP.Shared.Models;
using System;
using System.Text;

namespace PCTP.Presentation.Presenters
{
    public sealed class HVN_Presenter : IDisposable
    {
        private readonly HVNPresenterContext _context;
        private readonly PhieuPresenter _phieuPresenter;
        private readonly DocQrPresenter _docQrPresenter;
        private readonly GiaoDbPresenter _giaoDbPresenter;
        private readonly YmvnPresenter _ymvnPresenter;
        public int AddNM => _context.AddNM;
        public bool IsBanQR => _context.IsBanQR;
        public GioXuat GioXuatHienTai => _context.GioXuatHienTai;

        public HVN_Presenter(IHVNView view, IPhieuService phieuSvc, IPhieuLotService lotSvc, IDocQRService qrSvc, IInPhieuService inPhieuSvc, IHangThieuCaNgayService hangThieuCaNgayService, IGioXuatRepository gioXuatRepo, IEventBus bus, bool isMayBanQR, string tenBan, CustomerConfig cfg, IOrderCategoryResolver categoryResolver)
        {
            _context = new HVNPresenterContext(view, phieuSvc, lotSvc, qrSvc, inPhieuSvc, hangThieuCaNgayService, gioXuatRepo, bus, isMayBanQR, tenBan, cfg, categoryResolver);
            _phieuPresenter = new PhieuPresenter(_context);
            _docQrPresenter = new DocQrPresenter(_context);
            _giaoDbPresenter = new GiaoDbPresenter(_context);
            _ymvnPresenter = new YmvnPresenter(_context);
            _context.Bus.Subscribe<FifoReleaseConfirmationRequestedEvent>(OnFifoReleaseConfirmationRequested);
        }

        public void UpdateGioXuat(GioXuat gio) { _context.UpdateGioXuat(gio); }
        public bool OnGiaoDBChanging(int addNm) { return _giaoDbPresenter.OnGiaoDBChanging(addNm); }

        private void OnFifoReleaseConfirmationRequested(FifoReleaseConfirmationRequestedEvent e)
        {
            if (e == null || e.Violations == null || e.Violations.Count == 0)
            {
                e?.Cancel();
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("CẢNH BÁO FIFO");
            sb.AppendLine();
            sb.AppendLine("FIFO là điều kiện bắt buộc trước khi Cập Nhập Kho.");
            sb.AppendLine("Các dòng dưới đây đang chọn LOT không đúng thứ tự FIFO.");
            sb.AppendLine();
            sb.AppendLine("Các dòng QR sẽ được LẤY LẠI LOT và loại khỏi lần cập nhập kho nếu chọn OK:");
            sb.AppendLine();
            sb.AppendLine("STT | Mã hàng | LOT QR | SL | LOT phải xuất trước");
            sb.AppendLine(new string('-', 100));

            foreach (FifoViolation v in e.Violations)
            {
                if (v == null) continue;
                sb.AppendLine(string.Format("{0} | {1} | {2} | {3} | {4}", v.Stt, v.MaHang ?? string.Empty, v.LotDaChon ?? string.Empty, v.SoLuong, v.LotDungRaPhaiChon ?? string.Empty));
            }

            sb.AppendLine();
            sb.AppendLine("Yes: lấy lại LOT các dòng trên và tiếp tục CNK các QR hợp lệ còn lại.");
            sb.AppendLine("CANCEL: giữ nguyên LOT, không loại dòng và không thực hiện CNK.");

            if (_context.PhieuView.Confirm(sb.ToString()))
                e.Confirm();
            else
                e.Cancel();
        }

        public void Dispose()
        {
            _context.Bus.Unsubscribe<FifoReleaseConfirmationRequestedEvent>(OnFifoReleaseConfirmationRequested);
            _ymvnPresenter.Dispose();
            _giaoDbPresenter.Dispose();
            _docQrPresenter.Dispose();
            _phieuPresenter.Dispose();
        }
    }
}
