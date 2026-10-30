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
| `g6-delivery-hvn.json` | **BLOCKED / chưa đủ luồng HVN MP** | Fixture chỉ quét FCC QR; thiếu tem khách hàng theo thứ tự FCC → HVN và thao tác hoàn tất/CNK trước assert tồn kho/ledger. |
| `g6-delivery-hvn-multilot.json` | **BLOCKED / chưa đủ luồng HVN MP** | Sáu QR hiện tại đều là FCC; thiếu cặp tem khách hàng và thao tác hoàn tất/CNK. Cleanup/verify cũng cần kiểm kê theo writer thực tế. |

## Blocker HVN MP phải đóng trước G6 PASS

1. `DocQRScanEngine.ProcessScan` định tuyến QR 4 trường sang `ScanFCC`; các QR khác mới đi `ScanHVN`. Cấu hình HVN ghi thứ tự FCC → HVN, nhưng repo hiện chưa có fixture tem khách hàng HVN được xác nhận từ nhãn thực tế. Không tự tạo tem HVN bằng cách suy diễn định dạng.
2. Cả hai manifest hiện chỉ submit FCC QR rồi chạy SQL assert đòi hỏi `LUUPHIEUGIAOHANG`, `LUUDOCQRCODE`, `STOCKTP.SLXUAT` đã thay đổi. Chúng không thao tác hoàn tất giao hàng/CNK. Vì vậy không thể dùng kết quả assert đó để chứng minh luồng giao hàng hoàn tất.
3. HVN MP phải có test đúng cặp tem FCC + tem khách hàng, thứ tự đúng/sai, tem trùng, mã hàng không khớp, số lượng khớp/lệch và xác nhận chênh lệch; sau đó mới thực hiện đúng thao tác hoàn tất/CNK của UI và kiểm tra SQL.
4. Chỉ bật lại `configured:true` sau khi lấy được format tem HVN thực tế và xác định automationId thao tác hoàn tất/CNK từ UI thật.

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


## QR LOT format baseline (operator-confirmed 2026-10-30)

The production QR LOT field layout and test contract are defined in `docs/PCTP_QR_LOT_FORMAT_CONTRACT.md`:

- Total label (6 colon fields) is used for receiving.
- Box label (4 colon fields) is used for issue/delivery.
- LOTNO token has 28 characters: date 6 + product 5 + shift 1 + Gear 1 + line 3 + department 4 + daily label sequence 4 + embedded quantity 4.
- Stock LOT identity is the first 20 characters only. Label sequence and embedded quantity do not create a separate stock LOT.
- HVN Gear = `0`; YMVN Gear = `1..5` mapped to A..E.
- Box quantity is taken from the box QR's fourth field.

**The operator-confirmed QR pair is available and the sample discrepancy is closed:**

- Total/receiving: `2610300027230077170007512000:2210A-K2C-V000:30/10/2026:2000:2:A010000000059140`
- Box/issue: `2610300027230077170007520020:2210A-K2C-V000:30/10/2026:20`
- Both 28-character LOTNO tokens resolve to stock LOT identity `26103000272300771700`; the box issue quantity is 20.

Real box-label E2E is still **BLOCKED on production-path integration evidence**, not on QR input. The production `ParseTachLotQr` legacy 27-character assumption remains an integration item. Contract/unit tests do not prove stock/ledger E2E.


### Additional source-level blockers identified

The current parser leaves `QRCodeInfo.RawLotNoSL` empty, but `NhapTpReceivingService` uses null-coalescing (`RawLotNoSL ?? RawLotNo ?? LotNo`), which does not fall back from an empty string. Thus the no-matched-production-slip receiving path can derive an empty LOT. Separately, both `QRCodeWorkflowAdapter` methods forward the parser's unnormalized 28-character `LotNo` rather than the 20-character stock identity. Three skipped QC acceptance tests specify the expected parser/adapter behavior; they are not reported as PASS. Production source/SQL remains unchanged pending explicit authorization. The QR fixture is valid; the remaining blockers are integration defects, not missing QR data.
