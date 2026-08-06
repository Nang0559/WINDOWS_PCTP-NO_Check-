
namespace PCTP.QRCODE_HVN.NhanLaiNG.NhanLaiTP
{
    partial class frmNhanLaiTP
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmNhanLaiTP));
            this.label1 = new System.Windows.Forms.Label();
            this.lupKhungGioGiao = new DevExpress.XtraEditors.LookUpEdit();
            this.label2 = new System.Windows.Forms.Label();
            this.pNhaMay = new System.Windows.Forms.Panel();
            this.pChon = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.dtNgayGiao = new DevExpress.XtraEditors.DateEdit();
            this.panel1 = new System.Windows.Forms.Panel();
            this.textEdit1 = new DevExpress.XtraEditors.MemoExEdit();
            this.label3 = new System.Windows.Forms.Label();
            this.radioGroup1 = new DevExpress.XtraEditors.RadioGroup();
            this.panel2 = new System.Windows.Forms.Panel();
            this.cmdTK = new DevExpress.XtraEditors.SimpleButton();
            this.label4 = new System.Windows.Forms.Label();
            this.cmdNhanLai = new DevExpress.XtraEditors.SimpleButton();
            this.cmdCancel = new DevExpress.XtraEditors.SimpleButton();
            this.gridCtrDONHANG = new DevExpress.XtraGrid.GridControl();
            this.GridViewDONHANG = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
            this.gridBandDH = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            this.STT = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.GIO = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.cua = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.truyen = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.mahang = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.tenhang = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.LOT = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.dovi = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.soluong = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.xe = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.Hop = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.STATUSDOC = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.STATUSCNK = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn4 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            ((System.ComponentModel.ISupportInitialize)(this.lupKhungGioGiao.Properties)).BeginInit();
            this.pNhaMay.SuspendLayout();
            this.pChon.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dtNgayGiao.Properties.CalendarTimeProperties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dtNgayGiao.Properties)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.textEdit1.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radioGroup1.Properties)).BeginInit();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrDONHANG)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridViewDONHANG)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 17);
            this.label1.TabIndex = 1;
            this.label1.Text = "Ngày Giao";
            // 
            // lupKhungGioGiao
            // 
            this.lupKhungGioGiao.Location = new System.Drawing.Point(441, 48);
            this.lupKhungGioGiao.Name = "lupKhungGioGiao";
            this.lupKhungGioGiao.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lupKhungGioGiao.Size = new System.Drawing.Size(119, 22);
            this.lupKhungGioGiao.TabIndex = 0;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(304, 53);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(101, 17);
            this.label2.TabIndex = 1;
            this.label2.Text = "Khung Giờ Giao";
            // 
            // pNhaMay
            // 
            this.pNhaMay.BackColor = System.Drawing.Color.Silver;
            this.pNhaMay.Controls.Add(this.radioGroup1);
            this.pNhaMay.Location = new System.Drawing.Point(12, 12);
            this.pNhaMay.Name = "pNhaMay";
            this.pNhaMay.Size = new System.Drawing.Size(258, 116);
            this.pNhaMay.TabIndex = 2;
            // 
            // pChon
            // 
            this.pChon.BackColor = System.Drawing.Color.Silver;
            this.pChon.Controls.Add(this.dtNgayGiao);
            this.pChon.Controls.Add(this.lupKhungGioGiao);
            this.pChon.Controls.Add(this.label1);
            this.pChon.Controls.Add(this.label2);
            this.pChon.Location = new System.Drawing.Point(320, 12);
            this.pChon.Name = "pChon";
            this.pChon.Size = new System.Drawing.Size(573, 116);
            this.pChon.TabIndex = 2;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.gridCtrDONHANG);
            this.panel3.Location = new System.Drawing.Point(12, 134);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1577, 587);
            this.panel3.TabIndex = 2;
            // 
            // dtNgayGiao
            // 
            this.dtNgayGiao.EditValue = null;
            this.dtNgayGiao.Location = new System.Drawing.Point(91, 48);
            this.dtNgayGiao.Name = "dtNgayGiao";
            this.dtNgayGiao.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dtNgayGiao.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dtNgayGiao.Size = new System.Drawing.Size(180, 22);
            this.dtNgayGiao.TabIndex = 2;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Silver;
            this.panel1.Controls.Add(this.textEdit1);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Location = new System.Drawing.Point(945, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(258, 116);
            this.panel1.TabIndex = 2;
            // 
            // textEdit1
            // 
            this.textEdit1.Location = new System.Drawing.Point(16, 37);
            this.textEdit1.Name = "textEdit1";
            this.textEdit1.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.textEdit1.Properties.ScrollBars = System.Windows.Forms.ScrollBars.Horizontal;
            this.textEdit1.Size = new System.Drawing.Size(228, 22);
            this.textEdit1.TabIndex = 0;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 17);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(69, 17);
            this.label3.TabIndex = 1;
            this.label3.Text = "Lý Do Trả";
            // 
            // radioGroup1
            // 
            this.radioGroup1.Location = new System.Drawing.Point(20, 21);
            this.radioGroup1.Name = "radioGroup1";
            this.radioGroup1.Properties.Items.AddRange(new DevExpress.XtraEditors.Controls.RadioGroupItem[] {
            new DevExpress.XtraEditors.Controls.RadioGroupItem(null, "HVN Vĩnh Phúc", true, null, "rVP"),
            new DevExpress.XtraEditors.Controls.RadioGroupItem(null, "HVN Hà Nam", true, null, "rHN")});
            this.radioGroup1.Size = new System.Drawing.Size(204, 79);
            this.radioGroup1.TabIndex = 0;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.Silver;
            this.panel2.Controls.Add(this.label4);
            this.panel2.Controls.Add(this.cmdCancel);
            this.panel2.Controls.Add(this.cmdNhanLai);
            this.panel2.Controls.Add(this.cmdTK);
            this.panel2.Location = new System.Drawing.Point(1253, 12);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(336, 116);
            this.panel2.TabIndex = 2;
            // 
            // cmdTK
            // 
            this.cmdTK.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton1.ImageOptions.Image")));
            this.cmdTK.Location = new System.Drawing.Point(110, 17);
            this.cmdTK.Name = "cmdTK";
            this.cmdTK.Size = new System.Drawing.Size(111, 32);
            this.cmdTK.TabIndex = 0;
            this.cmdTK.Text = "Tìm Kiếm";
            // 
            // label4
            // 
            this.label4.BackColor = System.Drawing.Color.Black;
            this.label4.Location = new System.Drawing.Point(24, 54);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(273, 3);
            this.label4.TabIndex = 1;
            this.label4.Text = "fa";
            // 
            // cmdNhanLai
            // 
            this.cmdNhanLai.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton2.ImageOptions.Image")));
            this.cmdNhanLai.Location = new System.Drawing.Point(27, 68);
            this.cmdNhanLai.Name = "cmdNhanLai";
            this.cmdNhanLai.Size = new System.Drawing.Size(111, 32);
            this.cmdNhanLai.TabIndex = 0;
            this.cmdNhanLai.Text = "Nhận Lại";
            // 
            // cmdCancel
            // 
            this.cmdCancel.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton3.ImageOptions.Image")));
            this.cmdCancel.Location = new System.Drawing.Point(186, 68);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(111, 32);
            this.cmdCancel.TabIndex = 0;
            this.cmdCancel.Text = "Cancel";
            // 
            // gridCtrDONHANG
            // 
            this.gridCtrDONHANG.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCtrDONHANG.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrDONHANG.Location = new System.Drawing.Point(0, 0);
            this.gridCtrDONHANG.MainView = this.GridViewDONHANG;
            this.gridCtrDONHANG.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrDONHANG.Name = "gridCtrDONHANG";
            this.gridCtrDONHANG.Size = new System.Drawing.Size(1577, 587);
            this.gridCtrDONHANG.TabIndex = 5;
            this.gridCtrDONHANG.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GridViewDONHANG});
            // 
            // GridViewDONHANG
            // 
            this.GridViewDONHANG.Appearance.ColumnFilterButton.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Bold);
            this.GridViewDONHANG.Appearance.ColumnFilterButton.Options.UseFont = true;
            this.GridViewDONHANG.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.gridBandDH});
            this.GridViewDONHANG.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] {
            this.STT,
            this.GIO,
            this.cua,
            this.truyen,
            this.mahang,
            this.tenhang,
            this.LOT,
            this.dovi,
            this.soluong,
            this.xe,
            this.Hop,
            this.STATUSDOC,
            this.STATUSCNK,
            this.bandedGridColumn4});
            this.GridViewDONHANG.GridControl = this.gridCtrDONHANG;
            this.GridViewDONHANG.Name = "GridViewDONHANG";
            this.GridViewDONHANG.OptionsFilter.AllowMultiSelectInCheckedFilterPopup = false;
            this.GridViewDONHANG.OptionsFilter.ShowAllTableValuesInCheckedFilterPopup = false;
            this.GridViewDONHANG.OptionsSelection.CheckBoxSelectorColumnWidth = 24;
            this.GridViewDONHANG.OptionsSelection.CheckBoxSelectorField = "CHON";
            this.GridViewDONHANG.OptionsSelection.MultiSelect = true;
            this.GridViewDONHANG.OptionsSelection.ShowCheckBoxSelectorInColumnHeader = DevExpress.Utils.DefaultBoolean.False;
            // 
            // gridBandDH
            // 
            this.gridBandDH.AppearanceHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.gridBandDH.AppearanceHeader.BorderColor = System.Drawing.Color.Red;
            this.gridBandDH.AppearanceHeader.Font = new System.Drawing.Font("Times New Roman", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gridBandDH.AppearanceHeader.Options.UseBackColor = true;
            this.gridBandDH.AppearanceHeader.Options.UseBorderColor = true;
            this.gridBandDH.AppearanceHeader.Options.UseFont = true;
            this.gridBandDH.AppearanceHeader.Options.UseTextOptions = true;
            this.gridBandDH.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.gridBandDH.AppearanceHeader.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            this.gridBandDH.Caption = "gridBand3";
            this.gridBandDH.Columns.Add(this.STT);
            this.gridBandDH.Columns.Add(this.GIO);
            this.gridBandDH.Columns.Add(this.cua);
            this.gridBandDH.Columns.Add(this.truyen);
            this.gridBandDH.Columns.Add(this.mahang);
            this.gridBandDH.Columns.Add(this.tenhang);
            this.gridBandDH.Columns.Add(this.LOT);
            this.gridBandDH.Columns.Add(this.dovi);
            this.gridBandDH.Columns.Add(this.soluong);
            this.gridBandDH.Columns.Add(this.xe);
            this.gridBandDH.Columns.Add(this.Hop);
            this.gridBandDH.Columns.Add(this.STATUSDOC);
            this.gridBandDH.Columns.Add(this.STATUSCNK);
            this.gridBandDH.Columns.Add(this.bandedGridColumn4);
            this.gridBandDH.MinWidth = 12;
            this.gridBandDH.Name = "gridBandDH";
            this.gridBandDH.RowCount = 2;
            this.gridBandDH.VisibleIndex = 0;
            this.gridBandDH.Width = 1173;
            // 
            // STT
            // 
            this.STT.Caption = "GSTT";
            this.STT.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.STT.FieldName = "STT";
            this.STT.MinWidth = 12;
            this.STT.Name = "STT";
            this.STT.Visible = true;
            this.STT.Width = 34;
            // 
            // GIO
            // 
            this.GIO.Caption = "Giờ";
            this.GIO.DisplayFormat.FormatString = "HH";
            this.GIO.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            this.GIO.FieldName = "GIOGIAO";
            this.GIO.MinWidth = 24;
            this.GIO.Name = "GIO";
            this.GIO.Visible = true;
            this.GIO.Width = 90;
            // 
            // cua
            // 
            this.cua.Caption = "Cửa";
            this.cua.FieldName = "CUA";
            this.cua.MinWidth = 24;
            this.cua.Name = "cua";
            this.cua.Visible = true;
            this.cua.Width = 90;
            // 
            // truyen
            // 
            this.truyen.Caption = "Truyền";
            this.truyen.FieldName = "TRUYEN";
            this.truyen.MinWidth = 24;
            this.truyen.Name = "truyen";
            this.truyen.Visible = true;
            this.truyen.Width = 90;
            // 
            // mahang
            // 
            this.mahang.Caption = "Mã hàng";
            this.mahang.FieldName = "MAHANG";
            this.mahang.MinWidth = 24;
            this.mahang.Name = "mahang";
            this.mahang.Visible = true;
            this.mahang.Width = 90;
            // 
            // tenhang
            // 
            this.tenhang.Caption = "Tên Hàng";
            this.tenhang.FieldName = "TENHANG";
            this.tenhang.MinWidth = 24;
            this.tenhang.Name = "tenhang";
            this.tenhang.Visible = true;
            this.tenhang.Width = 90;
            // 
            // LOT
            // 
            this.LOT.Caption = "Số Lô";
            this.LOT.FieldName = "LOT";
            this.LOT.MinWidth = 24;
            this.LOT.Name = "LOT";
            this.LOT.Visible = true;
            this.LOT.Width = 90;
            // 
            // dovi
            // 
            this.dovi.Caption = "Đơn Vị";
            this.dovi.FieldName = "DV";
            this.dovi.MinWidth = 24;
            this.dovi.Name = "dovi";
            this.dovi.Visible = true;
            this.dovi.Width = 90;
            // 
            // soluong
            // 
            this.soluong.Caption = "Số Lượng";
            this.soluong.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.soluong.FieldName = "SOLUONG";
            this.soluong.MinWidth = 24;
            this.soluong.Name = "soluong";
            this.soluong.Visible = true;
            this.soluong.Width = 90;
            // 
            // xe
            // 
            this.xe.Caption = "Xe";
            this.xe.MinWidth = 24;
            this.xe.Name = "xe";
            this.xe.Visible = true;
            this.xe.Width = 79;
            // 
            // Hop
            // 
            this.Hop.Caption = "Hộp";
            this.Hop.DisplayFormat.FormatString = "n";
            this.Hop.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.Hop.FieldName = "HOP";
            this.Hop.MinWidth = 24;
            this.Hop.Name = "Hop";
            this.Hop.Visible = true;
            this.Hop.Width = 89;
            // 
            // STATUSDOC
            // 
            this.STATUSDOC.AppearanceCell.Options.UseTextOptions = true;
            this.STATUSDOC.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.STATUSDOC.AppearanceCell.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            this.STATUSDOC.Caption = "Kết Quả Đọc QRcode";
            this.STATUSDOC.FieldName = "STATUSDOC";
            this.STATUSDOC.MinWidth = 24;
            this.STATUSDOC.Name = "STATUSDOC";
            this.STATUSDOC.Visible = true;
            this.STATUSDOC.Width = 64;
            // 
            // STATUSCNK
            // 
            this.STATUSCNK.AppearanceCell.Options.UseTextOptions = true;
            this.STATUSCNK.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.STATUSCNK.AppearanceCell.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            this.STATUSCNK.Caption = "Trạng Thái Cập Nhập Kho";
            this.STATUSCNK.FieldName = "STATUS";
            this.STATUSCNK.MinWidth = 12;
            this.STATUSCNK.Name = "STATUSCNK";
            this.STATUSCNK.Visible = true;
            this.STATUSCNK.Width = 117;
            // 
            // bandedGridColumn4
            // 
            this.bandedGridColumn4.Caption = "Ghi Chú";
            this.bandedGridColumn4.FieldName = "Note";
            this.bandedGridColumn4.MinWidth = 29;
            this.bandedGridColumn4.Name = "bandedGridColumn4";
            this.bandedGridColumn4.Visible = true;
            this.bandedGridColumn4.Width = 70;
            // 
            // frmNhanLaiTP
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1601, 745);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.pChon);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.pNhaMay);
            this.Name = "frmNhanLaiTP";
            this.Text = "Nhận Lại Thành Phẩm";
            ((System.ComponentModel.ISupportInitialize)(this.lupKhungGioGiao.Properties)).EndInit();
            this.pNhaMay.ResumeLayout(false);
            this.pChon.ResumeLayout(false);
            this.pChon.PerformLayout();
            this.panel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dtNgayGiao.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dtNgayGiao.Properties)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.textEdit1.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radioGroup1.Properties)).EndInit();
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrDONHANG)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridViewDONHANG)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private DevExpress.XtraEditors.LookUpEdit lupKhungGioGiao;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel pNhaMay;
        private DevExpress.XtraEditors.RadioGroup radioGroup1;
        private System.Windows.Forms.Panel pChon;
        private DevExpress.XtraEditors.DateEdit dtNgayGiao;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Panel panel1;
        private DevExpress.XtraEditors.MemoExEdit textEdit1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label4;
        private DevExpress.XtraEditors.SimpleButton cmdCancel;
        private DevExpress.XtraEditors.SimpleButton cmdNhanLai;
        private DevExpress.XtraEditors.SimpleButton cmdTK;
        private DevExpress.XtraGrid.GridControl gridCtrDONHANG;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridView GridViewDONHANG;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBandDH;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn STT;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GIO;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn cua;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn truyen;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn mahang;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn tenhang;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn LOT;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn dovi;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn soluong;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn xe;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn Hop;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn STATUSDOC;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn STATUSCNK;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn4;
    }
}