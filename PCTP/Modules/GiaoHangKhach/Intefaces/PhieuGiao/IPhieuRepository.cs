

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    public interface IPhieuRepository :
    IPhieuTmpRepository, IPhieuValidationRepository, IPhieuLotRepository,
    IPhieuKhoRepository, IPhieuLuuTruRepository,
    IPhieuGiaoDBRepository
    {
        DataTable LoadHangThieu(bool isMayBanQR, string tenBan);
        Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList);
        void ExecNonQuery(string spName);
        void ExecSP(string spName, params SqlParameter[] parms);
        DataTable ExecSPWithResult(string spName, params SqlParameter[] parms);
    }
}
