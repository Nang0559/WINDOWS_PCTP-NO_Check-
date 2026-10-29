# Trạng thái 6 Gate — bằng chứng thực tế

> **Quyết định phát hành: BLOCKED — chưa được đưa vào production.**
>
> G1 được chạy tự động trên PR #6 bằng workflow [PCTP G1 traceability measurement](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40794437869), trên merge ref `1307b0b80699da5c14b40cfbb26bc0b2f1f5c0c4` (PR head `b7e3bd884e1772ca0ec40bfecaf5c3cc9eb0e1cf`). Kết quả: **FAIL — 87 dòng code, 72 dòng có gap, 15 SQL scripts, 100 gap tổng**. Báo cáo máy đọc được nằm trong artifact [pctp-g1-traceability](https://github.com/Nang0559/WINDOWS_PCTP-NO_Check-/actions/runs/40794437869/artifacts/12862991922).
>
> Phân loại 100 gap: **72 code-to-test/SQL-test**, **13 SQL script references**, **15 TestMatrix rows** còn `planned` hoặc `partial`. Trong 72 gap code, 9 yêu cầu SQL test (hiện analyzer chỉ thấy fake/unit evidence); phần còn lại chủ yếu thiếu test được liên kết tường minh. Đây là kết quả phân tích tĩnh, không phải bằng chứng chạy SQL thật. Xem [kế hoạch xử lý G1](G1_REMEDIATION_PLAN.md).
>
> | Điều kiện phát hành | Trạng thái | Điều kiện để mở khóa |
> |---|---|---|
> | Unit / source-contract | CI #1452 PASS trên commit `87d9ec15c97e69061ce7e3cf46455325303c9894` | Giữ CI xanh trên commit phát hành cuối cùng |
> | G3 / G4 trên SQL Server cô lập CI | PASS trên các run trước đó; run #1452 tiếp tục xác nhận SQL engine integration | Giữ CI xanh, fixture cleanup và rollback evidence thành công |
> | G5 UI navigation trên WinForms thực | **Chưa nghiệm thu** | Windows runner thực, mọi journey bắt buộc PASS và screenshots/logs |
> | G6 end-to-end nghiệp vụ | **Chưa nghiệm thu** | Seed → UI → SQL assertions → cleanup cho các luồng đã công bố |
> | G1 traceability / coverage | **FAIL — 100 gap** trên merge ref của PR #6 | Xử lý hoặc có ngoại lệ được review cho từng gap; chạy lại G1 |
> | SQL57 live target | **Chưa có bằng chứng nghiệm thu** | Preflight read-only trên SQL57; thử nghiệm ghi chỉ trên catalog cô lập `B7R2_FCC_QC*` |
> | Production safety | **Chưa đủ bằng chứng nghiệm thu cuối** | Xác nhận credential metadata chỉ đọc, SQL14 luôn read-only/blocked với mọi đường ghi, rollback và cleanup có bằng chứng |
>
> Không được đánh dấu product-ready chỉ vì unit tests hoặc G3/G4 xanh. Không ghi dữ liệu vào SQL14 `192.168.200.14\\BRAVO` hoặc catalog dùng chung SQL57 `B7R2_FCC`. Các acceptance có ghi chỉ được chạy trên SQL Server CI dùng riêng hoặc catalog cô lập `B7R2_FCC_QC*`, với fixture xác định và cleanup có xác nhận.

## Lịch sử bằng chứng

Gate chỉ PASS khi có log chạy thật gắn với commit trong `artifacts/gates/<commit>/<Gate>/gate-summary.json`. Bằng chứng trên commit cũ không tự động áp dụng cho commit mới.

| Gate | Trạng thái / bằng chứng | Việc còn lại |
|---|---|---|
| G1 | **FAIL: 100 gap** theo run #40794437869 | Ưu tiên gap ghi dữ liệu và các kiểm thử SQL chỉ dùng fake; xem kế hoạch G1 |
| G2 | **PASS lịch sử**: commit `9bcf9c9`, 225/225 pass, 0 fail, 0 skipped | Chạy lại trên commit phát hành cuối |
| G3 | **PASS lịch sử trong phạm vi tempdb/fixture**: commit `9bcf9c9` | Không suy rộng thành nghiệm thu SQL57 hoặc schema production |
| G4 | **PASS lịch sử**: commit `9bcf9c9`, DEL-003/DEL-004/DEL-007 chạy trên procedure thật với rollback | Chưa phủ `PhieuKhoRepository` end-to-end; cần bằng chứng mới trên commit cuối |
| G5 | **Chưa hoàn tất**: lịch sử `receive-navigation` lỗi trước khi cửa sổ nhập kho xuất hiện | Chạy lại trên Windows runner; lưu log, screenshot và gate summary |
| G6 | **Chưa hoàn tất**: framework có `sql-seed/sql-assert/sql-cleanup`, nhưng journey nghiệp vụ chưa đủ | Cấu hình và chạy các journey nghiệp vụ thực |
| Live SQL57 | **Chưa chạy** | Chỉ chạy inventory read-only bằng tài khoản metadata chuyên dụng; không ghi vào catalog dùng chung |

