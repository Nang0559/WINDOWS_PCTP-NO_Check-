using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Common
{
    public class TemFccParseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string LotFcc { get; set; }
        public string MaHangFcc { get; set; }
        public int SlTemFcc { get; set; }
        public string Gear { get; set; }
        public string SoPhieu { get; set; }   // chỉ có ở tem tổng
        public bool IsTongPhieu { get; set; }
    }

    /// <summary>
    /// Parse tem FCC theo ĐÚNG định dạng mà từng CustomerConfig dùng khi giao hàng
    /// thường (DocQRService) — dùng chung cho cả luồng Giao Bù NG để 2 nơi không
    /// bao giờ hiểu khác nhau về "tem của khách X là mấy phần".
    /// </summary>
    public static class TemFccParser
    {
        /// <summary>100003 (HTN) dùng tem TỔNG 6 phần; 100001/100002 dùng tem 4 phần.</summary>
        public static bool ExpectsTemTong(CustomerConfig cfg)
            => cfg.LoadTuBangRieng && !cfg.CoGear;

        public static TemFccParseResult Parse(
            string rawQr,
            CustomerConfig cfg,
            Func<string, string> getIdMaHangPadded,   // IDocQRRepository.GetIdMaHangPadded
            Func<int, string> getGearNameByCode)      // IDocQRRepository.GetGearName(int)
        {
            rawQr = (rawQr ?? "").Trim().ToUpper();
            var parts = rawQr.Split(':');
            bool expectTong = ExpectsTemTong(cfg);

            // ── 100003 (HTN): bắt buộc tem TỔNG 6 phần ─────────────────────────
            if (expectTong)
            {
                if (parts.Length != 6)
                    return Fail("Khách hàng này dùng TEM TỔNG (6 phần).\n" +
                                "Vui lòng bắn đúng tem tổng, không bắn tem thùng.");

                if (!int.TryParse(parts[3].Trim(), out int slTong))
                    return Fail("Số lượng trên tem tổng không hợp lệ.");

                return new TemFccParseResult
                {
                    Success = true,
                    LotFcc = LotCodeHelper.StripCounterAndQty(parts[0].Trim()),
                    MaHangFcc = parts[1].Trim(),
                    SlTemFcc = slTong,
                    SoPhieu = parts[4].Trim(),
                    IsTongPhieu = true
                };
            }

            // ── 100001 (HVN) / 100002 (YMVN): tem 4 phần ────────────────────────
            if (parts.Length != 4)
                return Fail("Khách hàng này dùng TEM THÙNG (4 phần).\n" +
                            "Vui lòng bắn đúng tem nội bộ dạng thùng, không bắn tem tổng.");

            string maHang = parts[1].Trim();
            if (!int.TryParse(parts[3].Trim(), out int slTem))
                return Fail("Số lượng trên tem không hợp lệ.");

            string lotSl = parts[0].Trim();
            string gear = "";
            string lotFcc;

            if (cfg.CoGear)
            {
                // ── 100002 YMVN: giống DocQRService.NormalizeLotFCC_YMVN ────────
                string gearRaw = LotCodeHelper.GetGearPart(lotSl);
                if (!string.IsNullOrEmpty(gearRaw))
                {
                    gear = int.TryParse(gearRaw, out int gearCode)
                        ? getGearNameByCode?.Invoke(gearCode) ?? ""
                        : gearRaw;
                }

                lotFcc = lotSl.Length >= LotCodeHelper.LEN_HEAD_FIXED
                    ? LotCodeHelper.StripCounterAndQty(lotSl)
                    : lotSl;
            }
            else
            {
                // ── 100001 HVN: giống DocQRService.NormalizeLotFCC ───────────────
                string idPadded = getIdMaHangPadded?.Invoke(maHang) ?? "";

                if (lotSl.Length < LotCodeHelper.LEN_HEAD_FIXED)
                {
                    lotFcc = lotSl.Length > 13
                        ? lotSl.Substring(0, 6) + idPadded + lotSl.Substring(13, 1)
                        : lotSl;
                }
                else
                {
                    lotFcc = LotCodeHelper.StripCounterAndQty(lotSl);
                }
            }

            return new TemFccParseResult
            {
                Success = true,
                LotFcc = lotFcc,
                MaHangFcc = maHang,
                SlTemFcc = slTem,
                Gear = gear,
                IsTongPhieu = false
            };
        }

        private static TemFccParseResult Fail(string msg) =>
            new TemFccParseResult { Success = false, ErrorMessage = msg };
    }
}
