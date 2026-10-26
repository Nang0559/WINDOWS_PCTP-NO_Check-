using System;
using System.Diagnostics;
using PCTP.Modules.XuLyHangLoi.Models;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class PerformanceContractTests
{
    [Theory]
    [InlineData(100, 2000)]
    [InlineData(1000, 3000)]
    [InlineData(5000, 5000)]
    public void QcFinal_SyntheticWorkload_HasBoundedExecution(int records, int maxMilliseconds)
    {
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < records; i++)
        {
            var ticket = new PhieuXuLyBatThuong
            {
                SoLuongLoi = 10,
                QCInspection = new QCInspectionResult
                {
                    SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                    SoLuongOK = 6, SoLuongNG = 4,
                    SoLuongRework = 3, SoLuongLoaiBoBanDau = 1
                },
                Rework = new ReworkResult { SoLuongRework = 3, SoLuongOK = 2, SoLuongNG = 1 },
                Disposition = new DispositionResult { SoLuongLoaiBoBanDau = 1, SoLuongReworkNG = 1, SoLuongLoaiBoCuoi = 2 }
            };
            ticket.ValidateDomainInvariants();
        }
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds <= maxMilliseconds,
            $"Synthetic QC workload exceeded bound: {watch.ElapsedMilliseconds}ms > {maxMilliseconds}ms for {records} records.");
    }
}
