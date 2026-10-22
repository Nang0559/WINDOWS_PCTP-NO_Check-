using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using Xunit;

namespace PCTP.QC.Tests
{
    public sealed class BulkStockAdjustServiceTests
    {
        private const string Lot = "26080700748100851600";

        [Fact]
        public void DeliveryPick_ConsumesVirtualSlotBeforePhysicalSlot()
        {
            var slots = new FakeBulkSlots
            {
                VirtualLots = new List<LotInfo> { new LotInfo { LotNo = Lot, ItemCode = "PART-01", Quantity = 3 } },
                PhysicalLots = new List<BulkSlotLotLocation> { new BulkSlotLotLocation { SlotId = 22, ItemCode = "PART-01", Quantity = 5 } }
            };
            var movement = new FakeMovement();
            var history = new FakeHistory();
            var uow = new FakeUnitOfWork();
            var service = new BulkStockAdjustService(slots, history, uow, movement);

            Assert.True(service.TruKhoAoTheoLot(Lot, 5));

            Assert.Equal(new[] { 3, 2 }, movement.Picks.ConvertAll(x => x.Quantity));
            Assert.Equal(new[] { 100, 22 }, movement.Picks.ConvertAll(x => x.SlotId.Value));
            Assert.Equal(2, history.Entries.Count);
            Assert.Equal(1, uow.BeginCount);
            Assert.Equal(1, uow.CommitCount);
            Assert.Equal(0, uow.RollbackCount);
        }

        [Fact]
        public void DeliveryPick_UsesPhysicalSlotWhenVirtualSlotHasNoMatchingLot()
        {
            var slots = new FakeBulkSlots
            {
                VirtualLots = new List<LotInfo>(),
                PhysicalLots = new List<BulkSlotLotLocation> { new BulkSlotLotLocation { SlotId = 31, ItemCode = "PART-02", Quantity = 4 } }
            };
            var movement = new FakeMovement();
            var service = new BulkStockAdjustService(slots, new FakeHistory(), new FakeUnitOfWork(), movement);

            Assert.True(service.TruKhoAoTheoLot(Lot, 4));
            var pick = Assert.Single(movement.Picks);
            Assert.Equal(31, pick.SlotId);
            Assert.Equal(4, pick.Quantity);
        }

        [Fact]
        public void DeliveryPick_WithManagedTransaction_RecordsShortfallAndCommits()
        {
            var slots = new FakeBulkSlots
            {
                VirtualLots = new List<LotInfo> { new LotInfo { LotNo = Lot, ItemCode = "PART-01", Quantity = 2 } }
            };
            var history = new FakeHistory();
            var uow = new FakeUnitOfWork();
            var service = new BulkStockAdjustService(slots, history, uow, new FakeMovement());

            Assert.True(service.TruKhoAoTheoLot(Lot, 5, manageTransaction: true));

            Assert.Equal(2, history.Entries.Count);
            Assert.Contains(history.Entries, x => x.Action == "EXPORT_SHORTFALL" && x.Quantity == 3);
            Assert.Equal(1, uow.CommitCount);
            Assert.Equal(0, uow.RollbackCount);
        }

        [Fact]
        public void DeliveryPick_WithSharedTransaction_ThrowsOnShortfallForCallerRollback()
        {
            var slots = new FakeBulkSlots
            {
                VirtualLots = new List<LotInfo> { new LotInfo { LotNo = Lot, ItemCode = "PART-01", Quantity = 2 } }
            };
            var uow = new FakeUnitOfWork();
            var service = new BulkStockAdjustService(slots, new FakeHistory(), uow, new FakeMovement());

            var ex = Assert.Throws<InvalidOperationException>(() => service.TruKhoAoTheoLot(Lot, 5, manageTransaction: false));

            Assert.Contains("thiếu 3", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, uow.BeginCount);
            Assert.Equal(0, uow.CommitCount);
            Assert.Equal(0, uow.RollbackCount);
        }

        [Fact]
        public void DeliveryPick_MovementFailureRollsBackOwnedTransaction()
        {
            var slots = new FakeBulkSlots
            {
                VirtualLots = new List<LotInfo> { new LotInfo { LotNo = Lot, ItemCode = "PART-01", Quantity = 2 } }
            };
            var uow = new FakeUnitOfWork();
            var movement = new FakeMovement { FailSlotId = 100 };
            var service = new BulkStockAdjustService(slots, new FakeHistory(), uow, movement);

            Assert.Throws<InvalidOperationException>(() => service.TruKhoAoTheoLot(Lot, 2));

            Assert.Equal(1, uow.BeginCount);
            Assert.Equal(0, uow.CommitCount);
            Assert.Equal(1, uow.RollbackCount);
        }

        [Fact]
        public void DeliveryPick_PartialMovement_ManagedTransactionRecordsActualShortfall()
        {
            var slots = new FakeBulkSlots
            {
                VirtualLots = new List<LotInfo> { new LotInfo { LotNo = Lot, ItemCode = "PART-01", Quantity = 2 } }
            };
            var history = new FakeHistory();
            var uow = new FakeUnitOfWork();
            var movement = new FakeMovement { ConsumedQuantityOverride = 1 };
            var service = new BulkStockAdjustService(slots, history, uow, movement);

            Assert.True(service.TruKhoAoTheoLot(Lot, 2, manageTransaction: true));

            Assert.Contains(history.Entries, x => x.Action == "EXPORT_AUTO_HVN" && x.Quantity == 1);
            Assert.Contains(history.Entries, x => x.Action == "EXPORT_SHORTFALL" && x.Quantity == 1);
            Assert.Equal(1, uow.CommitCount);
            Assert.Equal(0, uow.RollbackCount);
        }

