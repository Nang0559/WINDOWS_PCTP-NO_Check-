using DevExpress.XtraScheduler.Reporting;
using PCTP.Domain.Entities;
using PCTP.VIEWSTOCK.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Domain.Interfaces
{
    // ─── Phiếu giao hàng ────────────────────────────────────────────────────────
    public interface IPhieuRepository
    {
        // ── Đếm / kiểm tra ──────────────────────────────────────────────────────
        int CountDocQRCode(string docQRTable);
        bool CheckCoMaNG(string tenBan);
        bool KiemTraMaTrongPhieu(string maHang, string tenBan);

        // ── Load phiếu ──────────────────────────────────────────────────────────
        DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay,
                                  string gioFcc, int addNm,
                                  string tmpTable, string ifsTable,
                                  string docQRTable);
        DataTable LuuVaLoad(string tenSPBang, string tenSP, DataTable donHang,
                             string ngayGiao, string nhaMay,
                             string gioFcc, int addNm,
                             string tenBan, string docQRTable,
                             string ifsView = "");
        DataTable LoadHangThieu(bool isMayBanQR, string tenBan);
        DataTable LoadLuuPhieu(string nhaMay, string ngayGiao, string gioGiaoFcc);
        DataTable LoadTmpPhieuGiaoDB(string tenBan);
        DataTable LoadGhepLot();

        DataTable GetDanhSachLotTuKho(string maHang);

        // ── Trạng thái bắn QR ───────────────────────────────────────────────────
        TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable);
        TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable);
        void XoaDocQRCode(string docQRTable);

        DataTable GetDonHangHienTai(string tenBan);
        // ── Lot ─────────────────────────────────────────────────────────────────
        DataTable GetDonHangChuaLot(string tenBan, string docQRTable);
        DataTable GetDanhSachTrungMaSl(string maHang, int sl,
                                        string tenBan, string docQRTable);
        int CountTrungMaSl(string maHang, int sl,
                            string tenBan, string docQRTable);
        string GetLotNo(string maHang, int stt, int dem, int slGiao,
                string docQRTable = "DOCQRCODE",
                string tmpTable = "TMPPHIEUGIAOHANG");
        void CapNhapLotTmpPhieu(int stt, string lot, string tenBan);
        void LayLaiLotNo(int stt, string tenBan, string docQRTable);

        // ── Kho ─────────────────────────────────────────────────────────────────
       
        int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors);
        int LuuPhieuSP(string nhaMay, string ngayGiao,
                        string gioGiaoFcc, string loaiPhieu);
        void CapNhapTTPHIEU(string nhaMay, string ngayGiao,
                             string gioGiaoFcc, int stt, string ghiChu);
        int CapNhapKho(string gioGiaoFcc, string nhaMay,
               string tmpTable, string docQRTable,
               out DataTable errors);

        int CapNhapKhoHTN(string nhaMay, string tmpTable,
                           string docQRTable, out DataTable errors);
        bool CapNhapKhoYMVN(int stt, string lotSl, string maHang,
                          string ngayGiao, string gioGiao, string nhaMay,
                          out DS_ERR_CNK error);
        void DanhDauDaGiao(string poNo, string maHang,
            string ngayGiao, CustomerConfig cfg);

        // ── Giao DB ─────────────────────────────────────────────────────────────
        DataTable GetDanhSachMaHang();
        void LuuGiaoDB(DataTable donHang, string gioFccMoTa,
                        int addNm, string tmpTable, string ifsTable,
                        string nhaMayOverride = "");   // ← THÊM param mới
        //-YMVN
        void ExecNonQuery(string spName);
        void XoaTmpPhieu(string tenBan);
        void ExecSP(string spName, params SqlParameter[] parms);
        //DataTable LoadPhieuYMVN(string ngayGiao, string gioFcc,
        //                        bool isLoaiSP = false);
        DataTable ExecSPWithResult(string spName, params SqlParameter[] parms);
        DataTable LoadPhieuDangDocYMVN(string tmpTable, bool isLoaiSP);
        //DataTable LoadPhieuYMVN(string ngayXuatMDY, string gioFilter,
        //                        bool isLoaiSP, string dockCodeSP);
        DataTable LoadPhieuTuBangRieng(string ngayGiao, string gioFilter,
                                bool isLoaiSP, string dockCodeSP,
                                CustomerConfig cfg);
        DataTable LoadTuTmpTable(string tmpTable);
        IReadOnlyList<string> GetGioGiaoYMVN(string ngayGiao);
        IReadOnlyList<string> GetDanhSachGioYMVN(string ngayXuatMDY);
        void UploadMilkrunSP(DataTable donHang, string ngayGiao);
     
        //--- YMVN
        void InsertTmpYMVN(string stt, string cua, string truyen,
    string maHang, string tenHang, string lot, string dv,
    int slXuat, string ngayGiao, string gear,string gioXuat, string tmpTable,
    string poNo = "", string cusPoNo = "");
        // ── Helpers ─────────────────────────────────────────────────────────────
        Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList);
    }

    public interface IGioXuatRepository
    {
        IReadOnlyList<GioXuat> GetDanhSachGioVP();
        IReadOnlyList<GioXuat> GetDanhSachGioHN();
        /// <summary>Map GioFCC → GioMoTa để dùng khi gán KGX cho phiếu.</summary>
        IReadOnlyDictionary<string, string> GetDictGioVP();
        IReadOnlyDictionary<string, string> GetDictGioHN();
        List<string> GetDanhSachGioYMVN(string ngayGiao);
    }

    // ─── DocQRCode ───────────────────────────────────────────────────────────────
    public interface IDocQRRepository
    {
        // ── Đọc — dùng tên bảng động từ config ──────────────────────────────
        IReadOnlyList<DocQRCode> GetAll();
        IReadOnlyList<DocQRCode> GetAll(bool isSP);
        IReadOnlyList<DocQRCode> GetAll(string docQrTable);

        DataTable GetAllAsTable();
        DataTable GetAllAsTable(bool isSP);
        DataTable GetAllAsTable(string docQrTable);

        int Count();
        int Count(bool isSP);
        int Count(string docQrTable);

        int GetMaxStt();
        int GetMaxStt(bool isSP);
        int GetMaxStt(string docQrTable);

        int CountChuaDG();
        int CountChuaDG(bool isSP);
        int CountChuaDG(string docQrTable);

        string GetIdMaHangPadded(string maHang);


        // ── Kiểm tra khi quét HVN ───────────────────────────────────────────
        bool KiemTraTemMa(string maHvn);
        bool KiemTraTemSoLuong(string maHvn, int slTemHvn);
        bool KiemTraTrungTemTong(string lotFcc, string soPhieu, string docQrTable);

        // ── SL đã bắn ────────────────────────────────────────────────────────
        int GetTongSlDaBan(string maHang);
        int GetTongSlDaBan(string maHang, bool isSP);
        int GetTongSlDaBan(string maHang, string docQrTable);

        int GetSoLuongGiaoTheoMa(string maHang);
        int GetSoLuongGiaoTheoMa(string maHang, bool isSP);
        int GetSoLuongGiaoTheoMa(string maHang, string ifsTable);

        // ── Ghi ─────────────────────────────────────────────────────────────
        void InsertFCC(DocQRCode item);
        void InsertFCC(DocQRCode item, bool isSP);
        void InsertFCC(DocQRCode item, string docQrTable, bool coGear = false);

        void UpdateHVN(DocQRCode item);
        void UpdateHVN(DocQRCode item, bool isSP);
        void UpdateHVN(DocQRCode item, string docQrTable);

        void UpdateSlHvn(int stt, int slMoi);
        void UpdateSlHvn(int stt, int slMoi, bool isSP);
        void UpdateSlHvn(int stt, int slMoi, string docQrTable);

        // ── Xóa ─────────────────────────────────────────────────────────────
        void Delete(int stt);
        void Delete(int stt, bool isSP);
        void Delete(int stt, string docQrTable);

        void DeleteAll();
        void DeleteAll(bool isSP);
        void DeleteAll(string docQrTable);
        // YMVN specific
        string GetGearName(int gearCode);
        string GetGearName(string gear);
        bool KiemTraDuSlGear(string maHang, string gio,
                               string gear, int slCan,
                               string docQRTable);
        void UpdateGear(int stt, string gear, string docQRTable);
        DataTable GetThongKeGear(string maHang, string gio,
                                 string docQRTable);
    }
    // ─── IFS Oracle ─────────────────────────────────────────────────────────────
    public interface IIFSRepository
    {
        // ── Load phiếu thường — luôn dùng hinhThucIn = 1 (lọc addNm + giờ) ──
        DataTable GetCustomerOrderJoin(string ngayXuat, string gioXuat,
                                       string gioXuatH, string nhaMay,
                                       int addNm,
                                       CustomerConfig cfg);

        // ── In phiếu — hinhThucIn từ FRM_HTIN (1/2/3) ───────────────────────
        DataTable GetCustomerOrderJoin(string ngayXuat, string gioXuat,
                                       string gioXuatH, string nhaMay,
                                       int addNm, int hinhThucIn,
                                       CustomerConfig cfg);

        DataTable GetCustomerOrderJoinYMVN(string ngayXuat, string customerNo, string dockFilter);
        DataTable GetDockCodeDv(string po, string pno, string customerNo, string dockFilter);

        DataTable GetCustomerAddress(string customerNo);

        DataTable GetCustSchedLine(string customerNo, string shipAddrNo,
                                    string customerPartNo, string customerPoNo);
    }
    //public interface IIFSRepository
    //{

    //    // ── Load phiếu thường — luôn dùng hinhThucIn = 1 (lọc addNm + giờ) ─
    //    DataTable GetCustomerOrderJoin(string ngayXuat, string gioXuat,
    //                                   string gioXuatH, string nhaMay, int addNm);

    //    // ── In phiếu — hinhThucIn từ FRM_HTIN (1/2/3) ───────────────────────
    //    DataTable GetCustomerOrderJoin(string ngayXuat, string gioXuat,
    //                                   string gioXuatH, string nhaMay, int addNm,
    //                                   int hinhThucIn);

    //    DataTable GetCustomerAddress(string customerId);
    //    DataTable GetCustSchedLine(string customerNo, string shipAddrNo,
    //                               string customerPartNo, string customerPoNo);
    //}
}
