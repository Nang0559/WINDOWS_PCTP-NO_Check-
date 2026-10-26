# WMS — HƯỚNG DẪN SỬ DỤNG CHUẨN

> Tài liệu này dành cho người dùng vận hành PCTP/WMS. Mục tiêu là giúp người dùng **nhìn sơ đồ trước, hiểu mục đích sau, rồi mới thao tác**.
>
> Nội dung hướng dẫn trong ứng dụng được đồng bộ theo các topic trong `PCTP/Shell/Help/WmsHelpCatalog.cs`.

## 1. Bản đồ tổng thể WMS

```mermaid
flowchart TD
    A[Đăng nhập WMS] --> B[Main_APP / WMS Control Center]
    B --> C[Dashboard]
    B --> D[Nhập kho]
    B --> E[Xử lý hàng lỗi]
    B --> F[Xuất kho / Giao hàng]
    B --> G[Báo cáo & Tra cứu]
    B --> H[Tiện ích / Máy bắn QR]

    D --> D1[Nhập kho QR]
    D --> D2[Nhập kho không QR]
    E --> E1[NG / Rework]
    E --> E2[OK / NG]
    F --> F1[HVN MP/SP]
    F --> F2[YMVN MP/SP]
    G --> G1[QR Trace]
    G --> G2[LOT Trace]
    G --> G3[Tồn kho]
    G --> G4[Lịch sử kho]
    G --> G5[QC / Inspection]
    H --> H1[Chuyển máy QR]
```

### Nguyên tắc đọc sơ đồ

- **Dashboard**: nhìn tình trạng và chọn việc cần làm.
- **Nhập kho / Xử lý lỗi / Giao hàng**: nơi thực hiện nghiệp vụ và thay đổi trạng thái.
- **Báo cáo & Tra cứu**: nơi đọc dữ liệu, đối chiếu và traceability; không dùng để sửa nghiệp vụ.
- **Tiện ích**: thao tác hỗ trợ vận hành, ví dụ chuyển máy bắn QR.

---

## 2. Quy trình nghiệp vụ tổng quát

```mermaid
flowchart LR
    A[Hàng / QR / LOT] --> B[QC / Inspection]
    B --> C[Nhập kho]
    C --> D[Tồn kho / Slot]
    D --> E[Xuất kho]
    E --> F[Giao hàng]
    F --> G[Customer]

    C -. lỗi .-> H[Xử lý hàng lỗi]
    H --> B

    A --> I[Báo cáo & Trace]
    B --> I
    C --> I
    D --> I
    E --> I
    F --> I
```

**Khi có vấn đề:** không nhảy thẳng sang bước sau để bỏ qua kiểm soát. Hãy quay về bước đang có lỗi, tra cứu dữ liệu và xử lý đúng workflow.

---

## 3. Dashboard / WMS Control Center

```mermaid
flowchart TD
    A[WMS Control Center] --> B[Dashboard]
    A --> C[Tra cứu nhanh]
    A --> D[Hướng dẫn]
    A --> E[Làm mới]
    B --> F{Việc cần xử lý?}
    F -- Nhập kho --> G[Nhập kho]
    F -- Hàng lỗi --> H[Xử lý hàng lỗi]
    F -- Giao hàng --> I[Giao hàng]
    F -- Tra cứu --> C
    B --> J[Cảnh báo / trạng thái]
```

### Tra cứu nhanh từ Main_APP

Thanh **WMS CONTROL CENTER** là điểm vào nhanh cho người vận hành. Có thể nhập:

- `QR:xxxx` — tìm theo QR/carton.
- `LOT:xxxx` — tìm theo LOT.
- `PART:xxxx` — tìm theo PartNo.
- Không có tiền tố — mặc định xem là QR/carton.

Sau khi nhấn **Tra cứu**, Main_APP chỉ điều hướng sang module `BaoCao`; SQL/query không đặt trong Shell. Màn hình tra cứu là **read-only**.

### Người dùng nên làm gì trên Dashboard

