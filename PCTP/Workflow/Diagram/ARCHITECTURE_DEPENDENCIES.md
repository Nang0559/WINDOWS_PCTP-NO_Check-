# ARCHITECTURE_DEPENDENCIES

## WMS - Kiến trúc Dependency giữa các Phân khu

> **Mục đích:** Tài liệu này trả lời câu hỏi **"Module nào được phép gọi module nào?"**
>
> **Không thay thế `WORKFLOW_HANGLOI`**, tài liệu đó trả lời câu hỏi **"Luồng nghiệp vụ làm gì?"**

---

# 1. Tổng quan kiến trúc

Hệ thống WMS được chia thành 4 phân khu:

| # | Phân khu | Vai trò | Màu |
|---|----------|----------|------|
| 1 | Kho Core | Quản lý Slot, Warehouse, tồn kho và lịch sử tồn | 🟩 Xanh lá |
| 2 | Nhập Kho | Tiếp nhận hàng và cộng tồn | 🟦 Xanh dương |
| 3 | Xuất Kho | Pick, bảng chờ giao và trừ tồn | 🟧 Cam |
| 4 | Xử Lý Hàng Lỗi / QTChung | Điều phối phiếu lỗi, Rework và giao bù | 🟥 Đỏ |

---

# 2. Thành phần của từng phân khu

## 2.1 🟩 Kho Core

### Thành phần

| Thành phần | Vai trò |
|------------|----------|
| `ISlotService` | Quản lý Slot / SlotLot / số lượng tại vị trí |
| `IWarehouseService` | Quản lý thông tin kho |
| `IStockHistoryRepository` | Ghi lịch sử biến động tồn |
| `STOCKTP` | Dữ liệu tồn kho tổng |

### Nguyên tắc

> Kho Core là nơi cung cấp năng lực quản lý tồn kho nền tảng.
>
> Các module nghiệp vụ không được tự ý cập nhật tồn ngoài service được phân quyền.

---

## 2.2 🟦 Nhập Kho

### Thành phần

| Thành phần | Vai trò |
|------------|----------|
| `INhapTpReceivingService` | Điều phối nghiệp vụ nhập kho |

### Dependency được phép

```text
INhapTpReceivingService
    ├──> ISlotService
    ├──> IWarehouseService
    ├──> IStockHistoryRepository
    └──> STOCKTP
```

### Mục đích

- Cập nhật vị trí hàng
- Cộng tồn
- Ghi lịch sử nhập kho

---

## 2.3 🟧 Xuất Kho

### Thành phần

| Thành phần | Vai trò |
|------------|----------|
| `IStockExportService` | Pick và xử lý xuất kho |
| `IHangChoGiaoRepository` | Quản lý bảng chờ giao |
| `STOCKTP` | Tồn kho được trừ khi xuất |

### Dependency được phép

```text
IStockExportService
    ├──> ISlotService
    ├──> IStockHistoryRepository
    ├──> STOCKTP
    └──> IHangChoGiaoRepository
```

### Mục đích

- Pick hàng
- Trừ tồn
- Ghi lịch sử
- Đưa hàng vào / xử lý từ bảng chờ giao

---

# 3. 🟥 Xử Lý Hàng Lỗi / QTChung

## 3.1 Thành phần

| Thành phần | Vai trò |
|------------|----------|
| `IKhachTraHangService` | Tiếp nhận nguồn khách trả |
| `ITraNoiBoService` | Tiếp nhận nguồn nội bộ |
| `FormChonSlotNoiBo` | Chọn Slot/LOT để tạo phiếu nội bộ |
| `IQTChungService` | Điều phối QTChung |
| `IReworkStockService` | Xử lý nghiệp vụ xuất / nhập lại liên quan Rework |
| `IGiaoBuNGService` | Xử lý giao bù |
| `FVN_PhieuKhachTra` | Chứng từ khách trả |
| `FVN_PhieuXuLyBatThuong` | Phiếu xử lý bất thường |
| `FVN_TraHangQTChung_*` | Audit / Log QTChung |

---

# 4. Dependency của QTChung

## 4.1 Luồng điều phối chính

```text
IQTChungService
    ├──> IReworkStockService
    └──> IGiaoBuNGService
```

QTChung điều phối, không trực tiếp Pick hoặc trừ tồn.

---

## 4.2 Rework và Giao bù đi qua Xuất Kho

