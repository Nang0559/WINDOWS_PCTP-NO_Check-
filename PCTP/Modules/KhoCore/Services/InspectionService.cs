using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Services
{
    public sealed class InspectionService : IInspectionService
    {
        private readonly IInspectionLogRepository _logRepo;

        public InspectionService(IInspectionLogRepository logRepo)
            => _logRepo = logRepo
                ?? throw new ArgumentNullException(nameof(logRepo));

        public InspectionResult Inspect(
            QRCodeInfo temTong,
            InspectionConfig config,
            IReadOnlyList<string> rawBoxScans)
        {
            var details = new List<BoxScanResult>();

            foreach (string raw in rawBoxScans)
            {
                QRCodeInfo box;
                try { box = QRCodeParser.ParseQRCode(raw.ToUpper()); }
                catch
                {
                    details.Add(new BoxScanResult
                    {
                        TemCode = raw,
                        IsMatch = false,
                        MismatchFields = "Tem không đúng định dạng"
                    });
                    continue;
                }

                var mismatches = new List<string>();

                if (config.CheckItemCode &&
                    !string.Equals(box.ItemCode, temTong.ItemCode,
                        StringComparison.OrdinalIgnoreCase))
                    mismatches.Add(
                        $"Mã hàng [{box.ItemCode} ≠ {temTong.ItemCode}]");

                if (config.CheckLotNo)
                {
                    string lotT = Truncate(temTong.LotNo, 10);
                    string lotB = Truncate(box.LotNo, 10);
                    if (!string.Equals(lotT, lotB,
                        StringComparison.OrdinalIgnoreCase))
                        mismatches.Add(
                            $"LotNo [{box.LotNo} ≠ {temTong.LotNo}]");
                }

                if (config.CheckNSX &&
                    !string.Equals(box.NgaySX, temTong.NgaySX,
                        StringComparison.OrdinalIgnoreCase))
                    mismatches.Add(
                        $"Ngày SX [{box.NgaySX} ≠ {temTong.NgaySX}]");

                details.Add(new BoxScanResult
                {
                    TemCode = box.LotNo,
                    ItemCode = box.ItemCode,
                    NSX = box.NgaySX,
                    IsMatch = mismatches.Count == 0,
                    MismatchFields = string.Join(" | ", mismatches)
                });
            }

            return new InspectionResult
            {
                AllPassed = details.Count >= config.DefaultQty
                               && details.All(d => d.IsMatch),
                ScannedCount = details.Count,
                FailedCount = details.Count(d => !d.IsMatch),
                Details = details
            };
        }

        public void SaveLog(
            string inspectionCode,
            QRCodeInfo temTong,
            IReadOnlyList<BoxScanResult> results,
            string finalResult)
        {
            foreach (var box in results)
            {
                _logRepo.SaveLog(new InspectionLogEntry
                {
                    InspectionCode = inspectionCode,
                    ItemCode = temTong.ItemCode,
                    TemCodeTong = temTong.LotNo,
                    LotNoTong = temTong.LotNo,
                    NSXTong = temTong.NgaySX,
                    SoLuongTong = temTong.Quantity,
                    BoxTemCode = box.TemCode,
                    BoxLotNo = box.TemCode,
                    BoxNSX = box.NSX,
                    IsMatch = box.IsMatch,
                    CheckedAt = DateTime.Now,
                    FinalResult = finalResult,
                    MaPhieu = temTong.MaPhieu
                });
            }
        }

        private static string Truncate(string s, int len)
            => s?.Length >= len ? s.Substring(0, len) : s;
    }
}
