# MES Product Genealogy & Traceability — Thiết kế chuẩn

- **Mã tài liệu:** MES-PG-001
- **Phiên bản:** 1.0 — Baseline kiến trúc và nghiệp vụ
- **Ngày:** 2026-11-13
- **Repository:** Nang0559/WINDOWS_PCTP-NO_Check-
- **Branch:** feature/mes-product-genealogy-timeline
- **Database tham chiếu:** B7R2_FCC
- **Phạm vi ứng dụng:** FormBaoCaoStockHistory, FormBaoCaoTraceability

> **Trạng thái:** Baseline thiết kế đủ để thống nhất nghiệp vụ và tổ chức bước xác minh. Đây chưa phải đặc tả SQL đã nghiệm thu. Các khóa nối, quy tắc trạng thái chứng từ và trường số lượng chưa được kiểm chứng phải giữ trạng thái TBD; không được tự suy diễn khi triển khai.

---

## 1. Mục đích và kết quả cần đạt

Xây dựng khả năng truy xuất nguồn gốc MES hai chiều trên dữ liệu hiện hữu:

1. **Forward:** Lot/QR thành phẩm → phiếu sản xuất A → chi tiết linh kiện → Lot linh kiện → lịch sử kho → giao hàng/QR.
2. **Backward:** QR/phiếu giao/Lot thành phẩm → Lot thành phẩm → phiếu A → chi tiết linh kiện và Lot linh kiện.
3. **Impact analysis:** Lot linh kiện → tất cả phiếu A/thành phẩm có sử dụng hoặc được ghi nhận có liên quan → các lô giao hàng tương ứng.

Mở rộng hai form báo cáo hiện tại; không tạo ứng dụng genealogy độc lập và không thay thế luồng Stock History, QR hoặc Delivery Trace đang có.

## 2. Phạm vi

### 2.1 Trong phạm vi

- Liên kết Lot thành phẩm với số phiếu A được lưu tại SlotLot.TemCode.
- Tra cứu header và detail của chứng từ sản xuất.
- Truy xuất mã linh kiện và Lot linh kiện từ chi tiết chứng từ.
- Liên kết tiếp với lịch sử kho, QR và phiếu giao hàng khi xác minh được khóa.
- Hỗ trợ truy xuất thuận, truy xuất ngược và phân tích ảnh hưởng.
- Phân biệt dữ liệu cũ chưa có TemCode với dữ liệu mới có TemCode.
- Hiển thị trạng thái liên kết, nguồn dữ liệu và giới hạn xác minh.

### 2.2 Ngoài phạm vi

- Backfill hoặc tự sinh TemCode cho dữ liệu cũ.
- Sửa dữ liệu gốc MES, chứng từ kho, Lot hoặc QR.
- Tự động kết luận tiêu hao thực tế từ trường có tên gần nghĩa “thực tế”.
- Thay đổi quy trình sản xuất, cấp phát linh kiện hoặc hạch toán kho.
- Tạo quan hệ genealogy khi không có khóa nguồn đáng tin cậy.

## 3. Nguyên tắc nghiệp vụ đã thống nhất

1. SlotLot.TemCode là trường được bổ sung để lưu số phiếu A phục vụ liên kết MES. Người dùng xác nhận việc lưu số phiếu này mới được áp dụng từ đầu tháng 11/2026.
2. Lot cũ không có TemCode là đặc điểm lịch sử của dữ liệu, **không phải lỗi dữ liệu**.
3. Dữ liệu mới ưu tiên đường liên kết qua số phiếu A trong TemCode.
4. Không mặc định TemCode = B30AccDoc.DocNo nếu chưa đối chiếu mẫu phiếu A thực tế và cột đích chính xác.
5. Người dùng mô tả phần chi tiết linh kiện là detail của phiếu A. Metadata hiện có xác nhận B30AccDocInventory; tên B30AccDocDetail chưa được xác nhận là object thực tế. Phải kiểm tra catalog DB và nội dung chứng từ trước khi chốt tên.
6. Không mặc định một phiếu A chỉ có một Lot thành phẩm hoặc một Lot thành phẩm chỉ có một phiếu A.
7. Không dùng mã hàng, ngày sản xuất hoặc số lượng làm khóa thay thế cho khóa chứng từ.
8. Không coi định mức là tiêu hao thực tế. Các trường số lượng phải được trình bày riêng cho tới khi nghiệp vụ xác nhận.
9. Báo cáo chỉ đọc; không ghi ngược vào các bảng MES nguồn.
10. Mọi quan hệ phải truy ngược được về bảng, chứng từ và dòng dữ liệu nguồn.

