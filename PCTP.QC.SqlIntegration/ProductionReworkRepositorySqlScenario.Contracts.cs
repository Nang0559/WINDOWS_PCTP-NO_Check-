#nullable disable
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Edge and schema contracts for PhieuXuLyBatThuongRepository and TraHangQTChungRepository.
///
/// The happy path lives in ProductionReworkRepositorySqlScenario.cs. This file covers what that
/// path cannot see: unknown ids, argument/validation failures, ordering guarantees, multi-row
/// aggregation, per-ticket isolation, and the constraints declared by the REAL PHASE12/13/14
/// migration scripts (CHECK, NOT NULL, unique indexes).
///
/// Every scenario runs inside a caller-owned UnitOfWork transaction that is rolled back, so the
/// disposable fixtures are left unchanged between groups. Repository calls must never run outside
/// an active transaction (the test executor throws if they do).
/// </summary>
internal static partial class ProductionReworkRepositorySqlScenario
{
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlCheckViolation = 547;
    private const int SqlNotNullViolation = 515;

    // ------------------------------------------------------------------
    // PhieuXuLyBatThuongRepository
    // ------------------------------------------------------------------

    private static void RunAbnormalTicketEdgeContracts(string connectionString)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        var repository = new PhieuXuLyBatThuongRepository(new PhieuSqlExecutor(), uow);

        // Return lines are committed up front; the tickets that link to them are rolled back.
        for (var line = 930001; line <= 930005; line++)
            Execute(connectionString, $"INSERT INTO {ReturnLineTable}(Id, PhieuXuLyBatThuongId) VALUES ({line}, NULL);");

        const int missing = 999999;

        // 1) Unknown ids: reads return null/empty, conditional writes return false, unconditional
        //    writes fail loudly, and non-positive ids are rejected before any SQL runs.
        InRollbackTransaction(uow, () =>
        {
            Require(repository.GetById(missing) == null, "GetById must return null for an unknown ticket.");
            Require(repository.GetStatus(missing) == null, "GetStatus must return null for an unknown ticket.");
            Require(repository.GetByPhieuTraHangId(missing) == null,
                "GetByPhieuTraHangId must return null when the return has no ticket.");
            Require(repository.GetAllByPhieuTraHangId(missing).Count == 0,
                "GetAllByPhieuTraHangId must return an empty list when the return has no ticket.");
            Require(!repository.UpdateStatusIfCurrentIs(missing, QTChungStatus.Moi, QTChungStatus.DaDinhHuong, "EDGE"),
                "Compare-and-set on an unknown ticket must return false.");
            Require(!repository.UpdateLyDoHuy(missing, QTChungStatus.Moi, "reason", "EDGE"),
                "Conditional cancellation of an unknown ticket must return false.");

            Throws<InvalidOperationException>(
                () => repository.UpdateStatus(missing, QTChungStatus.DaDinhHuong, "EDGE"),
                "UpdateStatus on an unknown ticket must fail loudly.");
            Throws<InvalidOperationException>(
                () => repository.UpdateDinhHuong(missing, HuongXuLyBatThuong.CanRework, "EDGE"),
                "UpdateDinhHuong on an unknown ticket must fail loudly.");

            Throws<ArgumentOutOfRangeException>(
                () => repository.UpdateStatus(0, QTChungStatus.DaDinhHuong, "EDGE"),
                "UpdateStatus must reject id <= 0.");
            Throws<ArgumentOutOfRangeException>(
                () => repository.UpdateDinhHuong(0, HuongXuLyBatThuong.CanRework, "EDGE"),
                "UpdateDinhHuong must reject id <= 0.");
            Throws<ArgumentOutOfRangeException>(
                () => repository.UpdateLyDoHuy(0, QTChungStatus.Moi, "reason", "EDGE"),
                "UpdateLyDoHuy must reject id <= 0.");
            Throws<ArgumentOutOfRangeException>(
                () => repository.Insert(0, NewTicket("EDGE-INVALID-LINE", 930001, 78)),
                "Insert must reject a non-positive PhieuTraHangCTId.");
            Throws<ArgumentNullException>(
                () => repository.Insert(930001, null),
                "Insert must reject a null ticket.");
        });

