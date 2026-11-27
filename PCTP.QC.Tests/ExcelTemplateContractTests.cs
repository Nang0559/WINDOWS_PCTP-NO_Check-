using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PCTP.Modules.KhoLinhKien;
using PCTP.Modules.MesPlanning;
using PCTP.Shared.Helpers;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// File Excel mẫu phải luôn khớp reader: nếu ai đổi cột bắt buộc của <c>ComponentPoExcelReader</c> /
/// <c>ProductionPlanExcelReader</c> mà quên cập nhật template, test này đỏ. Nút tải template cũng phải còn nối vào các màn hình upload.
/// </summary>
public sealed class ExcelTemplateContractTests
{
    private static string Read(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));

    private static string[] ReaderNames(string source, string pattern)
    {
        Match m = Regex.Match(source, pattern, RegexOptions.Singleline);
        Assert.True(m.Success, "Không tìm thấy khai báo cột trong reader: " + pattern);
        return Regex.Matches(m.Groups["list"].Value, "\"(?<n>[A-Za-z]+)\"").Cast<Match>().Select(x => x.Groups["n"].Value).ToArray();
    }

    [Fact]
    public void Po_template_required_columns_equal_reader_required_columns()
    {
        string[] readerRequired = ReaderNames(Read("ComponentPoStatus.source.cs"),
            @"class ComponentPoExcelReader.*?Required\s*=\s*\{(?<list>[^}]*)\}");
        var def = ComponentPoTemplateDefinition.Create();
        Assert.Equal(readerRequired.OrderBy(x => x), def.RequiredColumnNames.OrderBy(x => x));
    }

    [Fact]
    public void Po_template_has_every_optional_column_the_reader_uses()
    {
        var names = ComponentPoTemplateDefinition.Create().Columns.Select(c => c.Name).ToList();
        foreach (string n in new[] { "PartDescription", "UnitCode", "NeedByDate", "Remarks" })
            Assert.Contains(n, names);
        Assert.Equal("PO", ComponentPoTemplateDefinition.Create().DataSheetName);
    }

    [Fact]
    public void Plan_template_required_columns_equal_reader_required_columns()
    {
        string[] readerRequired = ReaderNames(Read("ProductionPlanExcelReader.source.cs"),
            @"string\[\]\s*required\s*=\s*\{(?<list>[^}]*)\}");
        var def = ProductionPlanTemplateDefinition.Create();
        Assert.Equal(readerRequired.OrderBy(x => x), def.RequiredColumnNames.OrderBy(x => x));
    }

    [Fact]
    public void Plan_template_has_optional_planned_times_and_plan_sheet_name()
    {
        var def = ProductionPlanTemplateDefinition.Create();
        var names = def.Columns.Select(c => c.Name).ToList();
        Assert.Contains("PlannedStart", names);
        Assert.Contains("PlannedEnd", names);
        Assert.Equal("PLAN", def.DataSheetName);
    }

    [Fact]
    public void Template_column_names_are_unique_and_match_header_trim_rule()
    {
        foreach (var def in new[] { ComponentPoTemplateDefinition.Create(), ProductionPlanTemplateDefinition.Create() })
        {
            var names = def.Columns.Select(c => c.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(names, n => Assert.Equal(n.Trim(), n));
        }
    }

    [Fact]
    public void Template_buttons_are_wired_into_every_upload_screen()
    {
        Assert.Contains("ComponentPoTemplateAction.Create()", Read("Main_APP.Menu.internal-transfer.source.cs"));
        Assert.Contains("ProductionPlanTemplateAction.Create()", Read("ProductionScreenActions.source.cs"));
        Assert.Contains("ComponentPoTemplateDefinition.Create()", Read("FormKhoLinhKien.source.cs"));
        Assert.Contains("ProductionPlanTemplateDefinition.Create()", Read("FormUploadKeHoachSanXuat.source.cs"));
        Assert.Contains("ComponentPoTemplateDefinition.Create()", Read("ComponentPoUploadAction.source.cs"));
    }
}
