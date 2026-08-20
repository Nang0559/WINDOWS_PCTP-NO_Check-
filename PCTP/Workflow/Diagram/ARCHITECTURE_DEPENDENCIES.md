# Tài Liệu Kiến Trúc Tổng Thể: Phân Chia 4 Phân Khu WMS (Subsystems Architecture)

Tài liệu này mô tả sơ đồ kiến trúc tổng thể của hệ thống WMS, chia rõ ràng thành 4 phân khu độc lập nhưng có sự tương tác chặt chẽ thông qua các Service và Repository cốt lõi.

---

## 1. Mô Tả Chi Tiết 4 Phân Khu (Subsystems)

1. **Kho Core (Subsystem Xanh Lá):**
   * Đóng vai trò là tầng hạ tầng nền tảng chứa các dịch vụ xử lý không gian và dữ liệu cốt lõi.
   * *Các thành phần chính:* `ISlotService`, `IWarehouseService`, `IStockHistoryRepository` và bảng dữ liệu tổng `STOCKTP`.
2. **Nhập Kho (Subsystem Xanh Dương):**
   * Quản lý toàn bộ nghiệp vụ tiếp nhận hàng mới hoặc hàng Rework đạt chuẩn nhập lại kho.
   * *Thành phần chính:* `INhapTpReceivingService` (tương tác trực tiếp với Kho Core để cộng dồn tồn kho và cập nhật vị trí).
3. **Xuất Kho (Subsystem Cam):**
   * Quản lý luồng xuất hàng trực tiếp hoặc qua bảng trung gian chờ giao.
   * *Thành phần chính:* `IStockExportService`, `IHangChoGiaoRepository` và bảng dữ liệu `STOCKTP` (trừ tồn kho).
4. **Xử Lý Hàng Lỗi / QTChung (Subsystem Đỏ):**
   * Điều phối quy trình tiếp nhận phiếu bất thường từ khách hàng hoặc nội bộ, quản lý vòng đời Rework và giao bù.
   * *Thành phần chính:* `IKhachTraHangService`, `ITraNoiBoService`, `IQTChungService`, `IReworkStockService`, `IGiaoBuNGService` cùng các bảng quản lý phiếu (`FVN_PhieuKhachTra`, `FVN_PhieuXuLyBatThuong`, `FVN_TraHangQTChung_*`).

---

## 2. Sơ Đồ Kiến Trúc Tổng Thể (Mermaid Diagram)

```mermaid
graph TD
    %% ----------------------------------------------------
    %% KHU VUC 1: KHO CORE
    %% ----------------------------------------------------
    subgraph KhoCore_Zone["KHO CORE - Warehouse / Rack / Slot / SlotLot / StockHistory"]
        ISlotService["ISlotService"]
        IWarehouseService["IWarehouseService"]
        IStockHistoryRepo["IStockHistoryRepository"]
        StockTP_Core[("STOCKTP")]
    end

    %% ----------------------------------------------------
    %% KHU VUC 2: NHAP KHO
    %% ----------------------------------------------------
    subgraph NhapKho_Zone["NHAP KHO - STOCKTP Cong"]
        INhapTpService["INhapTpReceivingService"]
    end

    %% ----------------------------------------------------
    %% KHU VUC 3: XUAT KHO
    %% ----------------------------------------------------
    subgraph XuatKho_Zone["XUAT KHO - STOCKTP Tru + FVN_HangChoGiao"]
        IStockExportService["IStockExportService"]
        IHangChoGiaoRepo["IHangChoGiaoRepository"]
        StockTP_Xuat[("STOCKTP")]
    end

    %% ----------------------------------------------------
    %% KHU VUC 4: XU LY HANG LOI / QTCHUNG
    %% ----------------------------------------------------
    subgraph XuLyLoi_Zone["XU LY HANG LOI - Phieu / QTChung / Rework / GiaoBu"]

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

    %% ----------------------------------------------------
    %% NHAP KHO GOI KHO CORE
    %% ----------------------------------------------------
    INhapTpService -->|goi| ISlotService
    INhapTpService -->|goi| IWarehouseService
    INhapTpService -->|goi| IStockHistoryRepo
    INhapTpService -->|cap nhat ton| StockTP_Core

    %% ----------------------------------------------------
    %% XUAT KHO GOI KHO CORE
    %% ----------------------------------------------------
    IStockExportService -->|pick va tru ton| ISlotService
    IStockExportService -->|ghi lich su| IStockHistoryRepo
    IStockExportService -->|cap nhat ton| StockTP_Xuat
    IStockExportService -->|quan ly| IHangChoGiaoRepo

    %% ----------------------------------------------------
    %% XU LY HANG LOI - CAC LUONG KHOI TAO
    %% ----------------------------------------------------
    ServiceKhachTra -->|khoi tao| IQTChungService
    ServiceTraNoiBo -->|khoi tao| IQTChungService

    ServiceKhachTra -->|luu| TablePhieuKhachTra
    IQTChungService -->|luu phieu| TablePhieuXuLy

    %% ----------------------------------------------------
    %% NHANH TAO PHIEU NOI BO TU SLOT
    %% ----------------------------------------------------
    FormChonSlotNoiBo -.->|DOC Slot LOT dang ton| ISlotService

    FormChonSlotNoiBo -->|tao phieu Noi Bo| IQTChungService

    SlotReadOnly["READ ONLY<br/>Chi doc Slot LOT de lua chon<br/>Khong ghi hoac tru ton"]

    FormChonSlotNoiBo -.-> SlotReadOnly
    SlotReadOnly -.-> ISlotService

    %% ----------------------------------------------------
    %% QTCHUNG DIEU PHOI
    %% ----------------------------------------------------
    IQTChungService -->|dieu phoi| IReworkStockService
    IQTChungService -->|dieu phoi| IGiaoBuNGService

    %% ----------------------------------------------------
    %% REWORK / GIAO BU GOI XUAT KHO
    %% ----------------------------------------------------
    IReworkStockService -->|goi PickToChoGiao| IStockExportService
    IGiaoBuNGService -->|goi PickToChoGiao| IStockExportService

    %% Audit
    IReworkStockService -->|ghi audit| TableTraHangQTChung
    IGiaoBuNGService -->|ghi audit| TableTraHangQTChung

    %% ----------------------------------------------------
    %% STYLE
    %% ----------------------------------------------------
    style KhoCore_Zone fill:#e2f0d9,stroke:#385723,stroke-width:2px
    style NhapKho_Zone fill:#d9e1f2,stroke:#2f5597,stroke-width:2px
    style XuatKho_Zone fill:#fce4d6,stroke:#c65911,stroke-width:2px
    style XuLyLoi_Zone fill:#f8cecc,stroke:#b85450,stroke-width:2px

    style FormChonSlotNoiBo fill:#fff2cc,stroke:#bf9000,stroke-width:2px
    style SlotReadOnly fill:#fff2cc,stroke:#bf9000,stroke-width:1px
