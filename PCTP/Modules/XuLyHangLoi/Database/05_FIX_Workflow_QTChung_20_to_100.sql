/* Thêm transition QT_CHUNG: DaDinhHuong(20) -> HoanTat(100) cho trường hợp Initial QC 100% OK. Chạy lại an toàn. */
USE [B7R2_FCC];
SET XACT_ABORT ON;
GO
IF COLUMNPROPERTY(OBJECT_ID(N'dbo.FVN_sys_WorkflowTransitions'), N'Id', 'IsIdentity') <> 1
    THROW 51001, N'FVN_sys_WorkflowTransitions.Id không phải IDENTITY - cần sửa trước.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.FVN_sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' AND FromStatus=20 AND ToStatus=100 AND IsActive=1)
    INSERT INTO dbo.FVN_sys_WorkflowTransitions (ProcessCode, FromStatus, ToStatus, ActionName, Description, IsActive)
    VALUES ('QT_CHUNG', 20, 100, 'HOAN_TAT', N'Initial QC 100% OK, không NG/Rework, hoàn tất', 1);

SELECT FromStatus, ToStatus, ActionName, IsActive FROM dbo.FVN_sys_WorkflowTransitions WHERE ProcessCode='QT_CHUNG' ORDER BY FromStatus, ToStatus;
GO
