namespace PCTP.Modules.KhoVatLy.Repositories
{
    public interface IQuarantineReleaseAuditRepository
    {
        void RecordRelease(int slotLotId, int slotId, string lotNo, string itemCode, int quantity,
            string performedBy, string referenceId, string reason);
    }
}