## 4. Nguồn dữ liệu và mức độ xác minh

### 4.1 Lot và số phiếu A

| Nguồn | Vai trò dự kiến | Trường cần đối chiếu |
|---|---|---|
| SlotLot | Lot được lưu tại slot và số phiếu A liên kết | LotNo, ItemCode, Quantity, TemCode, SoPhieuTong, MaPhieu, ParentSoPhieu, PhieuStatus |
| Slot | Ngữ cảnh slot/quan hệ cha nếu cần | Cần kiểm tra schema và cardinality |
| B30AccDoc | Header chứng từ sản xuất | Id, Stt, DocNo, DocCode, DocDate, DocStatus, Posted, IsActive, ReceiptItemCode, ReceiptQuantity, ItemLotCodeTP |

**Lưu ý:** TemCode là số phiếu A theo quy ước nghiệp vụ người dùng cung cấp. Kết quả kiểm tra trước đó chưa chứng minh được cột tương ứng trong B30AccDoc. Vì vậy, cột đích và phép nối chính xác vẫn là mục xác minh bắt buộc.

### 4.2 Chi tiết linh kiện và danh mục Lot

| Nguồn | Vai trò | Trường đáng chú ý |
|---|---|---|
| B30AccDocInventory | Ứng viên chi tiết dòng chứng từ sản xuất | Id, Stt, RowId, ItemCode, Quantity, Quantity9, ItemLotCode, ItemLotCodeTP, NormQuantity, NeedQuantity, ReceiptQuantity, InfactQuantity, QuantityNG |
| B20ItemLot | Danh mục Lot | Id, Code, ItemCode, Quantity |
| B20ItemLotDetail | Chi tiết quan hệ Lot-vật tư | ItemLotCode, ItemCode |
| vB30AccDocInventoryLR_Lot | View ứng viên cho báo cáo Lot/phiếu LR | Cần đọc định nghĩa và đối chiếu mẫu |
| vB30AccDocInventory_ProductionLotInfo | Thông tin Lot sản xuất | RowId, BizDocId_SO, DocNo, ProductDesignNo, PartNo, SerialNo1, SerialNo2 |

Metadata được cung cấp cho thấy B30AccDoc.Stt có unique index; B30AccDocInventory.RowId có unique index và Stt có index không unique. Đây là cơ sở để kiểm tra hướng nối header-detail qua Stt, không thay thế việc xác nhận semantics của chứng từ.

### 4.3 Lịch sử kho và giao hàng

| Nguồn | Vai trò |
|---|---|
| STOCKTP | Dữ liệu Lot thành phẩm/kho với các trường Lot, mã hàng, ngày sản xuất, nhập, xuất và số lượng |
| GIAO_HANG | Nguồn thông tin giao hàng theo Lot |
| LUUPHIEUGIAOHANG | Lịch sử phiếu giao hàng |
| LUUDOCQRCODE | Lịch sử/quan hệ QR và chứng từ giao hàng |
| B30Export, B30ExportDetail | Chứng từ xuất và chi tiết xuất/chuyển Lot |
| vB30AccDocInventory_LotDelivery | View ứng viên cho lịch sử xuất theo Lot |
| vB30AccDocInventory_DeliveryLotInfo | Thông tin Lot giao hàng |