1. Xem số lượng việc cần xử lý.
2. Ưu tiên cảnh báo ảnh hưởng trực tiếp đến giao hàng/tồn kho.
3. Dùng **Tra cứu nhanh** khi cần kiểm tra QR/LOT/Part.
4. Mở đúng module bằng vùng chức năng tương ứng.
5. Sau khi xử lý, nhấn **Làm mới** để kiểm tra lại.
6. Nhấn **Hướng dẫn** khi cần xem sơ đồ và checklist của nghiệp vụ.

---

## 4. Nhập kho QR

```mermaid
flowchart TD
    A[Quét QR] --> B{QR hợp lệ?}
    B -- Không --> C[Kiểm tra QR / báo lỗi]
    B -- Có --> D[Đọc Part + LOT + Số lượng]
    D --> E{Dữ liệu hợp lệ?}
    E -- Không --> F[Dừng và đối chiếu]
    E -- Có --> G[Chọn Slot]
    G --> H[Xác nhận nhập kho]
    H --> I[Kiểm tra kết quả]
```

### Checklist

- QR đúng hàng.
- Part đúng.
- LOT đúng.
- Số lượng đúng.
- Slot đúng.
- Không có trạng thái đã nhập/trùng.

---

## 5. Nhập kho không QR

```mermaid
flowchart TD
    A[Part / LOT / SL] --> B[Kiểm tra chứng từ]
    B --> C{Khớp?}
    C -- Không --> D[Dừng / điều chỉnh nguồn]
    C -- Có --> E[Chọn Slot]
    E --> F[Xác nhận]
    F --> G[Cập nhật tồn]
```

Chỉ sử dụng luồng này khi nghiệp vụ cho phép. Không dùng nhập không QR để bỏ qua kiểm soát QR.

---

## 6. Xử lý hàng lỗi

```mermaid
flowchart TD
    A[Phiếu bất thường] --> B[Nguyên nhân + SL]
    B --> C[Rework / Xử lý]
    C --> D{Kết quả}
    D -- OK --> E[Hoàn tất]
    D -- NG --> F[Chuyển bước tiếp theo]
    F --> C
```

### Quy tắc

- Không tự sửa trạng thái trong DB.
- Mỗi bước phải đúng trạng thái trước đó.
- Khi phiếu đã kết thúc, không tiếp tục thao tác như phiếu đang mở.

---

## 7. Giao hàng HVN / YMVN

```mermaid
flowchart TD
    A[Phiếu giao] --> B[Chọn loại giao]
    B --> C[Kiểm tra Part / LOT / SL]
    C --> D{Đủ điều kiện?}
    D -- Không --> E[Tra cứu / xử lý dữ liệu]
    D -- Có --> F[Chuẩn bị giao]
    F --> G[In chứng từ nếu cần]
    G --> H[Xác nhận giao]
```

### Trước khi xác nhận giao

- Phiếu đúng khách hàng.
- Đúng loại giao MP/SP nếu có.
- Part đúng.
- LOT đủ và đúng.
- Số lượng khớp.
- Không còn trạng thái chặn giao.

---

## 8. Báo cáo & Traceability

```mermaid
flowchart TD
    A[Khóa tra cứu] --> B{QR / LOT / Part / Phiếu / Customer}
    B --> C[Query read-only]
    C --> D[Tồn hiện tại]
    C --> E[Lịch sử kho]
    C --> F[QC / Inspection]
    C --> G[Xuất / Giao]
    D --> H[Timeline]
    E --> H
    F --> H
    G --> H
```

### Quy tắc quan trọng

`BaoCao` là **read-only**. Người dùng không dùng màn hình báo cáo để thay đổi dữ liệu nghiệp vụ.

Nếu không có liên kết dữ liệu chắc chắn giữa hai nguồn, kết quả phải được xem là **chưa xác nhận**, không suy diễn thành lịch sử thực tế.

---

## 9. Tra cứu LOT

```mermaid
flowchart LR
    A[LOT] --> B[Tồn hiện tại]
    A --> C[Lịch sử nhập/xuất]
    A --> D[QC]
    A --> E[Giao hàng]
    B --> F[Đối chiếu Timeline]
    C --> F
    D --> F
    E --> F
```

Dùng LOT khi cần trả lời các câu hỏi:

- LOT đang ở đâu?
- Còn bao nhiêu?
- Đã nhập khi nào?
- Đã xuất/giao chưa?
- Có lịch sử QC/Inspection không?

