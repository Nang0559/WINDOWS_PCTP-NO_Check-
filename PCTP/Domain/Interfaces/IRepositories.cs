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
        string GetMaHangMapped(string maHang);
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
   
}
