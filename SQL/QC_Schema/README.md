# PCTP QC — SQL2 DB57 schema baseline

## Mục đích và ranh giới an toàn

CI tạo database SQL Server dùng một lần `PCTP_QC_CI` để chạy G3/G4/G6.
CI **không kết nối DB57 (192.168.200.57), DB14 hay production**. Không copy business data
từ DB57 vào Git hoặc database CI.

`docs/SQL.text` là bản xuất lịch sử bị mất/biến dạng thông tin. Không được dùng file này
để kết luận object/cột có hay không có trên DB57, cũng không dùng nó làm schema baseline.

## Nguồn dữ liệu chuẩn

- `docs/SQL2_ColumnInventory.csv`: metadata cột của dbo user tables, gồm thứ tự, kiểu,
  độ dài byte, precision/scale, nullability, identity, computed expression và default.
- `SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt`: metadata SQL2 cho module catalog,
  index, PK/unique, FK và CHECK.
- `SQL/QC_Schema/tools/sql2-db57-verified-type-map.json`: ánh xạ alias type đã được xác minh.
  Generator fail-closed nếu gặp alias/type chưa có mapping.
- C# SQL literals và SQL scripts trong repo: nguồn đối chiếu runtime/code/Git, không thay thế
  metadata DB57.

Object không thấy trong SQL2 snapshot được phân loại `GAP-db57-unverified`, không được
kết luận là "không tồn tại DB57". Các object ngoài dbo, synonym, linked server, dynamic name
và object bị thiếu trong snapshot cần xác minh riêng.

## Phạm vi CI — chỉ schema PCTP

B7R2_FCC là database nền dùng chung cho nhiều ứng dụng; PCTP chỉ sử dụng một tập đối tượng.
Vì vậy CI **không sinh 550 dbo user tables** và không sao chép toàn bộ schema B7R2.

Nguồn xác định phạm vi:
- `docs/PCTP_SQL_Runtime_Inventory_A-K.csv` là **nguồn scope có thẩm quyền**.
  Chỉ các dòng được phân loại rõ là bảng SQL Server runtime mới trở thành seed candidate.
- Câu SQL literal trong `PCTP/**/*.cs` là **completeness audit**, không tự động mở rộng DDL scope.
  Nếu code tham chiếu bảng không có trong CSV, coverage báo `GAP-not-in-runtime-inventory`
  để người phụ trách xác minh/cập nhật CSV trước khi đưa vào baseline.
- SQL2 metadata DB57 xác nhận candidate có thực sự là dbo table trên DB57 và cung cấp định nghĩa cột.
- FK SQL2: chỉ mở rộng đệ quy tới bảng dbo được tham chiếu bởi các bảng seed trong scope PCTP.

Các dòng IFS/Oracle, nguồn external, class C#, đối tượng hệ thống, test-only, template có
`{hostName}`, và dòng chưa xác định runtime không được tự động biến thành bảng CI.
Bảng có trong SQL2 nhưng không nằm trong CSV PCTP cũng không được đưa vào CI chỉ vì tồn tại
trong B7R2_FCC.
Candidate không tìm thấy trong SQL2 được ghi `GAP-db57-unverified`; không tự tạo bảng
theo suy đoán và không kết luận object không tồn tại trên DB57.

Do inventory có các biến thể theo khách hàng (HVN/SP/HTN/YMVN), mỗi tên vật lý chỉ được
đưa vào scope khi được cấu hình/runtime inventory xác nhận và khớp đúng dbo object trong
SQL2. Không gộp các biến thể có cấu trúc khác nhau.

`SQL/QC_Schema/01_Tables.sql` là fixture lịch sử, không phải baseline DB57.
CI tạo `02_Generated_From_DB57.sql` và `03_Generated_From_DB57_Constraints.sql`
từ cùng một coverage CSV tươi trong runner. Hai file này chỉ đại diện cho PCTP runtime
tables + FK dependencies, không đại diện toàn bộ B7R2_FCC.

Các bản checked-in của 02/03/04 được đánh dấu **STALE** cho tới khi regenerate theo scope mới.
Không deploy các bản này bằng tay. Lệnh regenerate bên dưới sẽ thay thế chúng bằng bản PCTP-only.

## Các generator và kiểm chứng

### 1. Table/column DDL

`tools/gen_schema_from_inventory.py` sinh `02_Generated_From_DB57.sql`.
Ngoài `CREATE TABLE` khi bảng chưa tồn tại, script phát sinh assertion hai chiều sau deploy
cho mọi bảng trong baseline. Assertion kiểm tra:
- số lượng và tên cột, thứ tự tương đối (không so sánh raw `column_id`, vì SQL Server có thể
  để gap sau khi drop cột);
- base SQL type, max_length theo byte, precision/scale;
- nullability, identity seed/increment;
- computed expression và default expression.

