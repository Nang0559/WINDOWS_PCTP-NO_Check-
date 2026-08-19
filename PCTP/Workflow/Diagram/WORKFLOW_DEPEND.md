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
    %% KHU VỰC 1: KHO CORE (MÀU XANH LÁ)
    %% ----------------------------------------------------
    subgraph KhoCore_Zone ["KHO CORE — Warehouse / Rack / Slot / SlotLot / StockHistory"]
        ISlotService[ISlotService]
        IWarehouseService[IWarehouseService]
        IStockHistoryRepo[IStockHistoryRepository]
        StockTP_Core[(STOCKTP)]
    end

    %% ----------------------------------------------------
    %% KHU VỰC 2: NHẬP KHO (MÀU XANH DƯƠNG)
    %% ----------------------------------------------------
    subgraph NhapKho_Zone ["NHẬP KHO — STOCKTP (cộng)"]
        INhapTpService[INhapTpReceivingService]
    end

    %% ----------------------------------------------------
    %% KHU VỰC 3: XUẤT KHO (MÀU CAM)
    %% ----------------------------------------------------
    subgraph XuatKho_Zone ["XUẤT KHO — STOCKTP (trừ) + FVN_HangChoGiao"]
        IStockExportService[IStockExportService]
        IHangChoGiaoRepo[IHangChoGiaoRepository]
        StockTP_Xuat[(STOCKTP)]
    end

    %% ----------------------------------------------------
    %% KHU VỰC 4: XỬ LÝ HÀNG LỖI / QTCHUNG (MÀU ĐỎ)
    %% ----------------------------------------------------
    subgraph XuLyLoi_Zone ["XỬ LÝ HÀNG LỖI — Phiếu / QTChung / Rework / GiaoBu"]
        ServiceKhachTra[IKhachTraHangService / ITraNoiBoService]
        IQTChungService[IQTChungService]
        IReworkStockService[IReworkStockService]
        IGiaoBuNGService[IGiaoBuNGService]
        
        TablePhieuKhachTra[(FVN_PhieuKhachTra)]
        TablePhieuXuLy[(FVN_PhieuXuLyBatThuong)]
        TableTraHangQTChung[(FVN_TraHangQTChung_*)]
    end

    %% ----------------------------------------------------
    %% CÁC LUỒNG LIÊN KẾT GIỮA CÁC PHÂN KHU
    %% ----------------------------------------------------
    
    %% Nhập kho gọi Core
    INhapTpService -->|gọi| ISlotService
    INhapTpService -->|gọi| IWarehouseService
    INhapTpService -->|gọi| IStockHistoryRepo
    INhapTpService -->|tự sở hữu| StockTP_Core

    %% Xuất kho gọi Core & Quản lý bảng chờ giao
    IStockExportService -->|gọi| ISlotService
    IStockExportService -->|gọi| IStockHistoryRepo
    IStockExportService -->|tự sở hữu| StockTP_Xuat
    IStockExportService -->|tự sở hữu| IHangChoGiaoRepo

    %% Xử lý hàng lỗi khởi tạo & lưu trữ bảng phụ
    ServiceKhachTra -->|khởi tạo| IQTChungService
    ServiceKhachTra -->|tự sở hữu| TablePhieuKhachTra
    IQTChungService -->|tự sở hữu| TablePhieuXuLy

    %% Xử lý hàng lỗi điều phối các Service nghiệp vụ chuyên sâu
    IQTChungService -->|điều phối| IReworkStockService
    IQTChungService -->|điều phối| IGiaoBuNGService

    %% Rework & Giao bù tương tác kiểm soát qua Xuất kho
    IReworkStockService -->|tự sở hữu, chỉ audit| TableTraHangQTChung
    IReworkStockService -->|gọi, KHÔNG tự đụng Slot/STOCKTP| IStockExportService
    IGiaoBuNGService -->|gọi, KHÔNG tự đụng Slot/STOCKTP| IStockExportService

    %% STYLING KHU VỰC
    style KhoCore_Zone fill:#e2f0d9,stroke:#385723,stroke-width:2px
    style NhapKho_Zone fill:#d9e1f2,stroke:#2f5597,stroke-width:2px
    style XuatKho_Zone fill:#fce4d6,stroke:#c65911,stroke-width:2px
    style XuLyLoi_Zone fill:#f8cecc,stroke:#b85450,stroke-width:2px