---

## 10. Tra cứu QR

```mermaid
flowchart LR
    A[QR / Tem] --> B[Part / LOT]
    B --> C[QC]
    B --> D[Kho]
    B --> E[Giao hàng]
    C --> F[Trace timeline]
    D --> F
    E --> F
```

Khi không tìm được QR, thử đối chiếu bằng LOT hoặc Part. Không tự kết luận QR đã giao chỉ vì LOT tương ứng tồn tại.

---

## 11. Thiết lập và kiểm soát FIFO

FIFO (**First In – First Out**) là nguyên tắc quan trọng trong quản lý tồn kho: hàng vào trước phải được ưu tiên xuất trước, trừ trường hợp nghiệp vụ/khách hàng có quy định khác.

```mermaid
flowchart TD
    A[Hàng nhập kho] --> B[Ghi nhận LOT + thời điểm nhập]
    B --> C[Tồn theo LOT / Slot]
    C --> D[Sắp xếp theo thứ tự FIFO]
    D --> E[Đề xuất LOT cũ hơn]
    E --> F{Đủ điều kiện xuất?}
    F -- Có --> G[Xuất / giao]
    F -- Không --> H[Chuyển LOT kế tiếp hoặc xử lý cảnh báo]
```

### Khi thiết lập FIFO

- Xác định **tiêu chí tuổi tồn** được hệ thống sử dụng, ưu tiên theo thời điểm nhập kho thực tế hoặc tiêu chí đã được nghiệp vụ thống nhất.
- Đảm bảo mỗi LOT có thông tin đủ để xác định thứ tự trước/sau.
- Không dùng ngày tạo phiếu thay cho ngày nhập kho nếu hai mốc có thể khác nhau.
- Kiểm tra FIFO theo **Part + LOT + vị trí tồn**; không trộn các Part khác nhau.
- Nếu khách hàng hoặc nghiệp vụ có quy tắc xuất đặc biệt, phải xác định rõ ngoại lệ trước khi xuất.

### Checklist vận hành FIFO

1. Kiểm tra LOT đang được đề xuất xuất.
2. So sánh thời điểm nhập với các LOT cùng Part còn tồn.
3. Nếu LOT cũ hơn vẫn còn tồn nhưng hệ thống đề xuất LOT mới hơn, **không tự bỏ qua**; phải kiểm tra cấu hình hoặc nguyên nhân nghiệp vụ.
4. Sau khi xuất, làm mới tồn và kiểm tra lại thứ tự LOT.
5. Khi phát hiện sai FIFO, lưu Part, LOT, Slot, thời điểm và phiếu liên quan để IT kiểm tra.

> **Lưu ý:** tài liệu này quy định nguyên tắc vận hành FIFO. Không tự thay đổi SQL/DB để ép thứ tự FIFO nếu chưa xác định chính xác cơ chế FIFO đang được triển khai trong module nghiệp vụ.

---

## 12. Cấu hình mã yêu cầu kiểm tra trùng khớp giữa thùng và hộp

Đây là cấu hình kiểm soát quan trọng khi hệ thống phải xác nhận **mã yêu cầu kiểm tra** giữa **thùng (carton)** và **hộp (box)** trước khi cho phép tiếp tục nghiệp vụ.

```mermaid
flowchart TD
    A[Cấu hình mã yêu cầu kiểm tra] --> B[Xác định mã áp dụng]
    B --> C[Quy định phạm vi Part / nghiệp vụ]
    C --> D[Quét mã thùng]
    D --> E[Quét mã hộp]
    E --> F{Mã yêu cầu có khớp?}
    F -- Có --> G[Cho phép tiếp tục]
    F -- Không --> H[Chặn thao tác + cảnh báo]
```

### Nguyên tắc cấu hình

- Mã yêu cầu kiểm tra phải được quản lý tập trung, có **mã rõ ràng và trạng thái hiệu lực**.
- Xác định chính xác mã áp dụng cho từng **Part / loại hàng / nghiệp vụ** nếu hệ thống có phân loại.
- Không dùng một mã chung cho tất cả trường hợp nếu yêu cầu kiểm tra thực tế khác nhau.
- Khi thay đổi mã, phải xác định thời điểm hiệu lực và người chịu trách nhiệm thay đổi.
- Không xóa hoặc sửa lịch sử cấu hình đang được sử dụng để truy vết.

