/* AUTO-GENERATED from SQL2 metadata. Review before use.
   Apply only to disposable PCTP_QC_CI, after 02_Generated_From_DB57.sql.
   Modules, data, permissions and external-table DDL are not generated. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.B20Gear') AND name=N'PK_GearList')
ALTER TABLE dbo.[B20Gear] ADD CONSTRAINT [PK_GearList] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.B20Item') AND name=N'IX_B20Item_BarCode')
CREATE NONCLUSTERED INDEX [IX_B20Item_BarCode] ON dbo.[B20Item] ([BarCode] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.B20Item') AND name=N'IX_B20Item_Code')
ALTER TABLE dbo.[B20Item] ADD CONSTRAINT [IX_B20Item_Code] UNIQUE CLUSTERED ([Code] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.B20Item') AND name=N'PK_B20Item')
ALTER TABLE dbo.[B20Item] ADD CONSTRAINT [PK_B20Item] PRIMARY KEY NONCLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_HangChoGiao') AND name=N'PK__FVN_Hang__3214EC07E6905088')
ALTER TABLE dbo.[FVN_HangChoGiao] ADD CONSTRAINT [PK__FVN_Hang__3214EC07E6905088] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_ItemFifoConfig') AND name=N'PK__FVN_Item__3ECC0FEBBDA5DBF4')
ALTER TABLE dbo.[FVN_ItemFifoConfig] ADD CONSTRAINT [PK__FVN_Item__3ECC0FEBBDA5DBF4] PRIMARY KEY CLUSTERED ([ItemCode] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History') AND name=N'IX_FifoHistory_ItemCode')
CREATE NONCLUSTERED INDEX [IX_FifoHistory_ItemCode] ON dbo.[FVN_ItemFifoConfig_History] ([ItemCode] ASC, [ThoiGian] DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_ItemFifoConfig_History') AND name=N'PK__FVN_Item__3214EC072BCF3DCA')
ALTER TABLE dbo.[FVN_ItemFifoConfig_History] ADD CONSTRAINT [PK__FVN_Item__3214EC072BCF3DCA] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_PhieuTraHang') AND name=N'PK_FVN_PhieuTraHang')
ALTER TABLE dbo.[FVN_PhieuTraHang] ADD CONSTRAINT [PK_FVN_PhieuTraHang] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name=N'IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId')
CREATE NONCLUSTERED INDEX [IX_FVN_PhieuTraHangCT_PhieuXuLyBatThuongId] ON dbo.[FVN_PhieuTraHangCT] ([PhieuXuLyBatThuongId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name=N'PK_FVN_PhieuTraHangCT')
ALTER TABLE dbo.[FVN_PhieuTraHangCT] ADD CONSTRAINT [PK_FVN_PhieuTraHangCT] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name=N'IX_FVN_PhieuXuLy_Header_Status_QC_FINAL')
CREATE NONCLUSTERED INDEX [IX_FVN_PhieuXuLy_Header_Status_QC_FINAL] ON dbo.[FVN_PhieuXuLyBatThuong] ([PhieuTraHangId] ASC, [Status] ASC) INCLUDE ([Id], [PhieuTraHangCTId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name=N'PK_FVN_PhieuXuLyBatThuong')
ALTER TABLE dbo.[FVN_PhieuXuLyBatThuong] ADD CONSTRAINT [PK_FVN_PhieuXuLyBatThuong] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name=N'UQ_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId')
ALTER TABLE dbo.[FVN_PhieuXuLyBatThuong] ADD CONSTRAINT [UQ_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId] UNIQUE NONCLUSTERED ([PhieuTraHangCTId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name=N'UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId')
CREATE UNIQUE NONCLUSTERED INDEX [UX_FVN_PhieuXuLyBatThuong_PhieuTraHangCTId] ON dbo.[FVN_PhieuXuLyBatThuong] ([PhieuTraHangCTId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.InspectionConfig') AND name=N'PK__Inspecti__C3BC335CB9A2E9A7')
ALTER TABLE dbo.[InspectionConfig] ADD CONSTRAINT [PK__Inspecti__C3BC335CB9A2E9A7] PRIMARY KEY CLUSTERED ([ConfigId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.InspectionConfig') AND name=N'UQ__Inspecti__3ECC0FEABAE82892')
ALTER TABLE dbo.[InspectionConfig] ADD CONSTRAINT [UQ__Inspecti__3ECC0FEABAE82892] UNIQUE NONCLUSTERED ([ItemCode] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.InspectionLog') AND name=N'PK__Inspecti__5E54864800C0C605')
ALTER TABLE dbo.[InspectionLog] ADD CONSTRAINT [PK__Inspecti__5E54864800C0C605] PRIMARY KEY CLUSTERED ([LogId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.StockHistory') AND name=N'PK__StockHis__3214EC0738B89F8A')
ALTER TABLE dbo.[StockHistory] ADD CONSTRAINT [PK__StockHis__3214EC0738B89F8A] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.sys_WorkflowTransitions') AND name=N'PK__sys_Work__3214EC078ACC2616')
ALTER TABLE dbo.[sys_WorkflowTransitions] ADD CONSTRAINT [PK__sys_Work__3214EC078ACC2616] PRIMARY KEY CLUSTERED ([Id] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.sys_WorkflowTransitions') AND name=N'UK_Transition')
ALTER TABLE dbo.[sys_WorkflowTransitions] ADD CONSTRAINT [UK_Transition] UNIQUE NONCLUSTERED ([ProcessCode] ASC, [FromStatus] ASC, [ToStatus] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.TMPPHIEUGIAODBHD') AND name=N'PK__TMPPHIEU__C4971C3EC4DB953D')
ALTER TABLE dbo.[TMPPHIEUGIAODBHD] ADD CONSTRAINT [PK__TMPPHIEU__C4971C3EC4DB953D] PRIMARY KEY CLUSTERED ([IDP] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT') AND name=N'PK__TMPPHIEU__3214EC273EFF485B')
ALTER TABLE dbo.[TMPPHIEUGIAOHANGDBCT] ADD CONSTRAINT [PK__TMPPHIEU__3214EC273EFF485B] PRIMARY KEY CLUSTERED ([ID] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name=N'CK_FVN_PhieuXuLyBatThuong_Nguon')
ALTER TABLE dbo.[FVN_PhieuXuLyBatThuong] WITH CHECK ADD CONSTRAINT [CK_FVN_PhieuXuLyBatThuong_Nguon] CHECK ([Nguon]=(2) OR [Nguon]=(1));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_PhieuXuLyBatThuong') AND name=N'CK_FVN_PhieuXuLyBatThuong_SoLuongLoi')
ALTER TABLE dbo.[FVN_PhieuXuLyBatThuong] WITH CHECK ADD CONSTRAINT [CK_FVN_PhieuXuLyBatThuong_SoLuongLoi] CHECK ([SoLuongLoi]>=(0));
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.FVN_PhieuTraHangCT') AND name=N'FK_FVN_PhieuTraHangCT_Header')
ALTER TABLE dbo.[FVN_PhieuTraHangCT] WITH CHECK ADD CONSTRAINT [FK_FVN_PhieuTraHangCT_Header] FOREIGN KEY ([PhieuTraHangId]) REFERENCES [dbo].[FVN_PhieuTraHang] ([Id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.TMPPHIEUGIAOHANGDBCT') AND name=N'FK_TMPPHIEUGIAOHANGDBCT_HD')
ALTER TABLE dbo.[TMPPHIEUGIAOHANGDBCT] WITH CHECK ADD CONSTRAINT [FK_TMPPHIEUGIAOHANGDBCT_HD] FOREIGN KEY ([IDP]) REFERENCES [dbo].[TMPPHIEUGIAODBHD] ([IDP]);
GO
