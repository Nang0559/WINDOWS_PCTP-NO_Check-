using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class DeliveryQrAllocationSourceContractTests
{
    private static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(path), "Missing production source fixture: " + path);
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    [Fact]
    public void SqlAllocator_MustSearchExactSubsetsInsteadOfGreedyAccumulation()
    {
        var sql = Read("Usp_Qrcode_Take_Lot2405.sql");
        Assert.Contains("CREATE TABLE #DP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE #DPPrev", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p.Qty+@qrQty<=@_SLGIAO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE Qty=@_SLGIAO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY STT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET @SLTT = @SLTT + @_SLTEMFCC", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlAllocator_MustMarkOnlyChosenQrRowsAndRollbackPartialWrites()
    {
        var sql = Read("Usp_Qrcode_Take_Lot2405.sql");
        Assert.Contains("JOIN #Chosen c ON c.STT=q.STT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @affected<>@chosenCount", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@LotTotal<>CONVERT(bigint,@_SLGIAO)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @affected<>1 THROW", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SAVE TRANSACTION PctpTakeLot2405", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROLLBACK TRANSACTION PctpTakeLot2405", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ISNULL(KETQUA,N'''')<>N''DG''", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repository_MustPersistLotInSameUnitOfWorkAsQrClaim()
    {
        var source = Read("PhieuLotRepository.source.cs");
        var transaction = source.IndexOf("return InTransaction(() =>", StringComparison.Ordinal);
        var procedure = source.IndexOf("ExecuteStoredProcedure(", transaction, StringComparison.Ordinal);
        var lotUpdate = source.IndexOf("UPDATE [{tmpTable}] SET LOT = @lot", transaction, StringComparison.Ordinal);
        Assert.True(transaction >= 0 && procedure > transaction && lotUpdate > procedure,
            "QR claim and selected TMP LOT write must run inside one repository transaction.");
        Assert.Contains("if (updated != 1)", source);
    }

    [Fact]
    public void LotService_MustProcessEachDuplicateGroupOnceAndValidateSelectedStt()
    {
        var source = Read("PhieuLotService.source.cs");
        Assert.Contains("processedGroups", source);
        Assert.Contains("if (!processedGroups.Add(groupKey))", source);
        Assert.Contains("while (pending.Rows.Count > 0)", source);
        Assert.Contains("if (pending.Rows.Count > 1)", source);
        Assert.Contains("FindByStt(pending, selectedStt)", source);
        Assert.DoesNotContain("_lotRepo.CapNhapLotTmpPhieu(stt", source);
    }

    [Fact]
    public void DuplicateDialog_MustNotUseStaticSelectionState()
    {
        var form = Read("FRM_LISTRUNGMSL.source.cs");
        var dialog = Read("PhieuDialogControl.source.cs");
        Assert.Contains("public int? SelectedStt", form);
        Assert.Contains("DialogResult = DialogResult.OK", form);
        Assert.DoesNotContain("static string MAHANG", form);
        Assert.DoesNotContain("FRM_LISTRUNGMSL.STTPHIEU", dialog);
        Assert.Contains("form.SelectedStt", dialog);
    }

    [Fact]
    public void DuplicateBusinessKey_MustStillContainBothPartAndQuantityFilters()
    {
        var source = Read("PhieuValidationRepository.source.cs");
        Assert.Contains("WHERE MAHANG = @ma AND SOLUONG = @sl", source);
    }
    [Fact]
    public void CustomerRouting_KeepsYmvNOnItsDedicatedConfiguredProcedureAndTables()
    {
        var config = Read("CustomerTableConfig.source.cs");
        var ymvn = Read("PhieuYmvnService.source.cs");
        var hvnProcedure = Read("Usp_Qrcode_Take_Lot2405.sql");
        var ymvnProcedure = Read("Usp_Qrcode_Take_LotYMVN2405.sql");

        Assert.Contains("DocQRTable = \"YMVN_DOCQRCODE\"", config);
        Assert.Contains("DocQRTableSP = \"SP_DOCQRCODE\"", config);
        Assert.Contains("TmpTableSP = \"SP_TMPPHIEUGIAOHANG\"", config);
        Assert.Contains("_phieuRepo.TakeLotYMVN(", ymvn);
        Assert.Contains("_cfg.Delivery.TmpTable", ymvn);
        Assert.Contains("_cfg.Delivery.DocQRTable", ymvn);
        Assert.Contains("isLoaiSP ? _cfg.Delivery.TmpTableSP : _cfg.Delivery.TmpTable", ymvn);
        Assert.Contains("isLoaiSP ? _cfg.Delivery.DocQRTableSP : _cfg.Delivery.DocQRTable", ymvn);
        Assert.Contains("N'SP_TMPPHIEUGIAOHANG'", ymvnProcedure);
        Assert.Contains("N'SP_DOCQRCODE'", ymvnProcedure);
        Assert.Contains("@ISLOAISP = 1", ymvnProcedure);
        Assert.DoesNotContain("YMVN_DOCQRCODE", hvnProcedure);
        Assert.DoesNotContain("SP_DOCQRCODE", hvnProcedure);
    }

    [Fact]
    public void TakeLotYmvn_CallerParameterNamesMatchProcedureDeclaration()
    {
        // Regression: caller sent @ISLOAIASP while the procedure declares @ISLOAISP,
        // which makes every YMVN Take_Lot call fail with "not a parameter" at runtime.
        var caller = Read("PhieuLotRepository.source.cs");
        var proc = Read("Usp_Qrcode_Take_LotYMVN2405.sql");
        foreach (var name in new[] { "@TMPTABLE", "@DOCQRTABLE", "@ISLOAISP" })
        {
            Assert.Contains(name + " ", proc);
            Assert.Contains("\"" + name + "\"", caller);
        }
        Assert.DoesNotContain("@ISLOAIASP", caller);
    }

    [Fact]
    public void TakeLotYmvn_ClaimsQrByGioAndSttBanInsideOneTransaction()
    {
        var sql = Read("Usp_Qrcode_Take_LotYMVN2405.sql");
        Assert.Contains("SET XACT_ABORT ON", sql);
        Assert.Contains("BEGIN TRANSACTION", sql);
        Assert.Contains("SAVE TRANSACTION PctpTakeLotYmvn2405", sql);
        // Same claim semantics as HVN: STTBAN = GIO = TMP.STT.
        Assert.Contains("d.STTBAN = CASE WHEN q.Owner > 0 THEN q.Owner END", sql);
        Assert.Contains("d.GIO    = CASE WHEN q.Owner > 0 THEN CONVERT(nvarchar(100), q.Owner) END", sql);
        // Optimistic check: a claim that did not touch exactly the expected rows must abort.
        Assert.Contains("@affected <> @expected", sql);
    }

    [Fact]
    public void ScanEngine_RoutesByFieldCountCustomerCapabilitiesAndMpSpMode()
    {
        var source = Read("DocQRScanEngine.source.cs");
        var start = source.IndexOf("public ScanResult ProcessScan(", StringComparison.Ordinal);
        var end = source.IndexOf("private string MapMaHang(", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var route = source.Substring(start, end - start);

        Assert.Contains("parts.Length == 6 && !_cfg.Delivery.CoNhieuNhaMay && !_cfg.Delivery.CoGear", route);
        Assert.Contains("parts.Length == 4 && _cfg.Delivery.CoGear", route);
        Assert.Contains("if (_cfg.Delivery.CoGear)", route);
        Assert.Contains("if (IsBanSP)", route);
        Assert.Contains("return ScanHVN(parts)", route);

        var hvnSp = MethodBody(source, "private ScanResult ScanFCC_SP(", "private ScanResult ScanFCC(");
        Assert.Contains("LotHVN = lotFcc", hvnSp);
        Assert.Contains("MaHangHVN = maHangThuc", hvnSp);
        var hvnMpFcc = MethodBody(source, "private ScanResult ScanFCC(", "public ScanResult ProcessScanYMVN(");
        Assert.Contains("_bus.Publish(new QRScannedEvent(item, \"FCC\"))", hvnMpFcc);
        var hvnCustomer = MethodBody(source, "private ScanResult ScanHVN(", "private ScanResult ScanFCC_YMVN(");
        Assert.Contains("_repo.UpdateHVN(item, DocQRTable)", hvnCustomer);
    }

    [Fact]
    public void CustomerConfig_SeparatesHvnYmvNAndHondaTradingTableFamilies()
    {
        var config = Read("CustomerTableConfig.source.cs");
        Assert.Contains("public const string HondaVietnam = \"100001\"", config);
        Assert.Contains("public const string YamahaVietnam = \"100002\"", config);
        Assert.Contains("public const string Customer100003 = \"100003\"", config);
        Assert.Contains("DocQRTable = \"DOCQRCODE\"", config);
        Assert.Contains("DocQRTableSP = \"DOCQRCODE_SP\"", config);
        Assert.Contains("DocQRTable = \"YMVN_DOCQRCODE\"", config);
        Assert.Contains("DocQRTableSP = \"SP_DOCQRCODE\"", config);
        Assert.Contains("TmpTableSP = \"SP_TMPPHIEUGIAOHANG\"", config);
        Assert.Contains("DocQRTable = \"DOCQRCODE_100003\"", config);
        Assert.Contains("TmpTable = \"TMPPHIEUGIAOHANG_100003\"", config);
        Assert.Contains("CoGear = true", config);
        Assert.Contains("CoNhieuNhaMay = true", config);
        Assert.Contains("LoadTheoNgay = true", config);
        Assert.Contains("OrderTable = \"Purchase_Order_HTN\"", config);
    }

    [Fact]
    public void YmvN_SpRawQrPathCurrentlyBypassesTheSessionDocQrTable()
    {
        // Characterization of a release blocker: raw P/K/Q QR routes to ScanYMVN,
        // but this method reads/writes the configured MP table, not session SP_DOCQRCODE.
        var source = Read("DocQRScanEngine.source.cs");
        var method = MethodBody(source, "private ScanResult ScanYMVN(", "private ScanResult ScanFCC_TongTem(");
        Assert.Contains("_cfg.Delivery.DocQRTable", method);
        Assert.DoesNotContain("GetDocQRTable(IsBanSP)", method);
        Assert.Contains("private string DocQRTable => _session.DocQrTable;", source);
    }

    [Fact]
    public void YmvN_SpTakeLotRequiresLiveSchemaCompatibilityBeforeAcceptance()
    {
        var config = Read("CustomerTableConfig.source.cs");
        var procedure = Read("Usp_Qrcode_Take_LotYMVN2405.sql");
        Assert.Contains("DocQRTableSP = \"SP_DOCQRCODE\"", config);
        Assert.Contains("TmpTableSP = \"SP_TMPPHIEUGIAOHANG\"", config);
        Assert.Contains("IF @TmpOk = 0 OR @LegacyQrOk = 0", procedure);
        Assert.Contains("THROW 51213", procedure);
    }

    private static string MethodBody(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from, "Could not isolate source method: " + start);
        return source.Substring(from, to - from);
    }

    private static string ClaimSection(string sql)
    {
        var i = sql.IndexOf("CLAIM-MODE-BEGIN", StringComparison.Ordinal);
        Assert.True(i >= 0, "CLAIM-MODE-BEGIN marker missing");
        return sql.Substring(i);
    }

    [Fact]
    public void TakeLotYmvn_ClaimMode_NeverUpdatesQrOrLotByPartCodeOnly()
    {
        var claim = ClaimSection(Read("Usp_Qrcode_Take_LotYMVN2405.sql"));
        // Old behaviour: LOT merge and KETQUA update joined only on MAHANGFCC across all phieu.
        Assert.DoesNotContain("#LotTmp", claim);
        Assert.DoesNotContain("WHERE RTRIM(d2.MAHANGFCC) = RTRIM(d.MAHANGFCC)", claim);
        // Rows are identified by the rowversion, because DOCQR.STT is not unique.
        Assert.Contains("INNER JOIN #Qr q ON q.Rid = d.TGLUU", claim);
        Assert.Contains("WHERE q.Owner <> q.OrigOwner", claim);
    }

    [Fact]
    public void TakeLotYmvn_SelectsModePerCustomerTableFamily_WithoutRuntimeDdl()
    {
        var sql = Read("Usp_Qrcode_Take_LotYMVN2405.sql");
        // YMVN_DOCQRCODE has no TGLUU -> legacy mode; SP_* tables have a different schema -> rejected.
        Assert.Contains("DECLARE @ClaimMode bit", sql);
        Assert.Contains("COL_LENGTH(@qrTbl, N'TGLUU')", sql);
        Assert.Contains("LEGACY-MODE-BEGIN", sql);
        Assert.Contains("THROW 51213", sql);
        // TMP.STT is not unique in production (IX_TMP_STT is non-unique).
        Assert.Contains("THROW 51214", sql);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER COLUMN", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TakeLotYmvn_ClaimIsCompatibleWithReleaseAndResultContract()
    {
        var sql = Read("Usp_Qrcode_Take_LotYMVN2405.sql");
        var repo = Read("PhieuLotRepository.source.cs");
        // Release path (LayLaiLotNo) frees QR by STTBAN = @stt, so the claim must write STTBAN = TMP.STT.
        Assert.Contains("WHERE ISNULL(STTBAN, 0) = @stt", repo);
        // Columns read by PhieuYmvnService.HoanThanhYMVN must still be returned.
        foreach (var column in new[] { "TONG_SLHVN", "SL_GIAO", "IsOK", "STATUSDOC", "t.LOT" })
            Assert.Contains(column, sql);
    }
}