### Quy trình kiểm tra thùng ↔ hộp

1. Quét mã **thùng**.
2. Đọc mã yêu cầu kiểm tra tương ứng từ dữ liệu cấu hình/nguồn nghiệp vụ.
3. Quét mã **hộp**.
4. Hệ thống đối chiếu mã yêu cầu của thùng và hộp.
5. **Khớp** → cho phép tiếp tục.
6. **Không khớp** → chặn thao tác, hiển thị cảnh báo và yêu cầu kiểm tra lại.

### Khi cấu hình hoặc kiểm tra không đúng

- Không tự sửa mã trên tem để làm cho hai mã giống nhau.
- Kiểm tra lại Part, mã thùng, mã hộp và mã yêu cầu đang có hiệu lực.
- Kiểm tra xem mã đã được cấu hình đúng phạm vi và thời điểm hiệu lực chưa.
- Nếu dữ liệu cấu hình đúng nhưng hệ thống vẫn báo không khớp, ghi lại mã thùng + mã hộp + Part + thời gian để IT kiểm tra.

> **Lưu ý:** đây là quy tắc vận hành và kiểm soát. Tên bảng, tên cột và màn hình cấu hình cụ thể phải được đối chiếu với implementation thực tế trước khi hướng dẫn người dùng nhập một giá trị cấu hình cụ thể.

---

## 13. Chuyển máy bắn QR

```mermaid
flowchart TD
    A[Máy hiện tại] --> B[Chọn máy đích]
    B --> C{Xác nhận?}
    C -- Không --> D[Giữ nguyên]
    C -- Có --> E[Cập nhật máy QR]
    E --> F[Khởi động lại ứng dụng]
    F --> G[Kiểm tra trạng thái]
```

Chỉ thực hiện khi xác định chính xác hostname máy đích.

---

## 14. Xử lý lỗi chuẩn

```mermaid
flowchart TD
    A[Lỗi WMS] --> B[Ghi module + chức năng]
    B --> C[Ghi QR / LOT / Phiếu]
    C --> D[Ghi thời gian]
    D --> E[Chụp ảnh lỗi]
    E --> F[Kiểm tra và thử lại 1 lần]
    F --> G{Còn lỗi?}
    G -- Không --> H[Tiếp tục]
    G -- Có --> I[Báo IT]
```

### Thông tin tối thiểu khi báo IT

1. Tên máy.
2. User.
3. Module/chức năng.
4. Thời gian lỗi.
5. QR/LOT/phiếu liên quan.
6. Nội dung lỗi.
7. Ảnh màn hình.

Không tự sửa DB hoặc dùng chức năng khác để bypass kiểm soát.

---

## 15. Hướng dẫn trong ứng dụng

Main_APP có nút **Hướng dẫn sử dụng**. Từ đây người dùng có thể chọn topic theo nghiệp vụ.

Mục tiêu kiến trúc:

```mermaid
flowchart LR
    A[Main_APP] --> B[WmsHelpService]
    B --> C[WmsHelpContext]
    B --> D[WmsHelpCatalog]
    D --> E[WmsHelpTopic]
    E --> F[FormWmsHelp]
    F --> G[Sơ đồ Mermaid]
    F --> H[Checklist thao tác]
    F --> I[Lỗi + cách xử lý]
```

Nội dung hướng dẫn **không nằm trực tiếp trong Main_APP**. Khi thêm một nghiệp vụ mới, chỉ cần thêm topic vào Help Catalog và sau đó gắn context vào form/module tương ứng.

---

## 16. Chuẩn thiết kế một hướng dẫn mới

Mọi chức năng WMS mới phải có tối thiểu 7 phần:

1. Mục đích.
2. Sơ đồ Mermaid.
3. Điều kiện trước khi thực hiện.
4. Các bước thao tác.
5. Điều kiện xác nhận.
6. Lỗi thường gặp.
7. Cách xử lý / thông tin cần báo IT.

Không viết hướng dẫn kiểu chỉ liệt kê nút bấm. Người dùng phải hiểu **vì sao thực hiện bước đó và bước tiếp theo là gì**.
