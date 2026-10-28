using System;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Shared.Common;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// Parity with legacy HoanTatSauKhiTruKho: close HangChoGiao rows of the delivered LOTs and write
    /// EXPORT_CONFIRMED_HVN history, inside the delivery command's transaction.
    /// </summary>
    public sealed class DeliveryHangChoGiaoPostStep : IDeliveryPostStockStep
    {
        private readonly IUnitOfWork _uow;
        private readonly IHangChoGiaoRepository _hangChoGiao;
        private readonly IStockHistoryRepository _history;

        public DeliveryHangChoGiaoPostStep(IUnitOfWork uow, IHangChoGiaoRepository hangChoGiao, IStockHistoryRepository history)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _hangChoGiao = hangChoGiao ?? throw new ArgumentNullException(nameof(hangChoGiao));
            _history = history ?? throw new ArgumentNullException(nameof(history));
        }

        public void Run(DeliveryStockCommand command)
        {
            if (!_uow.IsInTransaction)
                throw new InvalidOperationException("DeliveryHangChoGiaoPostStep phải tham gia transaction của delivery command.");

            var lots = command.Parts.SelectMany(p => p.Lots).Select(l => l.LotKey)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var closed = _hangChoGiao.CloseChoGiaoTheoLotAndReturn(_uow.Connection, _uow.Transaction, lots, command.PerformedBy);
            if (closed == null) return;

            foreach (var it in closed.Where(x => x.SlotIdNguon.HasValue))
                _history.SaveHistory(
                    "EXPORT_CONFIRMED_HVN", it.MaHang,
                    new LotInfo { LotNo = it.LotGoc, Quantity = it.SoLuong, TemCode = it.LotThung },
                    fromSlotId: it.SlotIdNguon, toSlotId: null, performedBy: command.PerformedBy);
        }
    }
}
