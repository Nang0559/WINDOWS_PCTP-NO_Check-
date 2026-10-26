using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Shared.Common;
using Xunit;

namespace PCTP.QC.Tests
{
    /// <summary>
    /// Behavioural tests (not source-contract) for DeliveryStockCommandHandler: P1,P2,P3,P5,P6,P7,P8,P9,P10.
    /// The fake world snapshots on Begin and restores on Rollback, so rollback assertions check real state.
    /// These run against in-memory fakes: they do NOT replace the SQL two-session evidence (G3/G5).
    /// </summary>
    public sealed class DeliveryStockCommandHandlerTests
    {
        private sealed class World : IUnitOfWork, IStockMovementService, IDeliveryCommandLedger, IDeliveryPendingGate, IDeliveryPostStockStep
        {
            public int PostCalls; public bool FailPost; public HashSet<string> PostMarks = new HashSet<string>();
            public void Run(DeliveryStockCommand c)
            {
                PostCalls++;
                if (FailPost) throw new InvalidOperationException("post step failed");
                PostMarks.Add(c.BusinessKey);
            }
            public Dictionary<string, int> StockTp = new Dictionary<string, int>();
            public Dictionary<int, int> SlotLot = new Dictionary<int, int>();
            public Dictionary<string, int> Ledger = new Dictionary<string, int>();
            public HashSet<string> Qr = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<int> PendingOpen = new HashSet<int>();
            public HashSet<string> Refs = new HashSet<string>();
            public int FailExportCall = -1; public int ExportCalls; public int Commits; public int Rollbacks;
            private World _snap; private int _nextId = 1;

            public void Begin() { _snap = Clone(); }
            public void Commit() { Commits++; _snap = null; }
            public void Rollback()
            {
                Rollbacks++;
                if (_snap == null) return;
                StockTp = _snap.StockTp; SlotLot = _snap.SlotLot; Ledger = _snap.Ledger;
                Qr = _snap.Qr; PendingOpen = _snap.PendingOpen; Refs = _snap.Refs; PostMarks = _snap.PostMarks; _snap = null;
            }
            private World Clone() => new World
            {
                StockTp = new Dictionary<string, int>(StockTp), SlotLot = new Dictionary<int, int>(SlotLot),
                Ledger = new Dictionary<string, int>(Ledger), Qr = new HashSet<string>(Qr, StringComparer.OrdinalIgnoreCase),
                PendingOpen = new HashSet<int>(PendingOpen), Refs = new HashSet<string>(Refs), PostMarks = new HashSet<string>(PostMarks)
            };

            public StockMovementResult Export(StockMovementRequest r)
            {
                ExportCalls++;
                if (FailExportCall == ExportCalls) throw new InvalidOperationException("injected failure");
                if (!Refs.Add(r.ReferenceType + "|" + r.ReferenceId)) return StockMovementResult.AlreadyProcessed("dup");
                int sl = SlotLot.TryGetValue(r.SlotLotId.Value, out var a) ? a : 0;
                int st = StockTp.TryGetValue(r.LotNo, out var b) ? b : 0;
                if (sl < r.Quantity || st < r.Quantity) return StockMovementResult.Fail("không đủ tồn");
                SlotLot[r.SlotLotId.Value] = sl - r.Quantity; StockTp[r.LotNo] = st - r.Quantity;
                return StockMovementResult.Ok();
            }
            public int? FindCommittedQuantity(string k) => Ledger.TryGetValue(k, out var q) ? q : (int?)null;
            public int Insert(DeliveryStockCommand c) { Ledger[c.BusinessKey] = c.TotalQuantity; return _nextId++; }
            public bool TryClaimQr(int id, string part, string qr) => Qr.Add(qr);
            public int ClosePending(DeliveryStockCommand c) => c.PendingStt.Count(s => PendingOpen.Remove(s));

