# WMS_STOCK_TRANSACTION_RULES

## 1. Mục tiêu

Đây là contract bắt buộc cho mọi nghiệp vụ làm thay đổi tồn kho trong PCTP.

Nguyên tắc cốt lõi: **một nghiệp vụ tồn kho = một transaction nguyên tử = một audit trail**.

Không module nghiệp vụ nào được tự cập nhật đồng thời `Slot`, `SlotLot`, `STOCKTP` và history theo cách riêng.

## 2. Single Writer

`KhoCore` là owner của stock state.

Các module `NhapKho`, `XuatKho/GiaoHangKhach`, `XuLyHangLoi` chỉ phát sinh **stock command**. Việc ghi tồn phải được thực hiện bởi một application service trung tâm của KhoCore.

```text
NhapKho ────────┐
XuatKho ────────┤
GiaoHangKhach ──┼──> StockMovementService ──> KhoCore
XuLyHangLoi ────┘
```

## 3. Transaction matrix

| Nghiệp vụ | Slot/SlotLot | STOCKTP | History | Ghi chú |
|---|---|---|---|---|
| Nhập kho OK | + | + | + | Receive |
| Pick xuất kho | - | 0 | + | Hàng rời vị trí, chưa xuất khỏi kho tổng |
| Xuất thực tế | 0 | - | + | Export |
| Chuyển Slot | - / + | 0 | + | Cùng một movement |
| Trả từ Rework OK | + | + | + | ReturnFromRework |
| Giao bù | - | - | + | Phải dùng stock command chuẩn |
| Hủy giao chưa xuất | + | + hoặc 0* | + | Theo trạng thái nguồn |

`*` Không được tự suy diễn; command phải xác định source state trước khi ghi.

## 4. Bất biến bắt buộc

1. Quantity > 0 đối với mọi movement thực tế.
2. Không được âm tồn.
3. Không vượt capacity của Slot.
4. Lot phải thuộc đúng ItemCode.
5. Movement phải có `ReferenceType` + `ReferenceId`.
6. Movement phải có user/audit information.
7. Một movement không được ghi một phần.
8. Không ghi history nếu stock transaction rollback.
9. Không cập nhật `STOCKTP` trực tiếp từ Form.
10. Không cập nhật `Slot` trực tiếp từ module nghiệp vụ ngoài KhoCore.

## 5. Idempotency

Các command từ nghiệp vụ có thể được gọi lại do retry. Vì vậy command có thể mang `MovementId`/`ReferenceId` duy nhất.

Nếu movement đã hoàn tất, service phải trả kết quả idempotent thay vì cộng/trừ lần hai.

## 6. Locking

Các operation cạnh tranh trên cùng Slot/Lot phải lock theo thứ tự ổn định:

```text
Slot source -> Slot destination -> STOCKTP key
```

Không giữ lock qua UI interaction.

## 7. Audit tối thiểu

Mọi stock movement phải truy được:

- ItemCode
- LotNo (nếu có)
- SourceSlot
- DestinationSlot (nếu có)
- Quantity
- MovementType
- ReferenceType
- ReferenceId
- User
- Timestamp
- Reason/Comment (khi nghiệp vụ yêu cầu)

## 8. Quy tắc cho module

### Nhập Kho

Không tự ghi `STOCKTP` và history ngoài StockMovementService.

### Xuất Kho / Giao Hàng Khách

Pick và Export là hai movement khác nhau. Không coi `HangChoGiao` là tồn kho.

### Xử Lý Hàng Lỗi

`Abnormal/Rework/GiaoBu` là business workflow. Chỉ khi phát sinh vật lý thay đổi tồn mới tạo stock command.

### KhoCore

Chịu trách nhiệm validate, lock, mutate stock state và audit transaction.

## 9. Anti-patterns cấm

```text
Form -> UPDATE STOCKTP
Form -> UPDATE Slot
Form -> INSERT StockHistory
ReworkService -> repository.UpdateSlot(...)
GiaoBuService -> repository.UpdateStock(...)
```

Các pattern trên phải được loại bỏ dần trong quá trình migration.
