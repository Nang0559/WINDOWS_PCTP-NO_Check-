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

## Phạm vi CI

Snapshot hiện tại có **550 dbo user tables**. CI sinh toàn bộ 550 bảng và các metadata
constraint/index tương ứng, không còn giới hạn ở allowlist WMS 19 bảng cũ. Bộ CI không phải
bản sao đầy đủ mọi tính năng của DB57: không có business data, non-dbo schemas, toàn bộ
2.345 module bodies, SQL Agent jobs, permissions hoặc linked-server configuration.

Transitive dbo FK dependencies được truy vết từ các bảng code tham chiếu. Trong baseline
550 bảng, target dbo đã nằm trong generated scope. Nếu FK/index không thể tái tạo từ SQL2,
`--strict-dependencies` làm CI thất bại thay vì âm thầm bỏ qua.

`SQL/QC_Schema/01_Tables.sql` là fixture lịch sử 15 bảng, **không còn được deploy**.
CI dùng `02_Generated_From_DB57.sql` làm table baseline chuẩn. Hai bảng
`DOCQRCODE_100003` và `TMPPHIEUGIAOHANG_100003` không còn lấy từ fixture cũ vì cấu trúc
khách hàng khác; chúng được tạo trực tiếp từ SQL2 inventory DB57.

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
`04_SQL2_Metadata_Gaps.md`. Script tái tạo:
- 248 ordinary/filtered indexes;
- 439 primary keys;
- 167 unique constraints;
- 31 foreign keys;
- 28 CHECK constraints.

Các con số là snapshot SQL2 hiện tại; CI sinh lại từ metadata trong từng run.

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
- `GAP-db57-baseline`: dbo table có trong SQL2 nhưng không phải runtime reference;
- `GAP-fk-dependency`: table được đưa vào do FK dependency;
- `GAP-db57-unverified`: không đủ metadata để kết luận;
- `GAP-no-git-ddl`, `GAP-ci-schema`, `OK-testable`: trạng thái đối chiếu code/Git/CI.

`docs/SCHEMA_COVERAGE.csv` là snapshot checked-in và phải được regenerate trước khi dùng
làm đầu vào thủ công. CI tạo coverage tươi trong runner temp directory rồi dùng đúng CSV đó
để sinh cả 02 và 03, không phụ thuộc snapshot cũ.

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
