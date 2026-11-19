using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Shared.Common;
using PCTP.Infrastructure.Security;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Data;


namespace PCTP.Modules.KhoVatLy.Application.Services
{
    public sealed class WarehouseService : IWarehouseService
    {
        private readonly IWarehouseRepository _repository;
        private readonly IRackRepository _rackRepo;
        private readonly IUnitOfWork _uow;
        public WarehouseService(
            IWarehouseRepository repository, IRackRepository rackRepo, IUnitOfWork uow)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
            _rackRepo = rackRepo
                ?? throw new ArgumentNullException(nameof(rackRepo));
            _uow = uow
                ?? throw new ArgumentNullException(nameof(uow));
        }
        public void RegisterWarehouseAndRack(
        string warehouseName, string rackName,
        int rowCount, int columnCount, int slotCapacity)
        {
            AuthorizationGuard.DemandActionAndScope("WAREHOUSE.STOCK_ADJUST", "ADMIN", "WAREHOUSE", warehouseName, null);
            if (string.IsNullOrWhiteSpace(warehouseName))
                throw new ArgumentException("Tên kho không được rỗng.");
            if (string.IsNullOrWhiteSpace(rackName))
                throw new ArgumentException("Tên Rack không được rỗng.");
            if (rowCount <= 0 || columnCount <= 0)
                throw new ArgumentException("Số hàng/cột phải lớn hơn 0.");
            if (slotCapacity <= 0)
                throw new ArgumentException("Sức chứa Slot phải lớn hơn 0.");

            _uow.Begin();
            try
            {
                int warehouseId = _repository.GetIdByName(warehouseName);
                if (warehouseId <= 0)
                    warehouseId = _repository.Insert(warehouseName);

                int rackId = _rackRepo.Create(warehouseId, rackName, rowCount, columnCount);

                int slotCount = rowCount * columnCount;
                for (int i = 1; i <= slotCount; i++)
                    _rackRepo.InsertSlot(rackId, i, slotCapacity);

                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }
        public List<Warehouse> GetAll()
        {
            var session = AuthorizationGuard.Demand("WAREHOUSE.STOCK_VIEW", "VIEW");
            return (_repository.GetAllWarehouses() ?? new List<Warehouse>())
                .Where(w => w != null && session.HasDataScope(
                    "WAREHOUSE.STOCK_VIEW", "WAREHOUSE", w.Name, null)).ToList();
        }
        public bool Exists(string warehouseName)
        {
            AuthorizationGuard.DemandActionAndScope("WAREHOUSE.STOCK_VIEW", "VIEW", "WAREHOUSE", warehouseName, null);
            if (string.IsNullOrWhiteSpace(warehouseName)) return false;
            return _repository.Exists(warehouseName);
        }
        public List<string> GetRackNames(string warehouseName)
        {
            AuthorizationGuard.DemandActionAndScope("WAREHOUSE.STOCK_VIEW", "VIEW", "WAREHOUSE", warehouseName, null);
            if (string.IsNullOrWhiteSpace(warehouseName))
                return new List<string>();
            return _repository.GetRackNames(warehouseName);
        }
        public DataTable GetActiveItemList()
        {
            AuthorizationGuard.Demand("WAREHOUSE.STOCK_VIEW", "VIEW");
            return _repository.GetActiveItemList();
        }

        public string GetProductName(string itemCode)
        {
            AuthorizationGuard.Demand("WAREHOUSE.STOCK_VIEW", "VIEW");
            if (string.IsNullOrWhiteSpace(itemCode))
                return string.Empty;

            return _repository.GetProductNameByCode(itemCode);
        }
        public InspectionConfig GetInspectionConfig(string itemCode)
        {
            AuthorizationGuard.Demand("SHIPPING.DISPATCH", "VIEW");
            if (string.IsNullOrWhiteSpace(itemCode)) return null;
            return _repository.GetInspectionConfig(itemCode);
        }

    }
}
