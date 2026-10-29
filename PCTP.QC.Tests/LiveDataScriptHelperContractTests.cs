using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// The SQL14 profile once failed on the runner with "The term 'ConvertTo-PctpBracket' is not recognized" because the script
/// called helpers that no file defined. These tests catch that class of error at unit-test time: every *-Pctp* helper
/// used by the live-data scripts must be defined by one of them.
/// </summary>
public sealed class LiveDataScriptHelperContractTests
{
    private static readonly string[] Scripts =
    {
        "Invoke-PctpLiveDataInventory.source.ps1",
        "Invoke-PctpLiveDataProfile.source.ps1",
        "PctpLiveData.Common.source.ps1",
    };

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));

    [Fact]
    public void EveryPctpHelperUsedByLiveDataScripts_IsDefined()
    {
        var all = Scripts.ToDictionary(s => s, Read);
        var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in all.Values)
            foreach (Match m in Regex.Matches(text, @"(?im)^\s*function\s+([A-Za-z]+-Pctp[A-Za-z0-9]+)"))
                defined.Add(m.Groups[1].Value);

        // Names followed by ".ps1" are script file names (docs/usage text), not helper calls.
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in all.Values)
            foreach (Match m in Regex.Matches(text, @"\b((?:ConvertTo|Get|Invoke|Assert|New|Read|Test|Start)-Pctp[A-Za-z0-9]+)\b(?!\.ps1)"))
                used.Add(m.Groups[1].Value);
        used.RemoveWhere(n => n.StartsWith("Invoke-PctpLiveData", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Invoke-PctpMp", StringComparison.OrdinalIgnoreCase));

        var missing = used.Where(n => !defined.Contains(n)).OrderBy(n => n).ToList();
        Assert.True(missing.Count == 0, "Live-data scripts call undefined helper(s): " + string.Join(", ", missing));
    }

    [Fact]
    public void ProfileHelpers_BracketQuotingAndBatching_AreDefinedInCommon()
    {
        var common = Read("PctpLiveData.Common.source.ps1");
        Assert.Contains("function ConvertTo-PctpBracket", common, StringComparison.Ordinal);
        Assert.Contains("function Get-PctpInBatches", common, StringComparison.Ordinal);
        Assert.Contains(".Replace(']', ']]')", common, StringComparison.Ordinal);
    }
}