Không được giả định STOCKTP.LOT = B30AccDocInventory.ItemLotCode. Một phép đối chiếu trước đây không tìm được match trực tiếp; cần xác định đúng hệ mã và bảng master Lot trước khi nối.

## 5. Mô hình logic mục tiêu

Chuỗi nghiệp vụ mục tiêu:

1. SlotLot: LotNo, ItemCode, TemCode.
2. TemCode → số phiếu A (cột đích trong chứng từ cần xác minh).
3. B30AccDoc: header chứng từ sản xuất.
4. B30AccDoc.Stt → dòng detail (khóa cần xác minh bằng mẫu).
5. Detail → Component ItemCode và Component ItemLotCode.
6. ItemLotCode → B20ItemLot.Code (cần kiểm tra tỷ lệ khớp và ItemCode).
7. Lot linh kiện → lịch sử kho linh kiện.
8. Lot thành phẩm → lịch sử kho thành phẩm → phiếu giao → QR/khách hàng.

Các cạnh chưa xác minh không được coi là JOIN đã chốt.

### 5.1 Grain dữ liệu

Grain báo cáo phải được xác định bằng dữ liệu thực tế, không mặc định theo Serial, WorkOrder hoặc ProductionLot.

Cần xác minh:
- Một TemCode có thể xuất hiện ở bao nhiêu dòng SlotLot?
- Một phiếu A có thể gắn với nhiều Lot thành phẩm không?
- Một Lot thành phẩm có thể có nhiều phiếu A không?
- Một dòng detail biểu diễn một ItemCode-Lot hay có thể gộp nhiều Lot?
- Một Lot linh kiện có thể xuất hiện trong nhiều phiếu A không?
- Serial/QR có quan hệ trực tiếp với dòng detail hay chỉ nối qua Lot thành phẩm?

Không được ép các quan hệ nhiều-nhiều thành một-một.

## 6. Quy tắc liên kết

### 6.1 SlotLot → phiếu A

- Đọc SlotLot.TemCode làm số phiếu A theo nghiệp vụ.
- Tìm cột tương ứng trong chứng từ bằng dữ liệu thực tế phát sinh từ tháng 11/2026.
- Không tự mặc định cột là B30AccDoc.DocNo.
- Nếu TemCode lặp trên nhiều dòng SlotLot, giữ đầy đủ từng Lot và kiểm tra xem đó là một phiếu A có nhiều Lot hay dữ liệu trùng.
- Nếu số phiếu A không tìm thấy hoặc tìm được nhiều chứng từ ứng viên, trả trạng thái phù hợp; không tự chọn theo ngày gần nhất.

### 6.2 Header → detail

- B30AccDoc.Stt là ứng viên khóa header-detail.
- B30AccDocInventory.Stt là ứng viên khóa dòng detail.
- Xác nhận bằng cách đối chiếu một số phiếu A đã biết và toàn bộ dòng linh kiện hiển thị trong hệ thống nguồn.
- Chỉ dùng trạng thái chứng từ hợp lệ theo quy tắc nghiệp vụ được xác nhận; không tự đặt DocStatus = 4 hoặc giá trị cụ thể làm điều kiện nghiệm thu.

### 6.3 Detail → Lot linh kiện

- Dòng detail phải cung cấp mã vật tư và Lot linh kiện.
- Đối chiếu ItemLotCode với B20ItemLot.Code bằng phép so sánh chính xác sau khi kiểm tra chuẩn hóa dữ liệu.
- Kiểm tra B20ItemLot.ItemCode có khớp B30AccDocInventory.ItemCode hay không.
- Nếu Lot không có master tương ứng, vẫn giữ dòng nguồn và đánh dấu thiếu master; không loại bỏ dòng detail.
- Không thay Lot bằng Lot thành phẩm hoặc ItemLotCodeTP khi chưa chứng minh semantics.

