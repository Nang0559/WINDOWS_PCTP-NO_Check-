using PCTP.Modules.GiaoHangKhach;
using System.Collections.Generic;

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
        List<FifoViolation> ReleaseFifoViolations(string tmpTable, string docQRTable);
    }
}
