using System;
using PCTP.QRCODE_HVN.PGH.Controls;

namespace PCTP.QRCODE_HVN.PGH
{
    partial class HVN_PGH
    {
        private void PhieuActionBarControl_ActionClicked(
            object sender,
            PhieuActionBarEventArgs e)
        {
            switch (e.Action)
            {
                case PhieuActionBarAction.DocQRCode:
                    DocQRCodeClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.KiemTraGhepLot:
                    if (string.Equals(e.Button.Tag as string,
                        "ACTION:GhepLotToggle",
                        StringComparison.Ordinal))
                    {
                        HandleGhepLotToggle(e.Button);
                    }
                    else
                    {
                        KiemTraGhepLotClicked.Invoke(this, EventArgs.Empty);
                    }
                    break;

                case PhieuActionBarAction.InPhieu:
                    InPhieuClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.InGhepLot:
                    InGhepLotClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.InTachLot:
                    InTachLotClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.CapNhapKho:
                    if (!_isLoading)
                        CapNhapKhoClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.KiemTraMaNG:
                    KiemTraMaNGClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.XemHangThieuCaNgay:
                    if (!_isLoading)
                        XemHangThieuCaNgayClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.XoaDongQR:
                    if (Confirm("Xóa dòng đang chọn?"))
                        XoaDongQRClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.XoaToanBoQR:
                    if (Confirm("Toàn bộ dữ liệu đọc sẽ bị xóa. Bạn chắc chắn?"))
                        XoaToanBoQRClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.SuaSoLuongTem:
                    SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.LayLaiLot:
                    HandleLayLaiLotAction();
                    break;

                case PhieuActionBarAction.UploadGiaoDB:
                    UploadGiaoDBClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.GhiChuStop:
                    HandleStopAction("STOP");
                    break;

                case PhieuActionBarAction.XoaGhiChuStop:
                    HandleStopAction("");
                    break;

                case PhieuActionBarAction.HoanThanh:
                    if (_cfg.Delivery.CoHoanThanhYMVN)
                        HoanThanhYMVNClicked.Invoke(this, EventArgs.Empty);
                    else
                        HoanThanhClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.HoanThanhYMVN:
                    HoanThanhYMVNClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.UploadMilkrunSP:
                    UploadMilkrunSPClicked.Invoke(this, EventArgs.Empty);
                    break;

                case PhieuActionBarAction.ToggleLoaiPhieu:
                    if (_phieuActionBarControl != null)
                    {
                        _isLoaiSP = !_isLoaiSP;
                        _phieuActionBarControl.UpdateLoaiPhieuCaption(_isLoaiSP);
                    }
                    LoaiPhieuChanged.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        private void HandleLayLaiLotAction()
        {
            int stt = _phieuGridControl != null
                ? _phieuGridControl.GetFocusedStt()
                : -1;

            if (stt < 0)
            {
                ShowInfo("Vui lòng chọn dòng cần lấy lại LOT trên danh sách đơn hàng!");
                return;
            }

            string lot = _phieuGridControl != null
                ? _phieuGridControl.OrderView.GetFocusedRowCellDisplayText("LOT").Trim()
                : string.Empty;

            if (string.IsNullOrEmpty(lot))
            {
                ShowInfo("Dòng này chưa có LOT, không cần lấy lại!");
                return;
            }

            string status = _phieuGridControl.OrderView
                .GetFocusedRowCellDisplayText("STATUS").Trim();
            if (status == "OK")
            {
                ShowInfo("Dòng này đã được Cập Nhập Kho, không thể lấy lại LOT!");
                return;
            }

            LayLaiLotNoClicked.Invoke(this, new LayLaiLotEventArgs(stt));
        }

        private void HandleStopAction(string value)
        {
            int stt = _phieuGridControl != null
                ? _phieuGridControl.GetFocusedStt()
                : -1;
            if (stt < 0)
                return;

            CapNhapTTPHIEUClicked.Invoke(this, new TTPHIEUEventArgs(stt, value));
        }
    }
}