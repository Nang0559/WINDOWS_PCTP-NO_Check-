using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Intefaces
{
    /// <summary>
    /// Khóa nhận diện 1 dòng trong tbl_QR_ComparePart khi xóa.
    /// Bảng này không có PK và STT cho phép NULL/trùng, nên xóa theo cả bộ
    /// (STT, PartNo, PartNoCompare) để không xóa nhầm dòng khác.
    /// </summary>
    public sealed class ComparePartKey : IEquatable<ComparePartKey>
    {
        public ComparePartKey(int? stt, string partNo, string partNoCompare)
        {
            Stt = stt;
            PartNo = partNo ?? string.Empty;
            PartNoCompare = partNoCompare ?? string.Empty;
        }

        public int? Stt { get; }
        public string PartNo { get; }
        public string PartNoCompare { get; }

        public bool Equals(ComparePartKey other)
        {
            return other != null
                && Stt == other.Stt
                && string.Equals(PartNo, other.PartNo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(PartNoCompare, other.PartNoCompare, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as ComparePartKey);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Stt.GetHashCode();
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(PartNo);
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(PartNoCompare);
                return hash;
            }
        }
    }

    /// <summary>
    /// Truy cập dữ liệu mapping mã hàng (tbl_QR_ComparePart) cho form ComaparePart.
    /// </summary>
    public interface IComparePartRepository
    {
        /// <summary>Danh sách mã gốc (B20Item): cột code, name.</summary>
        DataTable GetSourceParts();

        /// <summary>Danh sách mã thay thế bắt đầu bằng <paramref name="partNoPrefix"/>: cột code, name.</summary>
        DataTable GetTargetParts(string partNoPrefix);

        /// <summary>Danh sách mapping: STT, PartNo, PartName, ToPartNo, ToPartName, timeSet, IsActive.</summary>
        DataTable GetAll();

        /// <summary>Tạo mapping. Trả false nếu cặp (PartNo, PartNoCompare) đã tồn tại.</summary>
        bool TryInsert(string partNo, string partNoCompare, DateTime timeSet);

        /// <summary>Xóa các mapping trong 1 transaction. Trả về số dòng đã xóa.</summary>
        int Delete(IEnumerable<ComparePartKey> keys);
    }
}
