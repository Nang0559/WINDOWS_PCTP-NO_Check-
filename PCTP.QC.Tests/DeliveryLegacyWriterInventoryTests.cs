using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// B0-01 ratchet. Customer delivery still mutates STOCKTP outside the KhoCore movement owner.
/// These tests do NOT claim the blocker is closed: they pin the known set of legacy writers and the
/// single production composition path so that a new writer or a second, unwired construction path
/// cannot appear unnoticed while the delivery command is being built.
///
/// When the delivery command replaces a writer, change the matching assertion deliberately in the
/// same commit (and update WMS_B0_01_DELIVERY_PARITY.md); never delete the ratchet to get green.
/// </summary>
public sealed class DeliveryLegacyWriterInventoryTests
{
    private static string ReadSource(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        Assert.True(File.Exists(path), "Linked source fixture missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void PhieuKhoRepository_LegacyStockWriters_AreExactlyTheKnownSet()
    {
        var source = ReadSource("PhieuKhoRepository.source.cs");

        var updateStockProcedures = Regex.Matches(source, "\"(Usp_\\w*Update_Stock\\w*)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(new[] { "Usp_Qrcode_Update_Stock_SP", "Usp_Qrcode_Update_Stock2405" }
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), updateStockProcedures);

        Assert.Single(Regex.Matches(source, @"UPDATE\s+STOCKTP\b", RegexOptions.IgnoreCase));
        Assert.Empty(Regex.Matches(source, @"INSERT\s+INTO\s+STOCKTP\b", RegexOptions.IgnoreCase));
        Assert.Empty(Regex.Matches(source, @"DELETE\s+FROM\s+STOCKTP\b", RegexOptions.IgnoreCase));

        // One definition + exactly one caller (CapNhapKhoYMVN).
        Assert.Equal(2, Regex.Matches(source, @"\bTruStockTpFifo\(").Count);
    }

    [Fact]
    public void ProductionComposition_SuppliesMovementServiceToPhieuKhoRepository()
    {
        var factory = ReadSource("GiaoHangKhachFactory.source.cs");
        var phieu = ReadSource("PhieuRepository.source.cs");

        Assert.Contains("new StockMovementService(", factory);
        Assert.Matches(new Regex(@"new PhieuRepository\([^;]*stockMovement:\s*stockMovement\)", RegexOptions.Singleline), factory);
        Assert.Matches(new Regex(@"new PhieuKhoRepository\([^;]*stockMovement\);", RegexOptions.Singleline), phieu);
    }

    [Fact]
    public void DeliveryStockCommandHandler_IsNotWiredIntoProductionComposition_UntilCutoverIsDeliberate()
    {
        // B0-01 cutover ratchet. The handler exists and is tested, but it must not be constructed by the
        // production factory until P4/A0 are closed, the SQL scenario is green on the exact SHA and the
        // three legacy writers are replaced in the same commit. When that happens, flip this assertion
        // together with DeliveryLegacyWriterInventoryTests and WMS_B0_01_DELIVERY_PARITY.md.
        var factory = ReadSource("GiaoHangKhachFactory.source.cs");
        Assert.DoesNotContain("DeliveryStockCommandHandler", factory);
        Assert.DoesNotContain("DeliveryTmpPendingGate", factory);
    }

    [Fact]
    public void ProductionCode_HasSingleConstructionPathForPhieuRepositoryAndPhieuKhoRepository()
    {
        var root = FindRepositoryRoot();
        var sources = Directory.EnumerateFiles(Path.Combine(root, "PCTP"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToArray();

        string[] SitesOf(string token) => sources
            .Where(f => File.ReadAllText(f).Contains(token, StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "GiaoHangKhachFactory.cs" }, SitesOf("new PhieuRepository("));
        Assert.Equal(new[] { "PhieuRepository.cs" }, SitesOf("new PhieuKhoRepository("));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "PCTP", "Modules")) &&
                Directory.Exists(Path.Combine(dir.FullName, "PCTP.QC.Tests")))
                return dir.FullName;
            dir = dir.Parent;
        }
        Assert.Fail("Repository root (PCTP/Modules + PCTP.QC.Tests) not found above " + AppContext.BaseDirectory);
        return string.Empty;
    }
}
