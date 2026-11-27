using System;
using System.Data;
using PCTP.Modules.KhoLinhKien.Reports;
using PCTP.Shared.Labels;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>Đăng ký tem thùng và tem palet linh kiện vào <see cref="LabelCatalog"/>.</summary>
    public static class ComponentLabelCatalog
    {
        public const string CartonCode = "COMPONENT_CARTON";
        public const string PalletCode = "COMPONENT_PALLET";

        public static LabelDefinition Carton { get { return LabelCatalog.Get(CartonCode); } }
        public static LabelDefinition Pallet { get { return LabelCatalog.Get(PalletCode); } }

        public static void Register()
        {
            LabelCatalog.Register(new LabelDefinition(CartonCode, "Tem thùng linh kiện", "A4 ngang, 4 cột x 4 hàng = 16 tem / trang",
                () => new ComponentCartonLabelReport(), CartonSample, raw => ComponentPoLabelData.PrepareCarton(raw, DateTime.Now)));
            LabelCatalog.Register(new LabelDefinition(PalletCode, "Phiếu quản lý nhập linh kiện palet", "A4 ngang, 1 phiếu / trang",
                () => new ComponentPalletLabelReport(), PalletSample, ComponentPoLabelData.PreparePallet));
        }

        /// <summary>Các cột MES.usp_FVN_ComponentPO_LabelData.</summary>
        private static DataTable CartonSample()
        {
            var t = new DataTable();
            t.Columns.Add("ComponentLotId", typeof(long));
            t.Columns.Add("LotIssueDate", typeof(DateTime));
            t.Columns.Add("Seq", typeof(int));
            t.Columns.Add("PartNo", typeof(string));
            t.Columns.Add("PartDescription", typeof(string));
            t.Columns.Add("LineNo", typeof(int));
            t.Columns.Add("SupplierCode", typeof(string));
            t.Columns.Add("SupplierName", typeof(string));
            t.Columns.Add("QrPayload", typeof(string));
            t.Columns.Add("LotNo", typeof(string));
            t.Columns.Add("IssuedQty", typeof(decimal));
            for (int i = 1; i <= 16; i++)
                t.Rows.Add((long)i, DateTime.Today, i, "23123-KPH-9002", "RETAINER SPRING", 10, "VN001", "ABC CO LTD",
                    "SAMPLE-LOT-" + i.ToString("D4"), "261126131088731" + i.ToString("D5"), 2400m);
            return t;
        }

        /// <summary>Các cột MES.usp_FVN_ComponentPO_PalletData.</summary>
        private static DataTable PalletSample()
        {
            var t = new DataTable();
            t.Columns.Add("ComponentPalletId", typeof(long));
            t.Columns.Add("PalletSeq", typeof(int));
            t.Columns.Add("PalletCount", typeof(int));
            t.Columns.Add("LotIssueDate", typeof(DateTime));
            t.Columns.Add("PurchaseOrderNo", typeof(string));
            t.Columns.Add("LineNo", typeof(int));
            t.Columns.Add("PartNo", typeof(string));
            t.Columns.Add("PartDescription", typeof(string));
            t.Columns.Add("SupplierCode", typeof(string));
            t.Columns.Add("SupplierName", typeof(string));
            t.Columns.Add("TotalQty", typeof(decimal));
            t.Columns.Add("QrPayload", typeof(string));
            t.Columns.Add("LotCount", typeof(int));
            t.Columns.Add("PalletNo", typeof(string));
            for (int i = 1; i <= 2; i++)
                t.Rows.Add((long)i, i, 2, DateTime.Today, "PO-0001", 10, "23123-KPH-9002", "RETAINER SPRING", "VN001", "ABC CO LTD",
                    9600m, ComponentPalletQr.Prefix + "PLT-000" + i, 4, "PLT-000" + i);
            return t;
        }
    }
}
