using System;
using PCTP.Infrastructure.Security;
using System.Data;
namespace PCTP.Modules.MesPlanning
{
    public sealed class ComponentDemandService : IComponentDemandService
    {
        private readonly IComponentDemandRepository _repo;
        public ComponentDemandService(IComponentDemandRepository repo){_repo=repo??throw new ArgumentNullException(nameof(repo));}
        public DataTable GetPlanLines(string site){AuthorizationGuard.DemandScope("MES.COMPONENT_ISSUE","SITE",site,site);return _repo.GetPlanLines(site);}
        public long GenerateDemand(long id,string siteCode,string user)
        { AuthorizationGuard.DemandActionAndScope("MES.COMPONENT_ISSUE","CONFIRM","SITE",siteCode,siteCode); if(id<=0) throw new ArgumentException("ProductionPlanLineId không hợp lệ."); if(string.IsNullOrWhiteSpace(user)) throw new ArgumentException("Thiếu người thao tác."); return _repo.GenerateDemand(id,user.Trim()); }
            user = AuthorizationGuard.ActorCode;
        public DataTable GetDemandLines(long id,string siteCode){AuthorizationGuard.DemandActionAndScope("MES.COMPONENT_ISSUE","VIEW","SITE",siteCode,siteCode);return _repo.GetDemandLines(id);}
        public DataTable GetPartLots(string site,string part,string areaCode){AuthorizationGuard.DemandScope("MES.COMPONENT_ISSUE","SITE",site,site);return _repo.GetPartLots(site,part,areaCode);}
        public DataTable GetWipSlots(string site){AuthorizationGuard.DemandScope("MES.COMPONENT_ISSUE","SITE",site,site);return _repo.GetWipSlots(site);}
        public DataTable PostIssue(string site,string no,long line,long lot,int from,int to,decimal qty,string user)
        {
            user = AuthorizationGuard.ActorCode;
            AuthorizationGuard.DemandActionAndScope("MES.COMPONENT_ISSUE","CONFIRM","SITE",site,site);
            if(string.IsNullOrWhiteSpace(site)||string.IsNullOrWhiteSpace(no)||line<=0||lot<=0||from<=0||to<=0||from==to||qty<=0||string.IsNullOrWhiteSpace(user))
                throw new ArgumentException("Thông tin xuất linh kiện chưa đầy đủ hoặc không hợp lệ.");
            return _repo.PostIssue(site.Trim(),no.Trim(),line,lot,from,to,qty,user.Trim(),Guid.NewGuid().ToString("N"));
        }
    }
}