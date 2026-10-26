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
                new[] { Part("PART-A", 2) }));
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
                DateTime.UtcNow, new[] { part });

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
                new[] { Part("PART-A", 1), Part("part-a", 2) }));
        }

        [Fact]
        public void LotAllocation_RejectsNonPositiveQuantityAndInvalidSlotLot()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DeliveryLotAllocation("LOT-1", "LOT-1", 0, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DeliveryLotAllocation("LOT-1", "LOT-1", null, 0, 0));
        }

        private static DeliveryPartAllocation Part(string code, int quantity) =>
            new DeliveryPartAllocation(
                code, null, null,
                new[] { new DeliveryLotAllocation("LOT-" + code, "LOT-" + code, null, quantity, 0) },
                Enumerable.Empty<string>());
    }
}
