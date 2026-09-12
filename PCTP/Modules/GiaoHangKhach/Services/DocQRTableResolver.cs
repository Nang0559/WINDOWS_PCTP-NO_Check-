using PCTP.Shared.Models;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Centralizes DOCQRCODE/TMP table selection for the QR working session.
    /// MP/O TYPE share the normal tables; SP uses the SP tables.
    /// </summary>
    public sealed class DocQRTableResolver
    {
        private readonly CustomerConfig _cfg;

        public DocQRTableResolver(CustomerConfig cfg)
        {
            _cfg = cfg;
        }

        public string GetDocQrTable(bool isSp)
        {
            return _cfg.Delivery.GetDocQRTable(isSp);
        }

        public string GetTmpTable(bool isSp)
        {
            return _cfg.Delivery.GetTmpTable(isSp);
        }
    }
}
