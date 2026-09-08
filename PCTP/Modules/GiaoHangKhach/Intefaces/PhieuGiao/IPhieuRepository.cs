

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    public interface IPhieuRepository :
     IPhieuValidationRepository,
     IPhieuTmpRepository,
     IPhieuLotRepository,
     IPhieuKhoRepository,
     IPhieuLuuTruRepository,
     IPhieuGiaoDBRepository
    {
    }
}