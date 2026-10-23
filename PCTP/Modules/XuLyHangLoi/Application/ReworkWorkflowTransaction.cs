using System;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;

namespace PCTP.Modules.XuLyHangLoi.Application
{
    /// <summary>
    /// Common transaction boundary for QC/rework persistence. UI forms may supply
    /// the persistence delegate, but the domain validation and rollback contract
    /// are centralized here so stock/workflow rows cannot be partially committed.
    /// </summary>
    public sealed class ReworkWorkflowTransaction
    {
        private readonly IUnitOfWork _uow;

        public ReworkWorkflowTransaction(IUnitOfWork uow)
            => _uow = uow ?? throw new ArgumentNullException(nameof(uow));

        public void Execute(PhieuXuLyBatThuong ticket, Action persist)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            if (persist == null) throw new ArgumentNullException(nameof(persist));

            ticket.ValidateDomainInvariants();
            _uow.Begin();
            try
            {
                persist();
                _uow.Commit();
            }
            catch
            {
                try { _uow.Rollback(); } catch { }
                throw;
            }
        }
    }
}
