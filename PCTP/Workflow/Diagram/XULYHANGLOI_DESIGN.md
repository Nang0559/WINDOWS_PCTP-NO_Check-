# Thiết kế hệ thống — XuLyHangLoi / QT Chung

## 1. Phạm vi

Module xử lý toàn bộ vòng đời hàng lỗi: tạo phiếu → truy vết LOT → QC ban đầu → Rework → QC sau Rework → Disposition → Giao bù → Workflow/Audit → Báo cáo.

## 2. Nguyên tắc nghiệp vụ cốt lõi

Bốn khái niệm phải độc lập:

| Khái niệm | Ý nghĩa | Nguồn quyết định |
|---|---|---|
| NG | Kết quả chất lượng không đạt | QC |
| Rework | Phương pháp xử lý phần NG có thể sửa | QC/Quy trình kỹ thuật |
| Disposition | Xử lý phần không thể sửa hoặc NG cuối | QC/Quy định xử lý |
| Compensation | Nghĩa vụ thay thế cho khách hàng | Đơn hàng/Giao hàng |

**Không suy ra Compensation từ NG, Rework NG hoặc Disposition.**

## 3. Kiến trúc

```text
UI / WinForms
    ↓
Application Services
    ├── AffectedLotTraceService
    ├── InitialQCService
    ├── ReworkPhase4Service
    ├── HangLoiPhase5To9Service
    └── WorkflowTransitionService
    ↓
Repositories / Infrastructure
    ├── PhieuXuLyBatThuongRepository
    ├── TraHangQTChungRepository
    ├── Stock / Slot services
    └── LOT trace providers
    ↓
SQL Server
```

`XuLyHangLoiModuleFactory` là composition root. Module hiện expose các service chính qua `XuLyHangLoiModuleFactory.Module`, thay vì để Form tự dựng dependency hoặc tự chứa business rule.

## 4. LOT Trace

`AffectedLotTraceService` hợp nhất:

1. Tồn kho thành phẩm.
2. WIP/sản xuất.
3. Hàng khách trả.

Kết quả được snapshot vào `FVN_PhieuXuLyBatThuongAffectedLot`. Snapshot là nguồn dữ liệu cố định cho QC của phiếu.

Mỗi dòng giữ nguồn/reference/slot/LOT/sản phẩm/số lượng để có thể audit và truy ngược.

## 5. Initial QC

Bảng: `FVN_PhieuXuLyBatThuongQCInitial`.

Mỗi phiếu chỉ có một Initial QC.

Invariant:

```text
DaKiemTra = OK + NG
NG        = Rework + LoaiBoBanDau
DaKiemTra <= SoLuongAnhHuong
```

Tổng QC theo các LOT phải phủ toàn bộ snapshot ảnh hưởng.

Nếu hướng xử lý không cho Rework thì Rework phải bằng 0.

## 6. Phase 4 — Rework

Nguồn duy nhất của kế hoạch Rework:

```text
ReworkPlan = InitialQC.SoLuongRework
```

Không dùng `SoLuongLoi` legacy và không lấy toàn bộ NG làm Rework.

`ReworkPhase4Service` kiểm soát:

- kế hoạch Rework;
- đã xuất;
- đã giao sản xuất;
- còn phải xuất;
- còn phải giao;
- không xuất vượt kế hoạch;
- không giao trước khi xuất đủ;
- không QC trước khi giao đủ.

Cho phép nhiều lần xuất/giao, nhưng tổng lũy kế không vượt kế hoạch.

## 7. Phase 5 — Rework QC

Chỉ được QC sau khi toàn bộ Rework đã được xuất và giao sản xuất.

Invariant:

```text
ReworkQC.OK + ReworkQC.NG = ReworkPlan
```

Một phiếu chỉ có một kết quả Rework QC cuối.

## 8. Phase 6 — Disposition

```text
FinalDisposition = InitialLoaiBo + ReworkNG
```

NG sau Rework không tự quay lại Phase 4. Nếu cần một vòng xử lý mới phải có nghiệp vụ/phiếu mới với nguồn rõ ràng.

## 9. Phase 6 — Compensation

Compensation được tạo thành nghĩa vụ riêng:

```text
Nghĩa vụ khách hàng
    ↓
Compensation Required
    ↓
Chọn hàng thay thế
    ↓
Xuất/Giao
    ↓
Remaining = Required - Delivered
```

Không cho giao vượt Remaining.

Cơ chế tìm hàng thay thế sử dụng stock/LOT/FIFO hiện hành của hệ thống.

## 10. Phase 7 — Workflow

QT Chung dùng `ProcessCode = QT_CHUNG`.

Transition nằm trong `sys_WorkflowTransitions` và được đọc qua:

```text
IWorkflowRepository
    ↓
WorkflowTransitionService
    ↓
Application Service
```

Audit transition được lưu riêng. Không thêm dictionary transition mới trong Form.

Các nhánh chính:

```text
DaDinhHuong
 ├─ TuChoiGiaoBu → HoanTat
 ├─ ChoGiaoBu → DaGiaoBu → HoanTat
 └─ Rework → DaXuatKhoRework → DaGiaoSanXuat
                  → DaQCXacNhanCuoi
                     ├─ OK → HoanTat
                     └─ NG → DaNhapLaiKho → HoanTat
```

## 11. Phase 8 — UI boundary

Form chịu trách nhiệm:

- hiển thị dữ liệu;
- scan QR/LOT;
- nhập dữ liệu người dùng;
- hiển thị cảnh báo;
- gọi Application Service.

Form không chịu trách nhiệm:

- quyết định invariant;
- sửa tồn kho trực tiếp;
- tự tính Compensation;
- tự chuyển trạng thái trái workflow.

## 12. Phase 9 — Reporting

`vFVN_PXLB_ProcessReport` cung cấp tổng hợp theo phiếu:

- Affected quantity;
- Initial QC OK/NG;
- Rework planned/exported/delivered;
- Rework QC OK/NG;
- Final Disposition;
- Compensation required/delivered/remaining.

Báo cáo phải truy ngược được về LOT và lịch sử nghiệp vụ.

## 13. Transaction và audit

Các nghiệp vụ thay đổi tồn kho + ghi lịch sử + ghi trạng thái phải chạy trong cùng Unit of Work khi cùng một business transaction.

Khi có lỗi giữa chừng: rollback toàn bộ transaction liên quan.

## 14. Tương thích legacy

`SoLuongLoi` và các bảng legacy có thể còn được giữ để tương thích màn hình/code cũ, nhưng không được dùng làm source of truth cho Initial QC/Rework/Compensation mới.

## 15. Tài liệu liên quan

- `WORKFLOW_HANGLOI.md` — tài liệu workflow hiện hữu.
- `XULYHANGLOI_USER_GUIDE.md` — hướng dẫn vận hành.
- `PHASE4_9_COMPLETION_STATUS.md` — status Phase 4–9.
- `PHASE2_AffectedLotSnapshot.sql` — snapshot LOT.
- `PHASE3_InitialQC.sql` — Initial QC.
- `PHASE7_WorkflowSeed.sql` — workflow seed.
