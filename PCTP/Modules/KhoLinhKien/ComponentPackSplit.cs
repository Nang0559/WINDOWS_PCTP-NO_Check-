using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>Một palet trong kế hoạch chia: số thùng (tem nhỏ) và tổng số lượng.</summary>
    public sealed class ComponentPalletPlan
    {
        public int Seq { get; set; }
        public int CartonCount { get; set; }
        public decimal Qty { get; set; }
    }

    public sealed class ComponentPackSplitResult
    {
        public ComponentPackSplitResult()
        {
            Cartons = new List<decimal>();
            Pallets = new List<ComponentPalletPlan>();
            Warnings = new List<string>();
        }

        /// <summary>Số lượng từng thùng (tem nhỏ), theo thứ tự tem: các thùng đầy rồi thùng cuối giữ phần dư.</summary>
        public List<decimal> Cartons { get; private set; }
        /// <summary>Rỗng khi quy cách chưa có SL / palet.</summary>
        public List<ComponentPalletPlan> Pallets { get; private set; }
        public List<string> Warnings { get; private set; }
    }

    /// <summary>
    /// Cách chia số lượng đặt của MỘT dòng PO thành thùng và palet. Phản chiếu đúng thủ tục SQL để màn hình quy cách
    /// cho xem trước kết quả mà không cần phát hành:
    ///  - thùng:  usp_FVN_ComponentPO_Release  (migration 061): ceil(SL đặt / SL mỗi thùng) tem, tem cuối giữ phần dư, tối đa 5000 tem / dòng;
    ///  - palet:  usp_FVN_ComponentPO_PalletBuild (migration 063): gom các thùng liên tiếp cho đến khi thêm thùng nữa sẽ vượt MaxCloseQty.
    /// SQL vẫn là nơi quyết định; lớp này chỉ để xem trước và kiểm thử.
    /// </summary>
    public static class ComponentPackSplit
    {
        public const int MaxCartonsPerLine = 5000;

        public static ComponentPackSplitResult Plan(decimal orderQty, decimal packQty, decimal? maxCloseQty)
        {
            if (orderQty <= 0) throw new ArgumentException("Số lượng đặt phải lớn hơn 0.");
            if (packQty <= 0) throw new ArgumentException("SL / thùng phải lớn hơn 0.");

            decimal countDec = Math.Ceiling(orderQty / packQty);
            if (countDec > MaxCartonsPerLine)
                throw new InvalidOperationException("Quy cách đóng gói quá nhỏ so với số lượng đặt: vượt " + MaxCartonsPerLine + " tem cho một dòng.");
            int count = (int)countDec;

            var result = new ComponentPackSplitResult();
            for (int k = 1; k <= count; k++)
                result.Cartons.Add(k < count ? packQty : orderQty - packQty * (count - 1));

            if (!maxCloseQty.HasValue)
            {
                result.Warnings.Add("Chưa khai báo SL / palet (MaxCloseQty): chưa in được tem palet.");
                return result;
            }
            decimal max = maxCloseQty.Value;
            if (max <= 0) throw new ArgumentException("SL / palet phải lớn hơn 0.");

            ComponentPalletPlan current = null;
            foreach (decimal qty in result.Cartons)
            {
                if (current == null || current.Qty + qty > max)
                {
                    current = new ComponentPalletPlan { Seq = result.Pallets.Count + 1 };
                    result.Pallets.Add(current);
                }
                current.Qty += qty;
                current.CartonCount++;
            }

            if (max < packQty)
                result.Warnings.Add("SL / palet nhỏ hơn SL / thùng: mỗi thùng sẽ nằm trên một palet riêng.");
            else if (max % packQty != 0)
            {
                int perPallet = (int)Math.Floor(max / packQty);
                result.Warnings.Add("SL / palet không phải bội số của SL / thùng: một palet chỉ chứa tối đa " + perPallet +
                                    " thùng = " + (perPallet * packQty).ToString("#,##0.###", System.Globalization.CultureInfo.InvariantCulture) +
                                    " (thùng đóng sẵn không chia nhỏ được).");
            }
            return result;
        }

        /// <summary>Số thùng tối đa trên một palet theo quy cách (0 khi chưa có SL / palet).</summary>
        public static int CartonsPerPallet(decimal packQty, decimal? maxCloseQty)
        {
            if (packQty <= 0 || !maxCloseQty.HasValue || maxCloseQty.Value <= 0) return 0;
            return Math.Max(1, (int)Math.Floor(maxCloseQty.Value / packQty));
        }
    }
}