            public StockMovementResult Reserve(StockMovementRequest r) => throw new NotSupportedException();
            public StockMovementResult Receive(StockMovementRequest r) => throw new NotSupportedException();
            public StockMovementResult Pick(StockMovementRequest r) => throw new NotSupportedException();
            public StockMovementResult Move(StockMovementRequest r) => throw new NotSupportedException();
            public StockMovementResult ReturnFromRework(StockMovementRequest r) => throw new NotSupportedException();
            public StockMovementResult Correct(StockMovementRequest r) => throw new NotSupportedException();
            public StockMovementResult RestorePicked(int s, StockSlotLot l) => throw new NotSupportedException();
        }

        private static World Seed()
        {
            var w = new World();
            w.StockTp["LOT-1"] = 10; w.StockTp["LOT-2"] = 10;
            w.SlotLot[101] = 10; w.SlotLot[102] = 10;
            w.PendingOpen.UnionWith(new[] { 1, 2 });
            return w;
        }
        private static DeliveryStockCommandHandler Handler(World w, bool withPost = false) =>
            withPost ? new DeliveryStockCommandHandler(w, w, w, w, w) : new DeliveryStockCommandHandler(w, w, w, w);
        private static DeliveryPartAllocation Part(string part, string[] qr, params (string lot, int slotLot, int qty)[] lots) =>
            new DeliveryPartAllocation(part, "M", "G",
                lots.Select((l, i) => new DeliveryLotAllocation(l.lot, l.lot, l.slotLot, l.qty, i)), qr);
        private static DeliveryStockCommand Cmd(string key, int[] stt, params DeliveryPartAllocation[] parts) =>
            new DeliveryStockCommand(key, "HVN", "PGH-1", "op", new DateTime(2026, 10, 26), parts, stt);

