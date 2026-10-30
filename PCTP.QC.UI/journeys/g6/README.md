# G6 - journey nghiệp vụ có seed / reset

Mỗi journey theo thứ tự: `sql-seed` -> thao tác UI thật -> `sql-assert` -> `sql-cleanup` -> verify cleanup.

## Ranh giới DB và quyền chạy test

- **DB57 / 192.168.200.57 / B7R2_FCC là DB kiểm thử được phép ghi/xóa dữ liệu cho acceptance của dự án.** Chỉ chạy với `-LiveDb57`, tài khoản QC riêng, prefix duy nhất `QCUI_<8 ký tự>`, và cleanup có verify.
- **DB14 / BRAVO tuyệt đối không dùng để seed, chạy giao dịch test hoặc cleanup.** DB14 chỉ là đích promotion sau khi DB57 có đủ bằng chứng.
- Không được xem manifest `configured:true` là bằng chứng runtime. Chỉ Windows runner thực thi UI + SQL assertions + cleanup đầy đủ mới tạo evidence G6.

## Trạng thái manifest

| Manifest | Trạng thái hiện tại | Ghi chú |
|---|---|---|
| `g6-selftest.json` | Sẵn sàng về framework | Dùng bảng `#temp`, không ghi bảng nghiệp vụ |
| `g6-qc-direction.json` | **BLOCKED / chưa cấu hình** | Chưa có seed/assert/cleanup nghiệp vụ đã xác minh |
| `g6-qc-final.json` | **BLOCKED / chưa cấu hình** | Chưa có seed/assert/cleanup nghiệp vụ đã xác minh |
| `g6-receive.json` | Configured, chưa có runtime evidence | Seed B20Item/B30AccDoc/kho-kệ-slot; scan 2 QR thật; assert STOCKTP/SlotLot/StockHistory |
| `g6-delivery-hvn.json` | Configured nhưng cleanup chưa đầy đủ | Scan QR thật; hiện cleanup/verify chưa cover PCTPStockMovementGuard và FVN_DeliveryCommand* |
| `g6-delivery-hvn-multilot.json` | Configured nhưng cleanup chưa đầy đủ | 3 dòng 1/2/3 LOT, 6 QR; cùng blocker ledger/guard như delivery đơn |

## Blocker an toàn phải đóng trước G6 PASS

1. Delivery cleanup hiện ghi rõ chưa xử lý `PCTPStockMovementGuard` claims và `FVN_DeliveryCommand*` ledger rows. Verify hiện tại không đếm các bảng này nên `Remaining = 0` chưa chứng minh cleanup toàn phần.
2. Phải truy ra khóa liên kết thực tế giữa ledger/guard với `@Prefix` hoặc `@RunId` từ schema và code runtime. Sau đó mới viết cleanup và verify chính xác; **không xóa theo suy đoán**.
3. Receive cũng phải kiểm kê toàn bộ bảng/ledger mà receiving service thực sự ghi, rồi đảm bảo cleanup và verify bao phủ đủ.
4. Chỉ chuyển QC direction/final sang `configured:true` sau khi xác định được bảng nguồn, khóa fixture, UI action thực và hậu điều kiện SQL.
5. Export/Rework chưa có G6 business manifests; không tính là đã acceptance.

## Guard của runner

- `-AllowMutations` bắt buộc cho journey có seed/cleanup.
- SQL seed/cleanup phải tham chiếu `@Prefix`; cấm `TRUNCATE`, cấm DROP bảng thật, cấm DELETE/UPDATE không có WHERE.
- DB target mặc định và live acceptance được pin ở DB57; SQL14/BRAVO bị từ chối.
- App config được kiểm tra trước live run; khi chạy workflow DB57, connection strings được redirect/verify về DB57 bằng QC login.
- Cleanup được thực hiện trong luồng kết thúc journey; nếu app/process chết cứng hoặc cleanup không hoàn tất, run phải FAIL và cần chạy cleanup thủ công với đúng prefix từ log/artifact.
- Không coi `configured:true` là PASS nếu chưa có runtime artifact mới trên Windows runner.

## Evidence semantics

- Navigation PASS chỉ chứng minh mở màn hình và tìm thấy control.
- Business UI PASS cần thao tác thật và trạng thái UI sau thao tác.
- Data PASS cần SQL postconditions đúng.
- E2E PASS = Business UI PASS + Data PASS + cleanup toàn phần PASS.
- Thiếu khóa cleanup an toàn hoặc thiếu runtime evidence => BLOCKED, không được nâng trạng thái.

Chạy live acceptance trên DB57 từ workflow `.github/workflows/pctp-db57-acceptance.yml` với cả hai xác nhận ghi DB57 và cleanup. Không chạy workflow này khi target chưa được kiểm tra.
