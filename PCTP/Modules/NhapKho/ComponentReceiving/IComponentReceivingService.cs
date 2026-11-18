namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public interface IComponentReceivingService
    {
        ComponentReceiptResult Receive(ComponentReceiptRequest request);
    }
}