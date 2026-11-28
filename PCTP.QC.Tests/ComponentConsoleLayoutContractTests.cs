using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho bố cục màn Quản lý linh kiện MES: không để điều khiển bị khuất, sơ đồ phủ đều chiều rộng, chữ không bị cắt.</summary>
public sealed class ComponentConsoleLayoutContractTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));

    [Fact]
    public void Context_panel_gives_the_action_buttons_a_fixed_column_wide_enough_for_three_buttons()
    {
        var form = Read("FormKhoLinhKien.source.cs");
        var panel = form.Substring(form.IndexOf("private Control BuildContextPanel()", StringComparison.Ordinal));
        panel = panel.Substring(0, panel.IndexOf("private void RefreshContextData()", StringComparison.Ordinal));
        Assert.Contains("new ColumnStyle(SizeType.Absolute, 400)", panel);
        Assert.Contains("grid.Controls.Add(actions, 8, 1)", panel);
        Assert.Contains("THAO TÁC NHANH (ACTIVE CONTEXT)", panel);
    }

    [Fact]
    public void Flow_canvas_stretches_horizontally_and_fits_text_to_its_box()
    {
        var canvas = Read("MesFlowCanvasControl.source.cs");
        Assert.Contains("MaxStretch", canvas);
        Assert.Contains("_stretch", canvas);
        Assert.Contains("FitFont", canvas);
        Assert.Contains("StringFormatFlags.NoWrap", canvas);
        Assert.Contains("Sx(node.Rect).Contains(p)", canvas); // hit-test follows the stretched geometry
    }

    [Fact]
    public void Header_runtime_badge_is_measured_with_the_format_used_to_draw_it()
    {
        var chrome = Read("FvnMesConsoleChromeControls.source.cs");
        var header = chrome.Substring(0, chrome.IndexOf("class FvnConsoleStatusBarControl", StringComparison.Ordinal));
        Assert.DoesNotContain("g.MeasureString(_runtime, f, 800, StringFormat.GenericTypographic)", header);
        Assert.Contains("StringFormat.GenericDefault", header);
    }
}