### 6.4 Lot → lịch sử kho → giao hàng

- Xác minh khóa Lot giữa nguồn Lot master, STOCKTP và các view kho.
- Xác minh luồng xuất/giao riêng biệt; phiếu xuất kho không mặc nhiên là phiếu giao khách.
- Xác minh quan hệ QR với phiếu giao và Lot.
- Ghi nhận việc chuyển, tách, ghép hoặc đổi mã Lot như các sự kiện lineage riêng; không làm mất Lot gốc.

## 7. Chính sách dữ liệu theo thời gian

Việc lưu số phiếu A trong SlotLot bắt đầu từ đầu tháng 11/2026. Mốc này là mốc thay đổi cách ghi nhận dữ liệu, không phải mốc bắt đầu tồn tại dữ liệu sản xuất.

| Nhóm dữ liệu | Cách truy xuất |
|---|---|
| Lot có TemCode từ thời điểm áp dụng | Ưu tiên liên kết qua TemCode và chứng từ A sau khi xác nhận cột đích |
| Lot cũ không có TemCode | Tiếp tục dùng nguồn lịch sử Lot/kho/giao hàng hiện hữu; chỉ liên kết khi có khóa độc lập đã xác minh |
| Lot mới nhưng TemCode rỗng | Hiển thị chưa có số phiếu A; cần kiểm tra nghiệp vụ ghi nhận, không tự gán |
| TemCode có nhưng không tìm được chứng từ | Hiển thị chưa tìm thấy chứng từ; lưu giá trị đầu vào để đối chiếu |
| TemCode tìm được nhiều chứng từ | Hiển thị quan hệ mơ hồ; không tự chọn |

Không backfill TemCode cho dữ liệu cũ bằng ngày, mã hàng, ca, line hoặc số lượng.

### 7.1 Trạng thái liên kết chuẩn

| Mã | Ý nghĩa |
|---|---|
| LINKED | Đã xác định được quan hệ nguồn và kiểm tra được tính hợp lệ |
| NO_TEMCODE | Lot chưa có số phiếu A |
| DOC_NOT_FOUND | Có TemCode nhưng chưa tìm được chứng từ |
| DETAIL_NOT_FOUND | Có header nhưng không tìm thấy detail |
| PARTIAL | Một phần quan hệ đã xác định, phần còn lại thiếu |
| AMBIGUOUS | Có nhiều ứng viên, chưa đủ căn cứ chọn |
| LOT_MASTER_NOT_FOUND | Có Lot trên detail nhưng thiếu master Lot |
| SOURCE_INCONSISTENT | Các trường nguồn mâu thuẫn nhau |
| NOT_APPLICABLE | Quan hệ không áp dụng cho loại sự kiện/dữ liệu đó |

Trạng thái này là trạng thái báo cáo, không ghi ngược vào hệ thống MES.

## 8. Quy tắc số lượng và tiêu hao

Các trường sau phải được hiển thị riêng cho tới khi xác nhận định nghĩa nghiệp vụ:

| Trường | Cách xử lý |
|---|---|
| Quantity | Số lượng dòng chứng từ theo định nghĩa nguồn |
| Quantity9 | Số lượng/quy đổi theo quy ước nguồn; cần xác nhận đơn vị |
| NormQuantity | Số lượng định mức; không phải actual consumption |
| NeedQuantity | Nhu cầu tính toán; không mặc định là tiêu hao |
| ReceiptQuantity | Số lượng nhập/ghi nhận theo chứng từ |
| InfactQuantity | Chưa được công nhận là tiêu hao thực tế cho tới khi đối chiếu nghiệp vụ |
| QuantityNG | Số lượng NG theo nguồn |
| QuantityCL | Cần xác nhận ý nghĩa chính xác từ định nghĩa/logic nguồn |

