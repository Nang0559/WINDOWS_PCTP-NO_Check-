using System;
using PCTP.Modules.GiaoHangKhach.Services;

namespace PCTP.Workflow.Tests
{
    internal static class FifoSessionStateRegressionTests
    {
        private static int _passed;

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException("FAILED: " + name);

            _passed++;
        }

        private static FifoSessionState CreateState(int needQty)
        {
            var state = new FifoSessionState();
            state.Initialize(
                "PART01",
                needQty,
                new[]
                {
                    new FifoStockLine { LotKey = "PART01A", DisplayLot = "PART01A", AvailableQty = 5, FifoRank = 0 },
                    new FifoStockLine { LotKey = "PART01B", DisplayLot = "PART01B", AvailableQty = 5, FifoRank = 1 },
                    new FifoStockLine { LotKey = "PART01C", DisplayLot = "PART01C", AvailableQty = 5, FifoRank = 2 }
                });
            return state;
        }

        private static void CorrectOrderConsumesAllLots()
        {
            FifoSessionState state = CreateState(15);
            string message;

            AssertTrue(state.TryConsume(1, "PART01", "PART01A-5", 5, out message), "A must pass");
            AssertTrue(state.TryConsume(2, "PART01", "PART01B-5", 5, out message), "B must pass after A");
            AssertTrue(state.TryConsume(3, "PART01", "PART01C-5", 5, out message), "C must pass after B");
        }

        private static void SingleLotUsesQrQuantity()
        {
            FifoSessionState state = CreateState(5);
            string message;

            AssertTrue(state.TryConsume(1, "PART01", "PART01A", 5, out message), "single LOT must use QR quantity");
            AssertTrue(!state.TryConsume(2, "PART01", "PART01A", 1, out message), "consumed single LOT must no longer be available");
        }

        private static void WrongOrderIsRejectedWithoutConsumption()
        {
            FifoSessionState state = CreateState(10);
            string message;

            AssertTrue(!state.TryConsume(1, "PART01", "PART01B-5", 5, out message), "B before A must fail");
            AssertTrue(!string.IsNullOrEmpty(message), "FIFO failure must explain the reason");
            AssertTrue(state.TryConsume(2, "PART01", "PART01A-5", 5, out message), "A must still be available after rejected B");
            AssertTrue(state.TryConsume(3, "PART01", "PART01B-5", 5, out message), "B must become valid after A");
        }

        private static void ValidCompoundLotIsAccepted()
        {
            FifoSessionState state = CreateState(10);
            string message;

            AssertTrue(state.TryConsume(1, "PART01", "PART01A-3,PART01B-2", 5, out message), "valid compound LOT must pass");
            AssertTrue(state.TryConsume(2, "PART01", "PART01B-3,PART01C-2", 5, out message), "compound LOT must continue FIFO across components");
        }

        private static void MalformedCompoundLotIsRejectedStrictly()
        {
            FifoSessionState state = CreateState(10);
            string message;

            AssertTrue(!state.TryConsume(1, "PART01", "PART01A-3,,PART01B-2", 5, out message), "empty compound component must fail");
            AssertTrue(!state.TryConsume(2, "PART01", "PART01A-X,PART01B-2", 5, out message), "non-numeric compound quantity must fail");
            AssertTrue(!state.TryConsume(3, "PART01", "PART01A-0,PART01B-5", 5, out message), "zero compound quantity must fail");
            AssertTrue(!state.TryConsume(4, "PART01", "PART01A-3,PART01B-2", 4, out message), "compound quantity mismatch must fail");
        }

        private static void RejectedScanDoesNotConsumeStockBudget()
        {
            FifoSessionState state = CreateState(5);
            string message;

            AssertTrue(!state.TryConsume(1, "PART01", "PART01B-5", 5, out message), "wrong lot must fail");
            AssertTrue(state.TryConsume(2, "PART01", "PART01A-5", 5, out message), "rejected scan must not consume FIFO budget");
        }

        private static void RescanSameSttIsAtomic()
        {
            FifoSessionState state = CreateState(5);
            string message;

            AssertTrue(state.TryConsume(1, "PART01", "PART01A-3", 3, out message), "first scan must pass");
            AssertTrue(state.TryConsume(1, "PART01", "PART01A-5", 5, out message), "same STT must release previous reservation before re-evaluation");
        }

        private static void ReleaseRestoresReservation()
        {
            FifoSessionState state = CreateState(5);
            string message;

            AssertTrue(state.TryConsume(1, "PART01", "PART01A-5", 5, out message), "initial reservation must pass");
            state.Release(1);
            AssertTrue(state.TryConsume(2, "PART01", "PART01A-5", 5, out message), "released reservation must be reusable");
        }

        public static int RunAll()
        {
            _passed = 0;
            CorrectOrderConsumesAllLots();
            SingleLotUsesQrQuantity();
            WrongOrderIsRejectedWithoutConsumption();
            ValidCompoundLotIsAccepted();
            MalformedCompoundLotIsRejectedStrictly();
            RejectedScanDoesNotConsumeStockBudget();
            RescanSameSttIsAtomic();
            ReleaseRestoresReservation();
            return _passed;
        }

        public static void Main()
        {
            int passed = RunAll();
            Console.WriteLine("FIFO regression tests passed: " + passed);
        }
    }
}