```text
IReworkStockService
    └──> IStockExportService

IGiaoBuNGService
    └──> IStockExportService
```

### Ý nghĩa

| Module | Gọi | Mục đích |
|---------|------|----------|
| `IReworkStockService` | `IStockExportService` | Pick / Xuất Rework |
| `IGiaoBuNGService` | `IStockExportService` | Pick / Xuất giao bù |

> `IReworkStockService` và `IGiaoBuNGService`
> **không tự ý thao tác trực tiếp Slot hoặc STOCKTP để trừ tồn.**

---

# 5. Dependency đặc biệt: FormChonSlotNoiBo

## 5.1 Dependency

```text
FormChonSlotNoiBo
        │
        │ READ ONLY
        ▼
   ISlotService
```

Đây là dependency được phép.

`FormChonSlotNoiBo` sử dụng `ISlotService` để:

- Đọc danh sách Slot/LOT đang tồn
- Cho người dùng chọn Slot/LOT
- Tạo phiếu xử lý bất thường nguồn Nội Bộ

---

## 5.2 Read Only không đồng nghĩa với quyền cập nhật tồn

### Được phép

```text
FormChonSlotNoiBo
        --(READ ONLY)-->
        ISlotService
```

### Không được phép

```text
FormChonSlotNoiBo
    X--> ISlotService.AddQuantity
    X--> Trừ tồn
    X--> Cập nhật STOCKTP
```

### Nguyên tắc

> Đọc Slot/LOT để lựa chọn
> khác hoàn toàn với
> quyền thay đổi tồn kho.

---

# 6. Architecture Principles

## A1. Single Writer Principle

Chỉ các module Kho được quyền thay đổi tồn kho.

---

## A2. Read / Write Separation

Các module nghiệp vụ có thể đọc dữ liệu tồn kho nhưng không được trực tiếp cập nhật tồn.

---

## A3. Centralized Stock Movement

Mọi biến động tồn phải đi qua:

- `ISlotService`
- `STOCKTP`
- `IStockHistoryRepository`

---

## A4. Auditability

Mọi nghiệp vụ làm thay đổi tồn kho phải có dấu vết truy xuất được.

---

# 7. Ma Trận Dependency

> Đây là ma trận kiến trúc, không phải mô tả workflow.

| # | Module nguồn | Thành phần đích | Loại dependency | Mục đích |
|---|---|---|---|---|
| 1 | `INhapTpReceivingService` | `ISlotService` | Service Call | Cập nhật vị trí / tồn |
| 2 | `INhapTpReceivingService` | `IWarehouseService` | Service Call | Thông tin kho |
| 3 | `INhapTpReceivingService` | `IStockHistoryRepository` | Repository | Ghi lịch sử |
| 4 | `INhapTpReceivingService` | `STOCKTP` | Stock Update | Cộng tồn |
| 5 | `IStockExportService` | `ISlotService` | Service Call | Pick / Trừ tồn |
| 6 | `IStockExportService` | `IStockHistoryRepository` | Repository | Ghi lịch sử |
| 7 | `IStockExportService` | `STOCKTP` | Stock Update | Trừ tồn |
| 8 | `IStockExportService` | `IHangChoGiaoRepository` | Repository | Bảng chờ giao |
| 9 | `IKhachTraHangService` | `IQTChungService` | Service Call | Khởi tạo QTChung |
| 10 | `ITraNoiBoService` | `IQTChungService` | Service Call | Khởi tạo QTChung |
| 11 | `FormChonSlotNoiBo` | `ISlotService` | Read Only | Đọc Slot / LOT |
| 12 | `FormChonSlotNoiBo` | `IQTChungService` | Service Call | Tạo phiếu nội bộ |
| 13 | `IQTChungService` | `IReworkStockService` | Service Call | Điều phối Rework |
| 14 | `IQTChungService` | `IGiaoBuNGService` | Service Call | Điều phối giao bù |
| 15 | `IReworkStockService` | `IStockExportService` | Service Call | Pick Rework |
| 16 | `IGiaoBuNGService` | `IStockExportService` | Service Call | Pick giao bù |
| 17 | `IReworkStockService` | `FVN_TraHangQTChung_*` | Audit | Ghi audit |
| 18 | `IGiaoBuNGService` | `FVN_TraHangQTChung_*` | Audit | Ghi audit |

---

# 8. Dependency bị cấm