**Không được** lấy InfactQuantity làm actual consumption chỉ dựa vào tên trường. Các thống kê đã quan sát có giá trị bất thường lớn; phải kiểm tra đơn vị, phép nhân dòng, trạng thái chứng từ, kiểu dữ liệu và logic procedure trước khi tổng hợp.

Mỗi số lượng hiển thị phải có tên trường nguồn, đơn vị, mức chứng từ/dòng/Lot, trạng thái xác minh và quy tắc cộng gộp (nếu có).

Không cộng tổng các dòng ở grain khác nhau nếu chưa chứng minh không nhân bản do JOIN.

## 9. Hai chiều truy xuất

### 9.1 Forward traceability

**Đầu vào:** Lot thành phẩm, QR thành phẩm hoặc mã sản phẩm kèm điều kiện lọc.

1. Chuẩn hóa đầu vào và xác định Lot thành phẩm.
2. Đọc các dòng SlotLot liên quan.
3. Đọc TemCode nếu có.
4. Xác định chứng từ A bằng khóa đã kiểm chứng.
5. Đọc header và trạng thái chứng từ.
6. Đọc toàn bộ detail theo khóa header-detail đã xác minh.
7. Lấy mã linh kiện và Lot linh kiện.
8. Đối chiếu master Lot và lịch sử kho linh kiện.
9. Tìm lịch sử xuất/giao thành phẩm và QR.
10. Dựng timeline, giữ nguồn và trạng thái của từng sự kiện.

### 9.2 Backward traceability

**Đầu vào:** QR giao hàng, số phiếu giao, Lot thành phẩm hoặc Lot linh kiện.

1. Xác định chứng từ giao hàng/QR.
2. Xác định Lot thành phẩm được giao.
3. Xác định SlotLot tương ứng.
4. Đọc TemCode và chứng từ A nếu có.
5. Đọc detail linh kiện và Lot linh kiện.
6. Nếu đầu vào là Lot linh kiện, tìm toàn bộ detail có Lot đó.
7. Truy ra các phiếu A và Lot thành phẩm liên quan.
8. Truy ra trạng thái kho/giao của các thành phẩm liên quan.
9. Trả danh sách ảnh hưởng, không chỉ một kết quả đầu tiên.

### 9.3 Phân biệt trạng thái sản phẩm

Không đồng nhất các trạng thái:
- Đã ghi nhận sản xuất.
- Đã nhập kho.
- Đang tồn kho.
- Đã xuất kho.
- Đã giao khách hàng.

Một Lot có thể có nhiều lần xuất/giao hoặc chuyển kho. Báo cáo phải hiển thị sự kiện và số lượng theo từng chứng từ, không suy diễn tổng giao từ số lượng sản xuất.

## 10. Thiết kế giao diện trên form hiện hữu

### 10.1 FormBaoCaoStockHistory

Giữ nguyên chức năng lịch sử kho hiện có, bổ sung khu vực genealogy:
- Lot thành phẩm, ItemCode, số lượng và nguồn.
- TemCode / số phiếu A.
- Header chứng từ sản xuất.
- Danh sách linh kiện theo từng dòng detail.
- Lot linh kiện và trạng thái master.
- Số lượng nguồn (tách riêng định mức, nhu cầu, ghi nhận, actual đã xác minh).
- Link mở Stock History của Lot linh kiện.
- Link mở Traceability theo Lot thành phẩm hoặc Lot linh kiện.

Không xóa hoặc thay thế dữ liệu nhập/xuất hiện hữu.

### 10.2 FormBaoCaoTraceability

Các vùng thông tin:
1. Thông tin đầu vào và phạm vi truy xuất.
2. Thành phẩm và Lot thành phẩm.
3. Phiếu A / header chứng từ.
4. Chi tiết linh kiện và Lot linh kiện.
5. Lịch sử kho và chuyển đổi Lot.
6. Phiếu giao hàng và QR.
7. Timeline.
8. Cảnh báo dữ liệu thiếu/mơ hồ.

