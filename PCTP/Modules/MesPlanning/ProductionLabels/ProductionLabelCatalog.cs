using System;
using System.Data;
using PCTP.Modules.MesPlanning.Reports;
using PCTP.Shared.Labels;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>Đăng ký tem thùng và phiếu palet thành phẩm của LỆNH SẢN XUẤT vào <see cref="LabelCatalog"/> (có mặt trong menu Thiết kế layout tem).</summary>
    public static class ProductionLabelCatalog
    {
        public const string CartonCode = "PRODUCTION_CARTON";
        public const string PalletCode = "PRODUCTION_PALLET";

        public static LabelDefinition Carton { get { return LabelCatalog.Get(CartonCode); } }
        public static LabelDefinition Pallet { get { return LabelCatalog.Get(PalletCode); } }

        public static void Register()
        {
            LabelCatalog.Register(new LabelDefinition(CartonCode, "Tem thùng thành phẩm (lệnh sản xuất)", "A4 ngang, 1 tem / trang",
                () => new ProductionCartonLabelReport(), () => Sample("CARTON"), ProductionLabelData.PrepareCarton));
            LabelCatalog.Register(new LabelDefinition(PalletCode, "Phiếu quản lý nhập hàng thành phẩm ca (palet)", "A4 ngang, 1 phiếu / trang",
                () => new ProductionPalletLabelReport(), () => Sample("PALLET"), ProductionLabelData.PreparePallet));
        }

        /// <summary>Các cột MES.usp_FVN_ProductionOrder_LabelData (migration 065) với dữ liệu lấy từ hai mẫu tem thực tế.</summary>
        private static DataTable Sample(string kind)
        {
            var t = new DataTable();
            t.Columns.Add("ProductionOrderLabelId", typeof(long));
            t.Columns.Add("ProductionOrderId", typeof(long));
            t.Columns.Add("LabelKind", typeof(string));
            t.Columns.Add("Seq", typeof(int));
            t.Columns.Add("LabelNo", typeof(string));
            t.Columns.Add("QrPayload", typeof(string));
            t.Columns.Add("Qty", typeof(int));
            t.Columns.Add("PalletCode", typeof(string));
            t.Columns.Add("SiteCode", typeof(string));
            t.Columns.Add("OrderNo", typeof(string));
            t.Columns.Add("ProductPartNo", typeof(string));
            t.Columns.Add("ProductDescription", typeof(string));
            t.Columns.Add("ProductionDate", typeof(DateTime));
            t.Columns.Add("ShiftCode", typeof(string));
            t.Columns.Add("ShiftName", typeof(string));
            t.Columns.Add("DepartmentCode", typeof(string));
            t.Columns.Add("ProductionLineCode", typeof(string));
            t.Columns.Add("FactoryName", typeof(string));
            t.Columns.Add("DepartmentText", typeof(string));
            t.Columns.Add("FactoryAddress", typeof(string));
            t.Columns.Add("OriginText", typeof(string));
            t.Columns.Add("LabelCount", typeof(int));
            t.Columns.Add("IsPrinted", typeof(int));

            bool pallet = kind == "PALLET";
            int qty = pallet ? 300 : 5;
            string labelNo = pallet ? "2611270361510085170001830300" : "2611270361510085170001210005";
            string part = "23010-K1Z-J510-M1";
            string payload = labelNo + ":" + part + ":27/11/2026:" + qty + (pallet ? ":1:A000000000000001" : string.Empty);
            t.Rows.Add(1L, 1L, kind, 1, labelNo, payload, qty, pallet ? "A000000000000001" : null, "FVN", "PO-SAMPLE", part,
                "PULLEY ASSY DRIVEN", new DateTime(2026, 11, 27), "1", "Ca 1", "PD-2W", "L01", "FCC VIET NAM CO.L.T.D", "PD-2W",
                "ĐC: LÔ A5 , KCN THĂNG LONG , THIÊN LỘC ,HÀ NỘI", "Việt Nam", 1, 0);
            return t;
        }
    }
}