| Dependency | Trạng thái | Lý do |
|------------|------------|--------|
| `FormChonSlotNoiBo → IStockExportService` | ❌ | Form không phải module xuất kho |
| `FormChonSlotNoiBo → STOCKTP` | ❌ | Không được thay đổi tồn trực tiếp |
| `IReworkStockService → ISlotService.AddQuantity` | ❌ | Không được cập nhật tồn |
| `IGiaoBuNGService → ISlotService.AddQuantity` | ❌ | Không được cập nhật tồn |
| `IQTChungService → STOCKTP` | ❌ | QTChung chỉ điều phối |
| `IQTChungService → ISlotService` để trừ tồn | ❌ | Phải đi qua `IStockExportService` |

---

# 9. Quyền truy cập theo phân khu

| Phân khu | Đọc Slot/LOT | Cộng tồn | Trừ tồn | Pick | Điều phối Rework | Điều phối Giao bù |
|-----------|-------------|----------|----------|------|------------------|------------------|
| Kho Core | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Nhập Kho | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Xuất Kho | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ |
| Xử Lý Hàng Lỗi | ⚠️ Read Only | ❌ | ❌ | ❌ | ✅ | ✅ |

> Xử Lý Hàng Lỗi chỉ được đọc Slot/LOT thông qua:
>
> `FormChonSlotNoiBo -> ISlotService`
>
> Mọi thay đổi tồn phải đi qua module kho tương ứng.

---

# 10. Quy tắc kiến trúc chốt

## Rule 01 - Read Slot ≠ Modify Stock

```text
FormChonSlotNoiBo
        |
        | READ ONLY
        v
   ISlotService
```

Được phép.

---

## Rule 02 - Rework / Giao bù không tự trừ tồn

```text
Rework / GiaoBù
       |
       v
IReworkStockService / IGiaoBuNGService
       |
       v
IStockExportService
       |
       v
Slot / STOCKTP
```

---

## Rule 03 - QTChung là Orchestrator

```text
IQTChungService
       |
       +----> IReworkStockService
       |
       +----> IGiaoBuNGService
```

QTChung không trực tiếp quản lý tồn kho.

---

## Rule 04 - Workflow và Architecture phải tách riêng

### WORKFLOW_HANGLOI

Trả lời câu hỏi:

> Nghiệp vụ chạy theo trình tự nào?

```text
Tạo phiếu
   ↓
QC định hướng
   ↓
Giao bù / Rework
   ↓
QC cuối
   ↓
OK / NG
   ↓
Hoàn tất
```

### ARCHITECTURE_DEPENDENCIES

Trả lời câu hỏi:

> Module nào được phép gọi module nào?

```text
FormChonSlotNoiBo -. READ ONLY .-> ISlotService

IQTChungService --> IReworkStockService
IQTChungService --> IGiaoBuNGService

IReworkStockService --> IStockExportService
IGiaoBuNGService --> IStockExportService

IStockExportService --> ISlotService
IStockExportService --> STOCKTP
```

---

# 11. Kết luận

> Xử Lý Hàng Lỗi có thể đọc dữ liệu Slot/LOT từ Kho Core để lựa chọn nguồn hàng,
> nhưng không được tự ý thay đổi tồn.

```text
READ

FormChonSlotNoiBo
        -.-> ISlotService
              │
              │ Chỉ đọc
              ▼
         Slot / SlotLot


WRITE / STOCK MOVEMENT

IReworkStockService
        └──> IStockExportService
                    │
                    ├──> ISlotService
                    └──> STOCKTP
```

Điều này giúp:

- Tách biệt Workflow và Architecture
- Rõ ràng trách nhiệm từng phân khu
- Tập trung quản lý biến động tồn kho
- Dễ audit và truy vết
- Hạn chế cập nhật tồn kho ngoài luồng

Theo nguyên tắc:

**Kho Core → Nhập Kho → Xuất Kho → Xử Lý Hàng Lỗi**
được phân định trách nhiệm rõ ràng và không chồng chéo.
## 3. Sơ Đồ Kiến Trúc Tổng Thể (Mermaid Diagram)

