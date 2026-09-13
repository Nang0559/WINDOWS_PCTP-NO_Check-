# WMS_DATA_OWNERSHIP

## 1. Quy tắc owner

Mỗi business data chỉ có **một module sở hữu quyền ghi**. Module khác chỉ đọc qua query/contract hoặc yêu cầu thay đổi qua application service.

| Data | Owner | Module khác |
|---|---|---|
| Warehouse | KhoCore | Read |
| Slot | KhoCore | Read / Command |
| SlotLot | KhoCore | Read / Command |
| STOCKTP | KhoCore | Read |
| StockHistory | KhoCore | Read |
| StockMovement | KhoCore | Read |
| Nhập chứng từ | NhapKho | Read/Command contract |
| HangChoGiao | XuatKho | Read/Command contract |
| Delivery/Giao hàng | GiaoHangKhach | Read/Command contract |
| Abnormal ticket | XuLyHangLoi | Read/Command contract |
| Rework process | XuLyHangLoi | Read/Command contract |
| Giao bù | XuLyHangLoi | Read/Command contract |

## 2. Dependency direction

```text
                    ┌───────────────┐
                    │    KhoCore    │
                    │ Stock State   │
                    └───────▲───────┘
                            │
            stock commands │ / queries
                            │
     ┌──────────┬───────────┼───────────┬────────────┐
     │          │           │           │            │
 NhapKho   XuatKho   GiaoHangKhach  XuLyHangLoi   UI/Report
```

Business modules must not reference each other's persistence repositories.

## 3. KhoVatLy migration rule

`KhoVatLy` is treated as **legacy physical-warehouse implementation**, not as a second business module.

Target:

```text
PCTP.Modules.KhoVatLy
          ↓ migration
PCTP.Modules.KhoCore.Infrastructure
```

Trong thời gian migration, code mới không được tạo thêm dependency từ module nghiệp vụ vào `KhoVatLy` repositories/models.

## 4. Specific ownership rules

### Slot

Only KhoCore may perform authoritative writes to Slot/SlotLot.

### STOCKTP

Only KhoCore may perform authoritative writes. `STOCKTP` is a projection/aggregate of stock state, not a shared write table.

### HangChoGiao

Belongs to XuatKho/GiaoHangKhach. It represents outbound delivery preparation, not warehouse quantity itself.

### Hàng lỗi

A defect/rework record does not automatically mean a stock movement. The business service must explicitly create a stock command when physical quantity changes.

## 5. Read models

Legacy `DataTable`/view based queries may remain at the UI boundary during migration. They must not leak into Domain/Application contracts of KhoCore.

Preferred direction:

```text
SQL/View -> Infrastructure DTO -> Application DTO -> UI
```

not:

```text
SQL/View -> DataTable -> Domain service -> Form
```

## 6. Enforcement checklist

A code review must reject a change when it introduces:

- `STOCKTP` UPDATE outside KhoCore
- Slot/SlotLot UPDATE outside KhoCore
- direct repository dependency across business modules
- Form code that performs stock transaction orchestration
- a second stock history writer