        // 2) Insert validation (ValidateForInsert) rejects the ticket before anything is persisted.
        InRollbackTransaction(uow, () =>
        {
            var customerWithoutCustomerId = NewTicket("EDGE-KT-NO-CUSTOMER", 930001, 78);
            customerWithoutCustomerId.Nguon = NguonXuLyBatThuong.KhachTra;
            customerWithoutCustomerId.PhieuKhachTraId = null;
            Throws<InvalidOperationException>(
                () => repository.Insert(930001, customerWithoutCustomerId),
                "KhachTra without PhieuKhachTraId must be rejected.");

            var customerWithoutReturnId = NewTicket("EDGE-KT-NO-RETURN", 930001, 78);
            customerWithoutReturnId.Nguon = NguonXuLyBatThuong.KhachTra;
            customerWithoutReturnId.PhieuKhachTraId = 5001;
            customerWithoutReturnId.PhieuTraHangId = null;
            Throws<InvalidOperationException>(
                () => repository.Insert(930001, customerWithoutReturnId),
                "KhachTra without PhieuTraHangId must be rejected.");

            var internalWithoutSlot = NewTicket("EDGE-NB-NO-SLOT", 930001, 78);
            internalWithoutSlot.SlotIdNguon = null;
            Throws<InvalidOperationException>(
                () => repository.Insert(930001, internalWithoutSlot),
                "TraNoiBo without SlotIdNguon must be rejected.");

            var internalBlankLot = NewTicket("EDGE-NB-BLANK-LOT", 930001, 78);
            internalBlankLot.LotNguon = "   ";
            Throws<InvalidOperationException>(
                () => repository.Insert(930001, internalBlankLot),
                "TraNoiBo with a blank LotNguon must be rejected.");

            var zeroQuantity = NewTicket("EDGE-ZERO-QTY", 930001, 78);
            zeroQuantity.SoLuongLoi = 0;
            Throws<InvalidOperationException>(
                () => repository.Insert(930001, zeroQuantity),
                "SoLuongLoi <= 0 must be rejected.");

            Require(repository.GetAllByPhieuTraHangId(78).Count == 0,
                "Rejected inserts must not persist any ticket.");
        });