Do đó bảng fixture cũ không thể được chấp nhận chỉ vì `OBJECT_ID` đã tồn tại. Assertion chỉ
có thể kiểm tra các thuộc tính mà SQL2 inventory thực sự xuất; collation, sparse/rowguid,
XML schema collection và một số thuộc tính storage không có trong inventory hiện tại vẫn là
metadata gap cần bổ sung nếu cần parity tuyệt đối.

### 2. Constraint/index DDL

`tools/gen_schema_constraints.py` sinh `03_Generated_From_DB57_Constraints.sql` và
`04_SQL2_Metadata_Gaps.md`. Metadata SQL2 hiện có các nhóm index/constraint trên toàn B7R2; đó là số liệu catalog,
không phải số object PCTP sẽ deploy. Generator lọc theo đúng bảng trong PCTP runtime scope
và FK closure trước khi phát DDL. Số lượng thực tế được ghi ở đầu file SQL sinh ra và trong
`04_SQL2_Metadata_Gaps.md` của từng lần chạy CI.

Post-deploy assertions kiểm tra index type, uniqueness, PK/UQ identity, filter, disabled state,
key column order/direction và included columns; FK target, referential actions, column order,
trusted/disabled state; CHECK definition, trusted/disabled và not-for-replication state.
SQL2 FK section hiện không xuất `IsNotForReplication`; index section không xuất fill factor,
pad index, ignore-duplicate-key và row/page-lock options. Các thuộc tính không có nguồn
metadata không được giả vờ là đã kiểm chứng; chúng được nêu trong gap report.

### 3. SQL helper `dbo.Split`

`SQL/QC_Schema/05_Split.sql` contains the DB57 `dbo.Split` table-valued function.
Its definition has been checked against the structured SQL2 module catalog
(`ObjectType=TF`); it is deployed after the generated table/constraint baseline and before
the WMS stored procedures that call it. It is not sourced from the lossy `docs/SQL.text`.

### 3. Coverage

`tools/schema_coverage.py` dùng SQL2 structured metadata, không dùng `docs/SQL.text`.
Coverage phân biệt:
- `GAP-fk-dependency`: dbo table được đưa vào do FK dependency;
- `GAP-db57-unverified`: không đủ metadata để kết luận;
- `GAP-no-git-ddl`, `GAP-ci-schema`, `OK-testable`: trạng thái đối chiếu code/Git/CI.

`docs/SCHEMA_COVERAGE.csv` và `docs/SCHEMA_COVERAGE_columns.csv` checked-in hiện là
snapshot cũ trước khi chuyển sang PCTP-only scope; không dùng chúng làm bằng chứng hiện tại.
Phải regenerate trước khi dùng làm đầu vào thủ công. CI tạo coverage tươi trong runner temp directory, đánh dấu đúng tập bảng sẽ được sinh trong
lần chạy đó, rồi dùng cùng CSV để sinh cả 02 và 03. Các bảng không nằm trong CSV runtime scope và FK closure không được đưa vào DDL chỉ vì
chúng tồn tại trong SQL2 catalog; code references ngoài CSV được báo thành scope gap.

## Lệnh regenerate / kiểm thử local

Chạy từ repository root:

```powershell
python -m unittest discover -s SQL/QC_Schema/tools -p "test_*.py" -v
python SQL/QC_Schema/tools/schema_coverage.py --out docs/SCHEMA_COVERAGE --check
python SQL/QC_Schema/tools/gen_schema_from_inventory.py `
  --inventory docs/SQL2_ColumnInventory.csv `
  --coverage docs/SCHEMA_COVERAGE.csv `
  --type-map SQL/QC_Schema/tools/sql2-db57-verified-type-map.json `
  --out SQL/QC_Schema/02_Generated_From_DB57.sql
python SQL/QC_Schema/tools/gen_schema_constraints.py `
  --metadata SQL/Result_Export_SQL2_Complete_DDL_Metadata.txt `
  --coverage docs/SCHEMA_COVERAGE.csv `
  --out SQL/QC_Schema/03_Generated_From_DB57_Constraints.sql `
  --report SQL/QC_Schema/04_SQL2_Metadata_Gaps.md `
  --strict-dependencies
python SQL/QC_Schema/tools/sql_batch_check.py --files `
  SQL/QC_Schema/02_Generated_From_DB57.sql `
  SQL/QC_Schema/03_Generated_From_DB57_Constraints.sql
```

Chỉ kiểm chứng triển khai trên SQL Server disposable. Không chạy generated/candidate DDL lên DB57
hoặc production.

## Legacy YMVN ghép LOT

Schema mismatch của `GIAOHANGYMN.CMD_INGHEPLOT_Click` được ghi riêng tại
`docs/LEGACY_YMVN_GHEPLOT_GAP.md`. Không thêm cột vào DB57 để làm cho legacy SQL cũ chạy;
nếu form được tái kích hoạt phải migrate sang contract DB57 hiện có và bổ sung SQL integration
coverage.
