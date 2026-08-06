using PCTP.Domain.Entities;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Events
{
    // ─── Base ───────────────────────────────────────────────────────────────────
    public abstract class DomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.Now;
    }

    // ─── Phiếu events ───────────────────────────────────────────────────────────
    /// <summary>Phát sau khi load phiếu thành công — grid cần refresh.</summary>
    public class PhieuLoadedEvent : DomainEvent
    {
        public DataTable DonHangTable { get; }
        public DataTable HangThieuTable { get; }
        public string Caption { get; }

        public PhieuLoadedEvent(DataTable donHang, DataTable hangThieu, string caption)
        {
            DonHangTable = donHang;
            HangThieuTable = hangThieu;
            Caption = caption;
        }
    }

    /// <summary>Phát khi cập nhật kho xong — show kết quả.</summary>
    public class KhoUpdatedEvent : DomainEvent
    {
        public int SoLotCapNhap { get; }
        public DataTable Errors { get; }

        public KhoUpdatedEvent(int soLot, DataTable errors)
        {
            SoLotCapNhap = soLot;
            Errors = errors;
        }
    }

    // ─── DocQR events ────────────────────────────────────────────────────────────
    /// <summary>Phát sau mỗi lần scan QR thành công.</summary>
    public class QRScannedEvent : DomainEvent
    {
        public DocQRCode Item { get; }
        public string Company { get; }   // "FCC" | "HVN"
        //public ScanResult Result { get; }   // ← thêm

        public QRScannedEvent(DocQRCode item, string company)
        {
            Item = item;
            Company = company;
         
        }
    }

    /// <summary>Phát khi TinhTong xong — grid và badge cần cập nhật.</summary>
    public class TinhTongCompletedEvent : DomainEvent
    {
        public IReadOnlyList<(int Stt, string Lot)> Results { get; }
        public TinhTongCompletedEvent(IReadOnlyList<(int, string)> results)
            => Results = results;
    }

    // ─── Giờ xuất events ─────────────────────────────────────────────────────────
    /// <summary>Phát khi user đổi giờ xuất — presenter reload phiếu.</summary>
    public class GioXuatChangedEvent : DomainEvent
    {
        public GioXuat GioXuat { get; }
        public int AddNM { get; }

        public GioXuatChangedEvent(GioXuat gioXuat, int addNm)
        {
            GioXuat = gioXuat;
            AddNM = addNm;
        }
    }
}
