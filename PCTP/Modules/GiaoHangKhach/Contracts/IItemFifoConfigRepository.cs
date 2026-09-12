using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Intefaces
{
    public interface IItemFifoConfigRepository
    {
        DataTable GetAll();
        bool GetEnforceFifo(string itemCode);
        void Upsert(string itemCode, bool enforceFifo, string nguoiThucHien);   // ★ SỬA — thêm tham số
        void Delete(string itemCode, string nguoiThucHien);                     // ★ SỬA — thêm tham số
        DataTable GetHistory(string itemCode);                                  // ★ MỚI

        DataTable GetDanhSachMaHangKhaDung();
    }
}
