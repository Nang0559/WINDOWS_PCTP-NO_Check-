ARCHITECTURE_DEPENDENCIES — WMS
1. Mục đích
Tài liệu này mô tả dependency kiến trúc giữa 4 phân khu WMS.
`WORKFLOW_HANGLOI`: Luồng nghiệp vụ làm gì?
`ARCHITECTURE_DEPENDENCIES`: Module nào được phép gọi module nào?
---
2. Bốn phân khu
2.1. Kho Core — Xanh Lá
Quản lý không gian kho và dữ liệu tồn kho cốt lõi.
Thành phần:
`ISlotService`
`IWarehouseService`
`IStockHistoryRepository`
`STOCKTP`
2.2. Nhập Kho — Xanh Dương
Quản lý tiếp nhận hàng mới và hàng Rework đạt chuẩn nhập lại.
Thành phần: `INhapTpReceivingService`
Được phép cập nhật vị trí, cộng tồn và ghi lịch sử theo nghiệp vụ nhập.
2.3. Xuất Kho — Cam
Quản lý Pick, bảng chờ giao và trừ tồn.
Thành phần:
`IStockExportService`
`IHangChoGiaoRepository`
`STOCKTP`
2.4. Xử Lý Hàng Lỗi / QTChung — Đỏ
Điều phối phiếu bất thường, Rework và giao bù.
Thành phần:
`IKhachTraHangService`
`ITraNoiBoService`
`FormChonSlotNoiBo`
`IQTChungService`
`IReworkStockService`
`IGiaoBuNGService`
`FVN_PhieuKhachTra`
`FVN_PhieuXuLyBatThuong`
`FVN_TraHangQTChung_*`
Nguyên tắc: Xử Lý Hàng Lỗi không tự ý ghi/trừ tồn kho.
---
3. Quy tắc Dependency
3.1. Nhập Kho → Kho Core
```text
INhapTpReceivingService
    ├──> ISlotService
    ├──> IWarehouseService
    ├──> IStockHistoryRepository
    └──> STOCKTP
```
3.2. Xuất Kho → Kho Core
```text
IStockExportService
    ├──> ISlotService
    ├──> IStockHistoryRepository
    ├──> STOCKTP
    └──> IHangChoGiaoRepository
```
3.3. QTChung → Rework / Giao bù → Xuất Kho
```text
IQTChungService
    ├──> IReworkStockService
    └──> IGiaoBuNGService

IReworkStockService
    └──> IStockExportService

IGiaoBuNGService
    └──> IStockExportService
```
`IReworkStockService` và `IGiaoBuNGService` không tự Pick/Trừ tồn tại Slot/STOCKTP.
3.4. FormChonSlotNoiBo → Kho Core
Đây là dependency READ ONLY:
```text
FormChonSlotNoiBo
    -. READ ONLY: đọc Slot/LOT đang tồn .->
ISlotService
```
Mục đích:
Đọc danh sách Slot/LOT đang tồn.
Cho người dùng chọn Slot/LOT.
Tạo phiếu xử lý bất thường nguồn Nội Bộ.
Không được hiểu là quyền:
```text
FormChonSlotNoiBo
    X-> AddQuantity
    X-> Trừ tồn
    X-> Cập nhật STOCKTP
```
---
4. Sơ đồ kiến trúc
```mermaid
graph TD

    subgraph KhoCore_Zone["KHO CORE - Warehouse / Rack / Slot / SlotLot / StockHistory"]
        ISlotService["ISlotService"]
        IWarehouseService["IWarehouseService"]
        IStockHistoryRepo["IStockHistoryRepository"]
        StockTP_Core[("STOCKTP")]
    end

    subgraph NhapKho_Zone["NHAP KHO - STOCKTP Cong"]
        INhapTpService["INhapTpReceivingService"]
    end

    subgraph XuatKho_Zone["XUAT KHO - STOCKTP Tru + Hang Cho Giao"]
        IStockExportService["IStockExportService"]
        IHangChoGiaoRepo["IHangChoGiaoRepository"]
        StockTP_Xuat[("STOCKTP")]
    end

    subgraph XuLyLoi_Zone["XU LY HANG LOI - QTChung / Rework / GiaoBu"]
        ServiceKhachTra["IKhachTraHangService"]
        ServiceTraNoiBo["ITraNoiBoService"]
        FormChonSlotNoiBo["FormChonSlotNoiBo<br/>Tao phieu tu Slot LOT"]
        IQTChungService["IQTChungService"]
        IReworkStockService["IReworkStockService"]
        IGiaoBuNGService["IGiaoBuNGService"]
        TablePhieuKhachTra[("FVN_PhieuKhachTra")]
        TablePhieuXuLy[("FVN_PhieuXuLyBatThuong")]
        TableTraHangQTChung[("FVN_TraHangQTChung_*")]
    end

    INhapTpService -->|goi| ISlotService
    INhapTpService -->|goi| IWarehouseService
    INhapTpService -->|ghi lich su| IStockHistoryRepo
    INhapTpService -->|cong ton| StockTP_Core

    IStockExportService -->|Pick / tru ton| ISlotService
    IStockExportService -->|ghi lich su| IStockHistoryRepo
    IStockExportService -->|tru ton| StockTP_Xuat
    IStockExportService -->|quan ly bang cho giao| IHangChoGiaoRepo

    ServiceKhachTra -->|khoi tao| IQTChungService
    ServiceTraNoiBo -->|khoi tao| IQTChungService
    ServiceKhachTra -->|luu| TablePhieuKhachTra
    IQTChungService -->|luu phieu| TablePhieuXuLy

    FormChonSlotNoiBo -.->|READ ONLY - Doc Slot LOT| ISlotService
    FormChonSlotNoiBo -->|Tao phieu Noi Bo| IQTChungService

    IQTChungService -->|dieu phoi| IReworkStockService
    IQTChungService -->|dieu phoi| IGiaoBuNGService

    IReworkStockService -->|goi PickToChoGiao| IStockExportService
    IGiaoBuNGService -->|goi PickToChoGiao| IStockExportService

    IReworkStockService -->|ghi audit| TableTraHangQTChung
    IGiaoBuNGService -->|ghi audit| TableTraHangQTChung

    style KhoCore_Zone fill:#e2f0d9,stroke:#385723,stroke-width:2px
    style NhapKho_Zone fill:#d9e1f2,stroke:#2f5597,stroke-width:2px
    style XuatKho_Zone fill:#fce4d6,stroke:#c65911,stroke-width:2px
    style XuLyLoi_Zone fill:#f8cecc,stroke:#b85450,stroke-width:2px
    style FormChonSlotNoiBo fill:#fff2cc,stroke:#bf9000,stroke-width:2px
```
---
5. Ma trận Dependency
Nguồn	Đích	Quyền / Mục đích
`INhapTpReceivingService`	`ISlotService`	Cập nhật vị trí / tồn theo nghiệp vụ nhập
`INhapTpReceivingService`	`IWarehouseService`	Xử lý thông tin kho
`INhapTpReceivingService`	`IStockHistoryRepository`	Ghi lịch sử
`INhapTpReceivingService`	`STOCKTP`	Cộng tồn
`IStockExportService`	`ISlotService`	Pick / trừ tồn
`IStockExportService`	`IStockHistoryRepository`	Ghi lịch sử
`IStockExportService`	`STOCKTP`	Trừ tồn
`IStockExportService`	`IHangChoGiaoRepository`	Quản lý bảng chờ giao
`FormChonSlotNoiBo`	`ISlotService`	READ ONLY — đọc Slot/LOT
`FormChonSlotNoiBo`	`IQTChungService`	Tạo phiếu Nội Bộ
`IQTChungService`	`IReworkStockService`	Điều phối Rework
`IQTChungService`	`IGiaoBuNGService`	Điều phối giao bù
`IReworkStockService`	`IStockExportService`	Pick / xuất Rework
`IGiaoBuNGService`	`IStockExportService`	Pick / xuất giao bù
---
6. Dependency bị cấm
Không được có:
```text
FormChonSlotNoiBo
    X-> IStockExportService
```
Form chỉ đọc Slot/LOT để tạo phiếu, không phải module xuất kho.
Không được có:
```text
IReworkStockService
    X-> ISlotService.AddQuantity
```
và:
```text
IGiaoBuNGService
    X-> ISlotService.AddQuantity
```
Các thao tác xuất/trừ tồn phải đi qua `IStockExportService`.
Không được có:
```text
IQTChungService
    X-> STOCKTP
```
QTChung là module điều phối, không trực tiếp quản lý tồn kho.
---
7. Nguyên tắc chốt
Rule 1 — Đọc Slot ≠ thay đổi tồn
```text
FormChonSlotNoiBo
    -. READ ONLY .->
ISlotService
```
Dependency này được phép.
Rule 2 — Thay đổi tồn phải qua module kho được phân quyền
```text
Rework / GiaoBu
       |
       v
IStockExportService
       |
       v
Slot / STOCKTP
```
Rule 3 — QTChung chỉ điều phối
```text
IQTChungService
       |
       +--> IReworkStockService
       |
       +--> IGiaoBuNGService
```
Rule 4 — Workflow và Architecture không trộn vai trò
`WORKFLOW_HANGLOI` mô tả trình tự nghiệp vụ.
`ARCHITECTURE_DEPENDENCIES` mô tả dependency và quyền gọi.
Dependency quan trọng cần giữ:
```text
FormChonSlotNoiBo -. READ ONLY .-> ISlotService

IQTChungService --> IReworkStockService
IQTChungService --> IGiaoBuNGService

IReworkStockService --> IStockExportService
IGiaoBuNGService --> IStockExportService

IStockExportService --> ISlotService
IStockExportService --> STOCKTP
```
