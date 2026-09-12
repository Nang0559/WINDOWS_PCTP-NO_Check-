using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.WorkingState;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Orchestration layer của pipeline load phiếu giao hàng.
    ///
    /// Source chỉ lấy dữ liệu nguồn.
    /// WorkingState quản lý TMP/DOCQR.
    /// Service này ghép các bước thành OrderLoadResult.
    /// Không publish EventBus và không xử lý UI.
    /// </summary>
    public sealed class PhieuLoadService : IPhieuLoadService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IIFSRepository _ifsRepo;
        private readonly ITableOrderRepository _tableOrderRepo;
        private readonly IOrderSourceFactory _orderSourceFactory;
        private readonly IRowCategoryFilter _rowCategoryFilter;
        private readonly IDeliveryWorkingState _workingState;
        private readonly CustomerConfig _cfg;

        public PhieuLoadService(
            IPhieuRepository phieuRepo,
            IIFSRepository ifsRepo,
            ITableOrderRepository tableOrderRepo,
            IOrderSourceFactory orderSourceFactory,
            IRowCategoryFilter rowCategoryFilter,
            IDeliveryWorkingState workingState,
            CustomerConfig cfg)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _ifsRepo = ifsRepo ?? throw new ArgumentNullException(nameof(ifsRepo));
            _tableOrderRepo = tableOrderRepo ?? throw new ArgumentNullException(nameof(tableOrderRepo));
            _orderSourceFactory = orderSourceFactory ?? throw new ArgumentNullException(nameof(orderSourceFactory));
            _rowCategoryFilter = rowCategoryFilter ?? throw new ArgumentNullException(nameof(rowCategoryFilter));
            _workingState = workingState ?? throw new ArgumentNullException(nameof(workingState));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public OrderLoadResult Load(OrderLoadContext context)
        {
            ValidateContext(context);

            switch (context.Source)
            {
                case OrderSourceKind.IFS:
                    return LoadFromIfs(context);

                case OrderSourceKind.TableOrder:
                    return LoadFromTableOrder(context);

                case OrderSourceKind.GiaoDB:
                    return LoadFromGiaoDb(context);

                default:
                    throw new ArgumentOutOfRangeException(nameof(context.Source));
            }
        }

        private OrderLoadResult LoadFromIfs(OrderLoadContext context)
        {
            DateTime dt = context.NgayGiao;
            string ngayGiaoSP = dt.ToString("yyyy-MM-dd");
            string ngayXuat = dt.ToString("ddMMyyyy");

            string gioFccSP = _cfg.Delivery.LoadTheoNgay ? "" : context.GioFcc;
            string gioMoTaSP = _cfg.Delivery.LoadTheoNgay ? "Tất cả ca" : context.GioFccMoTa;

            string tmpTable = _cfg.Delivery.GetTmpTable(context.Category == OrderCategory.SP);
            string docQRTable = _cfg.Delivery.GetDocQRTable(context.Category == OrderCategory.SP);

            string caption = _cfg.Delivery.LoadTheoNgay
                ? $"ĐƠN HÀNG: {_cfg.DisplayName} - {context.NhaMay}"
                : $"ĐƠN HÀNG: {_cfg.DisplayName} - {context.NhaMay}   GIỜ GIAO: {gioMoTaSP}";

            if (context.MachineRole == MachineRole.DuocBanQR && context.IsBanQR)
            {
                int demQR = SWLog.Measure(
                    "1. CountDocQRCode",
                    () => _phieuRepo.CountDocQRCode(docQRTable));

                if (demQR > 0)
                {
                    DataTable donHangQr = SWLog.Measure(
                        "2. LoadPhieuDocQR",
                        () => _workingState.LoadFromQr(context));

                    bool coMaNG = !_cfg.Delivery.CoGear
                        && _phieuRepo.CheckCoMaNG(tmpTable);

                    return new OrderLoadResult
                    {
                        Orders = donHangQr,
                        HasMaNG = coMaNG,
                        HasDifference = false,
                        Source = context.Source,
                        Category = context.Category,
                        Caption = caption,
                        Warning = null,
                        IsQr = true
                    };
                }
            }

            OrderSourceResult sourceResult = _orderSourceFactory
                .GetSource(context)
                .Load(context);

            DataTable donHangIFS = sourceResult.Orders ?? new DataTable();

            SWLog.Measure(
                $"3. EnrichSttHop ({donHangIFS.Rows.Count})",
                () => EnrichSttHop(donHangIFS));

            if (context.MachineRole == MachineRole.DuocBanQR)
            {
                DataTable donHang = SWLog.Measure(
                    "4. SaveFromSource [IFS→TMP]",
                    () => _workingState.SaveFromSource(
                        context,
                        donHangIFS,
                        "Usp_Qrcode_LOAD_PHIEU_DOCQR2405"));

                bool coMaNG = !_cfg.Delivery.CoGear
                    && _phieuRepo.CheckCoMaNG(tmpTable);

                return new OrderLoadResult
                {
                    Orders = donHang,
                    HasMaNG = coMaNG,
                    HasDifference = HasRows(sourceResult.Difference),
                    Source = context.Source,
                    Category = context.Category,
                    Caption = caption,
                    Warning = sourceResult.Warning,
                    IsQr = false
                };
            }

            string ifsViewTable = _cfg.Delivery.GetIfsViewTable();
            string tenBanView = _cfg.Delivery.GetTmpTable(context.Category == OrderCategory.SP);

            DataTable donHangView = SWLog.Measure(
                "4. LuuVaLoad [IFSView→TMPView]",
                () => _phieuRepo.LuuVaLoad(
                    ifsViewTable,
                    "Usp_Qrcode_LOAD_PHIEU_DOCQRView2405",
                    donHangIFS,
                    ngayGiaoSP,
                    context.NhaMay,
                    gioFccSP,
                    context.AddNm,
                    tenBanView,
                    docQRTable,
                    ifsViewTable));

            bool coMaNGView = !_cfg.Delivery.CoGear
                && _phieuRepo.CheckCoMaNG(tenBanView);

            return new OrderLoadResult
            {
                Orders = donHangView,
                HasMaNG = coMaNGView,
                HasDifference = HasRows(sourceResult.Difference),
                Source = context.Source,
                Category = context.Category,
                Caption = caption,
                Warning = sourceResult.Warning,
                IsQr = false
            };
        }

        private OrderLoadResult LoadFromTableOrder(OrderLoadContext context)
        {
            PrepareIfsBaseline(context);

            if (_cfg.Delivery.CoGear
                && (context.CheckedGios == null || context.CheckedGios.Count == 0))
            {
                return OrderLoadResult.Empty(context);
            }

            string gioFcc = "";
            string gioMoTa = "";

            if (context.CheckedGios != null && context.CheckedGios.Count > 0)
            {
                var hours = context.CheckedGios
                    .Select(g => g.Split(':')[0].PadLeft(2, '0'))
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();

                gioFcc = string.Join(",", hours.Select(h => $"'{h}'"));
                gioMoTa = string.Join("+", context.CheckedGios) + "H";
            }

            string dockCodeSP = _cfg.Delivery.DockCodeSP;
            bool isSP = context.Category == OrderCategory.SP;
            string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);

            DataTable donHang;
            bool isQrMachine = context.MachineRole == MachineRole.DuocBanQR;

            if (isQrMachine && context.IsBanQR)
            {
                int demQR = _phieuRepo.CountDocQRCode(docQRTable);

                if (demQR > 0)
                {
                    string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
                    donHang = _workingState.LoadCurrentOrder(context);
                }
                else
                {
                    donHang = LoadTableOrder(context, gioFcc, isSP, dockCodeSP);
                }
            }
            else
            {
                donHang = LoadTableOrder(context, gioFcc, isSP, dockCodeSP);
            }

            DataTable hangThieu = _phieuRepo.TinhHangThieuTuDonHang(donHang);

            string caption = _cfg.Delivery.CoGear
                ? $"ĐƠN HÀNG {_cfg.DisplayName} ({(isSP ? "SP" : "MP")}): " +
                  $"{context.NgayGiao:dd/MM/yyyy}   GIỜ: {gioMoTa}"
                : $"ĐƠN HÀNG {_cfg.DisplayName}: {context.NgayGiao:dd/MM/yyyy}";

            return new OrderLoadResult
            {
                Orders = donHang,
                HasMaNG = false,
                HasDifference = HasRows(LoadDifference(context)),
                Source = context.Source,
                Category = context.Category,
                Caption = caption,
                Warning = context.IfsLoadError,
                IsQr = isQrMachine && context.IsBanQR
            };
        }

        private OrderLoadResult LoadFromGiaoDb(OrderLoadContext context)
        {
            OrderSourceResult sourceResult = _orderSourceFactory
                .GetSource(context)
                .Load(context);

            return new OrderLoadResult
            {
                Orders = sourceResult.Orders ?? new DataTable(),
                HasMaNG = false,
                HasDifference = HasRows(sourceResult.Difference),
                Source = context.Source,
                Category = context.Category,
                Caption = string.Empty,
                Warning = sourceResult.Warning,
                IsQr = context.IsBanQR
            };
        }

        private DataTable LoadTableOrder(
            OrderLoadContext context,
            string gioFcc,
            bool isSP,
            string dockCodeSP)
        {
            return _tableOrderRepo.LoadPhieuTuBangRieng(
                context.NgayGiao.ToString("yyyy-MM-dd"),
                gioFcc,
                isSP,
                dockCodeSP,
                _cfg);
        }

        private void PrepareIfsBaseline(OrderLoadContext context)
        {
            string ifsTable = _cfg.Delivery.GetIfsTable(
                context.Category == OrderCategory.SP);

            string ngayXuatIFS = context.NgayGiao.ToString("ddMMyyyy");

            try
            {
                DataTable ifsData = _ifsRepo.GetFullCustomerOrder(
                    ngayXuatIFS,
                    _cfg);

                _phieuRepo.PushIfsSnapshot(ifsTable, ifsData);

                DataTable ifsScoped = ifsData;

                if (_cfg.Delivery.CoGear)
                    ifsScoped = GioRowFilter.Filter(
                        ifsScoped,
                        context.CheckedGios ?? new List<string>());

                if (_cfg.Delivery.CoLoaiSP)
                    ifsScoped = _rowCategoryFilter.Filter(
                        ifsScoped,
                        context.Category,
                        _cfg);

                context.IfsDataDaLoc = ifsScoped;
                context.IfsLoadError = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PhieuLoadService] Lỗi đồng bộ IFS snapshot: {ex.Message}");

                context.IfsDataDaLoc = null;
                context.IfsLoadError =
                    "⚠ Không kết nối được IFS để so sánh lệch " +
                    "(dữ liệu đơn hàng chính vẫn hiển thị bình thường). " +
                    $"Chi tiết: {ex.Message}";
            }
        }

        private DataTable LoadDifference(OrderLoadContext context)
        {
            try
            {
                OrderSourceResult result = _orderSourceFactory
                    .GetSource(context)
                    .Load(context);

                return result.Difference;
            }
            catch
            {
                return new DataTable();
            }
        }

        private void EnrichSttHop(DataTable donHangIFS)
        {
            if (donHangIFS == null || donHangIFS.Rows.Count == 0)
                return;

            var maHangList = donHangIFS.AsEnumerable()
                .Select(r => r["MAHANG"].ToString().Trim())
                .Where(m => !string.IsNullOrEmpty(m))
                .Distinct()
                .ToList();

            Dictionary<string, int> qcDict =
                _phieuRepo.GetQcDongGoiBatch(maHangList);

            for (int i = 0; i < donHangIFS.Rows.Count; i++)
            {
                DataRow row = donHangIFS.Rows[i];
                row["STT"] = (i + 1).ToString();

                string maHang = row["MAHANG"].ToString().Trim();
                int slGiao = Convert.ToInt32(row["SOLUONG"]);

                if (qcDict.TryGetValue(maHang, out int qcDg) && qcDg > 0)
                {
                    int hop = slGiao / qcDg;
                    if (slGiao % qcDg > 0)
                        hop++;

                    row["HOP"] = hop.ToString();
                }
            }
        }

        private static bool HasRows(DataTable table)
        {
            return table != null && table.Rows.Count > 0;
        }

        private static void ValidateContext(OrderLoadContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Cfg == null)
                throw new ArgumentException("OrderLoadContext.Cfg không được null.", nameof(context));

            if (context.Cfg.Delivery == null)
                throw new ArgumentException("OrderLoadContext.Cfg.Delivery không được null.", nameof(context));
        }
    }
}
