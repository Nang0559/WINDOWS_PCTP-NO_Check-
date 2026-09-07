# Tài Liệu Kiến Trúc & Luồng Vận Hành Hệ Thống Kho (WMS / Production Management)

> **Vị trí trong bộ tài liệu:** Đây là tài liệu MẸ. Toàn bộ module nghiệp vụ (Nhập Kho, Xuất Kho, Giao Hàng Khách, Xử Lý Hàng Lỗi) đều là NGƯỜI DÙNG của Kho Core — không module nào được coi là "biệt lập". Chi tiết API nằm ở tài liệu con; tài liệu này chỉ mô tả cách 4 module nghiệp vụ nối vào Kho Core qua **1 cổng tồn kho duy nhất**.

---

## 1. Nguyên Tắc Kiến Trúc Cốt Lõi (đọc trước tiên)

**Một nguồn sự thật duy nhất cho tồn kho:** `STOCKTP` (tổng) và `Slot`/`SlotLot` (vị trí vật lý, kể cả kho ảo A0) phải LUÔN đồng bộ sau mỗi giao dịch. Để đảm bảo điều này, hệ thống áp dụng nguyên tắc:

> **Bất kỳ hành động nào làm thay đổi số lượng tồn kho — dù trigger từ Nhập Kho, Xuất Kho, Giao Hàng Khách, hay Xử Lý Hàng Lỗi — đều PHẢI đi qua `IStockExportService` (khi xuất) hoặc `INhapTpReceivingService`/`ISlotService` (khi nhập), KHÔNG được tự viết SQL/SP trừ thẳng `STOCKTP` hay `SlotLot` ở tầng module nghiệp vụ.**

Cụ thể: khi `GiaoHangKhach` xác nhận đã giao 1 Lot cho khách, nó không được tự trừ `STOCKTP` bằng SP riêng rồi tự trừ A0 bằng `BulkStockAdjustService` — nó phải gọi **`IStockExportService.ConfirmGiaoHangTuChoGiao(lotNo, ...)`**, và chính service này (dùng chung với nhánh Giao Bù NG, Rework) sẽ:
1. Tìm đúng Slot thật (hoặc A0) đang giữ Lot đó qua `ISlotService`.
2. Trừ/xoá `SlotLot` tương ứng — Slot cập nhật ngay trên `MainStockSV`.
3. Trừ `STOCKTP` qua `IStockTpRepository` (không qua SQL thô).
4. Ghi `IStockHistoryRepository.SaveHistory` với `ActionType = EXPORT`.
5. Đóng dòng `FVN_HangChoGiao` tương ứng.

→ **4 bước này chỉ tồn tại ở DUY NHẤT 1 nơi trong code** (`IStockExportService`), được cả `Xuất Kho` nội bộ lẫn `Giao Hàng Khách` cùng gọi — không phải 2 implementation song song như hiện tại.

---

## 2. Tổng Quan Kiến Trúc Hệ Thống

| Lớp | Vai trò | Quan hệ |
|---|---|---|
| **KHO CORE** | Nguyên thủy: Warehouse/Rack/Slot, `ISlotService`, `IStockHistoryRepository`. Không biết gì về "khách hàng", "phiếu giao", hay "QR code". | Bị gọi bởi cả 4 module nghiệp vụ bên dưới — không gọi ngược lại module nào. |
| **NHẬP KHO** | Nhận hàng mới/rework OK → ghi vào Slot + `STOCKTP` qua Kho Core. | → Kho Core |
| **XUẤT KHO** | Cổng DUY NHẤT xử lý mọi hình thức trừ kho: xuất A0 trực tiếp, hoặc qua `FVN_HangChoGiao` (2 pha Pick→Confirm) cho Giao Hàng / Giao Bù NG / Rework. | → Kho Core |
| **GIAO HÀNG KHÁCH** | Lớp nghiệp vụ khách hàng (đối chiếu QR, ghép Lot, in phiếu) — **KHÔNG tự xử lý tồn kho**, mà gọi `IStockExportService.ConfirmGiaoHangTuChoGiao` của module Xuất Kho ở bước cuối cùng. | → **XUẤT KHO** (không gọi thẳng Kho Core) |
| **XỬ LÝ HÀNG LỖI** | QC định hướng, rework, giao bù → cũng gọi `IStockExportService`/`IReworkStockService` để trừ/cộng kho. | → XUẤT KHO / Kho Core |

> **Sơ đồ phụ thuộc rút gọn:**
> `NHẬP KHO` ─┐
> `GIAO HÀNG KHÁCH` ─┤→ `XUẤT KHO` ─→ `KHO CORE`
> `XỬ LÝ HÀNG LỖI` ─┘
>
> Chỉ `XUẤT KHO` và `NHẬP KHO` được phép gọi thẳng `Kho Core`. `GIAO HÀNG KHÁCH` và `XỬ LÝ HÀNG LỖI` bắt buộc đi qua `XUẤT KHO` khi cần trừ kho — không có ngoại lệ, không có "đường tắt" riêng.