Mỗi event phải có Event type, thời điểm nguồn, số chứng từ, ItemCode/Lot, số lượng và đơn vị (nếu có), nguồn bảng/view, khóa dòng nguồn và trạng thái xác minh.

Không dựng timeline chỉ bằng cách gom ngày tháng rồi sắp xếp; event phải có liên hệ nghiệp vụ đã xác nhận.

## 11. Kiến trúc code đề xuất

Tái sử dụng Repository/Service hiện hữu. Các trách nhiệm logic:

| Thành phần logic | Trách nhiệm |
|---|---|
| Lot lookup | Tra cứu Lot và thông tin master |
| Production voucher lookup | Tìm phiếu A, header và detail |
| Component genealogy | Thành phẩm ↔ linh kiện ↔ Lot linh kiện |
| Stock history lookup | Nhập, xuất, chuyển và lịch sử tồn |
| Delivery trace lookup | QR, phiếu giao và Lot giao |
| Timeline builder | Chuẩn hóa, sắp xếp và trình bày event |

Đây là phân tách trách nhiệm, không phải yêu cầu tạo sáu repository/service mới. Trước khi code phải rà các lớp hiện hữu trong repo và mở rộng đúng điểm phù hợp; tránh trùng API, query hoặc logic truy xuất.

### 11.1 Hợp đồng dữ liệu logic

Một component genealogy record nên mang:
- FinishedLotNo
- FinishedItemCode
- ProductionVoucherNo (TemCode)
- ProductionDocumentKey
- ProductionDocumentDate
- ProductionDocumentStatus
- ComponentItemCode
- ComponentLotNo
- SourceDetailKey
- Các trường Quantity tách riêng
- QuantityUnit
- LinkStatus
- SourceObject
- SourceRowKey

Tên thuộc tính trên là hợp đồng logic đề xuất; kiểu dữ liệu và tên DTO thực tế phải theo conventions của project.

## 12. Hiệu năng, độ tin cậy và an toàn

1. Ưu tiên lọc theo Lot, TemCode, khóa chứng từ, QR hoặc phiếu giao.
2. Dùng phép so sánh chính xác cho mã chứng từ và Lot sau khi xác nhận định dạng.
3. Tránh LIKE '%value%' trên cột khóa nếu có thể dùng equality.
4. Không JOIN theo ItemCode đơn lẻ để xác định Lot.
5. Không dùng DISTINCT để che quan hệ một-nhiều hoặc lỗi nhân bản dòng.
6. Đo cardinality trước và sau từng JOIN.
7. Phân trang hoặc giới hạn truy vấn impact analysis lớn.
8. Có timeout phù hợp và thông báo lỗi thân thiện; không để lỗi truy vấn làm crash form.
9. Không ghi dữ liệu vào bảng nguồn.
10. Trả được nguồn và khóa của mọi kết quả để kiểm toán/đối chiếu.

## 13. Bộ xác minh dữ liệu trước khi khóa SQL

| ID | Nội dung cần xác minh | Bằng chứng chấp nhận |
|---|---|---|
| V-01 | TemCode là số phiếu A theo dữ liệu mới | Mẫu SlotLot từ tháng 11 và đối chiếu phiếu gốc |
| V-02 | Cột đích chứa số phiếu A | Một số phiếu A match chính xác trên object chứng từ |
| V-03 | Object detail thực tế | Catalog DB xác nhận table/view và định nghĩa |
| V-04 | Khóa header-detail | Các dòng detail khớp đúng chứng từ qua khóa xác nhận |
| V-05 | Lot thành phẩm trong detail/header | Đối chiếu với Lot trên SlotLot |
| V-06 | ItemLotCode là Lot linh kiện | Đối chiếu với master Lot và chứng từ nguồn |
| V-07 | Grain của quan hệ | Kiểm tra trường hợp một-nhiều và nhiều-nhiều |
| V-08 | Số lượng actual | Đối chiếu phiếu sản xuất hoàn tất và nghiệp vụ thực tế |
| V-09 | Điều kiện chứng từ hợp lệ | Xác nhận DocStatus/Posted/IsActive và quy trình nghiệp vụ |
| V-10 | Liên kết kho/giao/QR | Truy vết được mẫu chứng từ giao thực tế |
| V-11 | Chuyển/tách/ghép Lot | Đối chiếu lịch sử Lot gốc và Lot mới |
| V-12 | Dữ liệu cũ | Chứng minh báo cáo vẫn hoạt động khi TemCode rỗng |

