/*
 AUTO-GENERATED from SQL2 complete DDL metadata for the reviewed GAP-no-git-ddl allowlist.
 Contains PK/unique constraints, indexes, foreign keys and CHECK constraints only.
 DEFAULTs are already emitted inline in 02_Generated_From_DB57.sql and are not duplicated.
 Review required. Deploy only after 02_Generated_From_DB57.sql and only to disposable PCTP_QC_CI.
 Never apply this file to DB57 or production.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.B20Gear') AND name = N'PK_GearList')
BEGIN
    ALTER TABLE [dbo].[B20Gear] ADD CONSTRAINT [PK_GearList] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.B20Item') AND name = N'IX_B20Item_BarCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_B20Item_BarCode] ON [dbo].[B20Item] ([BarCode] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.B20Item') AND name = N'IX_B20Item_Code')
BEGIN
    ALTER TABLE [dbo].[B20Item] ADD CONSTRAINT [IX_B20Item_Code] UNIQUE CLUSTERED ([Code] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.B20Item') AND name = N'PK_B20Item')
BEGIN
    ALTER TABLE [dbo].[B20Item] ADD CONSTRAINT [PK_B20Item] PRIMARY KEY NONCLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_HangChoGiao') AND name = N'PK__FVN_Hang__3214EC07E6905088')
BEGIN
    ALTER TABLE [dbo].[FVN_HangChoGiao] ADD CONSTRAINT [PK__FVN_Hang__3214EC07E6905088] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History') AND name = N'IX_FifoHistory_ItemCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FifoHistory_ItemCode] ON [dbo].[FVN_ItemFifoConfig_History] ([ItemCode] ASC, [ThoiGian] DESC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History') AND name = N'PK__FVN_Item__3214EC072BCF3DCA')
BEGIN
    ALTER TABLE [dbo].[FVN_ItemFifoConfig_History] ADD CONSTRAINT [PK__FVN_Item__3214EC072BCF3DCA] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_ItemFifoConfig') AND name = N'PK__FVN_Item__3ECC0FEBBDA5DBF4')
BEGIN
    ALTER TABLE [dbo].[FVN_ItemFifoConfig] ADD CONSTRAINT [PK__FVN_Item__3ECC0FEBBDA5DBF4] PRIMARY KEY CLUSTERED ([ItemCode] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name = N'IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId] ON [dbo].[FVN_PhieuTraHangCT] ([PhieuXuLyBatThuongId] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name = N'PK_FVN_PhieuTraHangCT')
BEGIN
    ALTER TABLE [dbo].[FVN_PhieuTraHangCT] ADD CONSTRAINT [PK_FVN_PhieuTraHangCT] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHang') AND name = N'PK_FVN_PhieuTraHang')
BEGIN
    ALTER TABLE [dbo].[FVN_PhieuTraHang] ADD CONSTRAINT [PK_FVN_PhieuTraHang] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'IX_FVN_PhieuXuLy_Header_Status_QC_FINAL')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FVN_PhieuXuLy_Header_Status_QC_FINAL] ON [dbo].[FVN_PhieuXuLyBatThuong] ([PhieuTraHangId] ASC, [Status] ASC) INCLUDE ([Id], [PhieuTraHangCTId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'PK_FVN_PhieuXuLyBatThuong')
BEGIN
    ALTER TABLE [dbo].[FVN_PhieuXuLyBatThuong] ADD CONSTRAINT [PK_FVN_PhieuXuLyBatThuong] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'UQ_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId')
BEGIN
    ALTER TABLE [dbo].[FVN_PhieuXuLyBatThuong] ADD CONSTRAINT [UQ_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId] UNIQUE NONCLUSTERED ([PhieuTraHangCTId] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId] ON [dbo].[FVN_PhieuXuLyBatThuong] ([PhieuTraHangCTId] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.InspectionConfig') AND name = N'PK__Inspecti__C3BC335CB9A2E9A7')
BEGIN
    ALTER TABLE [dbo].[InspectionConfig] ADD CONSTRAINT [PK__Inspecti__C3BC335CB9A2E9A7] PRIMARY KEY CLUSTERED ([ConfigId] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.InspectionConfig') AND name = N'UQ__Inspecti__3ECC0FEABAE82892')
BEGIN
    ALTER TABLE [dbo].[InspectionConfig] ADD CONSTRAINT [UQ__Inspecti__3ECC0FEABAE82892] UNIQUE NONCLUSTERED ([ItemCode] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.InspectionLog') AND name = N'PK__Inspecti__5E54864800C0C605')
BEGIN
    ALTER TABLE [dbo].[InspectionLog] ADD CONSTRAINT [PK__Inspecti__5E54864800C0C605] PRIMARY KEY CLUSTERED ([LogId] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.StockHistory') AND name = N'PK__StockHis__3214EC0738B89F8A')
BEGIN
    ALTER TABLE [dbo].[StockHistory] ADD CONSTRAINT [PK__StockHis__3214EC0738B89F8A] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.sys_WorkflowTransitions') AND name = N'PK__sys_Work__3214EC078ACC2616')
BEGIN
    ALTER TABLE [dbo].[sys_WorkflowTransitions] ADD CONSTRAINT [PK__sys_Work__3214EC078ACC2616] PRIMARY KEY CLUSTERED ([Id] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.sys_WorkflowTransitions') AND name = N'UK_Transition')
BEGIN
    ALTER TABLE [dbo].[sys_WorkflowTransitions] ADD CONSTRAINT [UK_Transition] UNIQUE NONCLUSTERED ([ProcessCode] ASC, [FromStatus] ASC, [ToStatus] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD') AND name = N'PK__TMPPHIEU__C4971C3EC4DB953D')
BEGIN
    ALTER TABLE [dbo].[TMPPHIEUGIAODBHD] ADD CONSTRAINT [PK__TMPPHIEU__C4971C3EC4DB953D] PRIMARY KEY CLUSTERED ([IDP] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT') AND name = N'PK__TMPPHIEU__3214EC273EFF485B')
BEGIN
    ALTER TABLE [dbo].[TMPPHIEUGIAOHANGDBCT] ADD CONSTRAINT [PK__TMPPHIEU__3214EC273EFF485B] PRIMARY KEY CLUSTERED ([ID] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name = N'FK_FVN_PhieuTraHangCT_Header')
BEGIN
    ALTER TABLE [dbo].[FVN_PhieuTraHangCT] WITH CHECK ADD CONSTRAINT [FK_FVN_PhieuTraHangCT_Header] FOREIGN KEY ([PhieuTraHangId])
        REFERENCES [dbo].[FVN_PhieuTraHang] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION;
END;
ALTER TABLE [dbo].[FVN_PhieuTraHangCT] WITH CHECK CHECK CONSTRAINT [FK_FVN_PhieuTraHangCT_Header]
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT') AND name = N'FK_TMPPHIEUGIAOHANGDBCT_HD')
BEGIN
    ALTER TABLE [dbo].[TMPPHIEUGIAOHANGDBCT] WITH CHECK ADD CONSTRAINT [FK_TMPPHIEUGIAOHANGDBCT_HD] FOREIGN KEY ([IDP])
        REFERENCES [dbo].[TMPPHIEUGIAODBHD] ([IDP]) ON DELETE NO ACTION ON UPDATE NO ACTION;
END;
ALTER TABLE [dbo].[TMPPHIEUGIAOHANGDBCT] WITH CHECK CHECK CONSTRAINT [FK_TMPPHIEUGIAOHANGDBCT_HD]
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'CK_FVN_PhieuXuLyBatThuong_Nguon')
BEGIN
    ALTER TABLE [dbo].[FVN_PhieuXuLyBatThuong] WITH CHECK ADD CONSTRAINT [CK_FVN_PhieuXuLyBatThuong_Nguon] CHECK ([Nguon]=(2) OR [Nguon]=(1));
END;
ALTER TABLE [dbo].[FVN_PhieuXuLyBatThuong] WITH CHECK CHECK CONSTRAINT [CK_FVN_PhieuXuLyBatThuong_Nguon]
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'CK_FVN_PhieuXuLyBatThuong_SoLuongLoi')
BEGIN
    ALTER TABLE [dbo].[FVN_PhieuXuLyBatThuong] WITH CHECK ADD CONSTRAINT [CK_FVN_PhieuXuLyBatThuong_SoLuongLoi] CHECK ([SoLuongLoi]>=(0));
END;
ALTER TABLE [dbo].[FVN_PhieuXuLyBatThuong] WITH CHECK CHECK CONSTRAINT [CK_FVN_PhieuXuLyBatThuong_SoLuongLoi]
GO

-- Post-deployment metadata assertions: fail CI if any expected object is absent.
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.B20Gear') AND name = N'PK_GearList') THROW 52301, N'Missing index/constraint dbo.B20Gear.PK_GearList', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.B20Item') AND name = N'IX_B20Item_BarCode') THROW 52301, N'Missing index/constraint dbo.B20Item.IX_B20Item_BarCode', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.B20Item') AND name = N'IX_B20Item_Code') THROW 52301, N'Missing index/constraint dbo.B20Item.IX_B20Item_Code', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.B20Item') AND name = N'PK_B20Item') THROW 52301, N'Missing index/constraint dbo.B20Item.PK_B20Item', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_HangChoGiao') AND name = N'PK__FVN_Hang__3214EC07E6905088') THROW 52301, N'Missing index/constraint dbo.FVN_HangChoGiao.PK__FVN_Hang__3214EC07E6905088', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History') AND name = N'IX_FifoHistory_ItemCode') THROW 52301, N'Missing index/constraint dbo.FVN_ItemFifoConfig_History.IX_FifoHistory_ItemCode', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History') AND name = N'PK__FVN_Item__3214EC072BCF3DCA') THROW 52301, N'Missing index/constraint dbo.FVN_ItemFifoConfig_History.PK__FVN_Item__3214EC072BCF3DCA', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_ItemFifoConfig') AND name = N'PK__FVN_Item__3ECC0FEBBDA5DBF4') THROW 52301, N'Missing index/constraint dbo.FVN_ItemFifoConfig.PK__FVN_Item__3ECC0FEBBDA5DBF4', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name = N'IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId') THROW 52301, N'Missing index/constraint dbo.FVN_PhieuTraHangCT.IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name = N'PK_FVN_PhieuTraHangCT') THROW 52301, N'Missing index/constraint dbo.FVN_PhieuTraHangCT.PK_FVN_PhieuTraHangCT', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHang') AND name = N'PK_FVN_PhieuTraHang') THROW 52301, N'Missing index/constraint dbo.FVN_PhieuTraHang.PK_FVN_PhieuTraHang', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'IX_FVN_PhieuXuLy_Header_Status_QC_FINAL') THROW 52301, N'Missing index/constraint dbo.FVN_PhieuXuLyBatThuong.IX_FVN_PhieuXuLy_Header_Status_QC_FINAL', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'PK_FVN_PhieuXuLyBatThuong') THROW 52301, N'Missing index/constraint dbo.FVN_PhieuXuLyBatThuong.PK_FVN_PhieuXuLyBatThuong', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'UQ_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId') THROW 52301, N'Missing index/constraint dbo.FVN_PhieuXuLyBatThuong.UQ_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId') THROW 52301, N'Missing index/constraint dbo.FVN_PhieuXuLyBatThuong.UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InspectionConfig') AND name = N'PK__Inspecti__C3BC335CB9A2E9A7') THROW 52301, N'Missing index/constraint dbo.InspectionConfig.PK__Inspecti__C3BC335CB9A2E9A7', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InspectionConfig') AND name = N'UQ__Inspecti__3ECC0FEABAE82892') THROW 52301, N'Missing index/constraint dbo.InspectionConfig.UQ__Inspecti__3ECC0FEABAE82892', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InspectionLog') AND name = N'PK__Inspecti__5E54864800C0C605') THROW 52301, N'Missing index/constraint dbo.InspectionLog.PK__Inspecti__5E54864800C0C605', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockHistory') AND name = N'PK__StockHis__3214EC0738B89F8A') THROW 52301, N'Missing index/constraint dbo.StockHistory.PK__StockHis__3214EC0738B89F8A', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.sys_WorkflowTransitions') AND name = N'PK__sys_Work__3214EC078ACC2616') THROW 52301, N'Missing index/constraint dbo.sys_WorkflowTransitions.PK__sys_Work__3214EC078ACC2616', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.sys_WorkflowTransitions') AND name = N'UK_Transition') THROW 52301, N'Missing index/constraint dbo.sys_WorkflowTransitions.UK_Transition', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD') AND name = N'PK__TMPPHIEU__C4971C3EC4DB953D') THROW 52301, N'Missing index/constraint dbo.TMPPHIEUGIAODBHD.PK__TMPPHIEU__C4971C3EC4DB953D', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT') AND name = N'PK__TMPPHIEU__3214EC273EFF485B') THROW 52301, N'Missing index/constraint dbo.TMPPHIEUGIAOHANGDBCT.PK__TMPPHIEU__3214EC273EFF485B', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name = N'FK_FVN_PhieuTraHangCT_Header') THROW 52302, N'Missing FK dbo.FVN_PhieuTraHangCT.FK_FVN_PhieuTraHangCT_Header', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT') AND name = N'FK_TMPPHIEUGIAOHANGDBCT_HD') THROW 52302, N'Missing FK dbo.TMPPHIEUGIAOHANGDBCT.FK_TMPPHIEUGIAOHANGDBCT_HD', 1;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'CK_FVN_PhieuXuLyBatThuong_Nguon') THROW 52303, N'Missing CHECK dbo.FVN_PhieuXuLyBatThuong.CK_FVN_PhieuXuLyBatThuong_Nguon', 1;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name = N'CK_FVN_PhieuXuLyBatThuong_SoLuongLoi') THROW 52303, N'Missing CHECK dbo.FVN_PhieuXuLyBatThuong.CK_FVN_PhieuXuLyBatThuong_SoLuongLoi', 1;
SELECT N'PASS: generated SQL2 keys/indexes/FKs/CHECK constraints verified' AS Result;
GO
