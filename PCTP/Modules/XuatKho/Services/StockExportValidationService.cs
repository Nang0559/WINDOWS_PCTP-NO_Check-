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
            _stockTpRepo = stockTpRepo
                ?? throw new ArgumentNullException(nameof(stockTpRepo));

            _historyRepo = historyRepo
                ?? throw new ArgumentNullException(nameof(historyRepo));
        }

        // ============================================================
        // PICK -> CHỜ GIAO
        // ============================================================

        public StockExportValidationResult ValidatePickToChoGiao(
            StockExportRequest request)
        {
            var basic = ValidateBasic(
                request,
                requireSlotId: true);

            if (!basic.IsValid)
                return basic;

            // Kiểm tra chứng từ đã Pick trước đó chưa
            if (request.ReferenceType.HasValue &&
                request.ReferenceId.HasValue)
            {
                bool daPick = _historyRepo.ExistsHistoryForReference(
                    StockHistoryActionType.ChoGiao,
                    request.ReferenceType.Value,
                    request.ReferenceId.Value);

                if (daPick)
                {
                    return StockExportValidationResult.Fail(
                        StockExportStatus.Duplicate,
                        $"Chứng từ [{request.ReferenceType}#{request.ReferenceId}] " +
                        $"đã được pick chờ giao trước đó.");
                }
            }

            // --------------------------------------------------------
            // Không kiểm tra STOCKTP ở bước này.
            //
            // Pick chờ giao chỉ làm việc với:
            //     Slot / SlotLot
            //
            // Việc trừ STOCKTP thực hiện ở bước xuất kho thực tế.
            // --------------------------------------------------------

            return StockExportValidationResult.Ok();
        }


        // ============================================================
        // XUẤT TRỰC TIẾP
        // ============================================================

        public StockExportValidationResult ValidateXuatTrucTiep(
            StockExportRequest request)
        {
            var basic = ValidateBasic(
                request,
                requireSlotId: request?.Source == StockExportSource.Slot);

            if (!basic.IsValid)
                return basic;

            // Kiểm tra chứng từ đã xuất trước đó chưa
            if (request.ReferenceType.HasValue &&
                request.ReferenceId.HasValue)
            {
                bool daXuat = _historyRepo.ExistsHistoryForReference(
                    StockHistoryActionType.Export,
                    request.ReferenceType.Value,
                    request.ReferenceId.Value);

                if (daXuat)
                {
                    return StockExportValidationResult.Fail(
                        StockExportStatus.Duplicate,
                        $"Chứng từ [{request.ReferenceType}#{request.ReferenceId}] " +
                        $"đã được xuất kho trước đó.");
                }
            }

            // --------------------------------------------------------
            // Kiểm tra tồn STOCKTP
            // --------------------------------------------------------

            int slConLai = _stockTpRepo.GetSlConLai(
                request.LotNo);

            if (slConLai < request.Quantity)
            {
                return StockExportValidationResult.Fail(
                    StockExportStatus.InsufficientStock,
                    $"LOT [{request.LotNo}] chỉ còn {slConLai} " +
                    $"trong STOCKTP, không đủ {request.Quantity}.");
            }

            return StockExportValidationResult.Ok();
        }


        // ============================================================
        // VALIDATE CƠ BẢN
        // ============================================================

        private static StockExportValidationResult ValidateBasic(
            StockExportRequest request,
            bool requireSlotId)
        {
            if (request == null)
            {
                return StockExportValidationResult.Fail(
                    StockExportStatus.Failed,
                    "Request rỗng.");
            }

            if (string.IsNullOrWhiteSpace(request.LotNo))
            {
                return StockExportValidationResult.Fail(
                    StockExportStatus.Failed,
                    "Thiếu LotNo.");
            }

            if (string.IsNullOrWhiteSpace(request.ItemCode))
            {
                return StockExportValidationResult.Fail(
                    StockExportStatus.Failed,
                    "Thiếu ItemCode.");
            }

            if (request.Quantity <= 0)
            {
                return StockExportValidationResult.Fail(
                    StockExportStatus.Failed,
                    "Số lượng phải lớn hơn 0.");
            }

            // --------------------------------------------------------
            // Source = Slot
            //     => bắt buộc chỉ rõ SlotId nguồn
            //
            // Source = KhoAoA0
            //     => không bắt buộc SlotId
            //     => StockExportService tự resolve slot ảo A0
            // --------------------------------------------------------

            if (requireSlotId &&
                (!request.SlotId.HasValue ||
                 request.SlotId.Value <= 0))
            {
                return StockExportValidationResult.Fail(
                    StockExportStatus.Failed,
                    "Thiếu SlotId nguồn.");
            }

            return StockExportValidationResult.Ok();
        }
    }
}
