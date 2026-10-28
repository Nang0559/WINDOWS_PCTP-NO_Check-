using System;
using PCTP.Modules.XuLyHangLoi.Enums;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Pure quantity/status rules for Rework export and delivery (no DB, no side effects).
    /// Callers MUST evaluate this inside the transaction after taking QT_REWORK_TICKET:{id}
    /// and after re-reading the ticket status and the export/delivery ledgers.
    ///
    /// A = approved Rework quantity (Initial QC), X = exported, D = delivered.
    /// Invariants: D &lt;= X &lt;= A. "Fully delivered" means D == A.
    /// Export cap is A - X (never exceed the approved quantity); the gate "A - D &gt; 0"
    /// only decides whether the ticket still accepts any movement.
    /// </summary>
    public static class ReworkQuantityPolicy
    {
        public static ReworkQuantityDecision EvaluateExport(
            QTChungStatus status, bool canRework, int approved, int exported, int delivered, int quantity)
        {
            if (!canRework) return ReworkQuantityDecision.Deny("Chỉ phiếu CanRework mới được xuất Rework.");
            if (quantity <= 0) return ReworkQuantityDecision.Deny("Số lượng xuất Rework phải lớn hơn 0.");
            if (approved <= 0) return ReworkQuantityDecision.Deny("QC ban đầu không phân bổ số lượng Rework.");
            if (delivered >= approved)
                return ReworkQuantityDecision.Deny("Phiếu đã giao đủ Rework; không nhận xuất thêm.");
            if (status != QTChungStatus.DaDinhHuong && status != QTChungStatus.DaXuatKhoRework)
                return ReworkQuantityDecision.Deny($"Không thể xuất Rework khi trạng thái là {status}.");
            if (exported + quantity > approved)
                return ReworkQuantityDecision.Deny(
                    $"Xuất Rework vượt số lượng được QC phân bổ. Được phép={approved:n0}, đã xuất={exported:n0}, yêu cầu thêm={quantity:n0}.");
            return ReworkQuantityDecision.Allow(false);
        }

        public static ReworkQuantityDecision EvaluateDelivery(
            QTChungStatus status, bool canRework, int approved, int exported, int delivered, int quantity)
        {
            if (!canRework) return ReworkQuantityDecision.Deny("Chỉ phiếu CanRework mới được giao sản xuất.");
            if (quantity <= 0) return ReworkQuantityDecision.Deny("Số lượng giao Rework phải lớn hơn 0.");
            if (approved <= 0) return ReworkQuantityDecision.Deny("QC ban đầu không phân bổ số lượng Rework.");
            if (delivered >= approved)
                return ReworkQuantityDecision.Deny("Phiếu đã giao đủ Rework; không nhận giao thêm.");
            if (status != QTChungStatus.DaXuatKhoRework)
                return ReworkQuantityDecision.Deny($"Chỉ được giao Rework khi phiếu đã xuất kho Rework. Trạng thái hiện tại: {status}.");
            if (delivered + quantity > exported)
                return ReworkQuantityDecision.Deny(
                    $"Giao Rework vượt số lượng đã xuất. Đã xuất={exported:n0}, đã giao={delivered:n0}, yêu cầu thêm={quantity:n0}.");
            if (delivered + quantity > approved)
                return ReworkQuantityDecision.Deny("Tổng giao Rework vượt số lượng được QC phân bổ.");
            return ReworkQuantityDecision.Allow(delivered + quantity == approved);
        }
    }

    public sealed class ReworkQuantityDecision
    {
        public bool Allowed { get; private set; }
        public string Message { get; private set; }
        /// <summary>Delivery only: this movement brings D to A, so the ticket moves to DaGiaoSanXuat.</summary>
        public bool CompletesDelivery { get; private set; }

        private ReworkQuantityDecision(bool allowed, string message, bool completes)
        {
            Allowed = allowed;
            Message = message;
            CompletesDelivery = completes;
        }

        public static ReworkQuantityDecision Allow(bool completesDelivery)
        {
            return new ReworkQuantityDecision(true, null, completesDelivery);
        }

        public static ReworkQuantityDecision Deny(string message)
        {
            return new ReworkQuantityDecision(false, message, false);
        }
    }
}
