using System;

namespace PCTP.Shared.Common
{
    // Test-only transaction port; production IUnitOfWork remains untouched.
    public interface IUnitOfWork
    {
        void Begin();
        void Commit();
        void Rollback();
    }
}
