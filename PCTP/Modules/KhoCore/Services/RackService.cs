using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Services
{
    public sealed class RackService : IRackService
    {
        private readonly IRackRepository _repository;

        public RackService(
            IRackRepository repository)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
        }

        public List<Rack> GetByWarehouse(int warehouseId)
        {
            if (warehouseId <= 0)
                throw new ArgumentException(
                    "WarehouseId không hợp lệ.",
                    nameof(warehouseId));

            return _repository.GetRacksByWarehouse(warehouseId);
        }

        public Rack GetById(int rackId)
        {
            if (rackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.",
                    nameof(rackId));

            return _repository.GetById(rackId);
        }

        public int Create(
            int warehouseId,
            string rackName,
            int rowCount,
            int columnCount)
        {
            if (warehouseId <= 0)
                throw new ArgumentException(
                    "WarehouseId không hợp lệ.");

            if (string.IsNullOrWhiteSpace(rackName))
                throw new ArgumentException(
                    "Tên Rack không được rỗng.");

            if (rowCount <= 0)
                throw new ArgumentException(
                    "Số dòng Rack phải lớn hơn 0.");

            if (columnCount <= 0)
                throw new ArgumentException(
                    "Số cột Rack phải lớn hơn 0.");
            var rack = new Rack
            {
                WarehouseId = warehouseId,
                Name = rackName.Trim(),
                RackRowCount = rowCount,
                ColumnCount = columnCount
            };
            return _repository.Insert(
                            warehouseId,
                            rack);
        }

        public void UpdateLayout(
            int rackId,
            int rowCount,
            int columnCount)
        {
            if (rackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.");

            if (rowCount <= 0)
                throw new ArgumentException(
                    "Số dòng phải lớn hơn 0.");

            if (columnCount <= 0)
                throw new ArgumentException(
                    "Số cột phải lớn hơn 0.");

            _repository.UpdateLayout(
                rackId,
                rowCount,
                columnCount);
        }

        public void Delete(int rackId)
        {
            if (rackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.");

            _repository.DeleteCascade(rackId);
        }

        public List<RackRenderInfo> GetRackRenderInfos() => _repository.GetRackRenderInfos();
    }
}
