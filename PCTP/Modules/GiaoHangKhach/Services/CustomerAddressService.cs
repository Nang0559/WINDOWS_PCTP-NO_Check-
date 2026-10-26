using PCTP.Domain.Interfaces;
using PCTP.Infrastructure.Repositories;
using PCTP.Shared.Models;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Customer-bound address read boundary for the delivery module.
    /// UI code must not create or call IFSRepository directly.
    /// </summary>
    public sealed class CustomerAddressService
    {
        private readonly IIFSRepository _ifsRepository;

        public CustomerAddressService(IIFSRepository ifsRepository)
        {
            if (ifsRepository == null)
                throw new ArgumentNullException(nameof(ifsRepository));

            _ifsRepository = ifsRepository;
        }

        public DataTable GetAddress(string customerNo)
        {
            var config = CustomerTableConfig.GetForDelivery(customerNo);
            return _ifsRepository.GetCustomerAddress(config.CustomerNo) ?? new DataTable();
        }
    }
}
