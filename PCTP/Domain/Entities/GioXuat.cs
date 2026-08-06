using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Entities
{
    /// <summary>
    /// Value object đại diện cho một giờ xuất hàng.
    /// Bất biến (immutable) — không có setter công khai.
    /// </summary>
    public sealed class GioXuat
    {
        /// <summary>
        /// Mã SQL dùng trong câu truy vấn Oracle, ví dụ: "'06'", "'06','07'", "#".
        /// </summary>
        public string Ma { get; }

        /// <summary>
        /// Nhãn hiển thị trên UI, ví dụ: "(6H)", "(O TYPE 2)", "(GIAO DB)".
        /// </summary>
        public string MoTa { get; }

        public GioXuat(string ma, string moTa)
        {
            Ma = ma;
            MoTa = moTa;
        }

        // ── Value equality ────────────────────────────────────────────────────────
        public override bool Equals(object obj) =>
            obj is GioXuat other && Ma == other.Ma;

        public override int GetHashCode() => Ma.GetHashCode();

        public override string ToString() => MoTa;
    }
}
