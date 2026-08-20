Tài Liệu Quy Trình Nghiệp Vụ: Xử Lý Hàng Lỗi / QTChung (Exception & Rework Workflow)
Tài liệu này mô tả chi tiết luồng xử lý các phiếu bất thường từ khách hàng hoặc nội bộ, thực hiện QC định hướng, điều phối qua bảng chờ giao, tiến hành sửa chữa (Rework) tại xưởng và phân tách sản lượng OK/NG để nhập lại kho.
---
1. Mô Tả Chi Tiết Các Bước Trong Luồng Xử Lý Hàng Lỗi
1.1. Khởi Tạo & Tiếp Nhận Ban Đầu
Tiếp nhận thông tin từ `IPhieuKhachTraRepository` thông qua:
`IKhachTraHangService` — nguồn: Khách hàng.
`ITraNoiBoService` — nguồn: Nội bộ.
Gọi `IQTChungService.TaoPhieuXuLyBatThuong` để khởi tạo phiếu với trạng thái:
`Moi` → `DaTaoPhieuBatThuong`.
Nhánh 1c — Tạo trực tiếp từ Slot, nguồn Nội Bộ, không qua chứng từ khách trả
`FormChonSlotNoiBo` đọc danh sách Slot/LOT đang tồn qua `ISlotService.GetAllActiveSlotLots()` (Kho Core), sau đó gọi `IPhieuLoiRepository.InsertPhieuXuLyBatThuongNoiBo` để tạo phiếu với:
`Nguon = NguonPhieuBatThuong.NoiBo`
`TrangThai = ChoQC`
Phiếu rơi thẳng vào bước 2 (QC Định Hướng) và dùng chung toàn bộ luồng phía sau với phiếu sinh từ khách trả.
Liên kết kiến trúc quan trọng — đọc trực tiếp Kho Core:
```text
FormChonSlotNoiBo
    -. đọc để chọn Slot/LOT .-> ISlotService (Kho Core)
```
Đây là luồng đọc dữ liệu, không phải luồng ghi/trừ tồn kho. `FormChonSlotNoiBo` chỉ đọc danh sách Slot/LOT đang tồn để người dùng chọn và tạo phiếu; không được hiểu là module Xử Lý Hàng Lỗi tự ý cập nhật tồn tại `ISlotService`.
Điểm này khác bản chất với các luồng xuất kho như `IReworkStockService` / `IGiaoBuNGService` gọi `IStockExportService` để thực hiện Pick/điều phối và trừ tồn theo nghiệp vụ.
---
1.2. QC Định Hướng (Gate Quyết Định)
Thực hiện qua `IQTChungService.QCDinhHuongRework` để chuyển trạng thái sang `DaDinhHuongRework` và phân tách thành 3 nhánh xử lý chính:
Nhánh 1 — Khách không lỗi thật:
Dừng quy trình.
Từ chối giao bù.
`END`.
Nhánh 2 — Khách có lỗi thật, cần giao bù:
Gọi `IGiaoBuNGService.GiaoBuTheoQR`.
Gọi `IStockExportService.PickToChoGiao` với loại `GiaoBuNG`.
Xác nhận hoàn tất qua `IGiaoBuNGService.XacNhanHoanTatGiaoBu`.
Thực hiện `ConfirmGiaoHangTuChoGiao` để hoàn tất việc xuất/trừ tồn theo luồng giao bù.
Nhánh 3 — Nội bộ / Khách cần Rework:
Chuyển sang luồng xuất kho Rework.
---
1.3. Xuất Kho Rework & Giao Sản Xuất
Gọi `IQTChungService.XuatKhoRework`.
Gọi `IReworkStockService.XuatKhoRework`.
Gọi `IStockExportService.PickToChoGiao` với loại `Rework`.
Thực hiện xác nhận xuất qua `IReworkStockService.XacNhanXuatRework`, kết hợp:
`ConfirmGiaoHangTuChoGiao`
`ITraHangQTChungRepository.InsertXuat`
Chuyển trạng thái sang `DaXuatKhoRework`.
Ghi nhận giao sản xuất qua `IQTChungService.GhiNhanGiaoSanXuat`:
`ITraHangQTChungRepository.InsertGiao`
Không dùng Slot/STOCKTP.
Trạng thái: `DaGiaoSanXuat`.
---
1.4. Tiến Hành Rework & QC Xác Nhận Cuối
Sản phẩm được tiến hành sửa chữa tại xưởng — đây là mốc trạng thái ngoài hệ thống.
Sau khi hoàn tất, thực hiện `IQTChungService.QCXacNhanCuoi` qua `ITraHangQTChungRepository.InsertQC` để ghi nhận:
`SoLuongOK`
`SoLuongNG`
Chuyển trạng thái sang `DaQCXacNhanCuoi`.
1.4b. Kiểm Tra Tem Khi Nhập Lại Sau Rework
Sau `QCXacNhanCuoi`:
Nếu mã hàng có `NeedsInspection = true`
Thì chạy `FormInspection` cho phần hàng OK
Sau đó mới thực hiện `NhapLaiHangNG`.
---
1.5. Nhập Lại Kho & Hoàn Tất
Trường hợp sản phẩm đạt chuẩn hoàn toàn
Nếu `SoLuongNG = 0`:
Không phát sinh hàng NG.
Chuyển thẳng trạng thái `HoanTat`.
Trường hợp phát sinh phế phẩm
Nếu `SoLuongNG > 0`, gọi `IReworkStockService.NhapLaiHangNG` để:
Phần OK:
Cộng lại lượng tồn qua `ISlotService.AddQuantity` tại Kho Core.
Tăng tồn kho tổng `STOCKTP +`.
Phần NG:
Định tuyến vào Slot hàng lỗi riêng biệt.
Ghi nhận audit qua `ITraHangQTChungRepository.InsertNhapNG`.
Chuyển trạng thái sang `HoanTat`.
Kết thúc toàn bộ quy trình sự kiện QTChung.
---
2. Sơ Đồ Quy Trình Xử Lý Hàng Lỗi / QTChung
```mermaid
graph TD
    %% KHỞI TẠO & TIẾP NHẬN BAN ĐẦU
    StartRepo[IPhieuKhachTraRepository] --> B1[IKhachTraHangService<br/>Nguồn: Khách Hàng]
    StartRepo --> B2[ITraNoiBoService<br/>Nguồn: Nội Bộ]

    B1 --> Step1["IQTChungService.TaoPhieuXuLyBatThuong<br/>Status: Moi -> DaTaoPhieuBatThuong"]
    B2 --> Step1

    %% NHÁNH 1c: TẠO TRỰC TIẾP TỪ SLOT NỘI BỘ
    SlotForm["FormChonSlotNoiBo<br/>Tạo phiếu trực tiếp từ Slot/LOT"] -->|Đọc Slot/LOT đang tồn| SlotService["ISlotService<br/>(Kho Core)"]
    SlotService -->|GetAllActiveSlotLots()| SlotForm
    SlotForm -->|InsertPhieuXuLyBatThuongNoiBo<br/>Nguon = NoiBo<br/>TrangThai = ChoQC| Step2

    %% NHẤN MẠNH: ĐỌC, KHÔNG GHI/TRỪ TỒN
    SlotReadNote["READ ONLY<br/>Đọc dữ liệu để chọn Slot/LOT<br/>Không ghi/trừ tồn tại ISlotService"]
    SlotForm -.-> SlotReadNote
    SlotReadNote -.-> SlotService

    Step1 --> Step2["IQTChungService.QCDinhHuongRework<br/>(gate quyết định)<br/>Status: DaDinhHuongRework"]

    %% PHÂN NHÁNH 1: KHÁCH KHÔNG LỖI THẬT
    Step2 -->|Khách: Không lỗi thật| EndNoErr[END — Từ chối giao bù]

    %% PHÂN NHÁNH 2: KHÁCH CÓ LỖI THẬT (GIAO BÙ)
    Step2 -->|Khách: Có lỗi thật, chỉ cần giao bù| GiaoBu1["IGiaoBuNGService.GiaoBuTheoQR<br/>-> IStockExportService.PickToChoGiao<br/>(Loại = GiaoBuNG)"]

    GiaoBu1 --> GiaoBu2["IGiaoBuNGService.XacNhanHoanTatGiaoBu<br/>-> ConfirmGiaoHangTuChoGiao"]
    GiaoBu2 --> EndGiaoBu([END])

    %% PHÂN NHÁNH 3: NỘI BỘ / KHÁCH CẦN REWORK
    Step2 -->|Nội bộ / Khách cần Rework| Rework1["IQTChungService.XuatKhoRework<br/>-> IReworkStockService.XuatKhoRework<br/>-> IStockExportService.PickToChoGiao<br/>(Loại = Rework)<br/>Status: DaXuatKhoRework"]

    Rework1 --> Rework2["IReworkStockService.XacNhanXuatRework<br/>-> ConfirmGiaoHangTuChoGiao<br/>+ ITraHangQTChungRepository.InsertXuat"]

    Rework2 --> Step5["IQTChungService.GhiNhanGiaoSanXuat<br/>ITraHangQTChungRepository.InsertGiao<br/>(KHÔNG dùng Slot/STOCKTP)<br/>Status: DaGiaoSanXuat"]

    Step5 --> Step6[Rework tại xưởng<br/>mốc trạng thái ngoài hệ thống]

    Step6 --> Step7["IQTChungService.QCXacNhanCuoi<br/>ITraHangQTChungRepository.InsertQC<br/>(SoLuongOK / SoLuongNG)<br/>Status: DaQCXacNhanCuoi"]

    %% KIỂM TRA TEM SAU QC CUỐI
    Step7 -->|NeedsInspection = true| Inspection["FormInspection<br/>Kiểm tra tem phần hàng OK"]
    Step7 -->|NeedsInspection = false| QtyCheck["Kiểm tra SoLuongNG"]
    Inspection --> QtyCheck

    %% PHÂN TÁCH SAU QC CUỐI
    QtyCheck -->|SoLuongNG = 0| StatusHoanTat1[Status: HoanTat]
    QtyCheck -->|SoLuongNG > 0| Step8["IReworkStockService.NhapLaiHangNG<br/>- OK: ISlotService.AddQuantity + STOCKTP +<br/>- NG: Route Slot NG riêng<br/>- ITraHangQTChungRepository.InsertNhapNG"]

    Step8 --> StatusHoanTat2[Status: HoanTat]

    %% KẾT THÚC CHUNG
    StatusHoanTat1 --> FinalEnd([🏁 KẾT THÚC])
    StatusHoanTat2 --> FinalEnd
    EndNoErr --> FinalEnd
    EndGiaoBu --> FinalEnd

    %% STYLING
    style StartRepo fill:#f9f,stroke:#333,stroke-width:2px
    style Step2 fill:#bbf,stroke:#333,stroke-width:2px
    style GiaoBu1 fill:#bfb,stroke:#333,stroke-width:2px
    style Rework1 fill:#fbb,stroke:#333,stroke-width:2px
    style SlotForm fill:#ffeb99,stroke:#333,stroke-width:2px
    style SlotService fill:#d9ead3,stroke:#333,stroke-width:2px
    style SlotReadNote fill:#fff2cc,stroke:#333,stroke-width:1px
    style FinalEnd fill:#fbb,stroke:#333,stroke-width:2px
```
---
3. Nguyên Tắc Phân Biệt Đọc Kho và Ghi/Trừ Kho
Để tránh hiểu sai kiến trúc, cần phân biệt rõ hai loại tương tác với `ISlotService` / Kho Core:
Luồng	Thành phần	Hành động	Bản chất
Tạo phiếu từ Slot nội bộ	`FormChonSlotNoiBo` → `ISlotService.GetAllActiveSlotLots()`	Đọc Slot/LOT đang tồn	READ ONLY
Giao bù NG	`IGiaoBuNGService` → `IStockExportService.PickToChoGiao`	Pick/điều phối hàng giao bù	Ghi/trừ tồn theo nghiệp vụ
Xuất kho Rework	`IReworkStockService` → `IStockExportService.PickToChoGiao`	Pick/điều phối hàng Rework	Ghi/trừ tồn theo nghiệp vụ
Nhập lại hàng OK	`IReworkStockService` → `ISlotService.AddQuantity`	Cộng lại tồn	Ghi tồn
Nhập hàng NG	`IReworkStockService` → Slot NG	Định tuyến hàng lỗi	Ghi tồn/điều chỉnh theo nghiệp vụ
Kết luận: `FormChonSlotNoiBo` có quyền đọc `ISlotService` để lấy Slot/LOT đang tồn khi tạo phiếu nội bộ, nhưng không phải là luồng xuất kho và không được tự ý ghi/trừ tồn tại Kho Core.
---
4. Tóm Tắt Các Interface/Service Chính
Thành phần	Vai trò chính
`IPhieuKhachTraRepository`	Nguồn dữ liệu phiếu khách trả
`IKhachTraHangService`	Xử lý nguồn khách hàng
`ITraNoiBoService`	Xử lý nguồn nội bộ
`FormChonSlotNoiBo`	Chọn Slot/LOT đang tồn và tạo phiếu nội bộ trực tiếp
`ISlotService`	Đọc/cập nhật Slot tại Kho Core
`IPhieuLoiRepository`	Tạo phiếu xử lý bất thường nội bộ
`IQTChungService`	Điều phối nghiệp vụ QTChung
`IGiaoBuNGService`	Xử lý giao bù hàng NG
`IReworkStockService`	Điều phối xuất kho Rework và nhập lại sau Rework
`IStockExportService`	Pick/điều phối xuất hàng vào bảng chờ giao
`ITraHangQTChungRepository`	Ghi nhận audit các mốc Xuất/Giao/QC/Nhập NG
`FormInspection`	Kiểm tra tem cho hàng OK trước khi nhập lại trong trường hợp `NeedsInspection = true`
---
5. Điểm Kiến Trúc Cần Lưu Ý
`FormChonSlotNoiBo` đọc trực tiếp `ISlotService` để lấy danh sách Slot/LOT đang tồn.
Luồng đọc này không đi qua `IStockExportService`.
Việc đọc Slot/LOT khi tạo phiếu không đồng nghĩa với việc cập nhật tồn kho.
`IStockExportService` chỉ xuất hiện trong các luồng thực sự điều phối hàng vào `ChoGiao`, như:
Giao bù NG.
Xuất kho Rework.
Việc cộng tồn sau Rework được thực hiện qua `ISlotService.AddQuantity` trong `IReworkStockService.NhapLaiHangNG`.
Sau `QCXacNhanCuoi`, nếu `NeedsInspection = true`, phải chạy `FormInspection` cho phần hàng OK trước khi thực hiện `NhapLaiHangNG`.
Luồng tạo phiếu nội bộ trực tiếp từ Slot có `TrangThai = ChoQC`, do đó bỏ qua bước tạo phiếu từ chứng từ khách trả nhưng vẫn dùng chung Gate `QCDinhHuongRework` và toàn bộ luồng phía sau.
