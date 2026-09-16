using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Worklist "hàng còn thiếu cả ngày".
    ///
    /// Order source phải đi cùng CustomerConfig giống pipeline HVN_PGH:
    /// - LoadTuBangRieng = false: IFS là source đơn hàng.
    /// - LoadTuBangRieng = true: TableOrder là source đơn hàng.
    ///
    /// Trạng thái pending được tính từ source Order - LUUPHIEUGIAOHANG,
    /// sau đó mới phân bổ tồn kho theo giờ/FIFO để tạo Worklist.
    /// Không query trực tiếp bảng legacy GIAOHANGYMN/YAMAHAQRCDE_SP.
    /// </summary>
    public sealed class HangThieuCaNgayService : IHangThieuCaNgayService
    {
        private readonly IIFSRepository _ifsRepo;
        private readonly IPhieuLuuTruRepository _luuTruRepo;
        private readonly PhieuSqlExecutor _db;
        private readonly IOrderSourceFactory _orderSourceFactory;

        public HangThieuCaNgayService(
            IIFSRepository ifsRepo,
            IPhieuLuuTruRepository luuTruRepo,
            PhieuSqlExecutor db,
            IOrderSourceFactory orderSourceFactory)
        {
            _ifsRepo = ifsRepo ?? throw new ArgumentNullException(nameof(ifsRepo));
            _luuTruRepo = luuTruRepo ?? throw new ArgumentNullException(nameof(luuTruRepo));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _orderSourceFactory = orderSourceFactory ?? throw new ArgumentNullException(nameof(orderSourceFactory));
        }

        public DataTable TinhHangThieuCaNgay(
            DateTime ngayGiao, string nhaMay, int addNm, CustomerConfig cfg)
        {
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            // 1. Source Order phải giống HVN_PGH/PhieuLoadService.
            //    Không còn hard-code IFS cho Worklist khi customer dùng TableOrder.
            DataTable donHangNgay = LoadOrderSourceCaNgay(ngayGiao, nhaMay, addNm, cfg);

            // 2. Đã giao cả ngày — LUUPHIEUGIAOHANG là delivery ledger hiện tại.
            DataTable daGiaoNgay = _luuTruRepo.LoadLuuPhieuCaNgay(
                nhaMay, ngayGiao.ToString("yyyy-MM-dd"));

            // 3. Gộp đã giao theo (MAHANG, GIOGIAO) chuẩn hoá.
            var daGiaoMap = daGiaoNgay.AsEnumerable()
                .GroupBy(r => (
                    MaHang: r["MAHANG"]?.ToString().Trim() ?? "",
                    Gio: GioHelper.NormalizeGio(r["GIOGIAO"]?.ToString())))
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(r => DbValueHelper.SafeInt(r["SOLUONG"])));

            // 4. Pending = Order quantity - Delivered quantity.
            //    Status "đã giao đủ" không vào Worklist; giao thiếu mới vào Worklist.
            var canGiao = new List<(string MaHang, string Gio, int SlConLai)>();
            foreach (DataRow row in donHangNgay.Rows)
            {
                string maHang = row["MAHANG"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(maHang))
                    continue;

                string gio = GioHelper.NormalizeGio(row["GIOGIAO"]?.ToString());
                int slDat = DbValueHelper.SafeInt(row["SOLUONG"]);

                daGiaoMap.TryGetValue((maHang, gio), out int slDaGiao);
                int slConLai = slDat - slDaGiao;
                if (slConLai > 0)
                    canGiao.Add((maHang, gio, slConLai));
            }

            if (canGiao.Count == 0)
                return BuildEmptyResult();

            // 5. Tồn kho hiện tại — batch 1 query, không N+1.
            var maHangList = canGiao.Select(x => x.MaHang).Distinct().ToList();
            var tonMap = LoadTonKhoBatch(maHangList);

            // 6. Phân bổ FIFO theo giờ để xác định phần thực sự còn thiếu.
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

        private DataTable LoadOrderSourceCaNgay(
            DateTime ngayGiao,
            string nhaMay,
            int addNm,
            CustomerConfig cfg)
        {
            var context = new OrderLoadContext
            {
                Cfg = cfg,
                NgayGiao = ngayGiao,
                NhaMay = nhaMay,
                AddNm = addNm,
                GioFcc = string.Empty,
                GioFccMoTa = string.Empty,
                Category = OrderCategory.MP,
                Source = cfg.Delivery.LoadTuBangRieng
                    ? OrderSourceKind.TableOrder
                    : OrderSourceKind.IFS,
                MachineRole = MachineRole.ChiXem,
                IsBanQR = false,
                CheckedGios = new List<string>(),
                IfsDataDaLoc = null,
                IfsLoadError = null
            };

            var source = _orderSourceFactory.GetSource(context);
            var result = source.Load(context);
            return result?.Orders ?? new DataTable();
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
