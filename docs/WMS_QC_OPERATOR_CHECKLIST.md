# WMS QC — checklist chạy tại máy phát triển / SQL test

Tài liệu này là lối chạy ngắn cho người vận hành. Chi tiết triển khai SQL nghiệp vụ và điều kiện an toàn nằm tại [WMS_SQL_RUNBOOK.md](WMS_SQL_RUNBOOK.md); định nghĩa gate nằm tại PCTP/Workflow/Diagram/WMS_RELEASE_GATE.md.

## Phạm vi an toàn

- **Invoke-WmsQcPreflight.ps1** chỉ chạy Python/unit tests, kiểm tra file DDL đã sinh, mô phỏng chia batch SQL và sinh G1. Script **không kết nối SQL Server, không chạy DDL, không sửa DB**.
- **SQL/QC_Schema/01_Tables.sql**, **02_Generated_From_DB57.sql**, **03_Generated_From_DB57_Constraints.sql** và **05_Split.sql** là schema cho database QC tạm thời/CI. Không chạy chúng lên DB57, DB14 hoặc production.
- Script nghiệp vụ trong **SQL/** và **PCTP/Modules/*/Database/** chỉ được deploy lên DB test cô lập sau backup, phê duyệt của người quản DB và preflight nhận diện đúng máy chủ. Tuyệt đối không deploy vào production **192.168.200.14\\BRAVO**.

## A. Chạy tại máy Windows — chưa cần SQL Server

Mở PowerShell tại thư mục gốc repo:

~~~powershell
git switch feature/wms-end-to-end-consistency
git pull
git status --short
git rev-parse HEAD

.\PCTP.QC.UI\Invoke-WmsQcPreflight.ps1
~~~

Yêu cầu:
- git status --short không có output.
- Script kết thúc **PREFLIGHT PASS**.
- Nếu G1 còn gap, script dừng và tạo bằng chứng dưới **artifacts/gates/<sha>/G1/**. Không dùng **-AllowDirty** để tạo bằng chứng release; tùy chọn này chỉ dành cho chẩn đoán cục bộ.

Sau preflight, chạy unit/regression gate:

~~~powershell
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G2
~~~

## B. Xác nhận SQL Server trước khi chạy bất kỳ SQL nghiệp vụ nào

Trong SSMS, mở đúng kết nối test và chạy truy vấn chỉ đọc:

~~~sql
SELECT
    @@SERVERNAME AS ServerName,
    DB_NAME() AS DatabaseName,
    CONVERT(nvarchar(48), CONNECTIONPROPERTY('local_net_address')) AS SqlServerIp;
~~~

Đối chiếu với DBA: server test dự kiến **192.168.200.57**, database **B7R2_FCC**. Nếu IP trả về NULL, khác IP, hoặc không chắc kết nối đang vào máy nào: **dừng**, không chạy DDL. Không dựa riêng vào tên database vì DB test và production có thể cùng tên.

Không chạy **03_Generated_From_DB57_Constraints.sql** hay **02_Generated_From_DB57.sql** trên DB57. Các file đó được CI triển khai vào database QC tạm thời **PCTP_QC_CI**.

## C. Chạy SQL Integration và procedure acceptance

Chỉ chạy sau khi A PASS, đã có SQL test credentials và DBA xác nhận database test cô lập:

~~~powershell
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G3 -SqlUser <sql-user>
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G4 -SqlUser <sql-user>
~~~

- G3 dùng fixture SQL/engine integration; trong đó R1–R5 hai session chạy bên trong G3.
- G4 chỉ hợp lệ khi G3 PASS trên **cùng commit SHA**; procedure acceptance phải rollback-only.
- Không ghi password vào lệnh, script, file hoặc log. Dùng cơ chế biến môi trường/prompt đã mô tả trong WMS_SQL_RUNBOOK.md.

## D. UI

G5 chỉ kiểm tra điều hướng; G6 có thao tác ghi dữ liệu. Không chạy G6 cho tới khi ứng dụng test được cấu hình trỏ đúng DB test cô lập và người quản DB cho phép:

~~~powershell
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G5 -ExePath <path-to-isolated-copy-of-PCTP.exe>
.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G6 -ExePath <path-to-isolated-copy-of-PCTP.exe> -SqlUser <sql-user> -AllowMutations -ConfirmAppUsesIsolatedDb
~~~

Không sửa connection string trong thư mục build đang dùng hằng ngày. Tạo bản copy riêng theo hướng dẫn ở WMS_SQL_RUNBOOK.md. Không bỏ qua guard production của runner.

## E. Deploy migration WMS

Đây là công việc riêng, không phải điều kiện để chạy A. Chỉ khi cần cập nhật schema nghiệp vụ trên DB test, thực hiện theo đúng thứ tự và preflight từng script tại WMS_SQL_RUNBOOK.md §3. Một số script có thể thay đổi dữ liệu/cấu trúc; không chạy toàn bộ một cách mù quáng. **PCTP_StockTpIdentityAudit.sql** là bước đọc trước; **PCTP_EnforceUniqueStockTpLot.sql** chỉ được chạy nếu audit không còn LOT trùng.

## Bằng chứng phải lưu

Mỗi lần chạy lưu:
- commit SHA đầy đủ và git status --porcelain;
- lệnh PowerShell chính xác, ngày giờ, người chạy;
- server/database/IP đã xác minh (không lưu mật khẩu);
- toàn bộ thư mục **artifacts/gates/<sha>/<Gate>/**;
- kết quả PASS/FAIL. Không đổi **IMPLEMENTED / NOT VERIFIED** thành PASS nếu chưa có log chạy thật.
