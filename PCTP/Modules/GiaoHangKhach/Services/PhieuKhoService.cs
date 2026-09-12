using PCTP.Domain.Entities;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Shared.Models;
using System;
using System.Data;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Phase 7: business service cho nghiệp vụ cập nhật kho của phiếu giao.
    /// Không chứa UI; persistence vẫn do IPhieuRepository đảm nhiệm.
    /// </summary>
    public class PhieuKhoService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IEventBus _bus;
        private readonly CustomerConfig _cfg;

        public PhieuKhoService(
            IPhieuRepository phieuRepo,
            IEventBus bus,
            CustomerConfig cfg)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa, bool isSP)
        {
            int soLot;
            DataTable errors;

            try
            {
                if (_cfg.Delivery.LoadTuBangRieng && !_cfg.Delivery.CoGear)
                {
                    soLot = _phieuRepo.CapNhapKhoHTN(
                        nhaMay,
                        _cfg.Delivery.GetTmpTable(isSP),
                        _cfg.Delivery.GetDocQRTable(isSP),
                        out errors);
                }
                else
                {
                    soLot = _phieuRepo.CapNhapKho(
                        gioGiaoFcc,
                        nhaMay,
                        _cfg.Delivery.GetTmpTable(isSP),
                        _cfg.Delivery.GetDocQRTable(isSP),
                        out errors);
                }

                if (errors != null && errors.Rows.Count > 0)
                {
                    foreach (DataRow r in errors.Rows)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[CapNhapKho ERROR] MH={r["MH"]}, LOT={r["LOT"]}, STATUS={r["STATUS"]}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CapNhapKho EXCEPTION] {ex.Message}");
                throw;
            }

            _bus.Publish(new KhoUpdatedEvent(soLot, errors));
        }
    }
}
