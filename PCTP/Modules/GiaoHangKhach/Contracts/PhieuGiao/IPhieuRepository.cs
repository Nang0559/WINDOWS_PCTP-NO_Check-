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
        /// Applies the FIFO release only to the rows explicitly confirmed by the user.
        /// </summary>
        void ReleaseFifoViolations(string tmpTable, string docQRTable, IReadOnlyList<FifoViolation> violations);
    }
}
