using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Services
{
    public sealed class StockExportValidationService : IStockExportValidationService
    {
        private readonly IStockExportRepository _stockTpRepo;
        private readonly IStockExportHistoryRepository _historyRepo;

        public StockExportValidationService(
            IStockExportRepository stockTpRepo,
            IStockExportHistoryRepository historyRepo)
        {
            _stockTpRepo = stockTpRepo ?? throw new ArgumentNullException(nameof(stockTpRepo));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
        }

        public StockExportValidationResult ValidatePickToChoGiao(StockExportRequest request)
        {
            var basic = ValidateBasic(request, requireSlotId: true);
            if (!basic.IsValid) return basic;

            if (request.ReferenceType.HasValue && request.ReferenceId.HasValue)
            {
                bool daPick = _historyRepo.ExistsHistoryForReference(
                    StockHistoryActionType.ChoGiao, request.ReferenceType.Value, request.ReferenceId.Value);

                if (daPick)
                    return StockExportValidationResult.Fail(StockExportStatus.Duplicate,
                        $"Chứng từ [{request.ReferenceType}#{request.ReferenceId}] đã được pick chờ giao trước đó.");
            }

            // Không check STOCKTP ở đây — bước này chỉ đụng Slot/SlotLot.
            return StockExportValidationResult.Ok();
        }

        public StockExportValidationResult ValidateXuatTrucTiep(StockExportRequest request)
        {
            var basic = ValidateBasic(request, requireSlotId: request.Source == StockExportSource.Slot);
            if (!basic.IsValid) return basic;

            if (request.ReferenceType.HasValue && request.ReferenceId.HasValue)
            {
                bool daXuat = _historyRepo.ExistsHistoryForReference(
                    StockHistoryActionType.Export, request.ReferenceType.Value, request.ReferenceId.Value);

                if (daXuat)
                    return StockExportValidationResult.Fail(StockExportStatus.Duplicate,
                        $"Chứng từ [{request.ReferenceType}#{request.ReferenceId}] đã được xuất kho trước đó.");
            }

            int slConLai = _stockTpRepo.GetSlConLai(request.LotNo);
            if (slConLai < request.SoLuong)
                return StockExportValidationResult.Fail(StockExportStatus.InsufficientStock,
                    $"LOT [{request.LotNo}] chỉ còn {slConLai} trong STOCKTP, không đủ {request.SoLuong}.");

            return StockExportValidationResult.Ok();
        }

        private static StockExportValidationResult ValidateBasic(StockExportRequest request, bool requireSlotId)
        {
            if (request == null)
                return StockExportValidationResult.Fail(StockExportStatus.Failed, "Request rỗng.");
            if (string.IsNullOrWhiteSpace(request.LotNo))
                return StockExportValidationResult.Fail(StockExportStatus.Failed, "Thiếu LotNo.");
            if (request.SoLuong <= 0)
                return StockExportValidationResult.Fail(StockExportStatus.Failed, "Số lượng phải lớn hơn 0.");
            if (requireSlotId && (!request.SlotId.HasValue || request.SlotId.Value <= 0))
                return StockExportValidationResult.Fail(StockExportStatus.Failed, "Thiếu SlotId nguồn.");

            return StockExportValidationResult.Ok();
        }
    }
}
