/* PHASE 7 - QT CHUNG WORKFLOW TRANSITIONS */
IF OBJECT_ID(N'dbo.sys_WorkflowTransitions', N'U') IS NULL
    THROW 51000, 'sys_WorkflowTransitions chưa tồn tại. Chạy migration workflow core trước.', 1;
GO

/* DaDinhHuong -> branches */
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=20 AND ToStatus=25 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',20,25,'TU_CHOI_GIAO_BU','QC xác nhận không phát sinh giao bù',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=20 AND ToStatus=30 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',20,30,'CHO_GIAO_BU','Tạo nghĩa vụ giao bù độc lập',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=20 AND ToStatus=40 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',20,40,'BAT_DAU_REWORK','Initial QC đã phân bổ Rework',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=25 AND ToStatus=100 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',25,100,'HOAN_TAT','Khiếu nại không có căn cứ, kết thúc không giao bù',1);

/* Compensation */
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=30 AND ToStatus=35 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',30,35,'HOAN_TAT_GIAO_BU','Đã giao đủ nghĩa vụ giao bù',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=35 AND ToStatus=100 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',35,100,'HOAN_TAT','Hoàn tất nhánh giao bù',1);

/* Rework */
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=20 AND ToStatus=40 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',20,40,'BAT_DAU_REWORK','Initial QC đã phân bổ Rework',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=40 AND ToStatus=50 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',40,50,'GIAO_SAN_XUAT','Đã xuất đủ Rework và giao sản xuất',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=50 AND ToStatus=60 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',50,60,'QC_REWORK','QC xác nhận OK/NG sau Rework',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=60 AND ToStatus=70 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',60,70,'NHAP_LAI_NG','Có NG sau Rework cần nhập/Disposition',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=60 AND ToStatus=100 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',60,100,'HOAN_TAT','Rework QC 100% OK và không còn nghĩa vụ',1);
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=70 AND ToStatus=100 AND IsActive=1)
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    VALUES('QT_CHUNG',70,100,'HOAN_TAT','Đã xử lý toàn bộ NG/Disposition',1);

/* Common cancellation */
IF NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND ToStatus=900 AND IsActive=1)
BEGIN
    INSERT INTO dbo.sys_WorkflowTransitions(ProcessCode,FromStatus,ToStatus,ActionName,Description,IsActive)
    SELECT 'QT_CHUNG', v.StatusValue, 900, 'HUY', 'Hủy phiếu do sai thao tác/trùng/sai nguồn', 1
    FROM (VALUES(10),(20),(25),(30),(35),(40),(50),(60),(70)) v(StatusValue)
    WHERE NOT EXISTS(SELECT 1 FROM dbo.sys_WorkflowTransitions x WHERE x.ProcessCode='QT_CHUNG' AND x.FromStatus=v.StatusValue AND x.ToStatus=900 AND x.IsActive=1);
END;
GO
