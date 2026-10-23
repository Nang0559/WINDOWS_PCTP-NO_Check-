using System;
using System.Data.SqlClient;
using PCTP.Modules.XuLyHangLoi.Application;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class ReworkWorkflowTransactionTests
{
    [Fact]
    public void FinalQcSplit_MustReconcileApprovedRework()
    {
        var ticket = ValidTicket();
        ticket.Rework.SoLuongOK = 2;
        ticket.Rework.SoLuongNG = 1;
        ticket.ValidateDomainInvariants();
        Assert.Equal(ticket.Rework.SoLuongRework, ticket.Rework.SoLuongOK + ticket.Rework.SoLuongNG);
    }

    [Fact]
    public void InvalidFinalQcSplit_IsRejectedBeforeTransactionStarts()
    {
        var uow = new FakeUow();
        var tx = new ReworkWorkflowTransaction(uow);
        var ticket = ValidTicket();
        ticket.Rework.SoLuongNG = 2;

        Assert.Throws<InvalidOperationException>(() => tx.Execute(ticket, () => throw new Exception("must not execute")));
        Assert.Equal(0, uow.BeginCount);
    }

    [Fact]
    public void PersistFailure_RollsBackEntireReworkWorkflow()
    {
        var uow = new FakeUow();
        var tx = new ReworkWorkflowTransaction(uow);
        var persisted = false;

        Assert.Throws<InvalidOperationException>(() => tx.Execute(ValidTicket(), () =>
        {
            persisted = true;
            throw new InvalidOperationException("Injected rework persistence failure");
        }));

        Assert.True(persisted);
        Assert.Equal(1, uow.BeginCount);
        Assert.Equal(0, uow.CommitCount);
        Assert.Equal(1, uow.RollbackCount);
    }

    private static PhieuXuLyBatThuong ValidTicket() => new()
    {
        SoLuongLoi = 10,
        QCInspection = new QCInspectionResult
        {
            SoLuongAnhHuong = 10, SoLuongDaKiemTra = 10,
            SoLuongOK = 6, SoLuongNG = 4,
            SoLuongRework = 3, SoLuongLoaiBoBanDau = 1
        },
        Rework = new ReworkResult { SoLuongRework = 3, SoLuongOK = 2, SoLuongNG = 1 },
        Disposition = new DispositionResult { SoLuongLoaiBoBanDau = 1, SoLuongReworkNG = 1, SoLuongLoaiBoCuoi = 2 }
    };

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
