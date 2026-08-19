using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Services
{
    public sealed class WarehouseService : IWarehouseService
    {
        private readonly IWarehouseRepository _repository;

        public WarehouseService(
            IWarehouseRepository repository)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
        }

        public List<Warehouse> GetAll()
        {
            return _repository.GetAllWarehouses();
        }

        public void Save(Warehouse warehouse)
        {
            if (warehouse == null)
                throw new ArgumentNullException(nameof(warehouse));

            if (string.IsNullOrWhiteSpace(warehouse.Name))
                throw new ArgumentException(
                    "Tên kho không được rỗng.",
                    nameof(warehouse));

            _repository.SaveWarehouse(warehouse);
        }

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