### 13.1 Quy trình kiểm chứng bắt buộc

Chọn tối thiểu 3–5 phiếu A phát sinh từ khi bắt đầu lưu TemCode, gồm:
- Phiếu có một Lot thành phẩm.
- Phiếu có nhiều dòng linh kiện.
- Phiếu có nhiều Lot thành phẩm hoặc TemCode lặp trên SlotLot (nếu có).
- Phiếu có Lot linh kiện tra được master.
- Phiếu có thể lần tiếp tới lịch sử kho/giao hàng.

Với từng mẫu, đối chiếu:
1. Dữ liệu SlotLot.
2. Header/detail chứng từ sản xuất.
3. Màn hình hoặc chứng từ MES nghiệp vụ được người dùng xác nhận.

Lưu lại số dòng, khóa, mã hàng, Lot và số lượng trước/sau mỗi JOIN. Chỉ chốt SQL khi không mất dòng và không nhân bản ngoài cardinality đã giải thích.

## 14. Kịch bản kiểm thử và nghiệm thu

| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| GT-01 | Lot mới có TemCode hợp lệ | Tìm đúng phiếu A |
| GT-02 | Một phiếu A có nhiều detail | Hiển thị đủ dòng linh kiện |
| GT-03 | TemCode xuất hiện trên nhiều dòng SlotLot | Giữ quan hệ từng Lot, không nhân bản detail sai |
| GT-04 | Lot cũ không có TemCode | Không báo lỗi; dùng nguồn lịch sử thay thế đã xác minh |
| GT-05 | TemCode không tìm thấy chứng từ | DOC_NOT_FOUND |
| GT-06 | Header không có detail | DETAIL_NOT_FOUND |
| GT-07 | Lot linh kiện thiếu master | Giữ dòng nguồn, LOT_MASTER_NOT_FOUND |
| GT-08 | Một Lot linh kiện dùng ở nhiều phiếu A | Truy xuất ngược toàn bộ phiếu A |
| GT-09 | Một Lot có nhiều lần xuất/giao | Hiển thị từng chứng từ, không cộng trùng |
| GT-10 | QR giao hàng | Truy được Lot theo quan hệ đã xác minh |
| GT-11 | NormQuantity khác Quantity | Hiển thị riêng, không coi là actual |
| GT-12 | InfactQuantity bất thường | Không công nhận actual trước khi xác minh |
| GT-13 | Chứng từ không hợp lệ/chưa hoàn tất | Áp dụng trạng thái đã xác nhận; không đánh dấu tiêu hao đã xác minh |
| GT-14 | Một mã có nhiều chứng từ ứng viên | AMBIGUOUS |
| GT-15 | Dữ liệu trước và sau mốc TemCode | Hỗ trợ cả hai cơ chế, không ép hồi tố |
| GT-16 | Lot có lịch sử tách/chuyển | Thể hiện lineage Lot gốc/mới |
| GT-17 | Truy vấn impact analysis lớn | Có giới hạn/phân trang và thời gian phản hồi chấp nhận được |

### Tiêu chí nghiệm thu

