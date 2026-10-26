using System;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;
using Xunit;

namespace PCTP.QC.Tests
{
    public sealed class DeliveryStockCommandContractTests
    {
        [Fact]
        public void Command_RequiresDurableBusinessKey()
        {
            Assert.Throws<ArgumentException>(() => new DeliveryStockCommand(
                " ", "HVN", "PGH-1", "operator", DateTime.UtcNow,
                new[] { Part("PART-A", 2) }, new[] { 1 }));
        }

        [Fact]
        public void PartOwnsItsLotAllocations_AndQuantityIsTheirSum()
        {
            var part = new DeliveryPartAllocation(
                "PART-A", "MODEL-X", "GEAR-1",
                new[]
                {
                    new DeliveryLotAllocation("LOT-1", "LOT-1", 101, 3, 0),
                    new DeliveryLotAllocation("LOT-2", "LOT-2", 102, 2, 1)
                },
                new[] { "QR-1", "QR-2" });

            var command = new DeliveryStockCommand(
                "HVN:PGH-1:20261026:0800", "HVN", "PGH-1", "operator",
                DateTime.UtcNow, new[] { part }, new[] { 10, 11 });

            Assert.Equal(5, part.Quantity);
            Assert.Equal(5, command.TotalQuantity);
            Assert.Equal(2, part.Lots.Count);
            Assert.Equal(2, part.QrKeys.Count);
        }

        [Fact]
        public void Command_RejectsDuplicatePartRowsInsteadOfLosingAllocationOwnership()
        {
            Assert.Throws<ArgumentException>(() => new DeliveryStockCommand(
                "HVN:PGH-2", "HVN", "PGH-2", "operator", DateTime.UtcNow,
                new[] { Part("PART-A", 1), Part("part-a", 2) }, new[] { 1 }));
        }

        [Fact]
        public void LotAllocation_RejectsNonPositiveQuantityAndInvalidSlotLot()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DeliveryLotAllocation("LOT-1", "LOT-1", 0, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DeliveryLotAllocation("LOT-1", "LOT-1", null, 0, 0));
        }

        [Fact]
        public void Command_RequiresDistinctPositivePendingStt()
        {
            var parts = new[] { Part("PART-A", 1) };
            Assert.Throws<ArgumentException>(() => Build(parts, new int[0]));
            Assert.Throws<ArgumentException>(() => Build(parts, new[] { 0 }));
            Assert.Throws<ArgumentException>(() => Build(parts, new[] { 5, 5 }));
            Assert.Equal(new[] { 5, 6 }, Build(parts, new[] { 5, 6 }).PendingStt.ToArray());
        }

        [Fact]
        public void Part_RejectsDuplicateLotKeyAndDuplicateFifoSequence()
        {
            Assert.Throws<ArgumentException>(() => new DeliveryPartAllocation("PART-A", null, null,
                new[]
                {
                    new DeliveryLotAllocation("LOT-1", "LOT-1", null, 1, 0),
                    new DeliveryLotAllocation("lot-1", "LOT-1", null, 1, 1)
                }, Enumerable.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new DeliveryPartAllocation("PART-A", null, null,
                new[]
                {
                    new DeliveryLotAllocation("LOT-1", "LOT-1", null, 1, 0),
                    new DeliveryLotAllocation("LOT-2", "LOT-2", null, 1, 0)
                }, Enumerable.Empty<string>()));
        }

        [Fact]
        public void Part_ExposesLotsInFifoOrderRegardlessOfInputOrder()
        {
            var part = new DeliveryPartAllocation("PART-A", null, null,
                new[]
                {
                    new DeliveryLotAllocation("LOT-2", "LOT-2", null, 1, 1),
                    new DeliveryLotAllocation("LOT-1", "LOT-1", null, 1, 0)
                }, Enumerable.Empty<string>());
            Assert.Equal(new[] { "LOT-1", "LOT-2" }, part.Lots.Select(x => x.LotKey).ToArray());
        }

        [Fact]
        public void Part_DeduplicatesQrKeysCaseInsensitively()
        {
            var part = new DeliveryPartAllocation("PART-A", null, null,
                new[] { new DeliveryLotAllocation("LOT-1", "LOT-1", null, 2, 0) },
                new[] { "QR-1", "qr-1", " ", null, "QR-2" });
            Assert.Equal(2, part.QrKeys.Count);
        }

        private static DeliveryStockCommand Build(DeliveryPartAllocation[] parts, int[] stt) =>
            new DeliveryStockCommand("HVN:PGH-9", "HVN", "PGH-9", "operator", DateTime.UtcNow, parts, stt);

        private static DeliveryPartAllocation Part(string code, int quantity) =>
            new DeliveryPartAllocation(
                code, null, null,
                new[] { new DeliveryLotAllocation("LOT-" + code, "LOT-" + code, null, quantity, 0) },
                Enumerable.Empty<string>());
    }
}
