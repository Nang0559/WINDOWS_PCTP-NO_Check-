using System;
using System.Data;
using PCTP.QRCODE_HVN.Report;

namespace PCTP.Shared.Labels
{
    /// <summary>Đăng ký tem ghép LOT (mẫu GHEPLOT và GHEPLOT_YMVN) vào bộ dùng chung để chỉnh layout trong Report Designer.</summary>
    internal static class GhepLotLabelCatalog
    {
        public static void Register()
        {
            LabelCatalog.Register(new LabelDefinition("GHEPLOT", "Tem ghép LOT", "A4 ngang, 1 tem / trang",
                () => new GHEPLOT(), Sample));
            LabelCatalog.Register(new LabelDefinition("GHEPLOT_YMVN", "Tem ghép LOT - YMVN (có Gear)", "A4 ngang, 1 tem / trang",
                () => new GHEPLOT_YMVN(), Sample));
        }

        /// <summary>Các cột Usp_gheplotPrint trả về mà hai mẫu đang gắn.</summary>
        private static DataTable Sample()
        {
            var t = new DataTable();
            foreach (string c in new[] { "DocDate", "ItemCode", "ItemLotCode", "ItemName", "MO", "Model", "QRCODE", "Quantity9", "ShiftCode" })
                t.Columns.Add(c, typeof(string));
            t.Rows.Add(DateTime.Today.ToString("dd/MM/yyyy"), "23123-KPH-9002", "26112613108873100001", "RETAINER SPRING",
                "MO-0001", "GEAR-A", "26112613108873100001", "2400", "A");
            return t;
        }
    }
}
