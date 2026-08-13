namespace PCTP
{
    partial class NHAP_TP
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
            DevExpress.XtraSplashScreen.SplashScreenManager splashScreenManager1 = new DevExpress.XtraSplashScreen.SplashScreenManager(this, null, true, true);
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NHAP_TP));
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.sidePanel4 = new DevExpress.XtraEditors.SidePanel();
            this.cmdEX = new DevExpress.XtraEditors.SimpleButton();
            this.CMD_REFESH = new System.Windows.Forms.Button();
            this.cmd_Tonkho = new System.Windows.Forms.Button();
            this.CMD_KTLOT = new System.Windows.Forms.Button();
            this.CMD_MOLOT = new System.Windows.Forms.Button();
            this.CMD_NHAPKHO = new System.Windows.Forms.Button();
            this.sidePanel3 = new DevExpress.XtraEditors.SidePanel();
            this.RDOLOAIHINHNHAP = new DevExpress.XtraEditors.RadioGroup();
            this.TXT_DOCQRCODE = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.sidePanel2 = new DevExpress.XtraEditors.SidePanel();
            this.gridCTRNHAPKHO = new DevExpress.XtraGrid.GridControl();
            this.gridVNHAPKHO = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.STT = new DevExpress.XtraGrid.Columns.GridColumn();
            this.LOTNO = new DevExpress.XtraGrid.Columns.GridColumn();
            this.MAHANG = new DevExpress.XtraGrid.Columns.GridColumn();
            this.TENHANG = new DevExpress.XtraGrid.Columns.GridColumn();
            this.Model = new DevExpress.XtraGrid.Columns.GridColumn();
            this.NGAYSX = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CaSX = new DevExpress.XtraGrid.Columns.GridColumn();
            this.SLSX = new DevExpress.XtraGrid.Columns.GridColumn();
            this.NHAYNHAP = new DevExpress.XtraGrid.Columns.GridColumn();
            this.SLNDANHAP = new DevExpress.XtraGrid.Columns.GridColumn();
            this.SLDATRA = new DevExpress.XtraGrid.Columns.GridColumn();
            this.LDNG = new DevExpress.XtraGrid.Columns.GridColumn();
            this.SLTONKHO = new DevExpress.XtraGrid.Columns.GridColumn();
            this.SLSENHAP = new DevExpress.XtraGrid.Columns.GridColumn();
            this.KTLOT = new DevExpress.XtraGrid.Columns.GridColumn();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            this.sidePanel1.SuspendLayout();
            this.sidePanel4.SuspendLayout();
            this.sidePanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RDOLOAIHINHNHAP.Properties)).BeginInit();
            this.sidePanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridCTRNHAPKHO)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVNHAPKHO)).BeginInit();
            this.SuspendLayout();
            // 
            // splashScreenManager1
            // 
            splashScreenManager1.ClosingDelay = 500;
            // 
            // sidePanel1
            // 
            this.sidePanel1.Controls.Add(this.sidePanel4);
            this.sidePanel1.Controls.Add(this.sidePanel3);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.sidePanel1.Location = new System.Drawing.Point(0, 0);
            this.sidePanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(1501, 126);
            this.sidePanel1.TabIndex = 0;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // sidePanel4
            // 
            this.sidePanel4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sidePanel4.Appearance.BackColor = System.Drawing.Color.Silver;
            this.sidePanel4.Appearance.Options.UseBackColor = true;
            this.sidePanel4.Controls.Add(this.simpleButton1);
            this.sidePanel4.Controls.Add(this.cmdEX);
            this.sidePanel4.Controls.Add(this.CMD_REFESH);
            this.sidePanel4.Controls.Add(this.cmd_Tonkho);
            this.sidePanel4.Controls.Add(this.CMD_KTLOT);
            this.sidePanel4.Controls.Add(this.CMD_MOLOT);
            this.sidePanel4.Controls.Add(this.CMD_NHAPKHO);
            this.sidePanel4.Location = new System.Drawing.Point(895, 0);
            this.sidePanel4.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.sidePanel4.Name = "sidePanel4";
            this.sidePanel4.Size = new System.Drawing.Size(605, 126);
            this.sidePanel4.TabIndex = 1;
            this.sidePanel4.Text = "sidePanel4";
            // 
            // cmdEX
            // 
            this.cmdEX.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("cmdEX.ImageOptions.Image")));
            this.cmdEX.Location = new System.Drawing.Point(465, 18);
            this.cmdEX.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.cmdEX.Name = "cmdEX";
            this.cmdEX.Size = new System.Drawing.Size(129, 46);
            this.cmdEX.TabIndex = 18;
            this.cmdEX.Text = "Xuất Excel";
            this.cmdEX.Click += new System.EventHandler(this.cmdEX_Click);
            // 
            // CMD_REFESH
            // 
            this.CMD_REFESH.Location = new System.Drawing.Point(7, 71);
            this.CMD_REFESH.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CMD_REFESH.Name = "CMD_REFESH";
            this.CMD_REFESH.Size = new System.Drawing.Size(141, 38);
            this.CMD_REFESH.TabIndex = 10;
            this.CMD_REFESH.Text = "LÀM MỚI DL";
            this.CMD_REFESH.UseVisualStyleBackColor = true;
            this.CMD_REFESH.Click += new System.EventHandler(this.CMD_REFESH_Click);
            // 
            // cmd_Tonkho
            // 
            this.cmd_Tonkho.Location = new System.Drawing.Point(350, 18);
            this.cmd_Tonkho.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.cmd_Tonkho.Name = "cmd_Tonkho";
            this.cmd_Tonkho.Size = new System.Drawing.Size(80, 91);
            this.cmd_Tonkho.TabIndex = 11;
            this.cmd_Tonkho.Text = "XEM TỒN KHO";
            this.cmd_Tonkho.UseVisualStyleBackColor = true;
            this.cmd_Tonkho.Click += new System.EventHandler(this.cmd_Tonkho_Click);
            // 
            // CMD_KTLOT
            // 
            this.CMD_KTLOT.Location = new System.Drawing.Point(195, 73);
            this.CMD_KTLOT.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CMD_KTLOT.Name = "CMD_KTLOT";
            this.CMD_KTLOT.Size = new System.Drawing.Size(141, 36);
            this.CMD_KTLOT.TabIndex = 11;
            this.CMD_KTLOT.Text = "KẾT THÚC LOT";
            this.CMD_KTLOT.UseVisualStyleBackColor = true;
            this.CMD_KTLOT.Click += new System.EventHandler(this.CMD_KTLOT_Click);
            // 
            // CMD_MOLOT
            // 
            this.CMD_MOLOT.BackColor = System.Drawing.Color.CornflowerBlue;
            this.CMD_MOLOT.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CMD_MOLOT.Location = new System.Drawing.Point(195, 15);
            this.CMD_MOLOT.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CMD_MOLOT.Name = "CMD_MOLOT";
            this.CMD_MOLOT.Size = new System.Drawing.Size(149, 43);
            this.CMD_MOLOT.TabIndex = 9;
            this.CMD_MOLOT.Text = "MỞ LẠI LOT";
            this.CMD_MOLOT.UseVisualStyleBackColor = false;
            this.CMD_MOLOT.Click += new System.EventHandler(this.CMD_MOLOT_Click);
            // 
            // CMD_NHAPKHO
            // 
            this.CMD_NHAPKHO.BackColor = System.Drawing.Color.PaleTurquoise;
            this.CMD_NHAPKHO.Font = new System.Drawing.Font("Tahoma", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CMD_NHAPKHO.Location = new System.Drawing.Point(7, 14);
            this.CMD_NHAPKHO.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CMD_NHAPKHO.Name = "CMD_NHAPKHO";
            this.CMD_NHAPKHO.Size = new System.Drawing.Size(141, 43);
            this.CMD_NHAPKHO.TabIndex = 8;
            this.CMD_NHAPKHO.Text = "NHẬP KHO";
            this.CMD_NHAPKHO.UseVisualStyleBackColor = false;
            this.CMD_NHAPKHO.Click += new System.EventHandler(this.CMD_NHAPKHO_Click);
            // 
            // sidePanel3
            // 
            this.sidePanel3.Controls.Add(this.RDOLOAIHINHNHAP);
            this.sidePanel3.Controls.Add(this.TXT_DOCQRCODE);
            this.sidePanel3.Controls.Add(this.label1);
            this.sidePanel3.Location = new System.Drawing.Point(0, 0);
            this.sidePanel3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.sidePanel3.Name = "sidePanel3";
            this.sidePanel3.Size = new System.Drawing.Size(898, 126);
            this.sidePanel3.TabIndex = 0;
            this.sidePanel3.Text = "sidePanel3";
            // 
            // RDOLOAIHINHNHAP
            // 
            this.RDOLOAIHINHNHAP.Location = new System.Drawing.Point(705, 7);
            this.RDOLOAIHINHNHAP.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.RDOLOAIHINHNHAP.Name = "RDOLOAIHINHNHAP";
            this.RDOLOAIHINHNHAP.Properties.Items.AddRange(new DevExpress.XtraEditors.Controls.RadioGroupItem[] {
            new DevExpress.XtraEditors.Controls.RadioGroupItem(null, "NHẬP MỚI", true, null, "N"),
            new DevExpress.XtraEditors.Controls.RadioGroupItem(null, "NHẠP LẠI NG", true, null, "NG")});
            this.RDOLOAIHINHNHAP.Size = new System.Drawing.Size(177, 108);
            this.RDOLOAIHINHNHAP.TabIndex = 4;
            // 
            // TXT_DOCQRCODE
            // 
            this.TXT_DOCQRCODE.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TXT_DOCQRCODE.Location = new System.Drawing.Point(205, 48);
            this.TXT_DOCQRCODE.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.TXT_DOCQRCODE.Name = "TXT_DOCQRCODE";
            this.TXT_DOCQRCODE.Size = new System.Drawing.Size(472, 30);
            this.TXT_DOCQRCODE.TabIndex = 3;
            this.TXT_DOCQRCODE.TextChanged += new System.EventHandler(this.TXT_DOCQRCODE_TextChanged);
            this.TXT_DOCQRCODE.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.TXT_DOCQRCODE_KeyPress);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 48);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(187, 29);
            this.label1.TabIndex = 2;
            this.label1.Text = "ĐỌC QRCODE";
            // 
            // sidePanel2
            // 
            this.sidePanel2.Controls.Add(this.gridCTRNHAPKHO);
            this.sidePanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidePanel2.Location = new System.Drawing.Point(0, 126);
            this.sidePanel2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.sidePanel2.Name = "sidePanel2";
            this.sidePanel2.Size = new System.Drawing.Size(1501, 544);
            this.sidePanel2.TabIndex = 1;
            this.sidePanel2.Text = "sidePanel2";
            // 
            // gridCTRNHAPKHO
            // 
            this.gridCTRNHAPKHO.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCTRNHAPKHO.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCTRNHAPKHO.Location = new System.Drawing.Point(0, 0);
            this.gridCTRNHAPKHO.MainView = this.gridVNHAPKHO;
            this.gridCTRNHAPKHO.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCTRNHAPKHO.Name = "gridCTRNHAPKHO";
            this.gridCTRNHAPKHO.Size = new System.Drawing.Size(1501, 544);
            this.gridCTRNHAPKHO.TabIndex = 0;
            this.gridCTRNHAPKHO.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridVNHAPKHO});
            // 
            // gridVNHAPKHO
            // 
            this.gridVNHAPKHO.Appearance.ColumnFilterButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(255)))));
            this.gridVNHAPKHO.Appearance.ColumnFilterButton.BackColor2 = System.Drawing.SystemColors.Info;
            this.gridVNHAPKHO.Appearance.ColumnFilterButton.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gridVNHAPKHO.Appearance.ColumnFilterButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(255)))));
            this.gridVNHAPKHO.Appearance.ColumnFilterButton.Options.UseBackColor = true;
            this.gridVNHAPKHO.Appearance.ColumnFilterButton.Options.UseFont = true;
            this.gridVNHAPKHO.Appearance.ColumnFilterButton.Options.UseForeColor = true;
            this.gridVNHAPKHO.Appearance.FocusedRow.BackColor = System.Drawing.Color.White;
            this.gridVNHAPKHO.Appearance.FocusedRow.Options.UseBackColor = true;
            this.gridVNHAPKHO.Appearance.HeaderPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.gridVNHAPKHO.Appearance.HeaderPanel.Font = new System.Drawing.Font("Times New Roman", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gridVNHAPKHO.Appearance.HeaderPanel.Options.UseBackColor = true;
            this.gridVNHAPKHO.Appearance.HeaderPanel.Options.UseFont = true;
            this.gridVNHAPKHO.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.STT,
            this.LOTNO,
            this.MAHANG,
            this.TENHANG,
            this.Model,
            this.NGAYSX,
            this.CaSX,
            this.SLSX,
            this.NHAYNHAP,
            this.SLNDANHAP,
            this.SLDATRA,
            this.LDNG,
            this.SLTONKHO,
            this.SLSENHAP,
            this.KTLOT});
            this.gridVNHAPKHO.GridControl = this.gridCTRNHAPKHO;
            this.gridVNHAPKHO.Name = "gridVNHAPKHO";
            this.gridVNHAPKHO.RowCellStyle += new DevExpress.XtraGrid.Views.Grid.RowCellStyleEventHandler(this.gridVNHAPKHO_RowCellStyle);
            // 
            // STT
            // 
            this.STT.Caption = "STT";
            this.STT.FieldName = "STT";
            this.STT.MinWidth = 24;
            this.STT.Name = "STT";
            this.STT.OptionsColumn.AllowEdit = false;
            this.STT.Visible = true;
            this.STT.VisibleIndex = 0;
            this.STT.Width = 49;
            // 
            // LOTNO
            // 
            this.LOTNO.Caption = "LOT NO";
            this.LOTNO.FieldName = "LOT_NO";
            this.LOTNO.MinWidth = 24;
            this.LOTNO.Name = "LOTNO";
            this.LOTNO.OptionsColumn.AllowEdit = false;
            this.LOTNO.Visible = true;
            this.LOTNO.VisibleIndex = 1;
            this.LOTNO.Width = 92;
            // 
            // MAHANG
            // 
            this.MAHANG.Caption = "Mã Hàng";
            this.MAHANG.FieldName = "MA_SAN_PHAM";
            this.MAHANG.MinWidth = 24;
            this.MAHANG.Name = "MAHANG";
            this.MAHANG.OptionsColumn.AllowEdit = false;
            this.MAHANG.Visible = true;
            this.MAHANG.VisibleIndex = 2;
            this.MAHANG.Width = 120;
            // 
            // TENHANG
            // 
            this.TENHANG.Caption = "Tên Hàng";
            this.TENHANG.FieldName = "TEN_SAN_PHAM";
            this.TENHANG.MinWidth = 24;
            this.TENHANG.Name = "TENHANG";
            this.TENHANG.OptionsColumn.AllowEdit = false;
            this.TENHANG.Visible = true;
            this.TENHANG.VisibleIndex = 3;
            this.TENHANG.Width = 89;
            // 
            // Model
            // 
            this.Model.Caption = "Model";
            this.Model.FieldName = "Model";
            this.Model.MinWidth = 24;
            this.Model.Name = "Model";
            this.Model.OptionsColumn.AllowEdit = false;
            this.Model.Visible = true;
            this.Model.VisibleIndex = 4;
            this.Model.Width = 55;
            // 
            // NGAYSX
            // 
            this.NGAYSX.Caption = "Ngày Sản Xuất";
            this.NGAYSX.FieldName = "NGAY_SAN_XUAT";
            this.NGAYSX.MinWidth = 24;
            this.NGAYSX.Name = "NGAYSX";
            this.NGAYSX.OptionsColumn.AllowEdit = false;
            this.NGAYSX.Visible = true;
            this.NGAYSX.VisibleIndex = 5;
            this.NGAYSX.Width = 89;
            // 
            // CaSX
            // 
            this.CaSX.Caption = "Ca Sản Xuất";
            this.CaSX.FieldName = "CA_SAN_XUAT";
            this.CaSX.MinWidth = 24;
            this.CaSX.Name = "CaSX";
            this.CaSX.OptionsColumn.AllowEdit = false;
            this.CaSX.Visible = true;
            this.CaSX.VisibleIndex = 6;
            this.CaSX.Width = 89;
            // 
            // SLSX
            // 
            this.SLSX.Caption = "SL Sản Xuất";
            this.SLSX.FieldName = "SL_DA_SAN_XUAT";
            this.SLSX.MinWidth = 24;
            this.SLSX.Name = "SLSX";
            this.SLSX.OptionsColumn.AllowEdit = false;
            this.SLSX.Visible = true;
            this.SLSX.VisibleIndex = 7;
            this.SLSX.Width = 89;
            // 
            // NHAYNHAP
            // 
            this.NHAYNHAP.Caption = "Ngày Nhập Kho Gần Nhất";
            this.NHAYNHAP.FieldName = "NGAY_NHAP";
            this.NHAYNHAP.MinWidth = 24;
            this.NHAYNHAP.Name = "NHAYNHAP";
            this.NHAYNHAP.OptionsColumn.AllowEdit = false;
            this.NHAYNHAP.Visible = true;
            this.NHAYNHAP.VisibleIndex = 8;
            this.NHAYNHAP.Width = 89;
            // 
            // SLNDANHAP
            // 
            this.SLNDANHAP.Caption = "SL Đã Nhập";
            this.SLNDANHAP.FieldName = "SL_DA_NHAP";
            this.SLNDANHAP.MinWidth = 24;
            this.SLNDANHAP.Name = "SLNDANHAP";
            this.SLNDANHAP.OptionsColumn.AllowEdit = false;
            this.SLNDANHAP.Visible = true;
            this.SLNDANHAP.VisibleIndex = 9;
            this.SLNDANHAP.Width = 89;
            // 
            // SLDATRA
            // 
            this.SLDATRA.Caption = "SL TRẢ NG";
            this.SLDATRA.FieldName = "SL_DA_TRA";
            this.SLDATRA.MinWidth = 24;
            this.SLDATRA.Name = "SLDATRA";
            this.SLDATRA.OptionsColumn.AllowEdit = false;
            this.SLDATRA.Visible = true;
            this.SLDATRA.VisibleIndex = 10;
            this.SLDATRA.Width = 89;
            // 
            // LDNG
            // 
            this.LDNG.Caption = "Lý Do NG";
            this.LDNG.FieldName = "LY_DO_TRA";
            this.LDNG.MinWidth = 24;
            this.LDNG.Name = "LDNG";
            this.LDNG.OptionsColumn.AllowEdit = false;
            this.LDNG.Visible = true;
            this.LDNG.VisibleIndex = 11;
            this.LDNG.Width = 89;
            // 
            // SLTONKHO
            // 
            this.SLTONKHO.Caption = "SL Tồn Kho";
            this.SLTONKHO.FieldName = "TON_KHO_TP";
            this.SLTONKHO.MinWidth = 24;
            this.SLTONKHO.Name = "SLTONKHO";
            this.SLTONKHO.OptionsColumn.AllowEdit = false;
            this.SLTONKHO.Visible = true;
            this.SLTONKHO.VisibleIndex = 12;
            this.SLTONKHO.Width = 89;
            // 
            // SLSENHAP
            // 
            this.SLSENHAP.Caption = "Số Lượng Sẽ Nhập";
            this.SLSENHAP.FieldName = "SL_SE_NHAP";
            this.SLSENHAP.MinWidth = 24;
            this.SLSENHAP.Name = "SLSENHAP";
            this.SLSENHAP.OptionsColumn.AllowEdit = false;
            this.SLSENHAP.Visible = true;
            this.SLSENHAP.VisibleIndex = 13;
            this.SLSENHAP.Width = 89;
            // 
            // KTLOT
            // 
            this.KTLOT.Caption = "Kết Thúc LOT";
            this.KTLOT.FieldName = "KET_THUC_LOT";
            this.KTLOT.MinWidth = 24;
            this.KTLOT.Name = "KTLOT";
            this.KTLOT.Visible = true;
            this.KTLOT.VisibleIndex = 14;
            this.KTLOT.Width = 101;
            // 
            // simpleButton1
            // 
            this.simpleButton1.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton1.ImageOptions.Image")));
            this.simpleButton1.Location = new System.Drawing.Point(465, 73);
            this.simpleButton1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(129, 46);
            this.simpleButton1.TabIndex = 18;
            this.simpleButton1.Text = "Ảnh Kiểm tra";
            this.simpleButton1.Click += new System.EventHandler(this.simpleButton1_Click);
            // 
            // NHAP_TP
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1501, 670);
            this.Controls.Add(this.sidePanel2);
            this.Controls.Add(this.sidePanel1);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "NHAP_TP";
            this.Text = "NHAP_TP";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.NHAP_TP_Load);
            this.sidePanel1.ResumeLayout(false);
            this.sidePanel4.ResumeLayout(false);
            this.sidePanel3.ResumeLayout(false);
            this.sidePanel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RDOLOAIHINHNHAP.Properties)).EndInit();
            this.sidePanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridCTRNHAPKHO)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVNHAPKHO)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SidePanel sidePanel1;
        private DevExpress.XtraEditors.SidePanel sidePanel4;
        private DevExpress.XtraEditors.SidePanel sidePanel3;
        private DevExpress.XtraEditors.SidePanel sidePanel2;
        private DevExpress.XtraEditors.RadioGroup RDOLOAIHINHNHAP;
        private System.Windows.Forms.TextBox TXT_DOCQRCODE;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button CMD_REFESH;
        private System.Windows.Forms.Button CMD_KTLOT;
        private System.Windows.Forms.Button CMD_MOLOT;
        private System.Windows.Forms.Button CMD_NHAPKHO;
        private DevExpress.XtraGrid.GridControl gridCTRNHAPKHO;
        private DevExpress.XtraGrid.Views.Grid.GridView gridVNHAPKHO;
        private DevExpress.XtraGrid.Columns.GridColumn STT;
        private DevExpress.XtraGrid.Columns.GridColumn LOTNO;
        private DevExpress.XtraGrid.Columns.GridColumn MAHANG;
        private DevExpress.XtraGrid.Columns.GridColumn TENHANG;
        private DevExpress.XtraGrid.Columns.GridColumn Model;
        private DevExpress.XtraGrid.Columns.GridColumn NGAYSX;
        private DevExpress.XtraGrid.Columns.GridColumn CaSX;
        private DevExpress.XtraGrid.Columns.GridColumn SLSX;
        private DevExpress.XtraGrid.Columns.GridColumn NHAYNHAP;
        private DevExpress.XtraGrid.Columns.GridColumn SLNDANHAP;
        private DevExpress.XtraGrid.Columns.GridColumn SLDATRA;
        private DevExpress.XtraGrid.Columns.GridColumn LDNG;
        private DevExpress.XtraGrid.Columns.GridColumn SLTONKHO;
        private DevExpress.XtraGrid.Columns.GridColumn SLSENHAP;
        private DevExpress.XtraGrid.Columns.GridColumn KTLOT;
        private System.Windows.Forms.Button cmd_Tonkho;
        private DevExpress.XtraEditors.SimpleButton cmdEX;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
    }
}