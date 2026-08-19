using PCTP.Modules.XuatKho.Models;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Interfaces
{
    public interface IHangChoGiaoRepository
    {
        int Insert(HangChoGiao item);

        /// <summary>Đọc kèm khoá dòng (SELECT ... WITH (UPDLOCK, ROWLOCK)) — PHẢI gọi trong transaction.</summary>
        HangChoGiao GetForUpdate(int id);

        HangChoGiao GetById(int id);

        void UpdateStatus(int id, HangChoGiaoStatus status, string nguoiGiao = null);

        List<HangChoGiao> GetByReference(StockExportReferenceType type, int referenceId, HangChoGiaoStatus? status = null);
    }
}
