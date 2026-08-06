using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Presentation.Views
{
    /// <summary>
    /// Giao diện mà form phải implement.
    /// Presenter không import DevExpress — chỉ biết interface này.
    /// </summary>
    public interface IHVNView
    {

        void SetupNhaMayUI(CustomerConfig cfg);
        // ════════════════════════════════════════════════════════════════════
        // I. BIND DỮ LIỆU VÀO GRID
        // ════════════════════════════════════════════════════════════════════
        void BindDonHang(DataTable dt);
        void BindHangThieu(DataTable dt);
        void BindDocQRCode(DataTable dt);
        void BindGhepLot(DataTable dt);
        void SetGridCaption(string caption);
        void RefreshLotRow(int stt, string lot);
        //void RefreshDocQR();

        // ════════════════════════════════════════════════════════════════════
        // II. TRẠNG THÁI / CHUYỂN VIEW
        // ════════════════════════════════════════════════════════════════════
        void ShowLoading(bool show, string caption = "Đang xử lý...");
        void ShowError(string message);
        void ShowInfo(string message);
        bool Confirm(string message);
        void ShowReport(DataTable reportData);
        void SwitchToDocQRView();
        void SwitchToPhieuView();
        void SwitchToPhieuDBView();
        void SetupPhieuButtons(bool showCapNhapKho, bool showKiemTraMaNG,
                        bool showGhepLot, bool showDocQRCode,
                        bool showLayLaiLot,
                        bool showStop = false);
        // Thêm vào IHVNView:
        void SetDate(DateTime date);
        void SetTab(int addNM);                    // chuyển tab VP/HN
        void LockRadioExcept(string gioFCC);       // khoá tất cả radio trừ giờ đang bắn
        void UnlockAllRadio();                     // mở khoá khi hoàn thành
        bool HoiXoaDocQR();                        // hỏi user có xóa DOCQRCODE không
        void BindGioXuatVP(IReadOnlyList<GioXuat> danhSach);
        void BindGioXuatHN(IReadOnlyList<GioXuat> danhSach);

        void LockDatePicker();
        void UnlockDatePicker();
        //YMVN
        void SetupGridDonHangYMVN(bool coGear);
        void LockCheckListYMVN();
        void UnlockCheckListYMVN();
        // ════════════════════════════════════════════════════════════════════
        // III. ĐỌC GIÁ TRỊ TỪ UI
        // ════════════════════════════════════════════════════════════════════
        DateTime SelectedDate { get; }
        int SelectedTabAddNM { get; }
        string QRCodeInput { get; }
        void ClearQRInput();

        // ════════════════════════════════════════════════════════════════════
        // IV. ĐỌC DATA TỪ GRID
        // ════════════════════════════════════════════════════════════════════
        int SttDangSuaSl { get; }
        DataTable GetDonHangTable();
        DataTable GetAddressTable();
        int GetFocusedDocQRStt();
        (string LotFcc, int SlFcc, int SlHvn) GetFocusedDocQRTemInfo();
        void DeleteFocusedDocQRRow();
        void ClearDocQRRows();
        string GetFocusedDonHangMaHang();

        /// <summary>
        /// Lấy các dòng đang được chọn trên gridCTTGL để in ghép lot.
        /// Form gốc INGHEPLOT(): GridVTTGL.GetSelectedRows() → row[0]=MA, row[1]=GIO, row[2]=LOT
        /// </summary>
        IEnumerable<GhepLotItem> GetSelectedGhepLotRows();

        // ════════════════════════════════════════════════════════════════════
        // V. KIỂM TRA TRẠNG THÁI GRID
        // ════════════════════════════════════════════════════════════════════
        bool CoLotDeLuuKho();
        bool CoHangChuaOK();

        // ════════════════════════════════════════════════════════════════════
        // VI. DIALOG PHỨC TẠP
        // ════════════════════════════════════════════════════════════════════
        bool XuLyChuyenGiaoDB(int addNm);
        int ShowChonSttTrungMa(ListView danhSachTrung);
        void ShowKiemTraMaNG(string maHang);
        void ShowTachLot();
        void ShowLoiCapNhapKho(DataTable loiData);
        int? ShowSuaSoLuongTem(int sttBan, string lotFcc, int slFcc, int slHvn);
        int? GetSuaSoLuongResult();
        int ShowChonHinhThucIn();

        void SuspendGioXuatChanged();
        void ResumeGioXuatChanged();
        void ShowReportWithGioHeader(DataTable data, string gioHeader);

        // ── GIAO DB ─────────────────────────────────────────────────────────
        void XoaDongGiaoDB();

        /// <summary>
        /// Thêm dòng mới vào gridDONHANG và bind LookUp mã hàng + ComboBox giờ giao.
        /// Form gốc PBD_ThemMoi(): AddNewRow + RepositoryItemLookUpEdit + RepositoryItemComboBox
        /// </summary>
        void ThemDongGiaoDB(DataTable danhSachMaHang);
        void UpdateGioXuatFromDB(string gioFCC);
        bool IsLoaiSP { get; }
        // ════════════════════════════════════════════════════════════════════
        // VII. EVENTS
        // ════════════════════════════════════════════════════════════════════
        event EventHandler LoaiPhieuChanged;
        event EventHandler FormLoaded;
        event EventHandler DateChanged;
        event EventHandler GioXuatChanged;
        event EventHandler TabChanged;
        event EventHandler<ChonLotThuCongEventArgs> ChonLotThuCongClicked;

        // ── Phiếu thường ────────────────────────────────────────────────────
        event EventHandler CapNhapKhoClicked;
        event EventHandler InPhieuClicked;
        event EventHandler InGhepLotClicked;
        event EventHandler InTachLotClicked;
        event EventHandler DocQRCodeClicked;
        event EventHandler KiemTraGhepLotClicked;
        event EventHandler KiemTraMaNGClicked;

        // ── Màn hình đọc QR ─────────────────────────────────────────────────
        event EventHandler<string> QRCodeSubmitted;
        event EventHandler HoanThanhClicked;
        event EventHandler XoaDongQRClicked;
        event EventHandler XoaToanBoQRClicked;
        event EventHandler SuaSoLuongTemClicked;
        event EventHandler<LayLaiLotEventArgs> LayLaiLotNoClicked;

        // ── GIAO DB ─────────────────────────────────────────────────────────
        event EventHandler ThemDongGiaoDBClicked;
        event EventHandler XoaDongGiaoDBClicked;
        event EventHandler LuuGiaoDBClicked;
        event EventHandler<TTPHIEUEventArgs> CapNhapTTPHIEUClicked;

        // ── YMVN: thêm mới ───────────────────────────────────────────────
        event EventHandler GioXuatCheckedChanged;
        event EventHandler CheckGX_ItemCheck;
        // Hoàn thành scan YMVN — gọi SP Usp_Qrcode_Take_LotYMVN
        event EventHandler HoanThanhYMVNClicked;

        // Upload Milkrun SP — chỉ YMVN có
        event EventHandler UploadMilkrunSPClicked;

        // Bind danh sách giờ theo checkbox (YMVN dùng CheckListBox thay radio)
        void BindGioXuatCheckList(List<string> danhSachGio);
        List<string> GetCheckedGioXuat();

        // Bind grid ghép lot YMVN (có cột Gear)
        void BindGhepLotYMVN(DataTable dt);

        // Show report YMVN
        void ShowReportYMVN(DataTable reportData);

        // ── YMVN specific ────────────────────────────────────────────────────────
        /// <summary>Set lại các checkbox giờ YMVN khi khôi phục trạng thái</summary>
        void SetCheckedGiosYMVN(List<string> checkedGios);

        // Hiện EditForm chọn LOT từ tồn kho
        ChonLotResult ShowChonLotTuKho(int stt, string maHang, int soLuong, DataTable danhSachLot);
    }
}
