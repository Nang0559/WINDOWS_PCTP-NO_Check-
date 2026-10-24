using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PCTP.Domain.Events;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Services;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class DeliveryLotServiceUnitTests
{
    [Fact]
    public void TwoDuplicateLines_ShowDialogOnce_ThenProcessLastLineImplicitly()
    {
        var candidates = Rows((1, "P-A", 100), (3, "P-A", 100));
        var validation = new FakeValidation(candidates);
        var lot = new FakeLotRepository();
        var bus = new FakeEventBus();
        var service = new PhieuLotService(lot, validation, bus);
        var selectorCalls = 0;

        var result = service.TinhTongLot(candidates.Copy(), "TMP_VIEW", "DOCQRCODE", "TMPPHIEUGIAOHANG", rows =>
        {
            selectorCalls++;
            Assert.Equal(2, rows.Rows.Count);
            return 3;
        });

        Assert.Equal(new[] { 3, 1 }, result.Select(x => x.Stt));
        Assert.Equal(1, selectorCalls);
        Assert.Equal(new[] { 2, 1 }, lot.Calls.Select(x => x.Dem));
        Assert.Equal(new[] { 3, 1 }, lot.Calls.Select(x => x.Stt));
        Assert.Single(validation.Queries);
        Assert.IsType<TinhTongCompletedEvent>(Assert.Single(bus.Published));
    }

    [Fact]
    public void ThreeDuplicateLines_ShowDialogExactlyNMinusOneTimes()
    {
        var candidates = Rows((1, "P-A", 100), (2, "P-A", 100), (3, "P-A", 100));
        var validation = new FakeValidation(candidates);
        var lot = new FakeLotRepository();
        var service = new PhieuLotService(lot, validation, new FakeEventBus());
        var selected = new Queue<int>(new[] { 3, 2 });
        var selectorCalls = 0;

        var result = service.TinhTongLot(candidates.Copy(), "TMP_VIEW", "DOCQRCODE", "TMPPHIEUGIAOHANG", rows =>
        {
            selectorCalls++;
            Assert.True(rows.Rows.Count > 1);
            return selected.Dequeue();
        });

        Assert.Equal(2, selectorCalls);
        Assert.Equal(new[] { 3, 2, 1 }, result.Select(x => x.Stt));
        Assert.Equal(new[] { 3, 2, 1 }, lot.Calls.Select(x => x.Stt));
    }

    [Fact]
    public void CancelledDialog_DoesNotCallRepositoryOrConsumeQr()
    {
        var candidates = Rows((1, "P-A", 100), (3, "P-A", 100));
        var lot = new FakeLotRepository();
        var service = new PhieuLotService(lot, new FakeValidation(candidates), new FakeEventBus());

        var result = service.TinhTongLot(candidates.Copy(), "TMP_VIEW", "DOCQRCODE", "TMPPHIEUGIAOHANG", _ => -1);

        Assert.Empty(result);
        Assert.Empty(lot.Calls);
    }

    [Fact]
    public void StaleOrForeignSttFromDialog_IsRejected()
    {
        var candidates = Rows((1, "P-A", 100), (3, "P-A", 100));
        var lot = new FakeLotRepository();
        var service = new PhieuLotService(lot, new FakeValidation(candidates), new FakeEventBus());

        var result = service.TinhTongLot(candidates.Copy(), "TMP_VIEW", "DOCQRCODE", "TMPPHIEUGIAOHANG", _ => 999);

        Assert.Empty(result);
        Assert.Empty(lot.Calls);
    }

    [Fact]
    public void RepeatedInputRowsForSamePartAndQuantity_QueryDuplicateGroupOnlyOnce()
    {
        var input = Rows((1, "P-A", 100), (3, "P-A", 100));
        var candidates = Rows((1, "P-A", 100), (3, "P-A", 100));
        var validation = new FakeValidation(candidates);
        var service = new PhieuLotService(new FakeLotRepository(), validation, new FakeEventBus());

        service.TinhTongLot(input, "TMP_VIEW", "DOCQRCODE", "TMPPHIEUGIAOHANG", _ => 3);

        Assert.Single(validation.Queries);
    }

    [Fact]
    public void SamePartWithDifferentQuantity_IsProcessedAsSeparateBusinessGroups()
    {
        var input = Rows((1, "P-A", 100), (2, "P-A", 80));
        var validation = new FakeValidation(
            ("P-A", 100, Rows((1, "P-A", 100))),
            ("P-A", 80, Rows((2, "P-A", 80))));
        var lot = new FakeLotRepository();
        var service = new PhieuLotService(lot, validation, new FakeEventBus());

        var result = service.TinhTongLot(input, "TMP_VIEW", "DOCQRCODE", "TMPPHIEUGIAOHANG", _ => throw new InvalidOperationException("No duplicate dialog expected."));

        Assert.Equal(new[] { 1, 2 }, result.Select(x => x.Stt));
        Assert.Equal(new[] { 100, 80 }, lot.Calls.Select(x => x.SlGiao));
        Assert.Equal(2, validation.Queries.Count);
    }

    [Fact]
    public void FailedExactAllocation_StopsGroupWithoutTryingToReuseSameQrForNextStt()
    {
        var candidates = Rows((1, "P-A", 100), (3, "P-A", 100));
        var validation = new FakeValidation(candidates);
        var lot = new FakeLotRepository { ReturnEmpty = true };
        var service = new PhieuLotService(lot, validation, new FakeEventBus());

        var result = service.TinhTongLot(candidates.Copy(), "TMP_VIEW", "DOCQRCODE", "TMPPHIEUGIAOHANG", _ => 3);

        Assert.Empty(result);
        Assert.Single(lot.Calls);
        Assert.Equal(3, lot.Calls[0].Stt);
    }

    private static DataTable Rows(params (int Stt, string MaHang, int Sl)[] rows)
    {
        var table = new DataTable();
        table.Columns.Add("STT", typeof(int));
        table.Columns.Add("MAHANG", typeof(string));
        table.Columns.Add("SOLUONG", typeof(int));
        foreach (var row in rows) table.Rows.Add(row.Stt, row.MaHang, row.Sl);
        return table;
    }

    private sealed class FakeValidation : IPhieuValidationRepository
    {
        private readonly Dictionary<string, DataTable> _results = new(StringComparer.OrdinalIgnoreCase);
        public List<(string MaHang, int Sl)> Queries { get; } = new();

        public FakeValidation(DataTable result) => _results[Key("P-A", 100)] = result;
        public FakeValidation(params (string MaHang, int Sl, DataTable Result)[] results)
        {
            foreach (var item in results) _results[Key(item.MaHang, item.Sl)] = item.Result;
        }

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable)
        {
            Queries.Add((maHang, sl));
            return _results.TryGetValue(Key(maHang, sl), out var result) ? result.Copy() : Rows();
        }

        private static string Key(string ma, int sl) => ma.Trim().ToUpperInvariant() + "|" + sl;
    }

    private sealed class FakeLotRepository : IPhieuLotRepository
    {
        public List<(string MaHang, int Stt, int Dem, int SlGiao, string DocQr, string Tmp)> Calls { get; } = new();
        public bool ReturnEmpty { get; set; }

        public string GetLotNo(string maHang, int stt, int dem, int slGiao, string docQRTable, string tmpTable)
        {
            Calls.Add((maHang, stt, dem, slGiao, docQRTable, tmpTable));
            return ReturnEmpty ? string.Empty : "LOT-" + stt;
        }
    }

    private sealed class FakeEventBus : IEventBus
    {
        public List<DomainEvent> Published { get; } = new();
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent { }
        public void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent => Published.Add(domainEvent);
        public void PublishSynchronous<TEvent>(TEvent domainEvent) where TEvent : DomainEvent => Published.Add(domainEvent);
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent { }
    }
}
