# BAOCAO — LEGACY REPORT / TRA CỨU INVENTORY

## Nguyên tắc dọn dẹp

Không xóa theo tên thư mục. Phải phân biệt:

1. **Operational document report** — report thuộc nghiệp vụ đang sở hữu → `KEEP`.
2. **Cross-module report / history / lookup** → chuyển ownership sang `BaoCao`.
3. **Legacy lookup UI / SQL trong Shell** → `REDIRECT`, sau đó `DELETE`.
4. **Backup / dead code** → `DELETE` nếu không còn caller.

## Đã xác định

| Vị trí | Loại | Quyết định hiện tại | Lý do |
|---|---|---|---|
| `Modules/GiaoHangKhach/Reports` | Chứng từ giao hàng | KEEP tạm thời | Thuộc ownership GiaoHangKhach |
| `Modules/XuLyHangLoi/Reports` | Chứng từ xử lý lỗi | KEEP tạm thời | Thuộc ownership XuLyHangLoi |
| `Modules/KhoVatLy/Report` | Chứng từ/operation kho | KEEP tạm thời | Thuộc ownership kho |
| `Modules/KhoCore/Repositories/StockHistoryRepository.cs` | Stock history source | KEEP | Nguồn dữ liệu nghiệp vụ KhoCore |
| `Modules/KhoVatLy/FormStockHistory.cs` | UI tra cứu stock history | MIGRATE | Read/report concern nên chuyển dần sang BaoCao |
| `Presentation/Presenters/_backup/*` | Backup source | DELETE | Không phải runtime source |

## Main_APP

`Shell/Main_APP.cs` hiện còn dependency tới namespace legacy `PCTP.VIEWSTOCK` ở phần `using`. Trên branch này chưa xóa mù phần implementation vì cần xác minh caller và build parity trước.

Mục tiêu:

```text
Main_APP
  X--> SQL tra cứu lịch sử
  X--> VIEWSTOCK repository
  X--> legacy lookup form

Main_APP
  └──> BaoCao navigation
```

## Migration sequence

```text
Legacy lookup
     ↓
BaoCao query contract
     ↓
BaoCao read model
     ↓
BaoCao UI
     ↓
Redirect Main_APP
     ↓
Parity verification
     ↓
Delete legacy
```

## Không chuyển nhầm

Các report như `GHEPLOT`, `PHIEUGIAOHANG`, `RpPhieuXuLyBatThuong`, `RpInNhapKho` không được gom vào `BaoCao` chỉ vì chúng nằm trong thư mục `Reports/Report`.

Nếu report dùng để **in chứng từ của transaction mà module đó sở hữu**, report tiếp tục ở module nghiệp vụ.

## Phase tiếp theo

- Xác định toàn bộ `VIEWSTOCK`/lookup caller bằng code search và project build.
- Đối chiếu với `PCTP.csproj`.
- Migrate từng chức năng một.
- Chỉ xóa legacy sau khi replacement đã chạy đúng.
