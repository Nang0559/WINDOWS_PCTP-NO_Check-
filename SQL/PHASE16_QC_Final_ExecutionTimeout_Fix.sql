/* ============================================================================
   PHASE16 - QC XÁC NHẬN CUỐI: Execution Timeout
   ----------------------------------------------------------------------------
   Áp dụng cho luồng:
     QTChungService.QCXacNhanCuoi
       -> ReworkStockService.NhapLaiHangOK / NhapLaiHangNG
       -> StockMovementService.ReturnFromRework / Receive
       -> STOCKTP + SlotLot + FVN_TraHangQTChung*

   Mục tiêu:
     1) Loại bỏ full scan ở các predicate LOT / Slot / PhieuTraHangId + Status.
     2) Giảm thời gian giữ lock trong transaction QC cuối.
     3) Có script chẩn đoán blocker ngay khi timeout tái diễn.

   LƯU Ý:
     - Script chỉ tạo NONCLUSTERED INDEX nếu chưa tồn tại.
     - Không đổi dữ liệu nghiệp vụ.
     - Không SET LOCK_TIMEOUT toàn DB và không dùng NOLOCK cho nghiệp vụ tồn kho.
   ============================================================================ */

SET NOCOUNT ON;

/* ---------------------------------------------------------------------------
   1. STOCKTP: QC OK đọc tồn theo LOT và sau đó UPDATE cùng LOT.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.STOCKTP', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.STOCKTP', N'LOT') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOCKTP')
      AND name = N'IX_STOCKTP_LOT_QC_FINAL'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_STOCKTP_LOT_QC_FINAL
        ON dbo.STOCKTP (LOT)
        INCLUDE (SLCONLAI, SLXUAT, NGAYXUAT);
END;
GO

/* ---------------------------------------------------------------------------
   2. SlotLot: các thao tác QC/Rework thường lọc theo SlotId + PhieuStatus + LOT.
      Index này cũng hỗ trợ GetLots() khi đồng bộ header Slot.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.SlotLot', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.SlotLot', N'SlotId') IS NOT NULL
AND COL_LENGTH(N'dbo.SlotLot', N'PhieuStatus') IS NOT NULL
AND COL_LENGTH(N'dbo.SlotLot', N'LotNo') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SlotLot')
      AND name = N'IX_SlotLot_Slot_Status_Lot_QC_FINAL'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SlotLot_Slot_Status_Lot_QC_FINAL
        ON dbo.SlotLot (SlotId, PhieuStatus, LotNo)
        INCLUDE (Quantity, ItemCode, TemCode, ImportDate);
END;
GO

/* ---------------------------------------------------------------------------
   3. PhieuXuLyBatThuong: TryHoanTatHeader/ConChoXuLy kiểm tra theo Header + Status.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.FVN_PhieuXuLyBatThuong', N'PhieuTraHangId') IS NOT NULL
AND COL_LENGTH(N'dbo.FVN_PhieuXuLyBatThuong', N'Status') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong')
      AND name = N'IX_FVN_PhieuXuLy_Header_Status_QC_FINAL'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FVN_PhieuXuLy_Header_Status_QC_FINAL
        ON dbo.FVN_PhieuXuLyBatThuong (PhieuTraHangId, Status)
        INCLUDE (Id, PhieuTraHangCTId);
END;
GO

/* ---------------------------------------------------------------------------
   4. QC result: GetQC(phieuXuLyId) trong nhánh NG.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.FVN_TraHangQTChungQC', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.FVN_TraHangQTChungQC', N'PhieuXuLyBatThuongId') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FVN_TraHangQTChungQC')
      AND name = N'IX_FVN_TraHangQTChungQC_PhieuXuLy_QC_FINAL'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FVN_TraHangQTChungQC_PhieuXuLy_QC_FINAL
        ON dbo.FVN_TraHangQTChungQC (PhieuXuLyBatThuongId, ThoiGian DESC, Id DESC)
        INCLUDE (SoLuongOK, SoLuongNG, DaKiemTraTem);
END;
GO

/* ---------------------------------------------------------------------------
   5. Xác nhận các index đã có.
   --------------------------------------------------------------------------- */
SELECT
    OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType
FROM sys.indexes i
WHERE i.name IN
(
    N'IX_STOCKTP_LOT_QC_FINAL',
    N'IX_SlotLot_Slot_Status_Lot_QC_FINAL',
    N'IX_FVN_PhieuXuLy_Header_Status_QC_FINAL',
    N'IX_FVN_TraHangQTChungQC_PhieuXuLy_QC_FINAL'
)
ORDER BY TableName, IndexName;
GO

/* ============================================================================
   DIAGNOSTIC - chạy NGAY KHI popup timeout xuất hiện.
   Không chạy KILL tự động. Chỉ xác định session đang chờ và session blocker.
   ============================================================================ */
SELECT
    r.session_id,
    r.blocking_session_id,
    r.status,
    r.command,
    r.wait_type,
    r.wait_time,
    r.wait_resource,
    r.cpu_time,
    r.total_elapsed_time,
    DB_NAME(r.database_id) AS DatabaseName,
    s.host_name,
    s.program_name,
    s.login_name,
    txt.text AS SqlText
FROM sys.dm_exec_requests r
JOIN sys.dm_exec_sessions s
    ON s.session_id = r.session_id
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) txt
WHERE r.session_id <> @@SPID
  AND (r.blocking_session_id <> 0 OR r.wait_type IS NOT NULL)
ORDER BY r.total_elapsed_time DESC, r.wait_time DESC;
GO

/* Lock của các bảng chính trong luồng QC cuối. */
SELECT
    tl.request_session_id AS SessionId,
    DB_NAME(tl.resource_database_id) AS DatabaseName,
    OBJECT_SCHEMA_NAME(p.object_id, tl.resource_database_id) AS SchemaName,
    OBJECT_NAME(p.object_id, tl.resource_database_id) AS TableName,
    tl.resource_type,
    tl.request_mode,
    tl.request_status
FROM sys.dm_tran_locks tl
LEFT JOIN sys.partitions p
    ON p.hobt_id = tl.resource_associated_entity_id
WHERE tl.resource_database_id = DB_ID()
  AND OBJECT_NAME(p.object_id, tl.resource_database_id) IN
  (
      N'STOCKTP',
      N'SlotLot',
      N'Slot',
      N'FVN_PhieuXuLyBatThuong',
      N'FVN_PhieuTraHang',
      N'FVN_TraHangQTChungQC',
      N'FVN_TraHangQTChungNhapNG'
  )
ORDER BY tl.request_session_id, TableName, tl.request_mode;
GO
