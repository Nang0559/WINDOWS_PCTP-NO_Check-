using PCTP.Domain.Entities;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Presentation.Views
{
    /// <summary>
    /// UI contract for the normal delivery-ticket (phiếu) workflow.
    /// Kept separate from QR, GiaoDB and YMVN concerns.
    /// </summary>
    public interface IPhieuView : IViewFeedback
    {
        void SetupNhaMayUI(CustomerConfig cfg);
        void BindDonHang(DataTable dt);
        void BindHangThieu(DataTable dt);
        void BindGhepLot(DataTable dt);
        void SetGridCaption(string caption);
        void RefreshLotRow(int stt, string lot);
        void ShowHangThieuCaNgay(DataTable dt);
        void BindLechIFS(DataTable dt);
        void SwitchToPhieuView();
        void SwitchToPhieuDBView();
        void SetupPhieuButtons(bool showCapNhapKho, bool showKiemTraMaNG, bool showGhepLot, bool showDocQRCode, bool showLayLaiLot, bool showStop = false, bool showHangThieuCaNgay = true);
        void SetDate(DateTime date);
        void SetTab(int addNM);
        void LockRadioExcept(string gioFCC);
        void UnlockAllRadio();
        void LockDatePicker();
        void UnlockDatePicker();
        void BindGioXuatVP(IReadOnlyList<GioXuat> danhSach);
        void BindGioXuatHN(IReadOnlyList<GioXuat> danhSach);
        void SuspendGioXuatChanged();
        void ResumeGioXuatChanged();
        DateTime SelectedDate { get; }
        int SelectedTabAddNM { get; }
        GioXuat CurrentGioXuat { get; }
        DataTable GetDonHangTable();
        DataTable GetAddressTable();
        string GetFocusedDonHangMaHang();
        IEnumerable<GhepLotItem> GetSelectedGhepLotRows();
        bool CoLotDeLuuKho();
        bool CoHangChuaOK();
        int ShowChonSttTrungMa(ListView danhSachTrung);
        void ShowKiemTraMaNG(string maHang);
        void ShowTachLot();
        void ShowLoiCapNhapKho(DataTable loiData);
        int ShowChonHinhThucIn();
        ChonLotResult ShowChonLotTuKho(int stt, string maHang, int soLuong, DataTable danhSachLot);
        void ShowReport(DataTable reportData);
        void ShowReportWithGioHeader(DataTable data, string gioHeader);
        event EventHandler FormLoaded;
        event EventHandler DateChanged;
        event EventHandler GioXuatChanged;
        event EventHandler TabChanged;
        event EventHandler<ChonLotThuCongEventArgs> ChonLotThuCongClicked;
        event EventHandler XemHangThieuCaNgayClicked;
        event EventHandler CapNhapKhoClicked;
        event EventHandler InPhieuClicked;
        event EventHandler InGhepLotClicked;
        event EventHandler InTachLotClicked;
        event EventHandler DocQRCodeClicked;
        event EventHandler KiemTraGhepLotClicked;
        event EventHandler KiemTraMaNGClicked;
        event EventHandler HoanThanhClicked;
        event EventHandler<LayLaiLotEventArgs> LayLaiLotNoClicked;
        event EventHandler CapNhapTTPHIEUClicked;
    }
}
