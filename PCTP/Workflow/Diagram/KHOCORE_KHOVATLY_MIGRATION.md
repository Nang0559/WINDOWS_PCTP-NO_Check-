# KhoCore ↔ KhoVatLy Migration

## 1. Mục tiêu

`KhoCore` là owner của capability kho: Warehouse, Rack, Slot, SlotLot, tồn kho vật lý và stock movement.
`KhoVatLy` chỉ còn là vùng legacy/presentation trong giai đoạn chuyển đổi và không được tạo thêm business dependency mới.

## 2. Hiện trạng đã xác nhận

`PCTP/Modules/KhoCore/Services/SlotService.cs` hiện vẫn phụ thuộc trực tiếp vào nhiều namespace của `KhoVatLy`, UI/DataTable và model cũ. Đây là service quá rộng: query slot, mutation tồn, LOT, move LOT, virtual slot và backup/restore UI cùng nằm trong một class.

`ISlotService` cũng đang khai báo dưới namespace `PCTP.Modules.KhoVatLy.Application.Interfaces` và chứa `DataTable`, `Slot`, `LotInfo` cùng các API mutation. Điều này làm ranh giới KhoCore bị đảo ngược.

Repository vật lý đã nằm trong `KhoCore/Repositories`, nhưng `ISlotRepository` và `SlotRepository` vẫn mang namespace legacy `PCTP.Modules.KhoVatLy.Repositories`. Đây là compatibility debt cần xử lý sau khi caller migration hoàn tất.

## 3. Nguyên tắc migration

```text
Business module
    -> KhoCore.Application.Contracts
    -> legacy adapter (tạm thời)
    -> legacy storage implementation
```

Không cho phép:

- KhoCore Application mới import `Modules.KhoVatLy.*`.
- Business module mới dùng `ISlotService` legacy.
- Contract mới trả `DataTable`, `Control`, `Form`, `Slot` UI model hoặc legacy DTO.
- Gọi trực tiếp `STOCKTP`/`Slot` từ từng business module khi movement đã có thể đi qua `IStockMovementService`.

## 4. Phân loại

| Thành phần | Quyết định | Hướng xử lý |
|---|---|---|
| Warehouse/Rack/Slot/SlotLot persistence | KEEP in KhoCore | Chuẩn hóa namespace về KhoCore.Infrastructure/Repositories |
| `SlotService` legacy | TEMPORARY | Không mở rộng; migrate caller rồi loại bỏ |
| `ISlotService` legacy | TEMPORARY | Thay bằng query/command contracts |
| Slot query | KEEP | `ISlotQueryService` / `SlotLocation` |
| Stock mutation | MOVE | `IStockMovementService` |
| UI backup/restore/clear memory | MOVE OUT | UI/presentation adapter |
| `DataTable` lookup | MOVE OUT | UI adapter/query DTO |
| `MoveLot` | MOVE | Stock movement/internal warehouse command |
| Virtual slot | KEEP in KhoCore | Đưa thành warehouse capability, không để XuatKho tự quản lý |

## 5. Đã chuẩn hóa

- `KhoCore.Application.Contracts.Slot.SlotLocation`
- `KhoCore.Application.Contracts.Slot.ISlotQueryService`
- Legacy-side `KhoCoreSlotQueryAdapter`
- `KhoCore.Application.Contracts.Stock.StockMovementRequest`
- `KhoCore.Application.Contracts.Stock.StockMovementResult`
- `KhoCore.Application.Contracts.Stock.IStockMovementService`

Các contract mới không phụ thuộc WinForms, DevExpress, `DataTable` hoặc model của `KhoVatLy`.

## 6. Thứ tự migration tiếp theo

1. Migrate caller chỉ đọc slot sang `ISlotQueryService`.
2. Migrate caller mutation khỏi `ISlotService`.
3. Chuẩn hóa `ISlotRepository`/`SlotRepository` sang namespace KhoCore.
4. Đưa implementation DB vào `KhoCore/Infrastructure` hoặc `KhoCore/Repositories` theo một convention duy nhất.
5. Tách `SlotService` thành query + movement/command; không giữ một God Service mới.
6. Xóa compatibility adapter sau khi không còn caller.
7. Chuyển `StockExportService` sang `IStockMovementService`.

## 7. Gate

Migration chỉ được coi là hoàn tất khi:

- Không còn `KhoCore.Application` import `Modules.KhoVatLy.*`.
- Không còn business module mới phụ thuộc `ISlotService`.
- Tồn kho có một single writer qua stock movement boundary.
- UI types không đi xuyên qua Application contract.
