using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Domain.Events
{
    public abstract class DomainEvent { }
    public sealed class TinhTongCompletedEvent : DomainEvent
    {
        public IReadOnlyList<(int Stt, string Lot)> Results { get; }
        public TinhTongCompletedEvent(IReadOnlyList<(int Stt, string Lot)> results) => Results = results;
    }
}

namespace PCTP.Domain.Interfaces
{
    using PCTP.Domain.Events;
    public interface IEventBus
    {
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent;
        void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent;
        void PublishSynchronous<TEvent>(TEvent domainEvent) where TEvent : DomainEvent;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent;
    }
}

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public interface IPhieuLotService
    {
        List<(int Stt, string Lot)> TinhTongLot(
            DataTable bangTam, string tenBan, string docQRTable, string tmpTable,
            Func<DataTable, int> chonSttKhiTrung);
    }
}

namespace PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao
{
    public interface IPhieuLotRepository
    {
        string GetLotNo(string maHang, int stt, int dem, int slGiao, string docQRTable, string tmpTable);
    }

    public interface IPhieuValidationRepository
    {
        DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable);
    }
}
