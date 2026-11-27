using PCTP.Modules.KhoLinhKien;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Màn hình layout đọc Status dạng chữ (DRAFT...) từ view; nút workflow cần mã số để hỏi bảng workflow.</summary>
public sealed class ComponentPoStatusCodesTests
{
    [Theory]
    [InlineData("DRAFT", 0)]
    [InlineData("validated", 10)]
    [InlineData(" RELEASED ", 20)]
    [InlineData("PARTIALLY_RECEIVED", 30)]
    [InlineData("RECEIVED", 40)]
    [InlineData("CLOSED", 50)]
    [InlineData("CANCELLED", 900)]
    public void Status_name_maps_to_workflow_code(string name, int expected)
    {
        Assert.True(ComponentPoStatus.TryFromName(name, out int code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("UNKNOWN")]
    public void Unknown_status_name_is_rejected(string name)
    {
        Assert.False(ComponentPoStatus.TryFromName(name, out _));
    }

    [Fact]
    public void Cancel_and_early_close_need_a_reason_other_steps_do_not()
    {
        Assert.True(ComponentPoStatus.NeedsReason(ComponentPoStatus.Draft, ComponentPoStatus.Cancelled));
        Assert.True(ComponentPoStatus.NeedsReason(ComponentPoStatus.PartiallyReceived, ComponentPoStatus.Closed));
        Assert.False(ComponentPoStatus.NeedsReason(ComponentPoStatus.Received, ComponentPoStatus.Closed));
        Assert.False(ComponentPoStatus.NeedsReason(ComponentPoStatus.Draft, ComponentPoStatus.Validated));
        Assert.False(ComponentPoStatus.NeedsReason(ComponentPoStatus.Validated, ComponentPoStatus.Released));
    }
}
