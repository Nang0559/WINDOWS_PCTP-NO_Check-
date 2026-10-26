using System;
using PCTP.Modules.XuLyHangLoi.Models;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class QcFinalDomainInvariantTests
{
    [Fact]
    public void InitialQc_MustEqualOkPlusNg()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            SoLuongLoi = 10,
            QCInspection = new QCInspectionResult
            {
                SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                SoLuongOK = 6, SoLuongNG = 4,
                SoLuongRework = 3, SoLuongLoaiBoBanDau = 1
            }
        };

        ticket.ValidateDomainInvariants();
    }

    [Fact]
    public void InitialQc_RejectsInspectionQuantityNotEqualOkPlusNg()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            QCInspection = new QCInspectionResult
            {
                SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                SoLuongOK = 6, SoLuongNG = 3,
                SoLuongRework = 2, SoLuongLoaiBoBanDau = 1
            }
        };

        Assert.Throws<InvalidOperationException>(() => ticket.ValidateDomainInvariants());
    }

    [Fact]
    public void InitialQc_RejectsNgNotEqualReworkPlusInitialScrap()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            QCInspection = new QCInspectionResult
            {
                SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                SoLuongOK = 6, SoLuongNG = 4,
                SoLuongRework = 2, SoLuongLoaiBoBanDau = 1
            }
        };

        Assert.Throws<InvalidOperationException>(() => ticket.ValidateDomainInvariants());
    }

    [Fact]
    public void FinalQc_ReworkOkAndNgMustEqualApprovedReworkQuantity()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            QCInspection = new QCInspectionResult
            {
                SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                SoLuongOK = 6, SoLuongNG = 4,
                SoLuongRework = 3, SoLuongLoaiBoBanDau = 1
            },
            Rework = new ReworkResult { SoLuongRework = 3, SoLuongOK = 2, SoLuongNG = 1 },
            Disposition = new DispositionResult
            {
                SoLuongLoaiBoBanDau = 1, SoLuongReworkNG = 1, SoLuongLoaiBoCuoi = 2
            }
        };

        ticket.ValidateDomainInvariants();
    }

    [Fact]
    public void FinalQc_RejectsOkPlusNgDifferentFromReworkQuantity()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            QCInspection = new QCInspectionResult
            {
                SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                SoLuongOK = 6, SoLuongNG = 4,
                SoLuongRework = 3, SoLuongLoaiBoBanDau = 1
            },
            Rework = new ReworkResult { SoLuongRework = 3, SoLuongOK = 2, SoLuongNG = 2 }
        };

        Assert.Throws<InvalidOperationException>(() => ticket.ValidateDomainInvariants());
    }

    [Fact]
    public void Disposition_FinalScrapMustEqualInitialScrapPlusReworkNg()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            QCInspection = new QCInspectionResult
            {
                SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                SoLuongOK = 6, SoLuongNG = 4,
                SoLuongRework = 3, SoLuongLoaiBoBanDau = 1
            },
            Rework = new ReworkResult { SoLuongRework = 3, SoLuongOK = 2, SoLuongNG = 1 },
            Disposition = new DispositionResult
            {
                SoLuongLoaiBoBanDau = 1, SoLuongReworkNG = 1, SoLuongLoaiBoCuoi = 1
            }
        };

        Assert.Throws<InvalidOperationException>(() => ticket.ValidateDomainInvariants());
    }

    [Fact]
    public void Compensation_IsIndependentFromNgAndReworkQuantities()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            QCInspection = new QCInspectionResult
            {
                SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
                SoLuongOK = 6, SoLuongNG = 4,
                SoLuongRework = 3, SoLuongLoaiBoBanDau = 1
            },
            Compensation = new CustomerCompensation
            {
                SoLuongCanGiaoBu = 5, SoLuongDaGiaoBu = 4
            }
        };

        ticket.ValidateDomainInvariants();
    }

    [Fact]
    public void Compensation_RejectsDeliveredQuantityAboveObligation()
    {
        var ticket = new PhieuXuLyBatThuong
        {
            Compensation = new CustomerCompensation
            {
                SoLuongCanGiaoBu = 5, SoLuongDaGiaoBu = 6
            }
        };

        Assert.Throws<InvalidOperationException>(() => ticket.ValidateDomainInvariants());
    }

    [Fact]
    public void AffectedLot_RejectsNegativeQuantities()
    {
        var ticket = new PhieuXuLyBatThuong();
        ticket.AffectedLots.Add(new PhieuXuLyBatThuongAffectedLot { LotNo = "LOT-A", SoLuongAnhHuong = -1 });

        Assert.Throws<InvalidOperationException>(() => ticket.ValidateDomainInvariants());
    }
}
