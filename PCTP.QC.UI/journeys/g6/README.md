# G6 - journey nghiep vu co seed / reset

Moi journey gom 4 phan theo thu tu: `sql-seed` -> thao tac UI -> `sql-assert` -> `sql-cleanup`.

| Manifest | Trang thai | Ghi chu |
|---|---|---|
| `g6-selftest.json` | San sang | Kiem chung co che (prefix, assert, cleanup ve 0) bang bang `#temp`, khong cham bang nghiep vu |
| `g6-qc-direction.json` | **Chua cau hinh** | Mo rong `qc-direction-navigation.json` |
| `g6-qc-final.json` | **Chua cau hinh** | Mo rong `qc-final-navigation.json` |
| `g6-receive.json` | **Chua cau hinh** | Mo rong `receive-navigation.json` |
| `g6-delivery-hvn.json` | **Chua cau hinh** | Mo rong `delivery-hvn-navigation.json` |

Journey `"configured": false` bi runner tu choi chay; Gate bao `BLOCKED`. Khong co duong nao cho PASS gia.

## Can bo sung cho moi journey nghiep vu
1. `sql/<journey>/seed.sql` - tao du lieu duy nhat, **moi khoa/ma phieu bat dau bang `@Prefix`** (`QCUI_<runId8>`).
2. Cac buoc UI thao tac that (chon dong phieu, mo form chi tiet, bam nut) voi AutomationId lay tu file `*-tree.json` cua G5 hoac `CONTROL_CATALOG.md`.
3. `sql/<journey>/assert.sql` + `expectColumns` - truy van hau dieu kien (ton kho, SlotLot, history, `PCTPStockMovementGuard`).
4. `sql/<journey>/cleanup.sql` (xoa bang con truoc) va `verify.sql` tra ve cot `Remaining` (phai = 0). Sau do doi `"configured"` thanh `true`.

## Rang buoc an toan (runner tu kiem tra)
- `-AllowMutations` bat buoc; moi buoc seed/cleanup phai khai bao `mutatesData: true`; manifest co seed bat buoc co cleanup.
- Production `192.168.200.14` / `BRAVO` luon bi tu choi. Mac dinh chi `192.168.200.57/B7R2_FCC`; DB cach ly khac phai dat ro `PCTP_QC_ISOLATED_TARGET=server/database`.
- Cau lenh seed/cleanup bat buoc tham chieu `@Prefix`, khong `TRUNCATE`, khong `DROP` bang that, khong `DELETE/UPDATE` thieu `WHERE`.
- Runner khong the biet PCTP.exe dang tro vao DB nao: no doc `PCTP.exe.config`, tu choi neu thay production, neu khong xac minh duoc thi can `-ConfirmAppUsesIsolatedDb`.
- Cleanup luon chay trong `finally`, ke ca khi buoc UI loi giua chung.

Chay: `.\PCTP.QC.UI\Invoke-PctpQcGates.ps1 -Gate G6 -ExePath <PCTP.exe> -SqlUser <user> -AllowMutations -ConfirmAppUsesIsolatedDb`
