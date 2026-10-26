using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoCore.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class QuarantineReleaseServiceTests
{
    [Fact]
    public void Release_RejectsUnauthorizedBeforeAnyStockMutation()
    {
        var f = CreateFixture(authorized: false);
        var result = f.Service.Release(new QuarantineReleaseRequest
        {
            SlotLotId = 7, QcDispositionApproved = true, PerformedBy = "operator", ReferenceId = "REL-001"
        });

        Assert.False(result.Success);
        Assert.True(result.Unauthorized);
        Assert.Empty(f.Slots.Mutations);
        Assert.Equal(0, f.Balance.AdjustCount);
        Assert.Equal(0, f.Uow.BeginCount);
    }

    [Fact]
    public void Release_RequiresQcDisposition()
    {
        var f = CreateFixture(authorized: true);
        var result = f.Service.Release(new QuarantineReleaseRequest
        {
            SlotLotId = 7, QcDispositionApproved = false, PerformedBy = "qc-admin"
        });

        Assert.False(result.Success);
        Assert.Contains("kết luận QC", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(f.Slots.Mutations);
        Assert.Equal(0, f.Uow.BeginCount);
    }

    [Fact]
    public void Release_ConvertsQuarantineToActiveStockAndAuditsInOneTransaction()
    {
        var f = CreateFixture(authorized: true);
        var result = f.Service.Release(new QuarantineReleaseRequest
        {
            SlotLotId = 7,
            QcDispositionApproved = true,
            PerformedBy = "qc-admin",
            ReferenceId = "REL-001",
            Reason = "QC final OK"
        });

        Assert.True(result.Success);
        Assert.Equal(1, f.Uow.BeginCount);
        Assert.Equal(1, f.Uow.CommitCount);
        Assert.Equal(0, f.Uow.RollbackCount);
        Assert.Equal(new[] { "decrease:7:12", "add:12:12:0" }, f.Slots.Mutations);
        Assert.Equal(1, f.Slots.LockCount);
        Assert.Equal(2, f.Slots.GetLotDetailsCount);
        Assert.Equal(1, f.Balance.AdjustCount);
        Assert.Equal(1, f.Audit.Count);
    }

    [Fact]
    public void Release_RevalidatesQuarantineAfterWaitingForSlotLock()
    {
        var f = CreateFixture(authorized: true);
        f.Slots.StatusAfterLock = QuarantineReleaseService.ActiveStatus;

        var result = f.Service.Release(new QuarantineReleaseRequest
        {
            SlotLotId = 7, QcDispositionApproved = true, PerformedBy = "qc-admin", ReferenceId = "REL-RACE"
        });

        Assert.False(result.Success);
        Assert.Equal(1, f.Slots.LockCount);
        Assert.Empty(f.Slots.Mutations);
        Assert.Equal(0, f.Balance.AdjustCount);
        Assert.Equal(0, f.Audit.Count);
        Assert.Equal(0, f.Uow.CommitCount);
        Assert.Equal(1, f.Uow.RollbackCount);
    }

    [Fact]
    public void Release_FailureRollsBackEveryMutation()
    {
        var f = CreateFixture(authorized: true);
        f.Balance.Throw = true;
        var result = f.Service.Release(new QuarantineReleaseRequest
        {
            SlotLotId = 7, QcDispositionApproved = true, PerformedBy = "qc-admin", ReferenceId = "REL-FAIL"
        });

        Assert.False(result.Success);
        Assert.Equal(1, f.Uow.BeginCount);
        Assert.Equal(0, f.Uow.CommitCount);
        Assert.Equal(1, f.Uow.RollbackCount);
        Assert.Equal(0, f.Audit.Count);
    }

    private static Fixture CreateFixture(bool authorized)
    {
        var slots = new FakeSlots();
        var balance = new FakeBalance();
        var audit = new FakeAudit();
        var uow = new FakeUow();
        var auth = new FakeAuthorization(authorized);
        var service = new QuarantineReleaseService(slots, balance, audit, auth, uow);
        return new Fixture(service, slots, balance, audit, uow);
    }

    private sealed record Fixture(QuarantineReleaseService Service, FakeSlots Slots, FakeBalance Balance, FakeAudit Audit, FakeUow Uow);

    private sealed class FakeAuthorization : IQuarantineReleaseAuthorization
    {
        private readonly bool _authorized;
        public FakeAuthorization(bool authorized) => _authorized = authorized;
        public bool CanRelease(string performedBy) => _authorized;
    }

    private sealed class FakeAudit : IQuarantineReleaseAuditRepository
    {
        public int Count { get; private set; }
        public void RecordRelease(int slotLotId, int slotId, string lotNo, string itemCode, int quantity, string performedBy, string referenceId, string reason)
        {
            Count++;
        }
    }

    private sealed class FakeBalance : IStockBalanceRepository
    {
        public bool Throw { get; set; }
        public int AdjustCount { get; private set; }
        public int GetAvailableQuantity(string lotNo) => 0;
        public void DecreaseAvailableQuantity(string lotNo, int quantity) { }
        public void AdjustAvailableQuantity(string lotNo, int delta)
        {
            if (Throw) throw new InvalidOperationException("Injected balance failure");
            AdjustCount++;
        }
        public bool TryDecreaseAvailableQuantity(string lotNo, int quantity) => true;
    }

    private sealed class FakeSlots : IStockSlotRepository
    {
        public List<string> Mutations { get; } = new();
        public int LockCount { get; private set; }
        public int GetLotDetailsCount { get; private set; }
        public byte? StatusAfterLock { get; set; }
        private bool _locked;
        public int GetLotQuantity(int slotLotId) => 12;
        public byte GetPhieuStatus(int slotLotId) => QuarantineReleaseService.QuarantineStatus;
        public StockSlotLot GetLotDetails(int slotLotId)
        {
            GetLotDetailsCount++;
            return new StockSlotLot
            {
                LotNo = "LOT-Q", ItemCode = "ITEM-Q", Quantity = 12, TemCode = "TEM-Q", RawQr = "QR-Q",
                PhieuStatus = _locked && StatusAfterLock.HasValue ? StatusAfterLock.Value : (byte)3
            };
        }
        public string GetLotNo(int slotLotId) => "LOT-Q";
        public string GetItemCode(int slotLotId) => "ITEM-Q";
        public int? GetSlotId(int slotLotId) => 12;
        public void LockSlotForUpdate(int slotId) { LockCount++; _locked = true; }
        public void DecreaseLotQuantity(int slotLotId, int quantity) => Mutations.Add($"decrease:{slotLotId}:{quantity}");
        public void AddQuantity(int slotId, int quantity, string itemCode) => Mutations.Add($"addqty:{slotId}:{quantity}");
        public void AddLot(int slotId, StockSlotLot lot) => Mutations.Add($"add:{slotId}:{lot.Quantity}:{lot.PhieuStatus}");
        public bool TryRegisterMovement(string referenceType, string referenceId, string qrData, string temCode, bool checkExistingQr) => true;
        public StockSlotTakeResult TakeLot(int slotId, string lotNo, string itemCode, int quantity) => null;
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public SqlConnection Connection => null;
        public SqlTransaction Transaction => null;
        public bool IsInTransaction { get; private set; }
        public int BeginCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public void Begin() { BeginCount++; IsInTransaction = true; }
        public void Commit() { CommitCount++; IsInTransaction = false; }
        public void Rollback() { RollbackCount++; IsInTransaction = false; }
        public void Dispose() { }
    }
}
