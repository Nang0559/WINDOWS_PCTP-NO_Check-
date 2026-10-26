using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class TestMatrixIntegrityTests
{
    [Fact]
    public void EveryImplementedScenarioMustDeclareExecutableTestEvidence()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestMatrix.json");
        Assert.True(File.Exists(path), $"Test matrix was not copied to test output: {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var modules = document.RootElement.GetProperty("modules").EnumerateArray().ToArray();
        Assert.NotEmpty(modules);

        var implemented = modules
            .Where(x => string.Equals(x.GetProperty("status").GetString(), "implemented", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(implemented);
        foreach (var scenario in implemented)
        {
            var id = scenario.GetProperty("id").GetString();
            Assert.True(scenario.TryGetProperty("testEvidence", out var evidence),
                $"Scenario {id} is marked implemented but has no testEvidence.");
            Assert.False(string.IsNullOrWhiteSpace(evidence.GetString()),
                $"Scenario {id} has empty testEvidence.");
            Assert.DoesNotContain("Evidence missing", evidence.GetString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ImplementedScenarioMustNotAdmitAnOpenRuntimeGapInItsOwnEvidence()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestMatrix.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var gapPhrases = new[]
        {
            "remains a runtime acceptance gate", "remains unexecuted",
            "do not yet invoke", "has not yet been confirmed"
        };
        foreach (var scenario in document.RootElement.GetProperty("modules").EnumerateArray())
        {
            if (!string.Equals(scenario.GetProperty("status").GetString(), "implemented", StringComparison.OrdinalIgnoreCase)) continue;
            var evidence = scenario.GetProperty("testEvidence").GetString() ?? string.Empty;
            foreach (var phrase in gapPhrases)
                Assert.False(evidence.Contains(phrase, StringComparison.OrdinalIgnoreCase),
                    $"Scenario {scenario.GetProperty("id").GetString()} is 'implemented' but its evidence says \"{phrase}\". Mark it 'partial'.");
        }
    }

    [Fact]
    public void ScenarioIdsMustBeUniqueAndStatusesMustBeRecognized()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestMatrix.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var modules = document.RootElement.GetProperty("modules").EnumerateArray().ToArray();
        var ids = modules.Select(x => x.GetProperty("id").GetString()).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var scenario in modules)
        {
            var status = scenario.GetProperty("status").GetString();
            Assert.Contains(status, new[] { "implemented", "partial", "planned", "blocked" });
        }
    }

    [Fact]
    public void SafetyPolicyMustKeepProductionDatabaseAndMutationDisabled()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestMatrix.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var safety = document.RootElement.GetProperty("safety");

        Assert.False(safety.GetProperty("productionDatabaseAccess").GetBoolean());
        Assert.False(safety.GetProperty("mutatingProductionData").GetBoolean());
        Assert.False(safety.GetProperty("sqlScriptsExecutedByDefault").GetBoolean());
    }
}
