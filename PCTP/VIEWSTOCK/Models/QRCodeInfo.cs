using PCTP.VIEWSTOCK.Fuction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    //public class QRCodeInfo
    //{
    //    public string LotNo { get; set; }
    //    public string ItemCode { get; set; }
    //    public DateTime ImportDate { get; set; }
    //    public int Quantity { get; set; }
    //    public string Unit { get; set; }
    //    public string WarehouseCode { get; set; }
    //}
    public class QRCodeInfo
    {
        // ── Chung cho cả 2 loại tem ──────────────────────────
        public string LotNo { get; set; }  // 260521015721010540006956000
        public string ItemCode { get; set; }  // 22201-kyhn-a400-chec
        public string RawLotNo { get; set; }
        public string NgaySX { get; set; }  // "21/05/2026" (string vì format d/M/yyyy)
        public int Quantity { get; set; }  // 16000 (tổng) hoặc 400 (thùng)
        public bool IsTongPhieu { get; set; } // true = tem tổng, false = tem thùng

        // ── Chỉ có ở tem tổng (parts[4], parts[5]) ───────────
        public string SoPhieuTong { get; set; } // "1"
        public string RawQr { get; set; }
        public string MaPhieu { get; set; } // "a010000000122103"
        public string CaseNo { get; set; }


        // ── Giữ tương thích với code cũ ──────────────────────
        // ImportDate parse từ NgaySX
        private DateTime? _importDate;

        public DateTime? ImportDate
        {
            get
            {
                if (_importDate.HasValue)
                    return _importDate;

                if (DateTime.TryParseExact(
                    NgaySX,
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
                {
                    return dt;
                }

                return null;
            }
            set
            {
                _importDate = value;
            }
        }

        // WarehouseCode + Unit map từ LotNo (giữ để không break code cũ)
        public string WarehouseCode { get; set; } = "";
        public string Unit { get; set; } = "";
        public string ToQrString()
        {
            return RawQr ?? QRCodeBuilder.Build(this);
        }
    }

}
