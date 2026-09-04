using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Services
{
    public sealed class PrintService : IPrintService
    {
        private readonly ISlotService _slotService;
        private readonly IWarehouseService _warehouseService;

        public PrintService(ISlotService slotService, IWarehouseService warehouseService)
        {
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
        }

        public PrintLotResult CreatePrintData(List<LotInfo> lots)
            => LotNoHelper.CreatePrintData(lots);

        public List<PXuatINModel> BuildExportPreview(
            int slotId,
            int slotNumber,
            int exportQty,
            string itemCode,
            string nguoiThucHien = "")
        {
            if (slotId <= 0)
                throw new ArgumentException("SlotId không hợp lệ.", nameof(slotId));
            if (exportQty <= 0)
                throw new ArgumentException("Số lượng xuất phải lớn hơn 0.", nameof(exportQty));

            var lots = _slotService.GetLots(slotId);
            int tongSoLuong = LotNoHelper.GetTotalQuantity(lots);

            if (exportQty > tongSoLuong)
                throw new InvalidOperationException("Số lượng xuất lớn hơn tồn kho.");

            var split = LotNoHelper.SubtractLots(lots, exportQty);
            var exportPrint = LotNoHelper.CreatePrintData(split.ExportLots);
            var remainPrint = LotNoHelper.CreatePrintData(split.RemainingLots);

            string productName = GetProductNameByCode(itemCode);

            var dataSource = new List<PXuatINModel>
            {
                PrintHelper.CreatePrintModel(
                    printData: exportPrint,
                    loaiPhieu: "PHIẾU XUẤT",
                    productName: productName,
                    slotNumber: slotNumber,
                    soLuongXuat: exportPrint.Quantity,
                    soLuongTon: remainPrint.Quantity,
                    nguoiThucHien: nguoiThucHien)
            };

            if (remainPrint.Quantity > 0)
            {
                dataSource.Add(PrintHelper.CreatePrintModel(
                    printData: remainPrint,
                    loaiPhieu: "PHIẾU NHẬP LẠI KHO",
                    productName: productName,
                    slotNumber: slotNumber,
                    // Giữ đúng hành vi gốc: dòng "nhập lại kho" vẫn hiển thị SoLuongXuat = SL đã xuất
                    soLuongXuat: exportPrint.Quantity,
                    soLuongTon: remainPrint.Quantity,
                    nguoiThucHien: nguoiThucHien));
            }

            return dataSource;
        }

        public string GetProductNameByCode(string itemCode)
            => _warehouseService.GetProductName(itemCode);
    }
}