```mermaid
graph TD

    %% ==================================================
    %% KHO CORE
    %% ==================================================
    subgraph KhoCore_Zone["KHO CORE"]
        ISlotService["ISlotService"]
        IWarehouseService["IWarehouseService"]
        IStockHistoryRepo["IStockHistoryRepository"]
        STOCKTP[(STOCKTP)]
    end

    %% ==================================================
    %% NHAP KHO
    %% ==================================================
    subgraph NhapKho_Zone["NHAP KHO"]
        INhapTpService["INhapTpReceivingService"]
    end

    %% ==================================================
    %% XUAT KHO
    %% ==================================================
    subgraph XuatKho_Zone["XUAT KHO"]
        IStockExportService["IStockExportService"]
        IHangChoGiaoRepo["IHangChoGiaoRepository"]
    end

    %% ==================================================
    %% XU LY HANG LOI / QTCHUNG
    %% ==================================================
    subgraph XuLyLoi_Zone["XU LY HANG LOI / QTCHUNG"]

        ServiceKhachTra["IKhachTraHangService"]
        ServiceTraNoiBo["ITraNoiBoService"]

        FormChonSlotNoiBo["FormChonSlotNoiBo"]

        SlotReadOnly["READ ONLY"]

        IQTChungService["IQTChungService"]
        IReworkStockService["IReworkStockService"]
        IGiaoBuNGService["IGiaoBuNGService"]

        TablePhieuKhachTra[(FVN_PhieuKhachTra)]
        TablePhieuXuLy[(FVN_PhieuXuLyBatThuong)]
        TableTraHangQTChung[(FVN_TraHangQTChung_*)]

    end

*   %% ============================*=====================
    %% NHAP *HO -> CORE
    %% ================*=================================
*   INhapTpService -->|Cap nhat vi *ri| ISlotService
    INhapTpServic* -->|Thong tin kho| IWarehouseServ*ce
    INhapTpService -->|Ghi lich*su| IStockHistoryRepo
    INhapTpS*rvice -->|Cong ton| STOCKTP

    %* =================================*================
    %% XUAT KHO -* CORE
    %%*==================================*===============
    IStockExportSe*vice -->|*ick Tru ton| ISlotService
    ISto*kExportService -->|Ghi lich su| IS*ockHistoryRepo
    IStock*xportService -->|Cap nhat ton| STO*KTP
   *IStockExportService -->|Quan ly| I*angChoGiaoRepo

    %% ===========*==================================*===
    %% KHOI TAO QTCHUNG
    %%*==================================*===============
    ServiceKhachTr* -->|Khoi tao| IQTChungService
   *Service*raNoiBo -->|Khoi tao| IQTChungServ*ce

    ServiceKhachTra -->|Luu| T*blePhieuKhachTra
    IQTChungServi*e -->*Luu phieu| TablePhieuXuLy

    %% *==================================*==============
    %% FORM NOI BO
*   %% ============================*=====================
    FormChon*lotNoiBo -.->|Doc Slot LOT| ISlotS*rvice
    FormChonSlotNoiBo -->|Ta* phieu Noi Bo| IQTChungService

  * FormChon*lotNoiBo -.-> SlotReadOnly
   *SlotReadOnly -.*> ISlotService

    %% ===========*==================================*===
    %% QTCHUNG DIEU PHOI
   *%%*==================================*===============
    IQTChungServic* -->|*ieu phoi| IReworkStockService
    *QTChungService -->|Dieu phoi| IGia*BuNGService

    %% ==============*==================================*
    %% REWORK / GIAO BU
    %%*==================================*===============
    IRe*ork*tockService -->|PickToChoGiao| ISt*ckExportService
    IGiaoBuNGServi*e -->|PickToChoGiao| IStockExportS*rvice

    %% ====================*=============================
    *% AUDIT
    %% ===================*==============================
   *IReworkStockService -->|Audit| Tab*eTraHangQTChung
    IGiaoBuNGServi*e -->|Audit| TableTraHangQTChung

*   %% ============================*=====================
    %% STYLE*    %% ===========================*======================
    style K*oCore_Zone fill:#e2f0d9,stroke:#38*723,stroke-width:2px
    style Nh*pKho_Zone fill:#d9e1f2,stroke:#2f5*97,stroke-width:2px
    style X*at*ho_Zone fill:#fce4d6,stroke:#c6591*,stroke-width:2px
    style XuLyLo*_*one fill:#f8cecc,stroke:#b85450,st*oke-width:2px

    style FormChonS*otNoiBo fill:#fff2cc,stroke:#bf900*,stroke-width:2px
    style*Slot*eadOnly fill:#fff2cc,stroke:#bf900*,stroke-width:1px
```
