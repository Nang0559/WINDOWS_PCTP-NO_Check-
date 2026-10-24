using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// LOT workflow: group duplicate delivery lines once, ask the operator for
    /// STT choices until only one candidate remains, then process that final
    /// candidate implicitly. QR selection itself is automatic in SQL.
    /// </summary>
    public sealed class PhieuLotService : IPhieuLotService
    {
        private readonly IPhieuLotRepository _lotRepo;
        private readonly IPhieuValidationRepository _validationRepo;
        private readonly IEventBus _bus;

        public PhieuLotService(IPhieuLotRepository lotRepo, IPhieuValidationRepository validationRepo, IEventBus bus)
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
            var processedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (bangTam == null || bangTam.Rows.Count == 0)
            {
                _bus.Publish(new TinhTongCompletedEvent(results));
                return results;
            }

            foreach (DataRow inputRow in bangTam.Rows)
            {
                string maHang = ReadText(inputRow, "MAHANG");
                int sl = SafeInt(inputRow["SOLUONG"]);
                int inputStt = SafeInt(inputRow["STT"]);
                if (inputStt <= 0 || sl <= 0 || string.IsNullOrWhiteSpace(maHang))
                    continue;

                // A duplicated MAHANG + SOLUONG group is handled as one workflow.
                // This prevents the outer bangTam loop from reopening the same
                // duplicate dialog for each row in that group.
                string groupKey = maHang.Trim() + "\u001f" + sl.ToString(CultureInfo.InvariantCulture);
                if (!processedGroups.Add(groupKey))
                    continue;

                DataTable candidates = _validationRepo.GetDanhSachTrungMaSl(maHang, sl, tenBan, docQRTable);
                if (candidates == null || candidates.Rows.Count == 0)
                    continue;

                DataTable pending = candidates.Copy();
                while (pending.Rows.Count > 0)
                {
                    int selectedStt;
                    if (pending.Rows.Count > 1)
                    {
                        if (chonSttKhiTrung == null)
                            break;

                        selectedStt = chonSttKhiTrung(pending.Copy());
                        if (selectedStt <= 0)
                            break; // User cancelled: no repository call or QR mutation.
                    }
                    else
                    {
                        // The last remaining duplicate row is unambiguous.
                        selectedStt = SafeInt(pending.Rows[0]["STT"]);
                    }

                    DataRow selectedRow = FindByStt(pending, selectedStt);
                    if (selectedRow == null)
                        break; // Reject stale/foreign STT returned by the UI.

                    string selectedPart = ReadText(selectedRow, "MAHANG");
                    int selectedQty = SafeInt(selectedRow["SOLUONG"]);
                    if (!string.Equals(selectedPart, maHang, StringComparison.OrdinalIgnoreCase) || selectedQty != sl)
                        break;

                    // Repository performs QR exact-sum allocation and writes LOT
                    // within the same transaction that marks QR rows DG.
                    string lot = _lotRepo.GetLotNo(maHang, selectedStt, pending.Rows.Count, sl, docQRTable, tmpTable);
                    if (string.IsNullOrWhiteSpace(lot))
                        break; // No exact subset: leave remaining rows and QR candidates untouched.

                    results.Add((selectedStt, lot));
                    pending.Rows.Remove(selectedRow);
                    pending.AcceptChanges();
                }
            }

            _bus.Publish(new TinhTongCompletedEvent(results));
            return results;
        }

        private static DataRow FindByStt(DataTable table, int stt)
        {
            foreach (DataRow row in table.Rows)
                if (SafeInt(row["STT"]) == stt)
                    return row;
            return null;
        }

        private static string ReadText(DataRow row, string column)
        {
            if (row == null || row[column] == null || row[column] == DBNull.Value)
                return string.Empty;
            return row[column].ToString().Trim();
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;
            return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : 0;
        }
    }
}