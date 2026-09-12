using PCTP.Applications.Services;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Presentation.Views;
using PCTP.Shared.Models;
using System;

namespace PCTP.Presentation.Presenters
{
    /// <summary>
    /// Facade tương thích ngược cho màn hình Giao Hàng Khách.
    /// Use-case UI đã được tách thành PhieuPresenter, DocQrPresenter,
    /// GiaoDbPresenter và YmvnPresenter.
    /// </summary>
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

        public HVN_Presenter(IHVNView view, PhieuService phieuSvc, DocQRService qrSvc, InPhieuService inPhieuSvc, IHangThieuCaNgayService hangThieuCaNgayService, IGioXuatRepository gioXuatRepo, IEventBus bus, bool isMayBanQR, string tenBan, CustomerConfig cfg, IOrderCategoryResolver categoryResolver)
        {
            _context = new HVNPresenterContext(view, phieuSvc, qrSvc, inPhieuSvc, hangThieuCaNgayService, gioXuatRepo, bus, isMayBanQR, tenBan, cfg, categoryResolver);
            _phieuPresenter = new PhieuPresenter(_context);
            _docQrPresenter = new DocQrPresenter(_context);
            _giaoDbPresenter = new GiaoDbPresenter(_context);
            _ymvnPresenter = new YmvnPresenter(_context);
        }
        public void UpdateGioXuat(GioXuat gio) { _context.UpdateGioXuat(gio); }
        public bool OnGiaoDBChanging(int addNm) { return _giaoDbPresenter.OnGiaoDBChanging(addNm); }
        public void Dispose() { _ymvnPresenter.Dispose(); _giaoDbPresenter.Dispose(); _docQrPresenter.Dispose(); _phieuPresenter.Dispose(); }
    }
}
