using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public sealed class HangThieuCaNgayService : IHangThieuCaNgayService
    {
        private readonly IIFSRepository _ifsRepo;
        private readonly IPhieuLuuTruRepository _luuTruRepo;
        private readonly PhieuSqlExecutor _db; // chỉ để đọc STOCKTP — có thể thay bằng interface nhỏ hơn nếu muốn tách tiếp

        public HangThieuCaNgayService(
            IIFSRepository ifsRepo,
            IPhieuLuuTruRepository luuTruRepo,
            PhieuSqlExecutor db)
        {
            _ifsRepo = ifsRepo ?? throw new ArgumentNullException(nameof(ifsRepo));
            _luuTruRepo = luuTruRepo ?? throw new ArgumentNullException(nameof(luuTruRepo));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public DataTable TinhHangThieuCaNgay(
            DateTime ngayGiao, string nhaMay, int addNm, CustomerConfig cfg)
        {
            // 1. Cần giao cả ngày — IFS, không lọc giờ
            DataTable donHangNgay = _ifsRepo.GetCustomerOrderJoin(
                ngayGiao.ToString("ddMMyyyy"), "", "",
                nhaMay, addNm, hinhThucIn: 2, cfg);

            // 2. Đã giao cả ngày — LUUPHIEUGIAOHANG
            DataTable daGiaoNgay = _luuTruRepo.LoadLuuPhieuCaNgay(
                nhaMay, ngayGiao.ToString("yyyy-MM-dd"));

            // 3. Gộp đã giao theo (MAHANG, GIOGIAO chuẩn hoá 2 chữ số)
            var daGiaoMap = daGiaoNgay.AsEnumerable()
                .GroupBy(r => (
                    MaHang: r["MAHANG"]?.ToString().Trim() ?? "",
                    Gio: GioHelper.NormalizeGio(r["GIOGIAO"]?.ToString())))
                .ToDictionary(g => g.Key, g => g.Sum(r => DbValueHelper.SafeInt(r["SOLUONG"])));

            // 4. Tính "còn phải giao" = SL đặt (IFS) - SL đã giao (LUUPHIEUGIAOHANG)
            var canGiao = new List<(string MaHang, string Gio, int SlConLai)>();
            foreach (DataRow row in donHangNgay.Rows)
            {
                string maHang = row["MAHANG"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(maHang)) continue;

                string gio = GioHelper.NormalizeGio(row["GIOGIAO"]?.ToString());
                int slDat = DbValueHelper.SafeInt(row["SOLUONG"]);

                daGiaoMap.TryGetValue((maHang, gio), out int slDaGiao);
                int slConLai = slDat - slDaGiao;
                if (slConLai > 0)
                    canGiao.Add((maHang, gio, slConLai));
            }

            if (canGiao.Count == 0)
                return BuildEmptyResult();

            // 5. Tồn kho hiện tại — batch 1 query, không N+1
            var maHangList = canGiao.Select(x => x.MaHang).Distinct().ToList();
            var tonMap = LoadTonKhoBatch(maHangList);

            // 6. Phân bổ FIFO theo giờ — GIỐNG HỆT logic đã có trong TinhHangThieuTuDonHang,
            // chỉ khác input là "SlConLai" (đã trừ phần giao rồi) thay vì SOLUONG thô.
            DataTable result = BuildEmptyResult();
            foreach (var maHangGroup in canGiao.GroupBy(x => x.MaHang))
            {
                int tonConLai = tonMap.TryGetValue(maHangGroup.Key, out int t) ? t : 0;
                foreach (var dong in maHangGroup.OrderBy(x => x.Gio, StringComparer.OrdinalIgnoreCase))
                {
                    int slDuocCap = Math.Min(dong.SlConLai, Math.Max(tonConLai, 0));
                    int slThieu = dong.SlConLai - slDuocCap;
                    tonConLai -= slDuocCap;

                    if (slThieu > 0)
                        result.Rows.Add(dong.MaHang, dong.Gio, dong.SlConLai, slThieu);
                }
            }
            return result;
        }

        private Dictionary<string, int> LoadTonKhoBatch(List<string> maHangList)
        {
            string inClause = string.Join(",",
                maHangList.Select(m => $"'{SqlHelper.Esc(m)}'"));

            DataTable tonDt = _db.LoadData(
                $"SELECT PART, ISNULL(SUM(SLCONLAI),0) AS TONG_TON " +
                $"FROM STOCKTP WHERE PART IN ({inClause}) GROUP BY PART");

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in tonDt.Rows)
                map[row["PART"].ToString().Trim()] = Convert.ToInt32(row["TONG_TON"]);
            return map;
        }

        private static DataTable BuildEmptyResult()
        {
            var dt = new DataTable();
            dt.Columns.Add("MH", typeof(string));
            dt.Columns.Add("GIOGIAO", typeof(string));
            dt.Columns.Add("SLGIAO", typeof(int));
            dt.Columns.Add("SLTHIEU", typeof(int));
            return dt;
        }

        
    }
}
