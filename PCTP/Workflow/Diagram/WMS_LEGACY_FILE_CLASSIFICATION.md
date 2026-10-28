# WMS — Phân loại 17 file legacy chưa nằm trong `PCTP.csproj`

Baseline: `476bd6e5b5bcfe6b0bf6f066ecd0ece6399866bb`  
Quyết định nghiệp vụ (chủ repo): **giữ nghiệp vụ YMVN; không xóa `Usp_Qrcode_Take_LotYMVN2405`; không xóa hàng loạt; chỉ biên dịch form còn thực sự được dùng.**

Nhãn: `ACTIVE` · `REPLACED` · `LEGACY_UNUSED` · `NEEDS_BUSINESS_CONFIRMATION`.  
Không nhãn nào dưới đây chỉ dựa vào việc thấy/không thấy tên class; mỗi nhãn ghi bằng chứng và phần chưa kiểm được.

## 1. Bản đồ điều hướng đã quét (source-level)

| Điểm kiểm | Kết quả |
|---|---|
| Entry point | `Program.cs` chỉ chạy `Main_APP`; `GIAOHANGYMN`, `YAMAHAQRCDE_SP`, `frm_NhanHangHVN` chỉ còn là dòng comment. `frm_NhanHangHVN` không tồn tại ở bất kỳ file nào. |
| Tham chiếu trực tiếp từ code đang biên dịch | Không có tới 9 class của nhóm 17 file (kể cả `IN_GHP_LOT_YMVN`). |
| Khởi tạo động | Không có `Activator.CreateInstance`, `Type.GetType`, `GetTypes()` trong `PCTP/`. |
| Menu/quyền lưu trong DB hoặc XML | **Chưa kiểm được** (cần dữ liệu `Main_APP`/bảng quyền thật). Xem mục 5. |
| Luồng YMVN đang chạy | `HVN_PGH` → `PhieuPresenter` / `YmvnPresenter` → `PhieuYmvnService` → `PhieuKhoRepository.CapNhapKhoYMVN`; điều kiện rẽ nhánh bằng `Cfg.Delivery.CoGear`; lỗi hiển thị bằng `SubForm/frm_err-cnk` + `YMVN/DS_ERR_CNK.cs` (đã trong csproj). |
| SP đang được gọi từ code biên dịch | `Usp_Qrcode_Take_LotYMVN2405` (`PhieuLotRepository.cs:152`), `Usp_Qrcode_Update_Stock2405` (`PhieuKhoRepository.cs:76`). **Không được xóa.** |
| SP chỉ còn được gọi từ form chưa biên dịch | `Usp_Qrcode_gheplotYM1/YM2`, `Usp_BarCodeView_Ghep_YMVN`, `Usp_gheplotPrintYM1` (trong `GIAOHANGYMN.cs`). Giữ nguyên; chỉ đánh dấu để rà sau. |

## 2. Phân loại

