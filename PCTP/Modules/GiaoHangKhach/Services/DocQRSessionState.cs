using PCTP.Shared.Models;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Explicit QR session state. Keeps machine capability separate from the
    /// current QR delivery session and owns the current MP/SP/O-TYPE mode.
    /// </summary>
    public sealed class DocQRSessionState
    {
        private readonly DocQRTableResolver _tables;

        public DocQRSessionState(CustomerConfig cfg)
        {
            _tables = new DocQRTableResolver(cfg);
        }

        public bool IsBanSP { get; private set; }
        public bool IsBanOType { get; private set; }

        public string DocQrTable => _tables.GetDocQrTable(IsBanSP);
        public string TmpTable => _tables.GetTmpTable(IsBanSP);

        public void SetCategory(bool isSp, bool isOType)
        {
            IsBanSP = isSp;
            IsBanOType = isOType;
        }

        public void Reset()
        {
            IsBanSP = false;
            IsBanOType = false;
        }
    }
}
