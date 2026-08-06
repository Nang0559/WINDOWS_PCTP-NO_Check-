
namespace PCTP.FuctionPrint
{
    partial class UF_GHEPLOT
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UF_GHEPLOT));
            this.txtDocQR = new DevExpress.XtraEditors.TextEdit();
            this.GCT_DOCQR = new DevExpress.XtraGrid.GridControl();
            this.GV_ReadQR = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.gridColumn12 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn1 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.ItemName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn9 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn4 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn11 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn10 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn2 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn3 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.SLGHEP = new DevExpress.XtraGrid.Columns.GridColumn();
            this.TT = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn13 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.bt_Print = new DevExpress.XtraEditors.SimpleButton();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            this.gridColumn5 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.LOTGT = new DevExpress.XtraGrid.Columns.GridColumn();
            this.GCT_GEPLOT = new DevExpress.XtraGrid.GridControl();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.gridColumn6 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn7 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.gridColumn8 = new DevExpress.XtraGrid.Columns.GridColumn();
            this.btGL = new DevExpress.XtraEditors.SimpleButton();
            this.pictureEdit1 = new DevExpress.XtraEditors.PictureEdit();
            this.pictureEdit2 = new DevExpress.XtraEditors.PictureEdit();
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.imageCollection1 = new DevExpress.Utils.ImageCollection(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.txtDocQR.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCT_DOCQR)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_ReadQR)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCT_GEPLOT)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureEdit1.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureEdit2.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection1)).BeginInit();
            this.SuspendLayout();
            // 
            // txtDocQR
            // 
            this.txtDocQR.Location = new System.Drawing.Point(455, 56);
            this.txtDocQR.Name = "txtDocQR";
            this.txtDocQR.Properties.Appearance.Font = new System.Drawing.Font("Times New Roman", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtDocQR.Properties.Appearance.Options.UseFont = true;
            this.txtDocQR.Size = new System.Drawing.Size(641, 32);
            this.txtDocQR.TabIndex = 0;
            this.txtDocQR.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtDocQR_KeyPress);
            // 
            // GCT_DOCQR
            // 
            this.GCT_DOCQR.Location = new System.Drawing.Point(39, 113);
            this.GCT_DOCQR.MainView = this.GV_ReadQR;
            this.GCT_DOCQR.Name = "GCT_DOCQR";
            this.GCT_DOCQR.Size = new System.Drawing.Size(901, 453);
            this.GCT_DOCQR.TabIndex = 1;
            this.GCT_DOCQR.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GV_ReadQR});
            // 
            // GV_ReadQR
            // 
            this.GV_ReadQR.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.gridColumn12,
            this.gridColumn1,
            this.ItemName,
            this.gridColumn9,
            this.gridColumn4,
            this.gridColumn11,
            this.gridColumn10,
            this.gridColumn2,
            this.gridColumn3,
            this.SLGHEP,
            this.TT,
            this.gridColumn13});
            this.GV_ReadQR.GridControl = this.GCT_DOCQR;
            this.GV_ReadQR.GroupSummary.AddRange(new DevExpress.XtraGrid.GridSummaryItem[] {
            new DevExpress.XtraGrid.GridGroupSummaryItem(DevExpress.Data.SummaryItemType.None, "PartNo", null, "")});
            this.GV_ReadQR.Name = "GV_ReadQR";
            this.GV_ReadQR.OptionsSelection.CheckBoxSelectorColumnWidth = 20;
            this.GV_ReadQR.OptionsSelection.MultiSelect = true;
            this.GV_ReadQR.CellMerge += new DevExpress.XtraGrid.Views.Grid.CellMergeEventHandler(this.GV_ReadQR_CellMerge);
            this.GV_ReadQR.RowStyle += new DevExpress.XtraGrid.Views.Grid.RowStyleEventHandler(this.GV_ReadQR_RowStyle);
            this.GV_ReadQR.PopupMenuShowing += new DevExpress.XtraGrid.Views.Grid.PopupMenuShowingEventHandler(this.GV_ReadQR_PopupMenuShowing);
            this.GV_ReadQR.ShowingEditor += new System.ComponentModel.CancelEventHandler(this.GV_ReadQR_ShowingEditor);
            this.GV_ReadQR.MouseDown += new System.Windows.Forms.MouseEventHandler(this.GV_ReadQR_MouseDown);
            this.GV_ReadQR.MouseUp += new System.Windows.Forms.MouseEventHandler(this.GV_ReadQR_MouseUp);
            this.GV_ReadQR.ValidatingEditor += new DevExpress.XtraEditors.Controls.BaseContainerValidateEditorEventHandler(this.GV_ReadQR_ValidatingEditor);
            // 
            // gridColumn12
            // 
            this.gridColumn12.Caption = "STT Bắn";
            this.gridColumn12.FieldName = "STT";
            this.gridColumn12.MinWidth = 25;
            this.gridColumn12.Name = "gridColumn12";
            this.gridColumn12.Visible = true;
            this.gridColumn12.VisibleIndex = 0;
            this.gridColumn12.Width = 70;
            // 
            // gridColumn1
            // 
            this.gridColumn1.Caption = "Mã Hàng";
            this.gridColumn1.FieldName = "ItemCode";
            this.gridColumn1.MinWidth = 25;
            this.gridColumn1.Name = "gridColumn1";
            this.gridColumn1.OptionsColumn.AllowGroup = DevExpress.Utils.DefaultBoolean.True;
            this.gridColumn1.OptionsColumn.AllowMerge = DevExpress.Utils.DefaultBoolean.True;
            this.gridColumn1.Visible = true;
            this.gridColumn1.VisibleIndex = 1;
            this.gridColumn1.Width = 94;
            // 
            // ItemName
            // 
            this.ItemName.Caption = "Tên Hàng";
            this.ItemName.FieldName = "ItemName";
            this.ItemName.MinWidth = 25;
            this.ItemName.Name = "ItemName";
            this.ItemName.Visible = true;
            this.ItemName.VisibleIndex = 2;
            this.ItemName.Width = 94;
            // 
            // gridColumn9
            // 
            this.gridColumn9.Caption = "Model";
            this.gridColumn9.FieldName = "Model";
            this.gridColumn9.MinWidth = 25;
            this.gridColumn9.Name = "gridColumn9";
            this.gridColumn9.Visible = true;
            this.gridColumn9.VisibleIndex = 3;
            this.gridColumn9.Width = 94;
            // 
            // gridColumn4
            // 
            this.gridColumn4.Caption = "Lot No";
            this.gridColumn4.FieldName = "ItemLotCode";
            this.gridColumn4.MinWidth = 25;
            this.gridColumn4.Name = "gridColumn4";
            this.gridColumn4.Visible = true;
            this.gridColumn4.VisibleIndex = 4;
            this.gridColumn4.Width = 94;
            // 
            // gridColumn11
            // 
            this.gridColumn11.Caption = "Ngày Sản Xuất";
            this.gridColumn11.FieldName = "DocDate";
            this.gridColumn11.MinWidth = 25;
            this.gridColumn11.Name = "gridColumn11";
            this.gridColumn11.Visible = true;
            this.gridColumn11.VisibleIndex = 5;
            this.gridColumn11.Width = 94;
            // 
            // gridColumn10
            // 
            this.gridColumn10.Caption = "Ca sản xuất";
            this.gridColumn10.FieldName = "ShiftCode";
            this.gridColumn10.MinWidth = 25;
            this.gridColumn10.Name = "gridColumn10";
            this.gridColumn10.Visible = true;
            this.gridColumn10.VisibleIndex = 6;
            this.gridColumn10.Width = 94;
            // 
            // gridColumn2
            // 
            this.gridColumn2.Caption = "Số Lượng TEM";
            this.gridColumn2.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.gridColumn2.FieldName = "Quantity9";
            this.gridColumn2.MinWidth = 25;
            this.gridColumn2.Name = "gridColumn2";
            this.gridColumn2.OptionsColumn.AllowMerge = DevExpress.Utils.DefaultBoolean.False;
            this.gridColumn2.Visible = true;
            this.gridColumn2.VisibleIndex = 7;
            this.gridColumn2.Width = 94;
            // 
            // gridColumn3
            // 
            this.gridColumn3.Caption = "Quy Cách DG";
            this.gridColumn3.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.gridColumn3.FieldName = "QCDG";
            this.gridColumn3.MinWidth = 25;
            this.gridColumn3.Name = "gridColumn3";
            this.gridColumn3.Visible = true;
            this.gridColumn3.VisibleIndex = 8;
            this.gridColumn3.Width = 94;
            // 
            // SLGHEP
            // 
            this.SLGHEP.Caption = "SL Ghép";
            this.SLGHEP.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.SLGHEP.FieldName = "SLG";
            this.SLGHEP.MinWidth = 25;
            this.SLGHEP.Name = "SLGHEP";
            this.SLGHEP.Visible = true;
            this.SLGHEP.VisibleIndex = 9;
            this.SLGHEP.Width = 79;
            // 
            // TT
            // 
            this.TT.Caption = "Trang Thai Tach";
            this.TT.FieldName = "State";
            this.TT.MinWidth = 25;
            this.TT.Name = "TT";
            this.TT.Visible = true;
            this.TT.VisibleIndex = 10;
            this.TT.Width = 94;
            // 
            // gridColumn13
            // 
            this.gridColumn13.Caption = "gridColumn13";
            this.gridColumn13.FieldName = "QRCODE";
            this.gridColumn13.MinWidth = 25;
            this.gridColumn13.Name = "gridColumn13";
            this.gridColumn13.Width = 94;
            // 
            // bt_Print
            // 
            this.bt_Print.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("bt_Print.ImageOptions.SvgImage")));
            this.bt_Print.Location = new System.Drawing.Point(648, 585);
            this.bt_Print.Name = "bt_Print";
            this.bt_Print.Size = new System.Drawing.Size(157, 37);
            this.bt_Print.TabIndex = 2;
            this.bt_Print.Text = "IN";
            this.bt_Print.Click += new System.EventHandler(this.bt_Print_Click);
            // 
            // simpleButton1
            // 
            this.simpleButton1.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("simpleButton1.ImageOptions.SvgImage")));
            this.simpleButton1.Location = new System.Drawing.Point(918, 585);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(148, 37);
            this.simpleButton1.TabIndex = 2;
            this.simpleButton1.Text = "HUY";
            this.simpleButton1.Click += new System.EventHandler(this.simpleButton1_Click);
            // 
            // gridColumn5
            // 
            this.gridColumn5.Caption = "Số Lượng Ghép";
            this.gridColumn5.FieldName = "SLGHEP";
            this.gridColumn5.MinWidth = 25;
            this.gridColumn5.Name = "gridColumn5";
            this.gridColumn5.OptionsColumn.AllowMerge = DevExpress.Utils.DefaultBoolean.False;
            this.gridColumn5.Width = 94;
            // 
            // LOTGT
            // 
            this.LOTGT.Caption = "LOT GHEP/LOT TACH";
            this.LOTGT.FieldName = "GHEPLOT";
            this.LOTGT.MinWidth = 25;
            this.LOTGT.Name = "LOTGT";
            this.LOTGT.Width = 94;
            // 
            // GCT_GEPLOT
            // 
            this.GCT_GEPLOT.Location = new System.Drawing.Point(946, 113);
            this.GCT_GEPLOT.MainView = this.gridView1;
            this.GCT_GEPLOT.Name = "GCT_GEPLOT";
            this.GCT_GEPLOT.Size = new System.Drawing.Size(584, 453);
            this.GCT_GEPLOT.TabIndex = 3;
            this.GCT_GEPLOT.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridView1});
            // 
            // gridView1
            // 
            this.gridView1.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.gridColumn6,
            this.gridColumn7,
            this.gridColumn8});
            this.gridView1.GridControl = this.GCT_GEPLOT;
            this.gridView1.GroupSummary.AddRange(new DevExpress.XtraGrid.GridSummaryItem[] {
            new DevExpress.XtraGrid.GridGroupSummaryItem(DevExpress.Data.SummaryItemType.None, "PartNo", null, "")});
            this.gridView1.Name = "gridView1";
            // 
            // gridColumn6
            // 
            this.gridColumn6.Caption = "Mã Hàng";
            this.gridColumn6.FieldName = "ItemCode";
            this.gridColumn6.MinWidth = 25;
            this.gridColumn6.Name = "gridColumn6";
            this.gridColumn6.OptionsColumn.AllowGroup = DevExpress.Utils.DefaultBoolean.True;
            this.gridColumn6.OptionsColumn.AllowMerge = DevExpress.Utils.DefaultBoolean.True;
            this.gridColumn6.Visible = true;
            this.gridColumn6.VisibleIndex = 0;
            this.gridColumn6.Width = 94;
            // 
            // gridColumn7
            // 
            this.gridColumn7.Caption = "Lot No";
            this.gridColumn7.FieldName = "ItemLotCode";
            this.gridColumn7.MinWidth = 25;
            this.gridColumn7.Name = "gridColumn7";
            this.gridColumn7.Visible = true;
            this.gridColumn7.VisibleIndex = 1;
            this.gridColumn7.Width = 94;
            // 
            // gridColumn8
            // 
            this.gridColumn8.Caption = "Số Lượng TEM";
            this.gridColumn8.FieldName = "Quantity9";
            this.gridColumn8.MinWidth = 25;
            this.gridColumn8.Name = "gridColumn8";
            this.gridColumn8.OptionsColumn.AllowMerge = DevExpress.Utils.DefaultBoolean.False;
            this.gridColumn8.Visible = true;
            this.gridColumn8.VisibleIndex = 2;
            this.gridColumn8.Width = 94;
            // 
            // btGL
            // 
            this.btGL.Location = new System.Drawing.Point(518, 116);
            this.btGL.Name = "btGL";
            this.btGL.Size = new System.Drawing.Size(121, 38);
            this.btGL.TabIndex = 4;
            this.btGL.Text = "Ghep Lot";
            this.btGL.Click += new System.EventHandler(this.btGL_Click);
            // 
            // pictureEdit1
            // 
            this.pictureEdit1.EditValue = ((object)(resources.GetObject("pictureEdit1.EditValue")));
            this.pictureEdit1.Location = new System.Drawing.Point(258, 54);
            this.pictureEdit1.Name = "pictureEdit1";
            this.pictureEdit1.Properties.ShowCameraMenuItem = DevExpress.XtraEditors.Controls.CameraMenuItemVisibility.Auto;
            this.pictureEdit1.Properties.SizeMode = DevExpress.XtraEditors.Controls.PictureSizeMode.Stretch;
            this.pictureEdit1.Size = new System.Drawing.Size(111, 34);
            this.pictureEdit1.TabIndex = 5;
            // 
            // pictureEdit2
            // 
            this.pictureEdit2.EditValue = ((object)(resources.GetObject("pictureEdit2.EditValue")));
            this.pictureEdit2.Location = new System.Drawing.Point(384, 54);
            this.pictureEdit2.Name = "pictureEdit2";
            this.pictureEdit2.Properties.ShowCameraMenuItem = DevExpress.XtraEditors.Controls.CameraMenuItemVisibility.Auto;
            this.pictureEdit2.Size = new System.Drawing.Size(41, 33);
            this.pictureEdit2.TabIndex = 6;
            // 
            // labelControl1
            // 
            this.labelControl1.Appearance.Font = new System.Drawing.Font("UD Digi Kyokasho N-B", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.labelControl1.Appearance.Options.UseFont = true;
            this.labelControl1.Location = new System.Drawing.Point(531, 0);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(398, 46);
            this.labelControl1.TabIndex = 7;
            this.labelControl1.Text = "CHÉP LOT THÀNH PHẨM";
            // 
            // imageCollection1
            // 
            this.imageCollection1.ImageStream = ((DevExpress.Utils.ImageCollectionStreamer)(resources.GetObject("imageCollection1.ImageStream")));
            this.imageCollection1.Images.SetKeyName(0, "reset_32x32.png");
            // 
            // UF_GHEPLOT
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1555, 634);
            this.Controls.Add(this.labelControl1);
            this.Controls.Add(this.pictureEdit2);
            this.Controls.Add(this.pictureEdit1);
            this.Controls.Add(this.btGL);
            this.Controls.Add(this.GCT_GEPLOT);
            this.Controls.Add(this.simpleButton1);
            this.Controls.Add(this.bt_Print);
            this.Controls.Add(this.GCT_DOCQR);
            this.Controls.Add(this.txtDocQR);
            this.Name = "UF_GHEPLOT";
            this.Text = "GHEP LOT";
            ((System.ComponentModel.ISupportInitialize)(this.txtDocQR.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCT_DOCQR)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_ReadQR)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCT_GEPLOT)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureEdit1.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureEdit2.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.TextEdit txtDocQR;
        private DevExpress.XtraGrid.GridControl GCT_DOCQR;
        private DevExpress.XtraGrid.Views.Grid.GridView GV_ReadQR;
        private DevExpress.XtraEditors.SimpleButton bt_Print;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn1;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn4;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn2;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn3;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn5;
        private DevExpress.XtraGrid.Columns.GridColumn LOTGT;
        private DevExpress.XtraGrid.GridControl GCT_GEPLOT;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn6;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn7;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn8;
        private DevExpress.XtraEditors.SimpleButton btGL;
        private DevExpress.XtraGrid.Columns.GridColumn SLGHEP;
        private DevExpress.XtraGrid.Columns.GridColumn ItemName;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn9;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn11;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn10;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn12;
        private DevExpress.XtraEditors.PictureEdit pictureEdit1;
        private DevExpress.XtraEditors.PictureEdit pictureEdit2;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraGrid.Columns.GridColumn TT;
        private DevExpress.XtraGrid.Columns.GridColumn gridColumn13;
        private DevExpress.Utils.ImageCollection imageCollection1;
    }
}