| Tài liệu con | Link |
|---|---|
| Kho Core | [`WORKFLOW_KHOCORE.md`](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/blob/master/PCTP/Workflow/Diagram/WORKFLOW_KHOCORE.md) |
| Nhập Kho | [`WORKFLOW_NHAPKHO.md`](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/blob/master/PCTP/Workflow/Diagram/WORKFLOW_NHAPKHO.md) |
| Xuất Kho | [`WORKFLOW_XUATKHO.md`](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/blob/master/PCTP/Workflow/Diagram/WORKFLOW_XUATKHO.md) |
| Xử Lý Hàng Lỗi | [`WORKFLOW_HANGLOI.md`](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/blob/master/PCTP/Workflow/Diagram/WORKFLOW_HANGLOI.md) |
| Giao Hàng Khách | [`WORKFLOW_GIAOHANGKHACH.md`](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/blob/master/PCTP/Workflow/Diagram/WORKFLOW_GIAOHANGKHACH.md) |

---

## 3. Trạng Thái Hiện Tại vs Đích Cần Đạt (Giao Hàng Khách)

| | Hiện tại (code đang chạy) | Đích (đang thiết kế lại) |
|---|---|---|
| Trừ `STOCKTP` | SP riêng (`Usp_Qrcode_Update_Stock2405`) trong `PhieuKhoRepository` | Qua `IStockExportService.ConfirmGiaoHangTuChoGiao` → `IStockTpRepository` |
| Trừ Slot | **CHỈ** kho ảo A0 qua `BulkStockAdjustService`; **Slot thật KHÔNG được cập nhật** | Mọi Slot (thật + A0) qua `ISlotService`, cùng cơ chế Xuất Kho dùng |
| Đóng `FVN_HangChoGiao` | Gọi thẳng `IHangChoGiaoRepository.CloseChoGiaoTheoLotAndReturn` từ `PhieuKhoRepository` | Nằm bên trong `IStockExportService.ConfirmGiaoHangTuChoGiao`, `PhieuKhoRepository` không gọi trực tiếp `IHangChoGiaoRepository` nữa |
| Vai trò `PhieuKhoRepository` | Vừa đối chiếu QR vừa tự trừ kho | Chỉ đối chiếu QR/Lot xong → gọi 1 method Confirm của `IStockExportService`, không còn logic trừ kho nào trong chính nó |

**Việc cần làm để đạt đích:** chuyển toàn bộ khối code trừ kho hiện đang nằm trong `PhieuKhoRepository.CapNhapKho`/`CapNhapKhoSP`/`CapNhapKhoYMVN` sang bên trong `IStockExportService`, để `PhieuKhoRepository` chỉ còn gọi 1 dòng `_stockExportService.ConfirmGiaoHangTuChoGiao(...)` cho mỗi Lot — xoá hẳn phụ thuộc trực tiếp vào `BulkStockAdjustService` và `IHangChoGiaoRepository` khỏi module Giao Hàng Khách.

---

## 4. Sơ Đồ Tổng Thể (Mermaid) — Mọi Đường Trừ Kho Hội Tụ Về Xuất Kho

```mermaid
graph TD
    StartInbound([Yêu cầu Nhập Kho]) --> InboundProcess["INhapTpReceivingService"]
    InboundProcess --> CoreCore

    StartOutboundInternal([Xuất kho nội bộ / Rework]) --> ExportSvc
    StartHangLoi([Xử Lý Hàng Lỗi: Giao Bù NG]) --> ExportSvc

    StartGHK([Giao Hàng Khách<br/>HVN_PGH → PhieuKhoRepository]) -->|"🎯 ĐÍCH: chỉ gọi 1 method,<br/>KHÔNG tự trừ kho"| ConfirmCall["IStockExportService<br/>.ConfirmGiaoHangTuChoGiao(lotNo, ...)"]

    subgraph ExportGate [XUẤT KHO — CỔNG DUY NHẤT TRỪ KHO]
        ExportSvc["IStockExportService"]
        ConfirmCall
        PickToChoGiao["PickToChoGiao<br/>(khoá Slot, CHƯA trừ STOCKTP)"]
        ChoGiaoTable[(FVN_HangChoGiao)]
        ConfirmLogic["Confirm logic (dùng chung):<br/>1. Tìm Slot thật giữ Lot (ISlotService)<br/>2. Trừ SlotLot đúng Slot đó<br/>3. Trừ STOCKTP (IStockTpRepository)<br/>4. SaveHistory ActionType=EXPORT<br/>5. Đóng FVN_HangChoGiao"]

        ExportSvc --> PickToChoGiao --> ChoGiaoTable
        ChoGiaoTable --> ConfirmLogic
        ConfirmCall --> ConfirmLogic
    end

    ConfirmLogic --> CoreCore

    subgraph CoreLayer [KHO CORE]
        CoreCore["ISlotService / IStockTpRepository /<br/>IStockHistoryRepository"]
    end

    CoreCore --> CommitDB[(Commit Transaction)]
    CommitDB --> FinalEnd([Hoàn tất — Slot & STOCKTP đồng bộ])

    style ExportGate fill:#fff3e0,stroke:#f57c00,stroke-width:3px
    style CoreLayer fill:#e8f5e9,stroke:#388e3c,stroke-width:3px
    style InboundProcess fill:#e1f5fe,stroke:#0288d1,stroke-width:2px
    style OutboundProcess fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    style CoreInbound fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style CoreOutbound fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style FinalEnd fill:#bfb,stroke:#333,stroke-width:2px