- Mỗi kết quả genealogy truy ngược được tới chứng từ và dòng nguồn.
- Không mất hoặc nhân bản dòng detail ngoài quan hệ đã giải thích.
- Truy xuất thuận và ngược nhất quán.
- Dữ liệu cũ thiếu TemCode không bị coi là lỗi.
- Số lượng không bị gộp sai grain hoặc dùng sai trường.
- Trạng thái chưa xác minh được thể hiện rõ.
- Form hiện hữu không mất chức năng lịch sử kho/traceability cũ.
- Báo cáo không ghi sửa dữ liệu MES.

## 15. Kế hoạch triển khai

### Phase 1 — Source verification
- Xác nhận cột đích của TemCode.
- Xác nhận object detail.
- Xác nhận khóa header-detail.
- Xác nhận Lot thành phẩm/Lot linh kiện.
- Xác nhận semantics quantity và trạng thái chứng từ.

**Điều kiện hoàn tất:** V-01 đến V-09 có bằng chứng từ dữ liệu mẫu.

### Phase 2 — Query contract
- Xây truy vấn forward.
- Xây truy vấn backward.
- Xây truy vấn impact theo Lot linh kiện.
- Kiểm tra cardinality, null, trùng và số lượng.

**Điều kiện hoàn tất:** Kết quả đối chiếu được với 3–5 phiếu A thực tế.

### Phase 3 — Repository/service
- Mở rộng code hiện hữu.
- Chuẩn hóa DTO và trạng thái liên kết.
- Thêm xử lý thiếu/mơ hồ.
- Tối ưu truy vấn và phân trang.

### Phase 4 — UI
- Mở rộng FormBaoCaoStockHistory.
- Mở rộng FormBaoCaoTraceability.
- Bổ sung genealogy, timeline và source status.

### Phase 5 — Regression & acceptance
- Chạy GT-01 đến GT-17.
- Kiểm tra hồi quy hai form.
- Kiểm tra dữ liệu cũ/mới và hiệu năng.
- Nghiệm thu nghiệp vụ trước khi phát hành.

## 16. Quyết định kiến trúc và các điểm chưa được phép tự ý thay đổi

**Đã thống nhất**
- Dùng TemCode của SlotLot làm đầu mối số phiếu A cho dữ liệu mới.
- Không coi việc dữ liệu trước tháng 11 thiếu TemCode là lỗi.
- Mở rộng hai form hiện hữu.
- Truy xuất hai chiều.
- Không sửa dữ liệu nguồn.

**Chưa được phép mặc định**
- TemCode luôn bằng DocNo.
- Object detail chắc chắn tên B30AccDocDetail hoặc chắc chắn chỉ là B30AccDocInventory.
- InfactQuantity là tiêu hao thực tế.
- Một phiếu A tương ứng duy nhất một Lot thành phẩm.
- STOCKTP.LOT luôn bằng ItemLotCode.
- Phiếu xuất kho đồng nghĩa phiếu giao khách.
- Mọi QR đều có thể nối trực tiếp tới một dòng detail linh kiện.

Các điểm trên chỉ được chuyển sang trạng thái đã chốt khi có bằng chứng từ schema, dữ liệu mẫu và đối chiếu nghiệp vụ.

## 17. Kết luận

Thiết kế chuẩn lấy SlotLot.TemCode làm đầu mối liên kết phiếu A cho dữ liệu mới từ đầu tháng 11/2026, sau đó đi từ chứng từ sản xuất tới các dòng chi tiết linh kiện và Lot linh kiện. Lịch sử kho, chuyển Lot, QR và giao hàng được nối tiếp theo các khóa đã xác minh.

Đây là nền tảng thống nhất để phát triển MES Product Genealogy trên hai form hiện hữu. Tài liệu không tuyên bố các JOIN chưa kiểm chứng là quan hệ đã hoàn tất; bước tiếp theo là xác minh V-01 đến V-12 bằng các phiếu A thực tế trước khi đóng băng SQL và triển khai code.

**Đường dẫn:** docs/MES_PRODUCT_GENEALOGY_TRACEABILITY_DESIGN.md
