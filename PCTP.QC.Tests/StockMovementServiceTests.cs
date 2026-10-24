using System;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoCore.Application.Services;
using PCTP.Shared.Common;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class StockMovementServiceTests
{
    [Fact]
    public void Receive_RejectsNullAndInvalidQuantity()
    {
        var slots = new FakeSlots();
        var service = Create(slots);

        Assert.False(service.Receive(null).Success);
        Assert.False(service.Receive(new StockMovementRequest { TargetSlotId = 1, ItemCode = "A", Quantity = 0 }).Success);
        Assert.Empty(slots.AddedLots);
    }

    [Fact]
    public void NormalReceive_WritesOneSlotLotAndReceivingRecord_WithoutDoubleAdjustingBalance()
    {
        var slots = new FakeSlots();
        var balance = new FakeBalance();
        var receiving = new FakeReceiving();
        var service = new StockMovementService(balance, slots, receiving);

        var result = service.Receive(new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.Receive,
            TargetSlotId = 7, LotNo = "LOT-00000001", ItemCode = "PART-01", Quantity = 12
        });

        Assert.True(result.Success);
        Assert.Single(slots.AddedLots);
        Assert.Equal(12, receiving.Inserted.ReceivedQuantity);
        Assert.Equal(0, balance.AdjustCalls);
        Assert.Equal((byte)0, slots.AddedLots[0].PhieuStatus);
    }

    [Fact]
    public void Receive_RejectsRepeatedReferenceBeforeSecondMutation()
    {
        var slots = new FakeSlots();
        var service = Create(slots);
        var request = new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.Receive,
            TargetSlotId = 7, LotNo = "LOT-REF-01", ItemCode = "PART-01", Quantity = 4,
            ReferenceType = "RECEIVING", ReferenceId = "RCV-100"
        };

        Assert.True(service.Receive(request).Success);
        var replay = service.Receive(request);

        Assert.False(replay.Success);
        Assert.True(replay.Duplicate);
        Assert.Single(slots.AddedLots);
    }

    [Fact]
    public async System.Threading.Tasks.Task Receive_ConcurrentSameReferenceOnlyMutatesOnce()
    {
        var slots = new FakeSlots();
        var service = Create(slots);
        StockMovementRequest NewRequest() => new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.Receive,
            TargetSlotId = 7, LotNo = "LOT-REF-02", ItemCode = "PART-01", Quantity = 4,
            ReferenceType = "RECEIVING", ReferenceId = "RCV-CONCURRENT-100"
        };

        var results = await System.Threading.Tasks.Task.WhenAll(
            System.Threading.Tasks.Task.Run(() => service.Receive(NewRequest())),
            System.Threading.Tasks.Task.Run(() => service.Receive(NewRequest())));

        Assert.Single(results, x => x.Success);
        Assert.Single(results, x => x.Duplicate);
        Assert.Single(slots.AddedLots);
    }

    [Fact]
    public void Export_RejectsRepeatedReferenceWithoutSecondDeduction()
    {
        var slots = new FakeSlots { Status = 0, LotQuantity = 10, LotNo = "26080700748100851600", ItemCode = "PART-01" };
        var balance = new FakeBalance();
        var service = Create(slots, balance);
        var request = new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.Export,
            SlotLotId = 11, LotNo = "26080700748100851600", ItemCode = "PART-01", Quantity = 2,
            ReferenceType = "DELIVERY", ReferenceId = "DELIVERY-100"
        };

        Assert.True(service.Export(request).Success);
        var replay = service.Export(request);

        Assert.True(replay.Duplicate);
        Assert.Equal(1, balance.TryDecreaseCalls);
        Assert.Equal(1, slots.DecreaseCalls);
    }

    [Fact]
    public void Pick_BySlotAndLot_ClaimsReferenceBeforeMutatingAndRejectsReplay()
    {
        var slots = new FakeSlots { LotQuantity = 10, LotNo = "LOT-PICK-01", ItemCode = "PART-01" };
        var service = Create(slots);
        var request = new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.Pick,
            SlotId = 7,
            LotNo = "LOT-PICK-01",
            ItemCode = "PART-01",
            Quantity = 4,
            ReferenceType = "DELIVERY",
            ReferenceId = "DELIVERY-PICK-100"
        };

        var first = service.Pick(request);
        var replay = service.Pick(request);

        Assert.True(first.Success);
        Assert.True(replay.Duplicate);
        Assert.Equal(1, slots.RegisterMovementCalls);
        Assert.Equal(1, slots.TakeLotCalls);
    }

    [Fact]
    public void Receive_RejectsRepeatedQrEvenWhenReferenceDiffers()
    {
        var slots = new FakeSlots();
        var service = Create(slots);
        var first = service.Receive(new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.Receive, TargetSlotId = 7,
            LotNo = "LOT-QR-01", ItemCode = "PART-01", Quantity = 4,
            ReferenceType = "RECEIVING", ReferenceId = "RCV-QR-1", QrData = "QR-UNIQUE-001"
        });
        var replay = service.Receive(new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.Receive, TargetSlotId = 7,
            LotNo = "LOT-QR-02", ItemCode = "PART-01", Quantity = 4,
            ReferenceType = "RECEIVING", ReferenceId = "RCV-QR-2", QrData = "QR-UNIQUE-001"
        });

        Assert.True(first.Success);
        Assert.True(replay.Duplicate);
        Assert.Single(slots.AddedLots);
    }

    [Fact]
    public void ReworkNgReceive_CreatesQuarantineLotWithoutIncreasingAvailableBalance()
    {
        var slots = new FakeSlots();
        var balance = new FakeBalance();
        var service = Create(slots, balance);

        var result = service.Receive(new StockMovementRequest
        {
            MovementType = StockMovementRequest.Types.ReworkNgReceive,
            TargetSlotId = 3, LotNo = "LOT-NG-00000001", ItemCode = "PART-01", Quantity = 4
        });

        Assert.True(result.Success);
        Assert.Single(slots.AddedLots);
        Assert.Equal((byte)3, slots.AddedLots[0].PhieuStatus);
        Assert.Equal(0, balance.AdjustCalls);
    }

    [Fact]
    public void Export_RejectsQuarantinedLotWithoutChangingAnyQuantity()
    {
        var slots = new FakeSlots { Status = 3, LotQuantity = 10, LotNo = "LOT-00000001", ItemCode = "PART-01" };
        var balance = new FakeBalance();
        var service = Create(slots, balance);

        var result = service.Export(new StockMovementRequest
        {
            SlotLotId = 11, LotNo = "LOT-00000001", ItemCode = "PART-01", Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(0, balance.TryDecreaseCalls);
        Assert.Equal(0, slots.DecreaseCalls);
    }

    [Fact]
    public void Export_InsufficientSlotQuantity_DoesNotDecreaseAvailableBalance()
    {
        var slots = new FakeSlots { Status = 0, LotQuantity = 2, LotNo = "LOT-00000001", ItemCode = "PART-01" };
        var balance = new FakeBalance();
        var service = Create(slots, balance);

        var result = service.Export(new StockMovementRequest
        {
            SlotLotId = 11, LotNo = "LOT-00000001", ItemCode = "PART-01", Quantity = 3
        });

        Assert.False(result.Success);
        Assert.Equal(0, balance.TryDecreaseCalls);
        Assert.Equal(0, slots.DecreaseCalls);
    }

    [Fact]
    public void Export_WhenBothBalancesAreAvailable_DecreasesStockTpAndSlotLotOnce()
    {
        var lotNo = "26080700748100851600";
        var slots = new FakeSlots { Status = 0, LotQuantity = 10, LotNo = lotNo, ItemCode = "PART-01" };
        var balance = new FakeBalance();
        var service = Create(slots, balance);

        var result = service.Export(new StockMovementRequest
        {
            SlotLotId = 11, LotNo = lotNo, ItemCode = "PART-01", Quantity = 2
        });

        Assert.True(result.Success);
        Assert.Equal(1, balance.TryDecreaseCalls);
        Assert.Equal(1, slots.DecreaseCalls);
    }

    [Fact]
    public void Export_WhenBalanceReservationFails_DoesNotDecreaseSlotLot()
    {
        // Use a production-shaped 20-character LOT key so the identity guard
        // passes and the test reaches the STOCKTP reservation failure branch.
        var slots = new FakeSlots { Status = 0, LotQuantity = 10, LotNo = "26080700748100851600", ItemCode = "PART-01" };
        var balance = new FakeBalance { AllowDecrease = false };
        var service = Create(slots, balance);

        var result = service.Export(new StockMovementRequest
        {
            SlotLotId = 11, LotNo = "26080700748100851600", ItemCode = "PART-01", Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Equal(1, balance.TryDecreaseCalls);
        Assert.Equal(0, slots.DecreaseCalls);
    }

    [Fact]
    public void Export_RejectsMismatchedLotIdentity()
    {
        var slots = new FakeSlots { Status = 0, LotQuantity = 10, LotNo = "26080700748100851600", ItemCode = "PART-01" };
        var balance = new FakeBalance();
        var service = Create(slots, balance);

        var result = service.Export(new StockMovementRequest
        {
            SlotLotId = 11, LotNo = "26080700748100851699", ItemCode = "PART-01", Quantity = 1
        });

        Assert.False(result.Success);
        Assert.Equal(0, balance.TryDecreaseCalls);
        Assert.Equal(0, slots.DecreaseCalls);
    }

    [Fact]
    public void Move_PartialQuantityDoesNotDuplicateQrOrTemCode()
    {
        var slots = new FakeSlots
        {
            Status = 0, LotQuantity = 10,
            Details = new StockSlotLot { LotNo = "LOT-00000001", ItemCode = "PART-01", Quantity = 10, TemCode = "TEM-01", RawQr = "QR-01" }
        };
        var service = Create(slots);

        var result = service.Move(new StockMovementRequest { SlotLotId = 11, TargetSlotId = 12, Quantity = 4 });

        Assert.True(result.Success);
        Assert.Equal(4, slots.AddedLots[0].Quantity);
        Assert.Null(slots.AddedLots[0].TemCode);
        Assert.Null(slots.AddedLots[0].RawQr);
        Assert.Equal(1, slots.DecreaseCalls);
    }

    [Fact]
    public void Move_FullLotPreservesQrAndTemMetadata()
    {
        var slots = new FakeSlots
        {
            Status = 0, LotQuantity = 10,
            Details = new StockSlotLot { LotNo = "LOT-00000001", ItemCode = "PART-01", Quantity = 10, TemCode = "TEM-01", RawQr = "QR-01" }
        };
        var service = Create(slots);

        var result = service.Move(new StockMovementRequest { SlotLotId = 11, TargetSlotId = 12, Quantity = 10 });

        Assert.True(result.Success);
        Assert.Equal("TEM-01", slots.AddedLots[0].TemCode);
        Assert.Equal("QR-01", slots.AddedLots[0].RawQr);
    }

    [Fact]
    public void Move_RejectsQuarantinedLotBeforeMutation()
    {
        var slots = new FakeSlots { Status = 3, LotQuantity = 10 };
        var service = Create(slots);

        var result = service.Move(new StockMovementRequest { SlotLotId = 11, TargetSlotId = 12, Quantity = 2 });

        Assert.False(result.Success);
        Assert.Equal(0, slots.DecreaseCalls);
        Assert.Empty(slots.AddedLots);
    }

    [Fact]
    public void RestorePicked_RestoresSlotLotWithoutIncreasingAvailableBalance()
    {
        var slots = new FakeSlots();
        var balance = new FakeBalance();
        var service = Create(slots, balance);

        var result = service.RestorePicked(9, new StockSlotLot
        {
            LotNo = "LOT-00000001", ItemCode = "PART-01", Quantity = 3, TemCode = "TEM-01", RawQr = "QR-01"
        });

        Assert.True(result.Success);
        Assert.Single(slots.AddedLots);
        Assert.Equal(0, balance.AdjustCalls);
    }

    [Fact]
    public void Correct_RequiresUnitOfWorkBeforeTouchingStock()
    {
        var slots = new FakeSlots();
        var balance = new FakeBalance();
        var service = new StockMovementService(balance, slots);

        var result = service.Correct(new StockMovementRequest
        {
            SlotId = 1, LotNo = "LOT-00000001", ItemCode = "PART-01", Quantity = 2
        });

        Assert.False(result.Success);
        Assert.Empty(slots.AddedLots);
        Assert.Equal(0, balance.AdjustCalls);
    }

    private static StockMovementService Create(FakeSlots slots, FakeBalance balance = null)
        => new StockMovementService(balance ?? new FakeBalance(), slots);

    private sealed class FakeBalance : IStockBalanceRepository
    {
        public bool AllowDecrease { get; set; } = true;
        public int AdjustCalls { get; private set; }
        public int TryDecreaseCalls { get; private set; }
        public int GetAvailableQuantity(string lotNo) => 0;
        public void DecreaseAvailableQuantity(string lotNo, int quantity) { }
        public void AdjustAvailableQuantity(string lotNo, int delta) => AdjustCalls++;
        public bool TryDecreaseAvailableQuantity(string lotNo, int quantity)
        {
            TryDecreaseCalls++;
            return AllowDecrease;
        }
    }

    private sealed class FakeSlots : IStockSlotRepository
    {
        public byte Status { get; set; }
        public int LotQuantity { get; set; }
        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public StockSlotLot Details { get; set; }
        public int DecreaseCalls { get; private set; }
        public int RegisterMovementCalls { get; private set; }
        public int TakeLotCalls { get; private set; }
        public System.Collections.Generic.List<StockSlotLot> AddedLots { get; } = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _claims =
            new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        public int GetLotQuantity(int slotLotId) => LotQuantity;
        public byte GetPhieuStatus(int slotLotId) => Status;
        public StockSlotLot GetLotDetails(int slotLotId) => Details ?? new StockSlotLot { LotNo = LotNo, ItemCode = ItemCode, Quantity = LotQuantity };
        public string GetLotNo(int slotLotId) => LotNo;
        public string GetItemCode(int slotLotId) => ItemCode;
        public int? GetSlotId(int slotLotId) => 1;
        public void LockSlotForUpdate(int slotId) { }
        public void DecreaseLotQuantity(int slotLotId, int quantity) => DecreaseCalls++;
        public void AddQuantity(int slotId, int quantity, string itemCode) { }
        public void AddLot(int slotId, StockSlotLot lot) => AddedLots.Add(lot);
        public bool TryRegisterMovement(string referenceType, string referenceId, string qrData, string temCode, bool checkExistingQr)
        {
            RegisterMovementCalls++;
            if (!string.IsNullOrWhiteSpace(referenceType) || !string.IsNullOrWhiteSpace(referenceId))
            {
                if (string.IsNullOrWhiteSpace(referenceType) || string.IsNullOrWhiteSpace(referenceId)) return false;
                if (!_claims.TryAdd("REF:" + referenceType.Trim() + ":" + referenceId.Trim(), 0)) return false;
            }
            if (!string.IsNullOrWhiteSpace(qrData) && !_claims.TryAdd("QR:" + qrData.Trim(), 0)) return false;
            if (!string.IsNullOrWhiteSpace(temCode) && !_claims.TryAdd("TEM:" + temCode.Trim(), 0)) return false;
            return true;
        }
        public StockSlotTakeResult TakeLot(int slotId, string lotNo, string itemCode, int quantity)
        {
            TakeLotCalls++;
            return new StockSlotTakeResult { Quantity = quantity };
        }
    }

    private sealed class FakeReceiving : IStockReceivingRepository
    {
        public StockReceivingRecord Inserted { get; private set; }
        public bool Exists(string lotNo) => false;
        public int GetReceivedQuantity(string lotNo) => 0;
        public void Insert(StockReceivingRecord record) => Inserted = record;
        public void Update(string lotNo, int receivedQuantityDelta, int status) { }
    }
}
