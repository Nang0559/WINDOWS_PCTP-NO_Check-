using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Entities
{
    /// <summary>
    /// Entity ánh xạ bảng DOCQRCODE — không phụ thuộc SQL/UI/DevExpress.
    /// </summary>
    public class DocQRCode
    {
        // ── Khoá / thứ tự ───────────────────────────────────────────────────────
        public int STT { get; set; }
        public int SttBan { get; set; }

        // ── Phía FCC ────────────────────────────────────────────────────────────
        public string LotFCC { get; set; } = "";
        public string MaHangFCC { get; set; } = "";
        public string MaFCC { get; set; } = "";
        public int SlTemFCC { get; set; }
        public string SoPhieu { get; set; } = "";

        // ── Phía HVN ────────────────────────────────────────────────────────────
        public string LotHVN { get; set; } = "";
        public string MaHangHVN { get; set; } = "";
        public int SlTemHVN { get; set; }
        public string SuaLotHVN { get; set; } = "";   // SUALOTHVN

        // ── Kết quả / trạng thái ────────────────────────────────────────────────
        public int Status { get; set; }          // 0 = chưa ghép, 1 = đã ghép
        public string KetQua { get; set; } = "";

        // ── Phân loại vị trí ────────────────────────────────────────────────────
        public string Cua { get; set; } = "";
        public string Truyen { get; set; } = "";
        public string Gio { get; set; } = "";

        // ── Tìm kiếm nhanh ──────────────────────────────────────────────────────
        public string FindTem { get; set; } = "";

        // ── TGLUU là timestamp (rowversion) — chỉ đọc, không ghi ───────────────
        // Không ánh xạ vào entity; để Infrastructure tự bỏ qua khi INSERT/UPDATE.

        // ── Computed — không lưu DB ──────────────────────────────────────────────
        /// <summary>true khi dòng FCC đã được ghép với tem HVN.</summary>
        public bool DaQuetHVN => Status == 1 || !string.IsNullOrWhiteSpace(LotHVN);

        //-------------------------------------------------
        //YMVN
        public string Gear { get; set; } = "";  // ← thêm cho 100002
        public int SlGear { get; set; } = 0;
    }
}
