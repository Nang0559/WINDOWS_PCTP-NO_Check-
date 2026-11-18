namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public interface IComponentReceivingRepository
    {
        ComponentReceiptResult Post(ComponentReceiptRequest request);
    }
}