using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Production-orchestration regression guards for DEL-003/DEL-007.
/// These intentionally verify the real PhieuKhoRepository source contract; SQL
/// engine scenarios remain responsible for runtime transaction semantics.
/// </summary>
public sealed class DeliveryProductionBoundaryTests
{
    private static string Source()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PhieuKhoRepository.source.cs");
        Assert.True(File.Exists(path), "Linked PhieuKhoRepository source fixture missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void CustomerDelivery_EntersUowBeforeStoredProcedureAndFinalization()
    {
        var source = Source();
        var begin = source.IndexOf("Uow.Begin();", StringComparison.Ordinal);
        var procedure = source.IndexOf("Usp_Qrcode_Update_Stock2405", StringComparison.Ordinal);
        var stock = source.IndexOf("TruKhoAoTuKetQuaSP(stok", StringComparison.Ordinal);
        var finalization = source.IndexOf("HoanTatSauKhiTruKho(lotsDaXuatThanhCong", StringComparison.Ordinal);
        var commit = source.IndexOf("Uow.Commit();", StringComparison.Ordinal);

        Assert.True(begin >= 0 && procedure > begin && stock > procedure && finalization > stock && commit > finalization,
            "Production delivery must keep SP, stock deduction, finalization and commit inside one explicit UoW boundary.");
    }

    [Fact]
    public void CustomerDelivery_ErrorResultRollsBackBeforeAnyPostProcedureMutation()
    {
        var source = Source();
        var guard = source.IndexOf("if (errors.Rows.Count > 0)", StringComparison.Ordinal);
        var rollback = source.IndexOf("Uow.Rollback();", guard, StringComparison.Ordinal);
        var stock = source.IndexOf("TruKhoAoTuKetQuaSP(stok", guard, StringComparison.Ordinal);

        Assert.True(guard >= 0 && rollback > guard && stock > rollback,
            "Stored-procedure business errors must rollback before consuming returned stock rows.");
    }

    [Fact]
    public void CustomerDelivery_ExceptionPathRollsBackTheUnitOfWork()
    {
        var source = Source();
        var catchBlock = source.IndexOf("catch\n            {\n                Uow.Rollback();", StringComparison.Ordinal);
        Assert.True(catchBlock >= 0, "Production delivery must rollback the UoW on unexpected orchestration failures.");
    }
}
