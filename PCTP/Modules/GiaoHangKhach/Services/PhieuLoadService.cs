using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
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

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Phase 5/6: orchestration của pipeline load phiếu giao hàng.
    /// Source lấy dữ liệu nguồn; WorkingState quản lý TMP/DOCQR.
    /// Service trả về OrderLoadResult và không publish EventBus/UI.
    /// </summary>
    public sealed class PhieuLoadService : IPhieuLoadService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IIFSRepository _ifsRepo;
        private readonly IOrderSourceFactory _orderSourceFactory;
        private readonly IRowCategoryFilter _rowCategoryFilter;
        private readonly IDeliveryWorkingState _workingState;
        private readonly CustomerConfig _cfg;
        private readonly string _tenBan;

        public PhieuLoadService(IPhieuRepository phieuRepo, IIFSRepository ifsRepo, IOrderSourceFactory orderSourceFactory, IRowCategoryFilter rowCategoryFilter, IDeliveryWorkingState workingState, CustomerConfig cfg, string tenBan)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _ifsRepo = ifsRepo ?? throw new ArgumentNullException(nameof(ifsRepo));
            _orderSourceFactory = orderSourceFactory ?? throw new ArgumentNullException(nameof(orderSourceFactory));
            _rowCategoryFilter = rowCategoryFilter ?? throw new ArgumentNullException(nameof(rowCategoryFilter));
            _workingState = workingState ?? throw new ArgumentNullException(nameof(workingState));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _tenBan = tenBan;
        }

        public OrderLoadResult Load(OrderLoadContext context)
        {
            ValidateContext(context);
            switch (context.Source)
            {
                case OrderSourceKind.IFS: return LoadFromIfs(context);
                case OrderSourceKind.TableOrder: return LoadFromTableOrder(context);
                case OrderSourceKind.GiaoDB: return LoadFromGiaoDb(context);
                default: throw new ArgumentOutOfRangeException(nameof(context.Source));
            }
        }

        private OrderLoadResult LoadFromIfs(OrderLoadContext context)
        {
            DateTime dt = context.NgayGiao;
            string ngayGiaoSP = dt.ToString("yyyy-MM-dd");
            string ngayXuat = dt.ToString("ddMMyyyy");
            bool isSP = context.Category == OrderCategory.SP;

            // SP for 100001 is loaded by date only. MP's selected hour must
            // never be reused for the SP query.
            string gioFccSP = isSP ? "" : (_cfg.Delivery.LoadTheoNgay ? "" : context.GioFcc);
            string gioMoTaSP = isSP ? "Tất cả ca" : (_cfg.Delivery.LoadTheoNgay ? "Tất cả ca" : context.GioFccMoTa);
            string tmpTable = _cfg.Delivery.GetTmpTable(isSP);
            string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);
            string caption = BuildOrderCaption(context, gioMoTaSP);

            if (context.MachineRole == MachineRole.DuocBanQR && context.IsBanQR)
            {
                int demQR = SWLog.Measure("1. CountDocQRCode", () => _phieuRepo.CountDocQRCode(docQRTable));
                if (demQR > 0)
                {
                    DataTable donHangQr = SWLog.Measure("2. LoadPhieuDocQR", () => _workingState.LoadFromQr(context));
                    bool coMaNG = !_cfg.Delivery.CoGear && _phieuRepo.CheckCoMaNG(tmpTable);
                    return BuildResult(context, donHangQr, new DataTable(), caption, coMaNG, false, null, true);   // ← thêm new DataTable()
                }
            }

            OrderSourceResult sourceResult = _orderSourceFactory.GetSource(context).Load(context);
            DataTable donHangIFS = sourceResult.Orders ?? new DataTable();
            if (_cfg.Delivery.CoLoaiSP)
                donHangIFS = _rowCategoryFilter.Filter(donHangIFS, context.Category, _cfg);
            SWLog.Measure($"3. EnrichSttHop ({donHangIFS.Rows.Count})", () => EnrichSttHop(donHangIFS));

            if (context.MachineRole == MachineRole.DuocBanQR)
            {
                DataTable donHang = SWLog.Measure("4. SaveFromSource [IFS→TMP]", () => _workingState.SaveFromSource(context, donHangIFS, "Usp_Qrcode_LOAD_PHIEU_DOCQR2405"));
                bool coMaNG = !_cfg.Delivery.CoGear && _phieuRepo.CheckCoMaNG(tmpTable);
                return BuildResult(context, donHang, new DataTable(), caption, coMaNG, HasRows(sourceResult.Difference), sourceResult.Warning, false);   // ← thêm new DataTable()
            }

            string ifsViewTable = _cfg.Delivery.GetIfsViewTable();
            string tenBanView = context.MachineRole == MachineRole.DuocBanQR ? _cfg.Delivery.GetTmpTable(isSP) : _tenBan;
            DataTable donHangView = SWLog.Measure("4. LuuVaLoad [IFSView→TMPView]", () => _phieuRepo.LuuVaLoad(ifsViewTable, "Usp_Qrcode_LOAD_PHIEU_DOCQRView2405", donHangIFS, ngayGiaoSP, context.NhaMay, gioFccSP, context.AddNm, tenBanView, docQRTable, ifsViewTable));
            bool coMaNGView = !_cfg.Delivery.CoGear && _phieuRepo.CheckCoMaNG(tenBanView);
            return BuildResult(context, donHangView, new DataTable(), caption, coMaNGView, HasRows(sourceResult.Difference), sourceResult.Warning, false);   // ← thêm new DataTable()
        }

        private OrderLoadResult LoadFromTableOrder(OrderLoadContext context)
        {
            PrepareIfsBaseline(context);
            if (_cfg.Delivery.CoGear && (context.CheckedGios == null || context.CheckedGios.Count == 0)) return OrderLoadResult.Empty(context);
            string gioMoTa = context.CheckedGios != null && context.CheckedGios.Count > 0 ? string.Join("+", context.CheckedGios) + "H" : string.Empty;
            bool isSP = context.Category == OrderCategory.SP;
            string docQRTable = _cfg.Delivery.GetDocQRTable(isSP);
            bool isQrMachine = context.MachineRole == MachineRole.DuocBanQR;
            if (isQrMachine && context.IsBanQR)
            {
                int demQR = _phieuRepo.CountDocQRCode(docQRTable);
                if (demQR > 0) return BuildTableResult(context, _workingState.LoadCurrentOrder(context), gioMoTa, false);
            }
            OrderSourceResult sourceResult = _orderSourceFactory.GetSource(context).Load(context);
            return BuildTableResult(context, sourceResult.Orders ?? new DataTable(), gioMoTa, HasRows(sourceResult.Difference), sourceResult.Warning);
        }

        private OrderLoadResult BuildTableResult(OrderLoadContext context, DataTable donHang, string gioMoTa, bool hasDifference, string warning = null)
        {
            bool isSP = context.Category == OrderCategory.SP;
            DataTable hangThieu = _phieuRepo.TinhHangThieuTuDonHang(donHang) ?? new DataTable();
            string caption = BuildOrderCaption(context, gioMoTa);
            return BuildResult(context, donHang, hangThieu, caption, false, hasDifference, warning ?? context.IfsLoadError, context.MachineRole == MachineRole.DuocBanQR && context.IsBanQR);
        }

        private OrderLoadResult LoadFromGiaoDb(OrderLoadContext context)
        {
            OrderSourceResult sourceResult = _orderSourceFactory.GetSource(context).Load(context);
            return BuildResult(context, sourceResult.Orders ?? new DataTable(), new DataTable(), string.Empty, false, HasRows(sourceResult.Difference), sourceResult.Warning, context.IsBanQR);
        }

        private OrderLoadResult BuildResult(OrderLoadContext context, DataTable orders, DataTable hangThieu, string caption, bool hasMaNG, bool hasDifference, string warning, bool isQr)
        {
            return new OrderLoadResult { Orders = orders ?? new DataTable(), HangThieu = hangThieu ?? new DataTable(), HasMaNG = hasMaNG, HasDifference = hasDifference, Source = context.Source, Category = context.Category, Caption = caption ?? string.Empty, Warning = warning, IsQr = isQr, LoadRequestId = context.LoadRequestId };
        }

        private void PrepareIfsBaseline(OrderLoadContext context)
        {
            string ifsTable = _cfg.Delivery.GetIfsTable(context.Category == OrderCategory.SP);
            string ngayXuatIFS = context.NgayGiao.ToString("ddMMyyyy");
            try
            {
                DataTable ifsData = _ifsRepo.GetFullCustomerOrder(ngayXuatIFS, _cfg);
                _phieuRepo.PushIfsSnapshot(ifsTable, ifsData);
                DataTable ifsScoped = ifsData;
                if (_cfg.Delivery.CoGear) ifsScoped = GioRowFilter.Filter(ifsScoped, context.CheckedGios ?? new List<string>());
                if (_cfg.Delivery.CoLoaiSP) ifsScoped = _rowCategoryFilter.Filter(ifsScoped, context.Category, _cfg);
                context.IfsDataDaLoc = ifsScoped;
                context.IfsLoadError = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PhieuLoadService] Lỗi đồng bộ IFS snapshot: {ex.Message}");
                context.IfsDataDaLoc = null;
                context.IfsLoadError = "⚠ Không kết nối được IFS để so sánh lệch (dữ liệu đơn hàng chính vẫn hiển thị bình thường). Chi tiết: " + ex.Message;
            }
        }

        private void EnrichSttHop(DataTable donHangIFS)
        {
            if (donHangIFS == null || donHangIFS.Rows.Count == 0) return;
            var maHangList = donHangIFS.AsEnumerable().Select(r => r["MAHANG"].ToString().Trim()).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();
            Dictionary<string, int> qcDict = _phieuRepo.GetQcDongGoiBatch(maHangList);
            for (int i = 0; i < donHangIFS.Rows.Count; i++)
            {
                DataRow row = donHangIFS.Rows[i];
                row["STT"] = (i + 1).ToString();
                string maHang = row["MAHANG"].ToString().Trim();
                int slGiao = Convert.ToInt32(row["SOLUONG"]);
                if (qcDict.TryGetValue(maHang, out int qcDg) && qcDg > 0)
                {
                    int hop = slGiao / qcDg;
                    if (slGiao % qcDg > 0) hop++;
                    row["HOP"] = hop.ToString();
                }
            }
        }

        /// <summary>
        /// Chuẩn hoá caption đơn hàng tại một điểm duy nhất.
        /// CustomerConfig.DisplayName là tên khách hàng chuẩn; không dùng
        /// tên nhà máy/giờ ẩn làm fallback cho customer name.
        /// </summary>
        private string BuildOrderCaption(OrderLoadContext context, string gioMoTa)
        {
            if (_cfg.Delivery.LoadTheoNgay)
                return $"ĐƠN HÀNG: {_cfg.DisplayName}";

            if (_cfg.Delivery.CoGear)
            {
                bool isSP = context.Category == OrderCategory.SP;
                string category = isSP ? "SP" : "MP";
                string gio = string.IsNullOrWhiteSpace(gioMoTa)
                    ? string.Empty
                    : $"   GIỜ: {gioMoTa}";

                return $"ĐƠN HÀNG {_cfg.DisplayName} ({category}): {context.NgayGiao:dd/MM/yyyy}{gio}";
            }

            string factory = string.IsNullOrWhiteSpace(context.NhaMay)
                ? string.Empty
                : $" - {context.NhaMay}";
            string gioGiao = string.IsNullOrWhiteSpace(gioMoTa)
                ? string.Empty
                : $"   GIỜ GIAO: {gioMoTa}";

            return $"ĐƠN HÀNG: {_cfg.DisplayName}{factory}{gioGiao}";
        }

        private static bool HasRows(DataTable table) => table != null && table.Rows.Count > 0;

        private static void ValidateContext(OrderLoadContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Cfg == null) throw new ArgumentException("OrderLoadContext.Cfg không được null.", nameof(context));
            if (context.Cfg.Delivery == null) throw new ArgumentException("OrderLoadContext.Cfg.Delivery không được null.", nameof(context));
        }
    }
}
