using PCTP.ClassSQL;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Services;
using PCTP.VIEWSTOCK.Views;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Presenters
{
    // ── Presenters/NhapKhoPresenter.cs ───────────────────────────────────────
    public class NhapKhoPresenter
    {
        private readonly INhapKhoView _view;
        private readonly NhapKhoService _svc;
        private readonly SQLPROVIDER _sql;

        public NhapKhoPresenter(INhapKhoView view,
                                NhapKhoService svc,
                                SQLPROVIDER sql)
        {
            _view = view;
            _svc = svc;
            _sql = sql;
            Subscribe();
        }

        private void Subscribe()
        {
            _view.FormLoaded += (s, e) => LoadPhieu();
            _view.QRSubmitted += OnQRSubmitted;
            _view.NhapKhoClicked += OnNhapKho;
            _view.RefreshClicked += (s, e) => LoadPhieu();
            _view.ResetClicked += OnReset;
        }

        private void LoadPhieu()
        {
            _view.ShowLoading(true, "Đang tải phiếu nhập...");
            try
            {
                var dt = _svc.LoadPhieuNhap();
                _view.BindPhieuNhap(dt);
            }
            catch (Exception ex) { _view.ShowError(ex.Message); }
            finally { _view.ShowLoading(false); }
        }

        private void OnQRSubmitted(object sender, QRScanEventArgs e)
        {
            // Kiểm tra QR
            var check = _svc.KiemTraQR(e);
            if (!check.Success)
            {
                _view.ShowError(check.Message);
                _view.ClearQRInput();
                return;
            }

            if (_view.LoaiHinhNhap == "N")
                ProcessNhapMoi(e);
            else
                ProcessNhapNG(e);
        }

        private void ProcessNhapMoi(QRScanEventArgs e)
        {
            // Tìm phiếu trong grid theo các dạng LOT
            var finds = LotNoHelper.BuildFindList(e.LotNoSL, e.IDSP);
            PhieuNhapInfo phieu = null;

            foreach (string find in finds)
            {
                phieu = _view.FindPhieu(find);
                if (phieu != null) break;
            }

            if (phieu == null)
            {
                _view.ShowError("Không tồn tại phiếu nhập!");
                _view.ClearQRInput();
                return;
            }

            // Kiểm tra vượt SL sản xuất
            if (phieu.SlDaNhap + e.SoLuong > phieu.SlSanXuat)
            {
                bool ok = _view.Confirm(
                    $"Tổng SL nhập ({phieu.SlDaNhap + e.SoLuong}) " +
                    $"> SL sản xuất ({phieu.SlSanXuat}).\nXác nhận tiếp?");
                if (!ok) { _view.ClearQRInput(); return; }
            }

            _view.CapNhapSlSeNhap(phieu.Find, e.SoLuong);
            _view.ClearQRInput();
            _view.FocusQRInput();
        }

        private void ProcessNhapNG(QRScanEventArgs e)
        {
            DataTable tbNG = _sql.ExecuteQuery(_sql.B7R2_FCCdb, $@"
            SELECT LOT, NGAYTRA, SLTRA, SLNHANLAI, LY_DO_NG
            FROM STOCKTPTRAHANG
            WHERE STATUS = 0
              AND LOT = '{e.LotNoSL.Replace("'", "''")}'");

            if (tbNG.Rows.Count == 0)
            {
                _view.ShowError("Không tồn tại phiếu nhập NG!");
                _view.ClearQRInput();
                return;
            }

            var ng = _view.ShowChonNG(e.LotNoSL, tbNG);
            if (ng == null || ng.SoLuong <= 0)
            { _view.ClearQRInput(); return; }

            string findNG = e.LotNoSL + ng.LyDo;
            _view.CapNhapSlSeNhap(findNG, ng.SoLuong);
            _view.ClearQRInput();
            _view.FocusQRInput();
        }

        private void OnNhapKho(object sender, EventArgs e)
        {
            var ds = _view.GetDanhSachSeNhap();
            if (ds.Count == 0)
            { _view.ShowInfo("Không có dữ liệu để nhập kho!"); return; }

            _view.ShowLoading(true, "Đang nhập kho...");
            try
            {
                var (soLot, errors) = _svc.NhapKho(ds);

                if (errors.Count > 0)
                    _view.ShowError(
                        $"Nhập được {soLot} LOT.\nLỗi:\n" +
                        string.Join("\n", errors));
                else
                    _view.ShowInfo($"Nhập kho thành công {soLot} LOT!");

                LoadPhieu();  // Refresh grid
            }
            catch (Exception ex) { _view.ShowError(ex.Message); }
            finally { _view.ShowLoading(false); }
        }

        private void OnReset(object sender, EventArgs e)
        {
            _view.BindPhieuNhap(new DataTable());
            SQLPROVIDER.c_Ns.Clear();
            LoadPhieu();
        }
    }
}
