using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Common
{
    /// <summary>
    /// Gom nhóm 3 tên bảng luôn đi cùng nhau trong mọi thao tác bắn QR/CNK
    /// (tmp table đang bắn, bảng nguồn IFS/order, bảng tạm đọc QR). Thay thế
    /// việc truyền 3-5 tham số string rời rạc lặp lại ở gần như mọi method
    /// của PhieuRepository.
    /// </summary>
    public class PhieuTableSet
    {
        public string TmpTable { get; }
        public string SourceTable { get; }   // IFS table HOẶC OrderTable riêng
        public string DocQRTable { get; }
        public string TenBan { get; }        // view table (khi máy chỉ-xem)
        public string IfsView { get; }

        public PhieuTableSet(string tmpTable, string sourceTable, string docQRTable,
            string tenBan = null, string ifsView = null)
        {
            TmpTable = tmpTable;
            SourceTable = sourceTable;
            DocQRTable = docQRTable;
            TenBan = tenBan;
            IfsView = ifsView;
        }

        /// <summary>Tạo từ CustomerConfig — chọn đúng bộ bảng SP hay thường.</summary>
        public static PhieuTableSet FromConfig(CustomerConfig cfg, bool isSP, string tenBanView = null)
            => new PhieuTableSet(
                cfg.GetTmpTable(isSP),
                cfg.LoadTuBangRieng ? cfg.OrderTable : cfg.GetIfsTable(isSP),
                cfg.GetDocQRTable(isSP),
                tenBanView,
                cfg.GetIfsViewTable(isSP));
    }
}
