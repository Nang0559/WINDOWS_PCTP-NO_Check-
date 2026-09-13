using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// LOT business workflow. No WinForms dependency; the presenter supplies
    /// a selector only when duplicate MAHANG/SOLUONG requires user choice.
    /// </summary>
    public sealed class PhieuLotService
    {
        private readonly IPhieuLotRepository _lotRepo;
        private readonly IPhieuValidationRepository _validationRepo;
        private readonly IEventBus _bus;

        public PhieuLotService(
            IPhieuLotRepository lotRepo,
            IPhieuValidationRepository validationRepo,
            IEventBus bus)
        {
            _lotRepo = lotRepo ?? throw new ArgumentNullException(nameof(lotRepo));
            _validationRepo = validationRepo ?? throw new ArgumentNullException(nameof(validationRepo));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public List<(int Stt, string Lot)> TinhTongLot(
            DataTable bangTam,
            string tenBan,
            string docQRTable,
            string tmpTable,
            Func<DataTable, int> chonSttKhiTrung)
        {
            var results = new List<(int Stt, string Lot)>();
            if (bangTam == null || bangTam.Rows.Count == 0)
            {
                _bus.Publish(new TinhTongCompletedEvent(results));
                return results;
            }

            foreach (DataRow row in bangTam.Rows)
            {
                string maHang = row["MAHANG"] == DBNull.Value ? string.Empty : row["MAHANG"].ToString().Trim();
                int sl = SafeInt(row["SOLUONG"]);
                int stt = SafeInt(row["STT"]);
                if (stt <= 0 || sl <= 0 || string.IsNullOrWhiteSpace(maHang))
                    continue;

                DataTable trungDt = _validationRepo.GetDanhSachTrungMaSl(maHang, sl, tenBan, docQRTable);
                int dem = trungDt == null ? 0 : trungDt.Rows.Count;
                if (dem == 0)
                    continue;

                if (dem > 1)
                {
                    if (chonSttKhiTrung == null)
                        continue;

                    int sttChon = chonSttKhiTrung(trungDt);
                    if (sttChon <= 0)
                        continue;
                    stt = sttChon;
                }

                string lot = _lotRepo.GetLotNo(maHang, stt, dem, sl, docQRTable, tmpTable);
                if (string.IsNullOrWhiteSpace(lot))
                    continue;

                _lotRepo.CapNhapLotTmpPhieu(stt, lot, tenBan);
                results.Add((stt, lot));
            }

            _bus.Publish(new TinhTongCompletedEvent(results));
            return results;
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;
            int result;
            return int.TryParse(value.ToString(), out result) ? result : 0;
        }
    }
}
