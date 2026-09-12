using PCTP.Domain.Entities;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.WorkingState
{
    public sealed class DeliveryWorkingState : IDeliveryWorkingState
    {
        private readonly IPhieuTmpRepository _repository;

        public DeliveryWorkingState(IPhieuTmpRepository repository)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
        }

        public TrangThaiBan GetTrangThaiDangBan(
            OrderLoadContext context)
        {
            ValidateContext(context);

            var tables = GetTables(context);

            if (context.Cfg.Delivery.CoGear)
            {
                return _repository.GetTrangThaiDangBanYMVN(
                    tables.TmpTable,
                    tables.DocQRTable);
            }

            return _repository.GetTrangThaiDangBan(
                tables.TmpTable,
                tables.DocQRTable);
        }

        public DataTable LoadFromQr(
            OrderLoadContext context)
        {
            ValidateContext(context);

            var tables = GetTables(context);

            return _repository.LoadPhieuDocQR(
                 context.NgayGiao.ToString("yyyy-MM-dd"),
                 context.NhaMay,
                 context.GioFcc,
                 context.AddNm,
                 tables);
                    }

        public DataTable LoadCurrentOrder(
            OrderLoadContext context)
        {
            ValidateContext(context);

            var tables = GetTables(context);

            return _repository.LoadTuTmpTable(
                tables.TmpTable);
        }

        public DataTable SaveFromSource(
            OrderLoadContext context,
            DataTable orders,
            string storedProcedure)
        {
            ValidateContext(context);

            if (orders == null)
                throw new ArgumentNullException(nameof(orders));

            if (string.IsNullOrWhiteSpace(storedProcedure))
                throw new ArgumentException(
                    "Stored procedure không được rỗng.",
                    nameof(storedProcedure));

            var tables = GetTables(context);

            return _repository.LuuVaLoad(
                tables,
                storedProcedure,
                orders,
                context.NgayGiao.ToString("yyyy-MM-dd"),
                context.NhaMay,
                context.GioFcc,
                context.AddNm);
        }

        public void ClearTmp(
            OrderLoadContext context)
        {
            ValidateContext(context);

            var tables = GetTables(context);

            _repository.XoaTmpPhieu(
                tables.TmpTable);
        }

        public void ClearDocQr(
            OrderLoadContext context)
        {
            ValidateContext(context);

            var tables = GetTables(context);

            _repository.XoaDocQRCode(
                tables.DocQRTable);
        }

        private static void ValidateContext(
            OrderLoadContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Cfg == null)
                throw new InvalidOperationException(
                    "OrderLoadContext.Cfg chưa được cấu hình.");
        }

        private static PhieuTableSet GetTables(
            OrderLoadContext context)
        {
            bool isSP =
                context.Category == OrderCategory.SP;

            var delivery = context.Cfg.Delivery;

            return new PhieuTableSet(
                delivery.GetTmpTable(isSP),
                delivery.GetIfsTable(isSP),
                delivery.GetDocQRTable(isSP));
        }
    }
}
