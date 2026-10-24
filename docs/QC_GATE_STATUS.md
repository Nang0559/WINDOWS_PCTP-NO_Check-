# Trạng thái 6 Gate — bằng chứng thực tế

Cập nhật theo kết quả chạy trên máy Windows của người phát triển, commit \`0aaa66d\` (working tree sạch).

| Gate | Trạng thái | Bằng chứng |
|---|---|---|
| G1 | Chưa làm | Cần ma trận truy vết đối chiếu code/SQL thực tế |
| G2 | **PASS** | \`artifacts\gates\0aaa66d\G2\`: 224/224 pass, 0 fail, 0 skipped, có \`.trx\` (2026-10-24 09:42) |
| G3 | **PASS (phạm vi tempdb/fixture)** | \`artifacts\gates\0aaa66d\G3\`: preflight schema PASS + 34 dòng PASS, 0 FAIL, trên SQL 57 / \`B7R2_FCC\`, chỉ dùng bảng tạm |
| G4 | Một phần | Log G3 đã có rollback, duplicate reference, concurrency, deadlock victim, timeout (fixture tempdb). **Còn thiếu:** chạy thật \`Usp_Qrcode_Update_Stock2405\` và \`PhieuKhoRepository\` trên bản sao DB cô lập |
| G5 | Đang sửa | \`receive-navigation\`: bước 1-2 PASS; bước 3 FAIL vì runner không thấy dialog thuộc cửa sổ khác (xem \`Get-PctpWindows\`); 4 journey còn lại chưa có báo cáo |
| G6 | Chưa có | Cần DB cô lập + seed/assert/cleanup |

Ghi chú G3: log của chính nó nêu rõ các gate còn lại là SP thật trên bản sao schema cô lập, \`PhieuKhoRepository\` đầy đủ và journey UI nghiệp vụ.
