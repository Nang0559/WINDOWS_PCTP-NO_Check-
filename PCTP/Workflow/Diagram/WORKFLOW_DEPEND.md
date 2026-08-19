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
        TablePhieuXuLy[(FVN_PhieuXuLyBatThuuong)]
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