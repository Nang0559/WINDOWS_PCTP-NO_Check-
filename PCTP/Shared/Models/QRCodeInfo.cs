
using PCTP.Shared.Helpers;
using System;
using System.Globalization;


namespace PCTP.Shared.Models
{
    /// <summary>
    /// Thông tin được chuẩn hóa từ QR tem tổng / tem thùng.
    ///
    /// Quy ước:
    /// - LotNo      : LOT chuẩn dùng cho nghiệp vụ.
    /// - RawLotNoSL : LOT nguyên bản từ QR, trước khi normalize.
    /// - RawLotNo   : LOT gốc.
    /// - RawQr      : chuỗi QR nguyên bản, dùng để idempotency / trace.
    /// - MaPhieu    : mã phiếu / tem code.
    /// - CaseNo     : case production tương ứng.
    /// </summary>
    public class QRCodeInfo
    {
        // =========================================================
        // COMMON
        // =========================================================

        /// <summary>
        /// LOT chuẩn dùng cho nghiệp vụ.
        /// Ví dụ: 260521015721010540006956000
        /// </summary>
        public string LotNo { get; set; }

        /// <summary>
        /// LOT nguyên bản từ QR, có thể còn suffix SL/counter/quantity.
        /// </summary>
        public string RawLotNoSL { get; set; }

        /// <summary>
        /// LOT gốc sau khi tách phần không cần thiết.
        /// </summary>
        public string RawLotNo { get; set; }

        /// <summary>
        /// Mã sản phẩm.
        /// </summary>
        public string ItemCode { get; set; }

        /// <summary>
        /// Ngày sản xuất dạng string theo format QR.
        /// </summary>
        public string NgaySX { get; set; }

        /// <summary>
        /// Số lượng của tem.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// true  = tem tổng
        /// false = tem thùng
        /// </summary>
        public bool IsTongPhieu { get; set; }


        // =========================================================
        // TEM TỔNG
        // =========================================================

        /// <summary>
        /// Số phiếu tổng.
        /// Ví dụ: "1"
        /// </summary>
        public string SoPhieuTong { get; set; }

        /// <summary>
        /// Mã phiếu sản xuất / phiếu tham chiếu.
        /// </summary>
        public string MaPhieu { get; set; }

        /// <summary>
        /// Chuỗi QR nguyên bản.
        ///
        /// Đây là giá trị quan trọng cho:
        /// - idempotency
        /// - audit
        /// - DeliveryTrace
        /// </summary>
        public string RawQr { get; set; }

        /// <summary>
        /// Case production tương ứng với QR.
        ///
        /// Nếu parser đã xác định được CaseNo thì service
        /// nên ưu tiên sử dụng giá trị này.
        /// </summary>
        public string CaseNo { get; set; }


        // =========================================================
        // LEGACY COMPATIBILITY
        // =========================================================

        private DateTime? _importDate;

        /// <summary>
        /// Compatibility với code cũ.
        ///
        /// Nếu ImportDate chưa được set thì tự parse từ NgaySX.
        /// </summary>
        public DateTime? ImportDate
        {
            get
            {
                if (_importDate.HasValue)
                    return _importDate;

                if (string.IsNullOrWhiteSpace(NgaySX))
                    return null;

                DateTime dt;

                if (DateTime.TryParseExact(
                    NgaySX,
                    new[]
                    {
                        "dd/MM/yyyy",
                        "d/M/yyyy",
                        "dd/M/yyyy",
                        "d/MM/yyyy"
                    },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out dt))
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


        // =========================================================
        // LEGACY WAREHOUSE
        // =========================================================

        /// <summary>
        /// Giữ compatibility với code NhapKho cũ.
        /// </summary>
        public string WarehouseCode { get; set; } = "";

        /// <summary>
        /// Giữ compatibility với code NhapKho cũ.
        /// </summary>
        public string Unit { get; set; } = "";


        // =========================================================
        // NORMALIZATION / OUTPUT
        // =========================================================

        /// <summary>
        /// Trả lại chuỗi QR nguyên bản nếu có.
        /// Nếu không có thì build lại từ dữ liệu QR.
        /// </summary>
        public string ToQrString()
        {
            if (!string.IsNullOrWhiteSpace(RawQr))
                return RawQr;

            return QRCodeBuilder.Build(this);
        }
    }

}
