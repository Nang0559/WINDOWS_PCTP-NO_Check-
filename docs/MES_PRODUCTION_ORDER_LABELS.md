# Lệnh sản xuất: in 2 loại tem thành phẩm + upload kế hoạch / workflow

Nền SQL: migration **065** (tem, quy cách đóng gói, lịch sử in) và **066** (màn hình layout `PRODUCTION_PLAN`, `PRODUCTION_ORDER`).
Phần C# này nối hai migration đó vào ứng dụng, cùng khuôn với "Đặt hàng linh kiện" (`COMPONENT_PO`).

## Tem
| Mã LabelCatalog | Tem | Mẫu |
|---|---|---|
| `PRODUCTION_CARTON` | Tem thùng thành phẩm | Ca SX / Ngày SX / Tên hàng / Mã hàng / QR / Nơi SX / Xuất xứ / SL / số tem 28 ký tự |
| `PRODUCTION_PALLET` | Phiếu quản lý nhập hàng thành phẩm ca | STT xe, Tên / Mã SP, Lot No, Số lượng, QR, khung xác nhận giao nhận |

Cả hai đăng ký trong `LabelCatalog` nên chỉnh được ở menu **Thiết kế layout tem** (Report Designer, lưu `.repx`). Bố cục gốc dựng bằng code
(`ProductionLabelReports.cs`), A4 ngang, 1 tem / trang. Một lệnh sản xuất in được cả hai loại.

## Màn hình (menu "Màn hình UI")
**Kế hoạch sản xuất** (`PRODUCTION_PLAN`, quyền `MES.PLAN_UPLOAD`): `Upload Excel kế hoạch…` → `Kiểm tra` → `Chốt kế hoạch` / `Hủy KH`.
**Lệnh sản xuất** (`PRODUCTION_ORDER`, quyền `MES.PRODUCTION_EXECUTION`): `Phát hành`, `Bắt đầu`, `Tạm dừng`, `Hoàn thành`, `Đóng lệnh`, `Hủy lệnh`,
`In tem thùng`, `In lại tem thùng`, `In tem palet`, `In lại tem palet`.

- Nút chỉ bật khi bảng workflow chuẩn (`MES_PRODUCTION_PLAN` / `MES_PRODUCTION_ORDER`) cho phép chuyển từ trạng thái hiện tại; service kiểm lại quyền theo Site / bộ phận, SQL kiểm lại trạng thái.
- In lần đầu: tạo phần tem còn thiếu theo `MES.FVN_ProductionPackSpec` (thiếu quy cách thì hỏi SL / gói) → in tem chưa in → ghi `INITIAL_PRINT`.
- In lại: chọn tem, bắt buộc lý do → ghi `REPRINT`. In lại được cả lệnh đã đóng.
- Tạo lệnh từ dòng kế hoạch FIRM vẫn làm ở **Điều hành sản xuất MES** (cần BOM / routing / đủ linh kiện).

## File
`PCTP/Modules/MesPlanning/ProductionLabels/`: `ProductionStatusCodes`, `ProductionLabelData`, `ProductionLabelReports`, `ProductionLabelCatalog`,
`ProductionOrderLabelPrint`, `ProductionPlanExcelReader`, `ProductionScreenActions`. Test: `PCTP.QC.Tests/ProductionLabelTests.cs`.
