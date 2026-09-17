using System;
using System.Data;

namespace PCTP.Presentation.Views
{
    /// <summary>
    /// UI contract for the QR/document scanning workflow.
    /// </summary>
    public interface IDocQrView : IViewFeedback
    {
        void BindDocQRCode(DataTable dt);
        void SwitchToDocQRView();
        bool HoiXoaDocQR();
        void ClearQRInput();
        string QRCodeInput { get; }
        int GetFocusedDocQRStt();
        (string LotFcc, int SlFcc, int SlHvn) GetFocusedDocQRTemInfo();
        void DeleteFocusedDocQRRow();
        void ClearDocQRRows();
        void SetDocQrScanInputEnabled(bool enabled);
        void HideDocQrQuantityEditPanel();
        void LockDocQrDeliveryContext(bool isSP, string gioFCC);
        void UnlockDocQrDeliveryContext();
        event EventHandler DocQRCodeClicked;
        event EventHandler<string> QRCodeSubmitted;
        event EventHandler XoaDongQRClicked;
        event EventHandler XoaToanBoQRClicked;
    }
}
