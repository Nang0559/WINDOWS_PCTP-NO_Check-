SET NOCOUNT ON;

DECLARE @Tables TABLE (TableName SYSNAME);
INSERT INTO @Tables (TableName) VALUES
('IFSPHIEUGIAOHANG_100002'), ('IFSPHIEUGIAOHANG'), ('IFSPHIEUGIAOHANGView'), ('TMPPHIEUGIAOHANGView_DK_PC21'), 
('SP_DOCQRCODE'), ('YMVN_DOCQRCODE'), ('DOCQRCODE_SP'), ('DOCQRCODE'), ('PCTPStockMovementGuard'), 
('IFSPHIEUGIAOHANG_100003'), ('IFSPHIEUGIAOHANG_SP'), ('FVN_TraHangQTChungXuat'), ('FVN_sys_WorkflowTransitions'), 
('FVN_Purchase_Order_HVNDB'), ('FVN_PCTPQuarantineReleaseAudit'), ('FVN_DeliveryCommandLedger'), ('FVN_DeliveryCommandQr'), 
('LUUPHIEUGIAOHANG'), ('FVN_TraHangQTChungQC'), ('FVN_PhieuXuLyBatThuong'), ('SlotLot'), ('STOCKTP'), 
('FVN_HangChoGiaoPickDetail'), ('StockHistory'), ('FVN_TraHangQTChungNhapNG'), ('FVN_TraHangQTChungGiao'), 
('FVN_PhieuTraHangCT'), ('FVN_PhieuTraHang'), ('FVN_PXLB_WorkflowAudit'), ('FVN_PXLB_ReworkMovement'), 
('FVN_PXLB_CompensationMovement'), ('FVN_PXLB_Compensation'), ('FVN_PXLB_Disposition'), ('FVN_PXLB_ReworkQC'), 
('FVN_PXLB_ReworkPlan'), ('FVN_PhieuXuLyBatThuongQCInitial'), ('FVN_PhieuXuLyBatThuongAffectedLot'), 
('TMPPHIEUGIAOHANGDB_IFS'), ('TMPPHIEUGIAODBHD'), ('TMPPHIEUGIAOHANGDBCT'), ('TMPPHIEUGIAOHANGDB'), 
('FVN_ItemFifoConfig_History'), ('FVN_ItemFifoConfig'), ('TMPLOTGHEP'), ('FVN_TraHangQTChung_NhapNG'), 
('FVN_TraHangQTChung_QC'), ('FVN_TraHangQTChung_Giao'), ('FVN_TraHangQTChung_Xuat'), ('PhieuXuLyBatThuong');

DECLARE @TName SYSNAME;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @Tables;

OPEN cur;
FETCH NEXT FROM cur INTO @TName;

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT '--------------------------------------------------';
    PRINT '-- TABLE: ' + @TName;
    PRINT '--------------------------------------------------';
    PRINT 'IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N''[dbo].[' + @TName + ']'') AND type in (N''U''))';
    PRINT 'BEGIN';
    PRINT '    CREATE TABLE [dbo].[' + @TName + '] (';
    
    -- 1. Lấy danh sách cột
    DECLARE @ColDef NVARCHAR(MAX) = '';
    SELECT @ColDef = @ColDef + '        [' + c.name + '] ' + 
        t.name + 
        CASE 
            WHEN t.name in ('varchar', 'char', 'varbinary', 'binary') THEN '(' + CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length AS VARCHAR(10)) END + ')'
            WHEN t.name in ('nvarchar', 'nchar') THEN '(' + CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length / 2 AS VARCHAR(10)) END + ')'
            WHEN t.name in ('decimal', 'numeric') THEN '(' + CAST(c.precision AS VARCHAR(5)) + ',' + CAST(c.scale AS VARCHAR(5)) + ')'
            ELSE ''
        END + 
        CASE WHEN c.is_nullable = 0 THEN ' NOT NULL' ELSE ' NULL' END + ',' + CHAR(13) + CHAR(10)
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(@TName)
    ORDER BY c.column_id;

    IF LEN(@ColDef) > 0
        SET @ColDef = LEFT(@ColDef, LEN(@ColDef) - 3);

    PRINT @ColDef;
    PRINT '    );';
    PRINT 'END';
    PRINT 'GO';

    -- 2. Kiểm tra và sinh lệnh tạo Primary Key an toàn (Không bị lỗi BEGIN...END trống)
    DECLARE @PKScript NVARCHAR(MAX) = '';
    DECLARE @PKName SYSNAME, @PKCols NVARCHAR(MAX);
    
    DECLARE pk_cur CURSOR LOCAL FAST_FORWARD FOR 
        SELECT kc.name, 
               STUFF((SELECT ', [' + col.name + ']' 
                      FROM sys.index_columns ic 
                      INNER JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id 
                      WHERE ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id 
                      FOR XML PATH('')), 1, 2, '')
        FROM sys.key_constraints kc
        WHERE kc.parent_object_id = OBJECT_ID(@TName) AND kc.type = 'PK';
    
    OPEN pk_cur;
    FETCH NEXT FROM pk_cur INTO @PKName, @PKCols;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @PKScript = @PKScript + '    ALTER TABLE [dbo].[' + @TName + '] ADD CONSTRAINT [' + @PKName + '] PRIMARY KEY (' + @PKCols + ');' + CHAR(13) + CHAR(10);
        FETCH NEXT FROM pk_cur INTO @PKName, @PKCols;
    END
    CLOSE pk_cur;
    DEALLOCATE pk_cur;

    -- Chỉ in khối kiểm tra PK nếu bảng thực sự có khóa chính
    IF LEN(@PKScript) > 0
    BEGIN
        PRINT 'IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N''[dbo].[' + @TName + ']'') AND type = ''PK'')';
        PRINT 'BEGIN';
        PRINT @PKScript;
        PRINT 'END';
        PRINT 'GO';
    END

    PRINT '';

    FETCH NEXT FROM cur INTO @TName;
END

CLOSE cur;
DEALLOCATE cur;