        [Fact]
        public void DeliveryPick_PartialMovement_SharedTransactionThrowsForCallerRollback()
        {
            var slots = new FakeBulkSlots
            {
                VirtualLots = new List<LotInfo> { new LotInfo { LotNo = Lot, ItemCode = "PART-01", Quantity = 2 } }
            };
            var movement = new FakeMovement { ConsumedQuantityOverride = 1 };
            var uow = new FakeUnitOfWork();
            var service = new BulkStockAdjustService(slots, new FakeHistory(), uow, movement);

            Assert.Throws<InvalidOperationException>(() =>
                service.TruKhoAoTheoLot(Lot, 2, manageTransaction: false));

            Assert.Equal(0, uow.BeginCount);
            Assert.Equal(0, uow.CommitCount);
            Assert.Equal(0, uow.RollbackCount);
        }

        [Fact]
        public void DeliveryPick_InvalidRequestDoesNotOpenTransaction()
        {
            var uow = new FakeUnitOfWork();
            var service = new BulkStockAdjustService(new FakeBulkSlots(), new FakeHistory(), uow, new FakeMovement());

            Assert.False(service.TruKhoAoTheoLot(Lot, 0));
            Assert.False(service.TruKhoAoTheoLot(null, 2));
            Assert.Equal(0, uow.BeginCount);
        }

        private sealed class FakeBulkSlots : IBulkStockSlotRepository
        {
            public List<LotInfo> VirtualLots { get; set; } = new List<LotInfo>();
            public List<BulkSlotLotLocation> PhysicalLots { get; set; } = new List<BulkSlotLotLocation>();
            public int GetOrCreateVirtualSlotId(string warehouseName, string rackName, int capacity) => 100;
            public void LockSlotForUpdate(int slotId) { }
            public List<LotInfo> GetLots(int slotId) => VirtualLots;
            public List<BulkSlotLotLocation> FindSlotLotsByLot(string lotNo, int excludeSlotId) => PhysicalLots;
        }

        private sealed class FakeHistory : IStockHistoryRepository
        {
            public List<(string Action, int Quantity)> Entries { get; } = new List<(string, int)>();
            public void SaveHistory(string actionType, string itemCode, LotInfo lot, int? fromSlotId, int? toSlotId, string performedBy)
                => Entries.Add((actionType, lot?.Quantity ?? 0));
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public int BeginCount { get; private set; }
            public int CommitCount { get; private set; }
            public int RollbackCount { get; private set; }
            public void Begin() => BeginCount++;
            public void Commit() => CommitCount++;
            public void Rollback() => RollbackCount++;
            public void Dispose() { }
        }

        private sealed class FakeMovement : IStockMovementService
        {
            public List<StockMovementRequest> Picks { get; } = new List<StockMovementRequest>();
            public int? FailSlotId { get; set; }
            public int? ConsumedQuantityOverride { get; set; }
            public StockMovementResult Receive(StockMovementRequest request) => StockMovementResult.Fail("unused");
            public StockMovementResult Reserve(StockMovementRequest request) => StockMovementResult.Fail("unused");
            public StockMovementResult Export(StockMovementRequest request) => StockMovementResult.Fail("unused");
            public StockMovementResult Move(StockMovementRequest request) => StockMovementResult.Fail("unused");
            public StockMovementResult ReturnFromRework(StockMovementRequest request) => StockMovementResult.Fail("unused");
            public StockMovementResult Correct(StockMovementRequest request) => StockMovementResult.Fail("unused");
            public StockMovementResult RestorePicked(int slotId, StockSlotLot lot) => StockMovementResult.Fail("unused");
            public StockMovementResult Pick(StockMovementRequest request)
            {
                Picks.Add(request);
                if (FailSlotId == request.SlotId)
                    return StockMovementResult.Fail("injected pick failure");
                return StockMovementResult.OkWithConsumedLots(new List<StockSlotLot>
                {
                    new StockSlotLot { LotNo = request.LotNo, ItemCode = request.ItemCode, Quantity = ConsumedQuantityOverride ?? request.Quantity }
                });
            }
        }
    }
}

namespace PCTP.Modules.KhoVatLy.Kho.Models
{
    public sealed class LotInfo
    {
        public string ItemCode { get; set; }
        public string LotNo { get; set; }
        public int Quantity { get; set; }
        public string TemCode { get; set; }
        public string RawQr { get; set; }
    }
}

namespace PCTP.Shared.Models
{
    public static class BulkImportConfig
    {
        public const string WarehouseName = "TEST_BULK_WAREHOUSE";
        public const string RackName = "TEST_BULK_RACK";
        public const int Capacity = 1000;
    }
}

namespace PCTP.VIEWSTOCK.Models
{
    public sealed class TestNamespaceMarker { }
}

namespace PCTP.Common
{
    public sealed class TestNamespaceMarker { }
}