| Nhóm | File | Nhãn | Bằng chứng / việc còn lại |
|---|---|---|---|
| YMVN (10) | `GIAOHANGYMN`, `YAMAHAQRCDE_SP`, `YMVNDOCQRCODE`, `YMVN_CHONGIAO`, `ERR_CNK` (+ `.Designer.cs`) | `NEEDS_BUSINESS_CONFIRMATION` (nghiêng `REPLACED`) | Nghiệp vụ YMVN đang chạy qua kiến trúc mới (mục 1). Form cũ không có đường khởi chạy trong source. Chưa kiểm menu/quyền động và chưa đối chiếu từng chức năng của form cũ (đặc biệt `YMVN_CHONGIAO`, `YMVNDOCQRCODE`) với luồng mới. Chưa đưa vào csproj, chưa xóa. |
| Báo cáo (2) | `IN_GHP_LOT_YMVN` (+ `.Designer.cs`) | `REPLACED` (bởi `IN_GHP_LOT` + `IsGear`) | Không có caller. `IN_GHP_LOT` đã dùng `IsGear` lấy từ `CoGear`. **Patch này** bỏ phụ thuộc `GIAOHANGYMN.CHONYMVN`, thêm `IsGear` cùng contract với `IN_GHP_LOT`. File vẫn chưa biên dịch; muốn dùng lại chỉ cần thêm vào csproj và gán `IsGear`. Giữ file, không xóa. |
| NhanLaiNG (5) | `ImPortPGHDB`, `frmNhanLaiTP` (+ Designer), `TACKMH` | `NEEDS_BUSINESS_CONFIRMATION` (nghiêng `LEGACY_UNUSED`) | Ba file nguồn rất nhỏ (16/35/66 dòng), không chứa SQL/SP/repository. `frm_NhanHangHVN` đã không còn. Luồng gần nhất đang chạy là khách trả hàng ở `XuLyHangLoi` (`FormTiepNhanPhieuKhachTra`, `PhieuTraHang`) — **suy luận, chưa xác nhận là thay thế đúng nghiệp vụ nhận lại NG**. Phải trả lời bốn câu của chủ repo (menu, nhập phiếu trả, điểm gọi `TACKMH`, dữ liệu dùng cho tồn/LOT/lịch sử) trước khi quyết định. |

## 3. Thay đổi kèm patch

1. `IN_GHP_LOT_YMVN.cs`: bỏ `using PCTP.QRCODE_HVN.YMN`, thêm `IsGear`, thay `GIAOHANGYMN.CHONYMVN == 1` bằng `IsGear`.
2. `InPhieuService.cs`, `HangThieuCaNgayService.cs`: chỉ sửa **comment**, bỏ tên form legacy (hai file này đang biên dịch và làm hỏng bước kiểm ranh giới ở CI).
3. `pctp-refactor-verification.yml`: bước *Verify delivery architecture boundaries* không quét `Modules\GiaoHangKhach\YMVN\` và `NhanLaiNG\` (vùng cách ly, không biên dịch). Quy tắc cấm tên legacy vẫn áp dụng cho mọi nơi còn lại trong `Shell`, `BaoCao`, `GiaoHangKhach`.

> Lưu ý CI: workflow này chỉ kích hoạt khi push `feature/baocao-deliverytrace-refactor` hoặc PR vào `master` / nhánh đó. Nó **không** chạy trên push của `feature/wms-end-to-end-consistency`. Trước patch, bước quét sẽ báo lỗi (13 lần khớp, gồm 2 file biên dịch); sau patch, quét tương đương trên cây này cho 0 khớp.

## 4. B0-01 — phạm vi bắt buộc gồm nhánh YMVN (chưa sửa code)

Đường trừ tồn YMVN còn sống: `PhieuKhoRepository.CapNhapKhoYMVN` (dòng ~193) → `TruStockTpFifo` (dòng ~281, 428). Trước khi thay bằng một delivery command phải có **characterization test** khóa kết quả hiện tại:

1. FIFO theo LOT, QR/PART theo quy tắc khách hàng, nhiều LOT / nhiều Slot, giao một phần.
2. `STOCKTP` = tổng `SlotLot` hợp lệ sau mỗi ca; chứng từ + history + ledger cùng commit/rollback.
3. Retry cùng khóa nghiệp vụ không trừ tồn lần hai; lỗi giữa chừng rollback toàn bộ.
4. `IStockMovementService` trong `PhieuKhoRepository` hiện là tham số tùy chọn (`= null`); sau khi chuyển phải bắt buộc.

## 5. Việc chưa thể xác minh trong patch này

- Menu/quyền động (bảng cấu hình, `Main_APP`, XML) cho các form ở mục 2.
- Build MSBuild toàn solution trên Windows (cần DevExpress, WebView2, chứng chỉ ký).
- So khớp chức năng từng form YMVN cũ với luồng `HVN_PGH`.
- Xác nhận nghiệp vụ nhận lại NG.
