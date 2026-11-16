using System;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Định danh LOT trong MES ledger: (SiteCode, PartNo, MaterialLotId).
    /// LotNo chỉ là nhãn hiển thị/tra cứu; mọi lệnh kho phải đi bằng khoá này.
    /// </summary>
    public sealed class StockLotKey
    {
        public StockLotKey(string siteCode, string partNo, string lotNo, long materialLotId)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", nameof(siteCode));
            if (string.IsNullOrWhiteSpace(partNo)) throw new ArgumentException("PartNo là bắt buộc.", nameof(partNo));
            if (materialLotId <= 0) throw new ArgumentOutOfRangeException(nameof(materialLotId));
            SiteCode = siteCode.Trim();
            PartNo = partNo.Trim();
            LotNo = lotNo == null ? null : lotNo.Trim();
            MaterialLotId = materialLotId;
        }

        public string SiteCode { get; private set; }
        public string PartNo { get; private set; }
        public string LotNo { get; private set; }
        public long MaterialLotId { get; private set; }

        public override string ToString()
        {
            return SiteCode + "/" + PartNo + "/" + LotNo + "#" + MaterialLotId;
        }
    }

    /// <summary>Một dòng phát sinh gửi tới dbo.usp_FVN_PCTP_PostStockMovement.</summary>
    public sealed class StockLedgerEntry
    {
        /// <summary>RECEIPT, MOVE, ISSUE, RETURN, SCRAP, ADJUST_IN, ADJUST_OUT, QUARANTINE, RELEASE.</summary>
        public string MovementType { get; set; }
        public StockLotKey Lot { get; set; }
        public decimal Quantity { get; set; }
        public int? FromSlotId { get; set; }
        public int? ToSlotId { get; set; }
        public string FromStockState { get; set; }
        public string ToStockState { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceNo { get; set; }
        public int? ReferenceLineNo { get; set; }
        /// <summary>Bắt buộc. Cùng khoá + cùng dữ liệu = replay an toàn; cùng khoá khác dữ liệu = SQL từ chối.</summary>
        public string IdempotencyKey { get; set; }
        public string ActorCode { get; set; }
        public DateTime? OccurredAtUtc { get; set; }
    }
}