        // 3) Customer-return source, Unicode round-trip, ordering and state/cancel contracts.
        InRollbackTransaction(uow, () =>
        {
            const string unicode = "Lỗi tem: sai LOT — kiểm tra lại (Đà Nẵng)";
            var customerTicket = NewTicket("EDGE-KT-OK", 930002, 78, unicode);
            customerTicket.Nguon = NguonXuLyBatThuong.KhachTra;
            customerTicket.PhieuKhachTraId = 5001;
            customerTicket.SlotIdNguon = null;
            customerTicket.LotNguon = null;
            var customerId = repository.Insert(930002, customerTicket);

            var customerLoaded = repository.GetById(customerId);
            Require(customerLoaded.Nguon == NguonXuLyBatThuong.KhachTra && customerLoaded.PhieuKhachTraId == 5001,
                "KhachTra source keys were not persisted.");
            Require(customerLoaded.SlotIdNguon == null && string.IsNullOrEmpty(customerLoaded.LotNguon),
                "A KhachTra ticket must not invent slot/LOT source metadata.");
            Require(customerLoaded.NoiDungBatThuong == unicode,
                "Vietnamese text must round-trip through nvarchar without loss.");
            Require(customerLoaded.HuongXuLy == HuongXuLyBatThuong.ChuaXacDinh &&
                    !customerLoaded.NgayDinhHuong.HasValue,
                "A new ticket must start without an orientation.");

            // Ordering: three tickets of the same return, one per return line.
            var a = repository.Insert(930003, NewTicket("EDGE-ORD-A", 930003, 79));
            var b = repository.Insert(930004, NewTicket("EDGE-ORD-B", 930004, 79));
            var c = repository.Insert(930005, NewTicket("EDGE-ORD-C", 930005, 79));
            Require(a < b && b < c, "Ticket identities must be increasing in insert order.");

            Require(repository.GetAllByPhieuTraHangId(79).Select(t => t.Id).SequenceEqual(new[] { a, b, c }),
                "GetAllByPhieuTraHangId must be ordered by Id ascending.");
            Require(repository.GetByPhieuTraHangId(79)?.Id == c,
                "GetByPhieuTraHangId must return the newest ticket of the return.");

            var mine = new HashSet<int> { a, b, c };
            Require(repository.GetByNguon(NguonXuLyBatThuong.TraNoiBo)
                        .Where(t => mine.Contains(t.Id)).Select(t => t.Id).SequenceEqual(new[] { c, b, a }),
                "GetByNguon must return newest first.");
            Require(repository.GetByStatus(QTChungStatus.Moi)
                        .Where(t => mine.Contains(t.Id)).Select(t => t.Id).SequenceEqual(new[] { c, b, a }),
                "GetByStatus must return newest first.");
            Require(repository.CountByStatus(QTChungStatus.Moi) == repository.GetByStatus(QTChungStatus.Moi).Count,
                "CountByStatus and GetByStatus must agree.");
            Require(repository.GetByNguon(NguonXuLyBatThuong.KhachTra).Exists(t => t.Id == customerId) &&
                    !repository.GetByNguon(NguonXuLyBatThuong.TraNoiBo).Exists(t => t.Id == customerId),
                "GetByNguon must filter by source.");

            // Orientation records the decision but must NOT move the state machine.
            repository.UpdateDinhHuong(a, HuongXuLyBatThuong.CanRework, "QC-EDGE");
            var oriented = repository.GetById(a);
            Require(oriented.HuongXuLy == HuongXuLyBatThuong.CanRework &&
                    oriented.NguoiDinhHuong == "QC-EDGE" && oriented.NgayDinhHuong.HasValue &&
                    oriented.UpdatedBy == "QC-EDGE" && oriented.UpdatedAt.HasValue,
                "UpdateDinhHuong did not persist the orientation audit fields.");
            Require(oriented.Status == QTChungStatus.Moi,
                "UpdateDinhHuong must not change Status; the service owns the transition.");

            repository.UpdateStatus(a, QTChungStatus.DaDinhHuong, "SVC-EDGE");
            var moved = repository.GetById(a);
            Require(moved.Status == QTChungStatus.DaDinhHuong && moved.UpdatedBy == "SVC-EDGE" &&
                    moved.HuongXuLy == HuongXuLyBatThuong.CanRework,
                "UpdateStatus must change Status/UpdatedBy and leave the orientation intact.");

            Require(!repository.UpdateStatusIfCurrentIs(a, QTChungStatus.Moi, QTChungStatus.DaGiaoSanXuat, "STALE-EDGE"),
                "A stale compare-and-set must not apply.");
            var afterStale = repository.GetById(a);
            Require(afterStale.Status == QTChungStatus.DaDinhHuong && afterStale.UpdatedBy == "SVC-EDGE",
                "A stale compare-and-set must leave Status and UpdatedBy untouched.");
            Require(repository.UpdateStatusIfCurrentIs(a, QTChungStatus.DaDinhHuong, QTChungStatus.DaXuatKhoRework, "KHO-EDGE") &&
                    repository.GetById(a).UpdatedBy == "KHO-EDGE",
                "A matching compare-and-set must apply and stamp UpdatedBy.");

            // Cancellation: blank reason, stale state, success (trimmed), and no double-cancel.
            Throws<ArgumentException>(
                () => repository.UpdateLyDoHuy(b, QTChungStatus.Moi, "   ", "QC-EDGE"),
                "A blank cancellation reason must be rejected.");
            Require(!repository.UpdateLyDoHuy(b, QTChungStatus.DaDinhHuong, "stale", "QC-EDGE"),
                "Cancellation from the wrong expected state must return false.");
            var untouched = repository.GetById(b);
            Require(untouched.Status == QTChungStatus.Moi && string.IsNullOrEmpty(untouched.LyDoHuy) &&
                    string.IsNullOrEmpty(untouched.NguoiHuy) && !untouched.NgayHuy.HasValue,
                "A rejected cancellation must not write any cancellation field.");

            Require(repository.UpdateLyDoHuy(b, QTChungStatus.Moi, "  Khách đổi ý  ", "QC-EDGE"),
                "Cancellation from the expected state must succeed.");
            var cancelled = repository.GetById(b);
            Require(cancelled.Status == QTChungStatus.Huy && cancelled.LyDoHuy == "Khách đổi ý" &&
                    cancelled.NguoiHuy == "QC-EDGE" && cancelled.NgayHuy.HasValue && cancelled.UpdatedBy == "QC-EDGE",
                "Cancellation must trim the reason and stamp status, actor and dates in one statement.");
            Require(!repository.UpdateLyDoHuy(b, QTChungStatus.Moi, "again", "QC-EDGE") &&
                    repository.GetById(b).LyDoHuy == "Khách đổi ý",
                "A cancelled ticket must not be cancellable again or have its reason overwritten.");
        });

