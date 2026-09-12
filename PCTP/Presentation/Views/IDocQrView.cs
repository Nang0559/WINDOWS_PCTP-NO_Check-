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
        int SttDangSuaSl { get; }
        int GetFocusedDocQRStt();
        (string LotFcc, int SlFcc, int SlHvn) GetFocusedDocQRTemInfo();
        void DeleteFocusedDocQRRow();
        void ClearDocQRRows();
        int? ShowSuaSoLuongTem(int sttBan, string lotFcc, int slFcc, int slHvn);
        int? GetSuaSoLuongResult();
        event EventHandler DocQRCodeClicked;
        event EventHandler<string> QRCodeSubmitted;
        event EventHandler XoaDongQRClicked;
        event EventHandler XoaToanBoQRClicked;
        event EventHandler SuaSoLuongTemClicked;
    }
}
