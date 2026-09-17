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
        /// <summary>
        /// Read-only FIFO evaluation. No LOT/TMP/DOCQR data is changed.
        /// </summary>
        List<FifoViolation> EvaluateFifoViolations(string tmpTable, string docQRTable);

        /// <summary>
        /// Applies the FIFO release after the caller has explicitly confirmed it.
        /// </summary>
        List<FifoViolation> ReleaseFifoViolations(string tmpTable, string docQRTable);
    }
}
