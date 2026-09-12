using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Shared.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Phase 7: business service cho flow YMVN/MilkRun.
    /// YMVN là business flow, không phải OrderSourceKind.
    /// </summary>
    public class PhieuYmvnService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly ITableOrderRepository _tableOrderRepo;
        private readonly IEventBus _bus;
        private readonly CustomerConfig _cfg;

        public PhieuYmvnService(
            IPhieuRepository phieuRepo,
            ITableOrderRepository tableOrderRepo,
            IEventBus bus,
            CustomerConfig cfg)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _tableOrderRepo = tableOrderRepo ?? throw new ArgumentNullException(nameof(tableOrderRepo));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public void CapNhapKho(
            string ngayGiao,
            string gioXuat,
            string nhaMay,
            DataTable donHang)
        {
            var errors = new List<DS_ERR_CNK>();
            int soLot = 0;

            string giogiao = string.Join("+",
                gioXuat.Split(',')
                    .Select(g => g.Trim().Trim('\'').PadLeft(2, '0')));

            foreach (DataRow row in donHang.Rows)
            {
                string lot = row["LOT"]?.ToString().Trim() ?? "";
                string status = row["STATUS"]?.ToString().Trim() ?? "";
                int stt = SafeInt(row["STT"]);
                string maHang = row["MAHANG"]?.ToString().Trim() ?? "";

                if (lot == "" || status == "OK")
                    continue;

                bool ok = _phieuRepo.CapNhapKhoYMVN(
                    stt,
                    lot,
                    maHang,
                    ngayGiao,
                    giogiao,
                    nhaMay,
                    out DS_ERR_CNK err);

                if (ok)
                    soLot++;
                else if (err != null)
                    errors.Add(err);
            }

            _bus.Publish(new KhoUpdatedEvent(soLot, ToDataTable(errors)));
        }

        public void HoanThanh(bool isLoaiSP = false)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[HoanThanhYMVN] TmpTable={_cfg.Delivery.TmpTable}, DocQRTable={_cfg.Delivery.DocQRTable}, isLoaiSP={isLoaiSP}");

            DataTable result = _phieuRepo.TakeLotYMVN(
                _cfg.Delivery.TmpTable,
                _cfg.Delivery.DocQRTable,
                isLoaiSP);

            if (result == null || result.Rows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[HoanThanhYMVN] Không có dữ liệu trả về!");
                _bus.Publish(new HoanThanhYMVNCompletedEvent(new DataTable()));
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[HoanThanhYMVN] Số dòng trả về: {result.Rows.Count}");
            foreach (DataRow row in result.Rows)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"  STT={row["STT"]}, MAHANG={row["MAHANG"]}, LOT={row["LOT"]}, " +
                    $"SOLUONG={row["SOLUONG"]}, STATUS={row["STATUS"]}, " +
                    $"TONG_SLHVN={row["TONG_SLHVN"]}, SL_GIAO={row["SL_GIAO"]}, IsOK={row["IsOK"]}");
            }

            _bus.Publish(new HoanThanhYMVNCompletedEvent(result));
        }

        public List<string> GetDanhSachGio(string ngayXuatMDY)
            => _tableOrderRepo.GetDanhSachGioYMVN(ngayXuatMDY).ToList();

        public void UploadMilkrunSP(DataTable donHang, string ngayGiao)
            => _tableOrderRepo.UploadMilkrunSP(donHang, ngayGiao);

        public void SyncPhieuTuBangRiengChoDocQR(
            DataTable donHang,
            string ngayGiao,
            List<string> checkedGios = null)
        {
            if (donHang == null || donHang.Rows.Count == 0)
                return;

            _phieuRepo.XoaTmpPhieu(_cfg.Delivery.TmpTable);

            foreach (DataRow row in donHang.Rows)
            {
                string status = row["STATUS"]?.ToString() ?? "";
                if (status == "OK")
                    continue;

                string gio = "";
                if (row.Table.Columns.Contains("NGAYGIAO") &&
                    row["NGAYGIAO"] != DBNull.Value &&
                    DateTime.TryParse(row["NGAYGIAO"].ToString(), out DateTime dt))
                {
                    gio = dt.ToString("HH:mm");
                }

                if (checkedGios != null && checkedGios.Any())
                {
                    bool match = checkedGios.Any(g =>
                        gio.StartsWith(g.Length >= 2 ? g.Substring(0, 2) : g));
                    if (!match)
                        continue;
                }

                string nxh = row.Table.Columns.Contains("NGAYGIAO") &&
                             row["NGAYGIAO"] != DBNull.Value &&
                             DateTime.TryParse(row["NGAYGIAO"].ToString(), out DateTime ngay)
                    ? ngay.ToString("yyyy-MM-dd HH:mm:ss")
                    : ngayGiao + " 00:00:00";

                string Get(string col) => row.Table.Columns.Contains(col)
                    ? row[col]?.ToString() ?? "" : "";

                string gear = Get("GEAR");
                string poNo = Get("PO_NO");
                string orderNo = Get("ORDER_NO");
                string gioXuat = Get("GIO");
                if (string.IsNullOrEmpty(gioXuat))
                    gioXuat = gio;

                _tableOrderRepo.InsertTmpYMVN(
                    stt: Get("STT"),
                    cua: Get("CUA"),
                    truyen: Get("TRUYEN"),
                    maHang: Get("MAHANG"),
                    tenHang: Get("TENHANG"),
                    lot: Get("LOT"),
                    dv: !string.IsNullOrEmpty(Get("DV")) ? Get("DV") : "PCS",
                    slXuat: SafeInt(row.Table.Columns.Contains("SOLUONG")
                                  ? row["SOLUONG"] : DBNull.Value),
                    ngayGiao: nxh,
                    gear: gear,
                    gioXuat: gioXuat,
                    tmpTable: _cfg.Delivery.TmpTable,
                    poNo: poNo,
                    cusPoNo: orderNo);
            }
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;

            return int.TryParse(value.ToString(), out int result)
                ? result
                : 0;
        }

        private static DataTable ToDataTable(List<DS_ERR_CNK> list)
        {
            var dt = new DataTable();
            dt.Columns.Add("MH");
            dt.Columns.Add("LOT");
            dt.Columns.Add("SLC", typeof(int));
            dt.Columns.Add("SLTK", typeof(int));
            dt.Columns.Add("SLT", typeof(int));
            dt.Columns.Add("STATUS");

            foreach (var e in list)
                dt.Rows.Add(e.MH, e.LOT, e.SLC, e.SLTK, e.SLT, e.Ms);

            return dt;
        }
    }
}
