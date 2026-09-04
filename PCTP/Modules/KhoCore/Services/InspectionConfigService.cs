using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Services
{
    public sealed class InspectionConfigService : IInspectionConfigService
    {
        private readonly IInspectionConfigRepository _repo;

        public InspectionConfigService(IInspectionConfigRepository repo)
            => _repo = repo
                ?? throw new ArgumentNullException(nameof(repo));

        public List<InspectionConfig> GetAll() => _repo.GetAll();

        public InspectionConfig GetByItemCode(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return null;
            return _repo.GetByItemCode(itemCode);
        }

        public void Save(InspectionConfig cfg)
        {
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.ItemCode))
                throw new ArgumentException("Mã hàng không được rỗng.");
            if (cfg.DefaultQty <= 0)
                throw new ArgumentException("Số thùng KT phải lớn hơn 0.");

            if (cfg.ConfigId > 0)
                _repo.Update(cfg);
            else
                cfg.ConfigId = _repo.Insert(cfg);
        }

        public void Delete(int configId)
        {
            if (configId <= 0)
                throw new ArgumentException("ConfigId không hợp lệ.");
            _repo.Delete(configId);
        }

        // ✅ Dùng trong luồng xuất kho — kiểm tra nhanh
        public bool NeedsInspection(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return false;
            var cfg = _repo.GetByItemCode(itemCode);
            return cfg != null && cfg.IsActive;
        }
    }
}