        [Fact]
        public void P1_P5_OnePartOneLot_PartialDeduct_StockTpAndSlotLotMoveTogether()
        {
            var w = Seed();
            var r = Handler(w).Execute(Cmd("K1", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 4))));
            Assert.True(r.Success); Assert.Equal(4, r.CommittedQuantity);
            Assert.Equal(6, w.StockTp["LOT-1"]); Assert.Equal(6, w.SlotLot[101]);
            Assert.Equal(1, w.Commits);
        }

        [Fact]
        public void P2_P3_MultiLotPart_AndSameLotAcrossParts_DeductAggregate()
        {
            var w = Seed();
            var r = Handler(w).Execute(Cmd("K2", new[] { 1, 2 },
                Part("A", new[] { "QR1" }, ("LOT-1", 101, 3), ("LOT-2", 102, 2)),
                Part("B", new[] { "QR2" }, ("LOT-1", 101, 5))));
            Assert.True(r.Success); Assert.Equal(10, r.CommittedQuantity);
            Assert.Equal(2, w.StockTp["LOT-1"]); Assert.Equal(8, w.StockTp["LOT-2"]);
            Assert.Equal(w.StockTp["LOT-1"], w.SlotLot[101]);
        }

        [Fact]
        public void P7_SameBusinessKeyRetry_ReturnsPriorOutcome_NoSecondMutation()
        {
            var w = Seed(); var h = Handler(w);
            var cmd = Cmd("K3", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 4)));
            Assert.True(h.Execute(cmd).Success);
            var again = h.Execute(cmd);
            Assert.True(again.Success); Assert.True(again.Duplicate); Assert.Equal(4, again.CommittedQuantity);
            Assert.Equal(6, w.StockTp["LOT-1"]); Assert.Equal(1, w.ExportCalls);
        }

        [Fact]
        public void P8_SameQrUnderAnotherBusinessKey_IsRejected_WithNoMutation()
        {
            var w = Seed(); var h = Handler(w);
            Assert.True(h.Execute(Cmd("K4", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 2)))).Success);
            var r = h.Execute(Cmd("K5", new[] { 2 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 2))));
            Assert.False(r.Success);
            Assert.Equal(8, w.StockTp["LOT-1"]); Assert.Equal(8, w.SlotLot[101]);
            Assert.False(w.Ledger.ContainsKey("K5")); Assert.Contains(2, w.PendingOpen);
        }

        [Fact]
        public void P9_P10_FailureAfterFirstExport_RollsBackEverything()
        {
            var w = Seed(); w.FailExportCall = 2;
            var r = Handler(w).Execute(Cmd("K6", new[] { 1, 2 },
                Part("A", new[] { "QR1" }, ("LOT-1", 101, 3)), Part("B", new[] { "QR2" }, ("LOT-2", 102, 3))));
            Assert.False(r.Success);
            Assert.Equal(10, w.StockTp["LOT-1"]); Assert.Equal(10, w.SlotLot[101]);
            Assert.Empty(w.Ledger); Assert.Empty(w.Qr); Assert.Equal(2, w.PendingOpen.Count);
            Assert.Equal(0, w.Commits);
        }

        [Fact]
        public void P6_PendingNotFullyClosed_RollsBackWholeCommand()
        {
            var w = Seed(); w.PendingOpen.Remove(2); // STT 2 already closed elsewhere
            var r = Handler(w).Execute(Cmd("K7", new[] { 1, 2 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 3))));
            Assert.False(r.Success);
            Assert.Equal(10, w.StockTp["LOT-1"]); Assert.Contains(1, w.PendingOpen); Assert.Empty(w.Ledger);
        }

        [Fact]
        public void InsufficientStock_FailsAndLeavesNoLedgerOrQr()
        {
            var w = Seed();
            var r = Handler(w).Execute(Cmd("K8", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 11))));
            Assert.False(r.Success); Assert.Empty(w.Ledger); Assert.Empty(w.Qr); Assert.Equal(10, w.SlotLot[101]);
        }

        [Fact]
        public void AllocationWithoutSlotLotId_FailsClosed_BeforeAnyTransaction()
        {
            var w = Seed();
            var part = new DeliveryPartAllocation("A", "", "",
                new[] { new DeliveryLotAllocation("LOT-1", "LOT-1", null, 1, 0) }, new[] { "QR1" });
            var r = Handler(w).Execute(Cmd("K9", new[] { 1 }, part));
            Assert.False(r.Success); Assert.Equal(0, w.ExportCalls); Assert.Equal(0, w.Rollbacks); Assert.Equal(0, w.Commits);
        }

        [Fact]
        public void ReferenceIds_AreStableAcrossRetries_NeverRandom()
        {
            var w = Seed();
            Handler(w).Execute(Cmd("K10", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 1))));
            Assert.Contains("GIAOHANG_KHACH|K10|A|0", w.Refs);
        }

        [Fact]
        public void PostStockStep_RunsInsideTransaction_AndItsFailureRollsBackStock()
        {
            var ok = Seed();
            Assert.True(Handler(ok, true).Execute(Cmd("K11", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 2)))).Success);
            Assert.Equal(1, ok.PostCalls); Assert.Contains("K11", ok.PostMarks);

            var bad = Seed(); bad.FailPost = true;
            var r = Handler(bad, true).Execute(Cmd("K12", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 2))));
            Assert.False(r.Success);
            Assert.Equal(10, bad.StockTp["LOT-1"]); Assert.Equal(10, bad.SlotLot[101]);
            Assert.Empty(bad.Ledger); Assert.Equal(2, bad.PendingOpen.Count); Assert.Equal(0, bad.Commits);
        }

        [Fact]
        public void PostStockStep_IsNotRunOnReplay()
        {
            var w = Seed(); var h = Handler(w, true);
            var cmd = Cmd("K13", new[] { 1 }, Part("A", new[] { "QR1" }, ("LOT-1", 101, 2)));
            h.Execute(cmd); h.Execute(cmd);
            Assert.Equal(1, w.PostCalls);
        }
    }
}
