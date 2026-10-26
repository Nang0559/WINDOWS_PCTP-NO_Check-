using System;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Services;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class FifoSessionStateTests
{
    private static FifoSessionState Create(int need = 10)
    {
        var state = new FifoSessionState();
        state.Initialize("PART01", need, new[]
        {
            new FifoStockLine { LotKey = "PART01A", DisplayLot = "PART01A", AvailableQty = 5, FifoRank = 0 },
            new FifoStockLine { LotKey = "PART01B", DisplayLot = "PART01B", AvailableQty = 5, FifoRank = 1 },
            new FifoStockLine { LotKey = "PART01C", DisplayLot = "PART01C", AvailableQty = 5, FifoRank = 2 }
        });
        return state;
    }

    [Fact]
    public void OldestLotMustBeConsumedFirst()
    {
        var state = Create();
        Assert.False(state.TryConsume(1, "PART01", "PART01B-5", 5, out var message));
        Assert.Contains("FIFO", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(state.TryConsume(2, "PART01", "PART01A-5", 5, out _));
        Assert.True(state.TryConsume(3, "PART01", "PART01B-5", 5, out _));
    }

    [Fact]
    public void CompoundLotMustMatchQrQuantity()
    {
        var state = Create();
        Assert.False(state.TryConsume(1, "PART01", "PART01A-3,PART01B-2", 6, out _));
        Assert.True(state.TryConsume(2, "PART01", "PART01A-5", 5, out _));
    }

    [Fact]
    public void MalformedCompoundLotMustNotReserveStock()
    {
        var state = Create();
        Assert.False(state.TryConsume(1, "PART01", "PART01A-3,,PART01B-2", 5, out _));
        Assert.True(state.TryConsume(2, "PART01", "PART01A-5", 5, out _));
    }

    [Fact]
    public void RescanSameSequenceReplacesPreviousReservation()
    {
        var state = Create(5);
        Assert.True(state.TryConsume(7, "PART01", "PART01A-2", 2, out _));
        Assert.True(state.TryConsume(7, "PART01", "PART01A-5", 5, out _));
        Assert.Equal(0, state.GetRamSnapshot().Single().RemainingAllowedQty);
    }

    [Fact]
    public void ReleaseRestoresReservedQuantity()
    {
        var state = Create(5);
        Assert.True(state.TryConsume(1, "PART01", "PART01A-5", 5, out _));
        state.Release(1);
        Assert.Equal(5, state.GetRamSnapshot().Single().RemainingAllowedQty);
    }

    [Fact]
    public void ResetClearsSessionState()
    {
        var state = Create(5);
        Assert.True(state.TryConsume(1, "PART01", "PART01A-5", 5, out _));
        state.Reset();
        Assert.Empty(state.GetRamSnapshot());
    }
}