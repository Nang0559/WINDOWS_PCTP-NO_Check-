using System;
using System.Data;
using System.Globalization;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// Chuẩn bị dữ liệu cho báo cáo tem thành phẩm: thêm các cột chữ đã định dạng để bố cục trong Report Designer chỉ cần gắn [Cột].
    /// Nguồn: MES.usp_FVN_ProductionOrder_LabelData (migration 065).
    /// </summary>
    public static class ProductionLabelData
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Tem thùng: ShiftText (Ca SX), DateText dd/MM/yy (Ngày SX), QtyText "SL: n", DepartmentLine "(PD-2W)".</summary>
        public static DataTable PrepareCarton(DataTable source)
        {
            DataTable t = source.Copy();
            foreach (string c in new[] { "ShiftText", "DateText", "QtyText", "DepartmentLine" }) Ensure(t, c);
            foreach (DataRow r in t.Rows)
            {
                r["ShiftText"] = Text(r, "ShiftCode");
                r["DateText"] = DateText(r["ProductionDate"], "dd/MM/yy");
                r["QtyText"] = "SL: " + QtyText(r["Qty"]);
                r["DepartmentLine"] = DepartmentLine(Text(r, "DepartmentText"));
            }
            return t;
        }

        /// <summary>Phiếu palet: SeqText (STT xe / palet), DateText dd/MM/yyyy, QtyText số lượng.</summary>
        public static DataTable PreparePallet(DataTable source)
        {
            DataTable t = source.Copy();
            foreach (string c in new[] { "ShiftText", "SeqText", "DateText", "QtyText" }) Ensure(t, c);
            foreach (DataRow r in t.Rows)
            {
                r["ShiftText"] = Text(r, "ShiftCode");
                r["SeqText"] = Convert.ToString(r["Seq"], Inv);
                r["DateText"] = DateText(r["ProductionDate"], "dd/MM/yyyy");
                r["QtyText"] = QtyText(r["Qty"]);
            }
            return t;
        }

        /// <summary>"PD-2W" → "(PD-2W)"; đã có ngoặc hoặc rỗng thì giữ nguyên.</summary>
        public static string DepartmentLine(string department)
        {
            string d = (department ?? string.Empty).Trim();
            if (d.Length == 0 || (d.StartsWith("(", StringComparison.Ordinal) && d.EndsWith(")", StringComparison.Ordinal))) return d;
            return "(" + d + ")";
        }

        private static void Ensure(DataTable t, string column)
        {
            if (!t.Columns.Contains(column)) t.Columns.Add(column, typeof(string));
        }

        private static string Text(DataRow r, string column)
        {
            if (!r.Table.Columns.Contains(column)) return string.Empty;
            object v = r[column];
            return v == null || v == DBNull.Value ? string.Empty : Convert.ToString(v, Inv).Trim();
        }

        private static string DateText(object value, string format)
        {
            return value == null || value == DBNull.Value ? string.Empty : Convert.ToDateTime(value, Inv).ToString(format, Inv);
        }

        private static string QtyText(object value)
        {
            return value == null || value == DBNull.Value ? string.Empty : Convert.ToDecimal(value, Inv).ToString("#,##0.###", Inv);
        }
    }
}
