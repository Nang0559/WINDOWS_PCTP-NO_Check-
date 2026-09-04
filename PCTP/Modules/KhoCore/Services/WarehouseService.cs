using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            return _repository.GetAllWarehouses();
        }
        public bool Exists(string warehouseName)
        {
            if (string.IsNullOrWhiteSpace(warehouseName)) return false;
            return _repository.Exists(warehouseName);
        }
        public List<string> GetRackNames(string warehouseName)
        {
            if (string.IsNullOrWhiteSpace(warehouseName))
                return new List<string>();
            return _repository.GetRackNames(warehouseName);
        }
        public DataTable GetActiveItemList() => _repository.GetActiveItemList();

        public string GetProductName(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                return string.Empty;

            return _repository.GetProductNameByCode(itemCode);
        }
        public InspectionConfig GetInspectionConfig(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return null;
            return _repository.GetInspectionConfig(itemCode);
        }

    }
}
