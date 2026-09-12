using PCTP.Domain.Entities;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.WorkingState;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.WorkingState
{
    /// <summary>
    /// Phase 4 — implementation của <see cref="IDeliveryWorkingState"/>.
    /// KHÔNG tự viết SQL — chỉ điều phối <see cref="IPhieuTmpRepository"/> và
    /// <see cref="IPhieuValidationRepository"/> (roadmap mục 11: "Persistence vẫn
    /// do PhieuTmpRepository chịu trách nhiệm"). Mọi tên bảng đều suy từ
    /// context.Cfg.Delivery + Category — không nhận CustomerConfig rời qua
    /// constructor để khỏi lệch với context truyền vào từng lời gọi.
    /// </summary>
    public class DeliveryWorkingState : IDeliveryWorkingState
    {
        private readonly IPhieuTmpRepository _tmpRepo;
        private readonly IPhieuValidationRepository _validationRepo;

        public DeliveryWorkingState(
            IPhieuTmpRepository tmpRepo,
            IPhieuValidationRepository validationRepo)
        {
            _tmpRepo = tmpRepo ?? throw new ArgumentNullException(nameof(tmpRepo));
            _validationRepo = validationRepo ?? throw new ArgumentNullException(nameof(validationRepo));
        }

        // ════════════════════════════════════════════════════════════════
        // HasQr
        // ════════════════════════════════════════════════════════════════
        public bool HasQr(OrderLoadContext context)
        {
            var tables = BuildTables(context);
            return _validationRepo.CountDocQRCode(tables.DocQRTable) > 0;
        }

        // ════════════════════════════════════════════════════════════════
        // GetTrangThaiDangBan
        // ════════════════════════════════════════════════════════════════
        public TrangThaiBan GetTrangThaiDangBan(OrderLoadContext context)
        {
            var tables = BuildTables(context);

            // Giữ nguyên behavior cũ của PhieuService.GetTrangThaiDangBan():
            // CoGear (YMVN) dùng logic kiểm tra khác (ADDNM=0 + đếm QR),
            // không phải mọi nguồn có Category=SP đều là YMVN.
            return context.Cfg.Delivery.CoGear
                ? _tmpRepo.GetTrangThaiDangBanYMVN(tables.TmpTable, tables.DocQRTable)
                : _tmpRepo.GetTrangThaiDangBan(tables.TmpTable, tables.DocQRTable);
        }

        // ════════════════════════════════════════════════════════════════
        // LoadFromQr
        // ════════════════════════════════════════════════════════════════
        public DataTable LoadFromQr(OrderLoadContext context)
        {
            var tables = BuildTables(context);
            return _tmpRepo.LoadPhieuDocQR(
                NgayGiaoSp(context), context.NhaMay, context.GioFcc, context.AddNm, tables);
        }

        // ════════════════════════════════════════════════════════════════
        // LoadCurrentOrder — chỉ dùng cho MachineRole.DuocBanQR.
        // Máy ChiXem dùng tên bảng view riêng (_tenBan trong PhieuService cũ),
        // KHÔNG suy được từ context — vẫn phải đi qua GetDonHangHienTai(tenBan)
        // ở tầng repo cho tới khi context có field tương ứng (Phase 7).
        // ════════════════════════════════════════════════════════════════
        public DataTable LoadCurrentOrder(OrderLoadContext context)
        {
            if (context.MachineRole != MachineRole.DuocBanQR)
                throw new InvalidOperationException(
                    "LoadCurrentOrder(context) chỉ hỗ trợ MachineRole.DuocBanQR. " +
                    "Máy ChiXem cần dùng bảng view riêng — gọi thẳng repo với tenBan tương ứng.");

            var tables = BuildTables(context);
            return _tmpRepo.GetDonHangHienTai(tables.TmpTable);
        }

        // ════════════════════════════════════════════════════════════════
        // SaveFromSource — facade cho LuuVaLoad(), cầu nối Order Source →
        // Delivery Working State (thay thế SyncChoDocQR() bỏ hoang ở Phase 3).
        // ════════════════════════════════════════════════════════════════
        public DataTable SaveFromSource(
            OrderLoadContext context, DataTable orders, string storedProcedure)
        {
            ValidateContext(context);
            if (orders == null) throw new ArgumentNullException(nameof(orders));
            if (string.IsNullOrWhiteSpace(storedProcedure))
                throw new ArgumentNullException(nameof(storedProcedure));

            var tables = BuildTables(context);
            return _tmpRepo.LuuVaLoad(
                tables, storedProcedure, orders,
                NgayGiaoSp(context), context.NhaMay, context.GioFcc, context.AddNm);
        }

        // ════════════════════════════════════════════════════════════════
        // ClearTmp / ClearDocQr
        // ════════════════════════════════════════════════════════════════
        public void ClearTmp(OrderLoadContext context)
            => _tmpRepo.XoaTmpPhieu(BuildTables(context).TmpTable);

        public void ClearDocQr(OrderLoadContext context)
            => _tmpRepo.XoaDocQRCode(BuildTables(context).DocQRTable);

        // ════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════
        /// <summary>
        /// Guard dùng chung cho MỌI method public — trước đây chỉ BuildTables()
        /// kiểm tra context/Cfg.Delivery null, khiến SaveFromSource gọi
        /// ValidateContext(context) nhưng hàm này chưa từng được định nghĩa
        /// (lỗi biên dịch). Tách riêng ra khỏi BuildTables để các method không
        /// cần bảng (không gọi BuildTables) vẫn được validate context đầu vào.
        /// </summary>
        private static void ValidateContext(OrderLoadContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.Cfg?.Delivery == null)
                throw new ArgumentException(
                    "OrderLoadContext.Cfg.Delivery không được null.", nameof(context));
        }
        private static PhieuTableSet BuildTables(OrderLoadContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Cfg?.Delivery == null)
                throw new ArgumentException(
                    "OrderLoadContext.Cfg.Delivery không được null.", nameof(context));

            bool isSP = context.Category == OrderCategory.SP;
            return PhieuTableSet.FromConfig(context.Cfg, isSP);
        }

        private static string NgayGiaoSp(OrderLoadContext context)
            => context.NgayGiao.ToString("yyyy-MM-dd");
    }
}