        Console.WriteLine("PASS: production PhieuXuLyBatThuongRepository unknown-id/validation/ordering/orientation/CAS/cancel/Unicode contracts.");
    }

    // ------------------------------------------------------------------
    // TraHangQTChungRepository
    // ------------------------------------------------------------------

    private static void RunReturnWorkflowEdgeContracts(string connectionString)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        var repository = new TraHangQTChungRepository(new PhieuSqlExecutor(), uow);

        const int emptyTicket = 930101;
        const int busyTicket = 930102;
        const int otherTicket = 930103;
        var day = new DateTime(2026, 10, 26);

        // 1) A ticket with no ledger rows: empty lists, null singles, zero totals, false flags.
        InRollbackTransaction(uow, () =>
        {
            Require(repository.GetXuat(emptyTicket).Count == 0 && repository.GetGiao(emptyTicket).Count == 0 &&
                    repository.GetNhapNG(emptyTicket).Count == 0 && repository.GetTimeline(emptyTicket).Count == 0,
                "Ledger lists and timeline must be empty for a ticket with no rows.");
            Require(repository.GetQC(emptyTicket) == null && repository.GetXuatById(999999) == null &&
                    repository.GetGiaoById(999999) == null && repository.GetNhapNGById(999999) == null &&
                    repository.GetXuatByOperationId(Guid.NewGuid()) == null,
                "Single-row lookups must return null for unknown keys.");
            Require(repository.GetTongSoLuongDaXuat(emptyTicket) == 0 && repository.GetTongSoLuongDaGiao(emptyTicket) == 0 &&
                    repository.GetTongSoLuongOK(emptyTicket) == 0 && repository.GetTongSoLuongNG(emptyTicket) == 0 &&
                    repository.GetTongSoLuongDaNhapNG(emptyTicket) == 0,
                "Totals must be zero (not null/exception) when there are no rows.");
            Require(!repository.DaXuatKho(emptyTicket) && !repository.DaGiaoSanXuat(emptyTicket) &&
                    !repository.DaQCXacNhan(emptyTicket) && !repository.DaNhapNG(emptyTicket),
                "Progress flags must be false when there are no rows.");

            Throws<ArgumentOutOfRangeException>(() => repository.GetXuat(0), "GetXuat must reject id <= 0.");
            Throws<ArgumentOutOfRangeException>(() => repository.DaXuatKho(-1), "DaXuatKho must reject id <= 0.");
            Throws<ArgumentOutOfRangeException>(() => repository.GetTongSoLuongDaXuat(0), "Totals must reject id <= 0.");
            Throws<ArgumentOutOfRangeException>(() => repository.GetTimeline(0), "GetTimeline must reject id <= 0.");
            Throws<ArgumentException>(() => repository.GetXuatByOperationId(Guid.Empty),
                "GetXuatByOperationId must reject an empty OperationId.");
            Throws<ArgumentNullException>(() => repository.InsertXuat(null), "InsertXuat must reject null.");
            Throws<ArgumentNullException>(() => repository.InsertGiao(null), "InsertGiao must reject null.");
            Throws<ArgumentNullException>(() => repository.InsertQC(null), "InsertQC must reject null.");
            Throws<ArgumentNullException>(() => repository.InsertNhapNG(null), "InsertNhapNG must reject null.");

            Throws<InvalidOperationException>(
                () => repository.UpdateLatestGiaoNote(emptyTicket, "tiến độ", "SX-EDGE"),
                "UpdateLatestGiaoNote must fail when the ticket has no delivery.");
            Throws<InvalidOperationException>(
                () => repository.UpdateDaKiemTraTem(999999, true),
                "UpdateDaKiemTraTem must fail for an unknown QC row.");
        });

        // 2) Multi-row ordering, aggregation, latest-QC selection, note append, timeline, isolation.
        InRollbackTransaction(uow, () =>
        {
            // Inserted deliberately out of chronological order.
            var exportLate = repository.InsertXuat(NewExport(busyTicket, 3, day.AddHours(10)));
            repository.InsertXuat(NewExport(busyTicket, 8, day.AddHours(8)));
            repository.InsertXuat(NewExport(busyTicket, 5, day.AddHours(9)));
            repository.InsertXuat(NewExport(otherTicket, 100, day.AddHours(8)));

            Require(repository.GetXuat(busyTicket).Select(x => x.SoLuongXuat).SequenceEqual(new[] { 8, 5, 3 }),
                "GetXuat must be ordered by NgayXuat, not by insert order.");
            Require(repository.GetTongSoLuongDaXuat(busyTicket) == 16,
                "Export total must sum every export row of the ticket only.");
            Require(repository.GetTongSoLuongDaXuat(otherTicket) == 100,
                "Another ticket's export must not leak into this ticket's total.");
            Require(repository.GetXuatById(exportLate)?.PhieuXuLyBatThuongId == busyTicket,
                "GetXuatById must map the owning ticket.");

            var giaoEarly = repository.InsertGiao(NewDelivery(busyTicket, 5, day.AddHours(9), "note-A"));
            var giaoLate = repository.InsertGiao(NewDelivery(busyTicket, 3, day.AddHours(11), "note-B"));
            Require(repository.GetGiao(busyTicket).Select(x => x.SoLuongGiao).SequenceEqual(new[] { 5, 3 }),
                "GetGiao must be ordered by ThoiGian.");
            Require(repository.GetTongSoLuongDaGiao(busyTicket) == 8, "Delivery total is incorrect.");

            // Progress note: appended to the LATEST delivery only, never overwriting, and repeatable.
            repository.UpdateLatestGiaoNote(busyTicket, "  tiến độ 50%  ", "  SX-EDGE ");
            const string expectedPrefix = "note-B\n[ĐANG REWORK] tiến độ 50% | SX-EDGE | ";
            var firstNote = repository.GetGiaoById(giaoLate).Note;
            Require(firstNote.StartsWith(expectedPrefix, StringComparison.Ordinal) &&
                    firstNote.Length == expectedPrefix.Length + 19,
                $"UpdateLatestGiaoNote must append '<note>\\n[ĐANG REWORK] <text> | <user> | yyyy-MM-dd HH:mm:ss'. Actual: '{firstNote}'.");
            Require(repository.GetGiaoById(giaoEarly).Note == "note-A",
                "UpdateLatestGiaoNote must not touch earlier deliveries.");
            repository.UpdateLatestGiaoNote(busyTicket, "tiến độ 100%", "SX-EDGE");
            var secondNote = repository.GetGiaoById(giaoLate).Note;
            Require(secondNote.StartsWith(firstNote + "\n[ĐANG REWORK] tiến độ 100% | SX-EDGE | ", StringComparison.Ordinal),
                "A second progress note must be appended after the first, preserving history.");
            Throws<ArgumentException>(
                () => repository.UpdateLatestGiaoNote(busyTicket, "   ", "SX-EDGE"),
                "A blank progress note must be rejected.");
            Throws<ArgumentException>(
                () => repository.UpdateLatestGiaoNote(busyTicket, "tiến độ", "  "),
                "A blank actor must be rejected.");

            // QC: GetQC picks the newest ThoiGian even when it has the LOWER identity.
            var qcLate = repository.InsertQC(NewQc(busyTicket, 5, 4, 1, day.AddHours(12), "round-2"));
            var qcEarly = repository.InsertQC(NewQc(busyTicket, 5, 3, 2, day.AddHours(10), "round-1"));
            Require(qcEarly > qcLate, "Fixture precondition: the earlier QC must have the higher identity.");
            var latestQc = repository.GetQC(busyTicket);
            Require(latestQc.Id == qcLate && latestQc.KetLuan == "round-2",
                "GetQC must return the QC with the newest ThoiGian, not the highest Id.");
            // The ledger has no unique key per ticket, so totals are defined as SUM over all QC rows.
            Require(repository.GetTongSoLuongOK(busyTicket) == 7 && repository.GetTongSoLuongNG(busyTicket) == 3,
                "QC OK/NG totals must sum every QC row deterministically.");
            Require(repository.DaQCXacNhan(busyTicket), "DaQCXacNhan must be true once any QC row exists.");

            repository.UpdateDaKiemTraTem(qcLate, true);
            Require(repository.GetQC(busyTicket).DaKiemTraTem, "DaKiemTraTem=true was not persisted.");
            repository.UpdateDaKiemTraTem(qcLate, false);
            Require(!repository.GetQC(busyTicket).DaKiemTraTem, "DaKiemTraTem=false was not persisted.");

            var ngLate = repository.InsertNhapNG(NewNgReceipt(busyTicket, 2, day.AddHours(15)));
            repository.InsertNhapNG(NewNgReceipt(busyTicket, 1, day.AddHours(13)));
            Require(repository.GetNhapNG(busyTicket).Select(x => x.SoLuongNG).SequenceEqual(new[] { 1, 2 }),
                "GetNhapNG must be ordered by NgayNhap.");
            Require(repository.GetTongSoLuongDaNhapNG(busyTicket) == 3, "NG receipt total is incorrect.");
            Require(repository.GetNhapNGById(ngLate)?.SoLuongNG == 2, "GetNhapNGById must map the row.");

            // Timeline: all four sources, chronological, only this ticket, QC quantity = OK + NG.
            var timeline = repository.GetTimeline(busyTicket);
            Require(timeline.Count == 3 + 2 + 2 + 2, $"Timeline should hold 9 events, got {timeline.Count}.");
            Require(timeline.TrueForAll(t => t.PhieuXuLyId == busyTicket),
                "Timeline must not include another ticket's events.");
            for (var i = 1; i < timeline.Count; i++)
                Require(timeline[i - 1].ThoiGian <= timeline[i].ThoiGian, "Timeline must be chronological.");
            var qcEvent = timeline.Find(t => t.Buoc == "QC" && t.RefId == qcLate);
            Require(qcEvent != null && qcEvent.SoLuong == 5 && qcEvent.NoiDung == "round-2" &&
                    string.IsNullOrEmpty(qcEvent.LotNo) && string.IsNullOrEmpty(qcEvent.MaHang),
                "QC timeline event must expose OK+NG as quantity, KetLuan as content and no LOT/part.");
            Require(timeline.FindAll(t => t.Buoc == "XUAT").Count == 3 &&
                    timeline.FindAll(t => t.Buoc == "GIAO").Count == 2 &&
                    timeline.FindAll(t => t.Buoc == "NHAP_NG").Count == 2,
                "Timeline event counts per step are incorrect.");
        });

        Console.WriteLine("PASS: production TraHangQTChungRepository empty/ordering/aggregation/latest-QC/progress-note/timeline/isolation contracts.");
    }

    /// <summary>
    /// Constraints that exist only because the fixture is built from the real migrations.
    /// If any of these start failing after a DDL change, the repository contract changed.
    /// </summary>
    private static void RunSchemaEnforcedLedgerContracts(string connectionString)
    {
        var provider = new SQLPROVIDER { B7R2_FCCdb = connectionString };
        using var uow = new UnitOfWork(provider);
        var repository = new TraHangQTChungRepository(new PhieuSqlExecutor(), uow);
        const int ticket = 930201;
        var at = new DateTime(2026, 10, 26, 8, 0, 0);

        // CHECK constraints (PHASE13): quantities must be positive; QC quantities non-negative.
        RejectedBySql(uow, "Export quantity 0 must violate CK_FVN_TraHangQTChungXuat_SoLuong.",
            () => repository.InsertXuat(NewExport(ticket, 0, at)), SqlCheckViolation);
        RejectedBySql(uow, "Negative export quantity must violate the CHECK constraint.",
            () => repository.InsertXuat(NewExport(ticket, -1, at)), SqlCheckViolation);
        RejectedBySql(uow, "Delivery quantity 0 must violate CK_FVN_TraHangQTChungGiao_SoLuong.",
            () => repository.InsertGiao(NewDelivery(ticket, 0, at, "n")), SqlCheckViolation);
        RejectedBySql(uow, "Negative QC OK quantity must violate CK_FVN_TraHangQTChungQC_SoLuong.",
            () => repository.InsertQC(NewQc(ticket, 1, -1, 2, at, "bad")), SqlCheckViolation);
        RejectedBySql(uow, "NG receipt quantity 0 must violate CK_FVN_TraHangQTChungNhapNG_SoLuong.",
            () => repository.InsertNhapNG(NewNgReceipt(ticket, 0, at)), SqlCheckViolation);

        // NOT NULL columns (PHASE13): the repository does not default them, so SQL must refuse.
        RejectedBySql(uow, "Export without LotXuat must violate NOT NULL.",
            () =>
            {
                var e = NewExport(ticket, 1, at);
                e.LotXuat = null;
                repository.InsertXuat(e);
            }, SqlNotNullViolation);
        RejectedBySql(uow, "Export without MaHang must violate NOT NULL.",
            () =>
            {
                var e = NewExport(ticket, 1, at);
                e.MaHang = null;
                repository.InsertXuat(e);
            }, SqlNotNullViolation);
        RejectedBySql(uow, "NG receipt without LotNhapLai must violate NOT NULL.",
            () =>
            {
                var n = NewNgReceipt(ticket, 1, at);
                n.LotNhapLai = null;
                repository.InsertNhapNG(n);
            }, SqlNotNullViolation);

        // Boundary values the CHECK constraints allow.
        InRollbackTransaction(uow, () =>
        {
            Require(repository.InsertQC(NewQc(ticket, 0, 0, 0, at, "all-zero")) > 0,
                "A QC row with all-zero quantities is valid (CHECK is >= 0).");
            Require(repository.InsertGiao(NewDelivery(ticket, 1, at, null)) > 0,
                "A delivery with a null note is valid.");
        });

        // Idempotency (PHASE14): the same OperationId can only produce one export row.
        var operationId = Guid.NewGuid();
        InRollbackTransaction(uow, () =>
        {
            var firstId = repository.InsertXuat(NewExport(ticket, 2, at, operationId));
            Require(repository.GetXuatByOperationId(operationId)?.Id == firstId,
                "The first export must be resolvable by its OperationId.");
            Require(repository.GetTongSoLuongDaXuat(ticket) == 2, "Fixture precondition: exactly one export of 2.");

            var rejected = false;
            try
            {
                repository.InsertXuat(NewExport(ticket, 2, at, operationId));
            }
            catch (SqlException ex) when (ex.Number == SqlUniqueIndexViolation || ex.Number == SqlUniqueConstraintViolation)
            {
                rejected = true;
            }

            Require(rejected, "UX_FVN_TraHangQTChungXuat_OperationId must reject a duplicate OperationId.");
        });

        // After all rollbacks nothing may remain for the ticket.
        var leftovers = Convert.ToInt32(Scalar(connectionString, $@"
SELECT
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungXuat WHERE PhieuXuLyBatThuongId IN (930101, 930102, 930103, {ticket})) +
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungGiao WHERE PhieuXuLyBatThuongId IN (930101, 930102, 930103, {ticket})) +
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungQC WHERE PhieuXuLyBatThuongId IN (930101, 930102, 930103, {ticket})) +
    (SELECT COUNT(1) FROM dbo.FVN_TraHangQTChungNhapNG WHERE PhieuXuLyBatThuongId IN (930101, 930102, 930103, {ticket}));"));
        Require(leftovers == 0, $"Edge-contract rollbacks left {leftovers} ledger rows behind.");

        Console.WriteLine("PASS: production TraHangQTChungRepository schema-enforced contracts (CHECK, NOT NULL, OperationId unique index) from the real PHASE13/14 DDL.");
    }

    // ------------------------------------------------------------------
    // Entity builders
    // ------------------------------------------------------------------

    private static TraHangQTChungXuat NewExport(int ticketId, int quantity, DateTime at, Guid? operationId = null) => new()
    {
        OperationId = operationId ?? Guid.NewGuid(),
        PhieuXuLyBatThuongId = ticketId,
        SlotIdNguon = 12,
        LotXuat = "LOT-EDGE",
        LoaiXuat = "Rework",
        MaHang = "PART-EDGE",
        SoLuongXuat = quantity,
        TonTruoc = 100,
        TonSau = 100 - quantity,
        NgayXuat = at,
        NguoiXuat = "KHO-EDGE",
        LyDo = "edge",
        Note = "edge"
    };

    private static TraHangQTChungGiao NewDelivery(int ticketId, int quantity, DateTime at, string note) => new()
    {
        PhieuXuLyBatThuongId = ticketId,
        LotGiao = "LOT-EDGE",
        MaHang = "PART-EDGE",
        SoLuongGiao = quantity,
        ThoiGian = at,
        NgayGiao = at.ToString("yyyy-MM-dd"),
        NguoiNhan = "SX-EDGE",
        BoPhanNhan = "REWORK",
        SoPhieuGiaoNhan = "GIAO-EDGE",
        Note = note
    };

    private static TraHangQTChungQC NewQc(int ticketId, int reworked, int ok, int ng, DateTime at, string conclusion) => new()
    {
        PhieuXuLyBatThuongId = ticketId,
        SoLuongDaRework = reworked,
        SoLuongOK = ok,
        SoLuongNG = ng,
        DaKiemTraTem = false,
        ThoiGian = at,
        NguoiQC = "QC-EDGE",
        KetLuan = conclusion,
        Note = "edge"
    };

    private static TraHangQTChungNhapNG NewNgReceipt(int ticketId, int quantity, DateTime at) => new()
    {
        PhieuXuLyBatThuongId = ticketId,
        SlotIdOK = 12,
        SlotIdNG = 13,
        LotNhapLai = "LOT-EDGE-NG",
        MaHang = "PART-EDGE",
        SoLuongNG = quantity,
        SlotIdNhap = 13,
        NgayNhap = at,
        NguoiNhap = "KHO-EDGE",
        LyDo = "edge",
        Note = "edge"
    };

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Runs the body inside a caller-owned transaction that is always rolled back.</summary>
    private static void InRollbackTransaction(UnitOfWork uow, Action body)
    {
        uow.Begin();
        try
        {
            body();
        }
        finally
        {
            if (uow.IsInTransaction) uow.Rollback();
        }
    }

    /// <summary>Asserts the action throws exactly the given exception type (or a subclass).</summary>
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (ex is T) return;
            throw new InvalidOperationException(
                $"{message} Expected {typeof(T).Name} but got {ex.GetType().Name}: {ex.Message}", ex);
        }

        throw new InvalidOperationException($"{message} Expected {typeof(T).Name} but nothing was thrown.");
    }

    /// <summary>
    /// Runs the action in its own rolled-back transaction and requires SQL Server to reject it with
    /// one of the expected error numbers. Each rejection gets a fresh transaction so a failed
    /// statement can never poison the next assertion.
    /// </summary>
    private static void RejectedBySql(UnitOfWork uow, string message, Action action, params int[] expectedNumbers)
    {
        var expected = string.Join("/", expectedNumbers);
        uow.Begin();
        try
        {
            try
            {
                action();
            }
            catch (SqlException ex) when (Array.IndexOf(expectedNumbers, ex.Number) >= 0)
            {
                return;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    $"{message} Expected SQL error {expected} but got {ex.Number}: {ex.Message}", ex);
            }

            throw new InvalidOperationException($"{message} Expected SQL error {expected} but the statement succeeded.");
        }
        finally
        {
            if (uow.IsInTransaction) uow.Rollback();
        }
    }

    private static string ReadMigration(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Database", fileName);
        Require(File.Exists(path), $"Migration script was not copied to the harness output directory: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Executes a SQL Server script containing GO batch separators on ONE connection, so a
    /// BEGIN TRANSACTION ... COMMIT that spans batches (as in PHASE13/14) keeps working.
    /// </summary>
    private static void ExecuteScript(string connectionString, string script)
    {
        var batches = Regex.Split(script, @"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        try
        {
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var command = new SqlCommand(batch, connection) { CommandTimeout = 30 };
                command.ExecuteNonQuery();
            }
        }
        catch
        {
            try
            {
                using var rollback = new SqlCommand("IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;", connection);
                rollback.ExecuteNonQuery();
            }
            catch
            {
                // The original failure is the one worth reporting.
            }

            throw;
        }
    }
}
