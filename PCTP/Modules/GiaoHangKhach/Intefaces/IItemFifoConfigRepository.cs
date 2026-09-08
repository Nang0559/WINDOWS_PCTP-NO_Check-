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
        void Upsert(string itemCode, bool enforceFifo);
        void Delete(string itemCode);
    }
}
