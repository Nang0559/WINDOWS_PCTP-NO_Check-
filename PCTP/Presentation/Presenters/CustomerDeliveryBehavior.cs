using PCTP.Shared.Models;

namespace PCTP.Presentation.Presenters
{
    /// <summary>
    /// Customer-specific presentation behavior for GiaoHangKhach.
    /// Keeps customer decisions out of individual presenters.
    /// </summary>
    internal sealed class CustomerDeliveryBehavior
    {
        private readonly CustomerConfig _cfg;

        internal CustomerDeliveryBehavior(CustomerConfig cfg)
        {
            _cfg = cfg;
        }

        internal bool SupportsYmvnCompletion
        {
            get { return _cfg.Delivery.CoHoanThanhYMVN; }
        }

        internal bool UsesDateBasedOrderUpload
        {
            get { return _cfg.Delivery.LoadTheoNgay; }
        }

        internal string GetOrderUploadTable()
        {
            return _cfg.Delivery.OrderTable;
        }

        internal string GetOrderUploadTitle()
        {
            if (!string.IsNullOrWhiteSpace(_cfg.Delivery.TenNhaMay))
                return "Upload PO " + _cfg.Delivery.TenNhaMay;

            if (!string.IsNullOrWhiteSpace(_cfg.DisplayName))
                return "Upload PO " + _cfg.DisplayName;

            return "Upload PO";
        }
    }
}
