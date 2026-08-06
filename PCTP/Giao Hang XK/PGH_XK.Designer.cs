namespace PCTP.Giao_Hang_XK
{
    partial class PGH_XK
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PGH_XK));
            this.panel1 = new System.Windows.Forms.Panel();
            this.sidePanel4 = new DevExpress.XtraEditors.SidePanel();
            this.groupControl1 = new DevExpress.XtraEditors.GroupControl();
            this.RChonTK = new DevExpress.XtraEditors.RadioGroup();
            this.sidePanel3 = new DevExpress.XtraEditors.SidePanel();
            this.ControlKH = new DevExpress.XtraEditors.GroupControl();
            this.lookUpKH = new DevExpress.XtraEditors.LookUpEdit();
            this.sidePanel2 = new DevExpress.XtraEditors.SidePanel();
            this.ControlNgayXH = new DevExpress.XtraEditors.GroupControl();
            this.dateENXH = new DevExpress.XtraEditors.DateEdit();
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.simpleButton2 = new DevExpress.XtraEditors.SimpleButton();
            this.panel2 = new System.Windows.Forms.Panel();
            this.gridCDH = new DevExpress.XtraGrid.GridControl();
            this.gridVDH = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.panel1.SuspendLayout();
            this.sidePanel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).BeginInit();
            this.groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RChonTK.Properties)).BeginInit();
            this.sidePanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ControlKH)).BeginInit();
            this.ControlKH.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpKH.Properties)).BeginInit();
            this.sidePanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ControlNgayXH)).BeginInit();
            this.ControlNgayXH.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dateENXH.Properties.CalendarTimeProperties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateENXH.Properties)).BeginInit();
            this.sidePanel1.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridCDH)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVDH)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.sidePanel4);
            this.panel1.Controls.Add(this.sidePanel3);
            this.panel1.Controls.Add(this.sidePanel2);
            this.panel1.Controls.Add(this.sidePanel1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1597, 97);
            this.panel1.TabIndex = 0;
            // 
            // sidePanel4
            // 
            this.sidePanel4.Controls.Add(this.groupControl1);
            this.sidePanel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidePanel4.Location = new System.Drawing.Point(0, 0);
            this.sidePanel4.Name = "sidePanel4";
            this.sidePanel4.Size = new System.Drawing.Size(407, 97);
            this.sidePanel4.TabIndex = 6;
            this.sidePanel4.Text = "sidePanel4";
            // 
            // groupControl1
            // 
            this.groupControl1.Controls.Add(this.RChonTK);
            this.groupControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupControl1.Location = new System.Drawing.Point(0, 0);
            this.groupControl1.Margin = new System.Windows.Forms.Padding(4);
            this.groupControl1.Name = "groupControl1";
            this.groupControl1.Size = new System.Drawing.Size(407, 97);
            this.groupControl1.TabIndex = 2;
            this.groupControl1.Text = "Chọn Hình Thức Thống Kê";
            // 
            // RChonTK
            // 
            this.RChonTK.EditValue = 1;
            this.RChonTK.Location = new System.Drawing.Point(31, 41);
            this.RChonTK.Margin = new System.Windows.Forms.Padding(4);
            this.RChonTK.Name = "RChonTK";
            this.RChonTK.Properties.Items.AddRange(new DevExpress.XtraEditors.Controls.RadioGroupItem[] {
            new DevExpress.XtraEditors.Controls.RadioGroupItem(1, "Chọn Tất Cả", true, null, "All"),
            new DevExpress.XtraEditors.Controls.RadioGroupItem(2, "Chọn Theo KH", true, null, "KH")});
            this.RChonTK.Properties.SelectedIndexChanged += new System.EventHandler(this.RChonTK_Properties_SelectedIndexChanged);
            this.RChonTK.Size = new System.Drawing.Size(275, 40);
            this.RChonTK.TabIndex = 1;
            // 
            // sidePanel3
            // 
            this.sidePanel3.Controls.Add(this.ControlKH);
            this.sidePanel3.Dock = System.Windows.Forms.DockStyle.Right;
            this.sidePanel3.Location = new System.Drawing.Point(407, 0);
            this.sidePanel3.Name = "sidePanel3";
            this.sidePanel3.Size = new System.Drawing.Size(390, 97);
            this.sidePanel3.TabIndex = 5;
            this.sidePanel3.Text = "sidePanel3";
            // 
            // ControlKH
            // 
            this.ControlKH.Controls.Add(this.lookUpKH);
            this.ControlKH.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ControlKH.Location = new System.Drawing.Point(1, 0);
            this.ControlKH.Margin = new System.Windows.Forms.Padding(4);
            this.ControlKH.Name = "ControlKH";
            this.ControlKH.Size = new System.Drawing.Size(389, 97);
            this.ControlKH.TabIndex = 0;
            this.ControlKH.Text = "Chọn Mã Khách Hàng";
            // 
            // lookUpKH
            // 
            this.lookUpKH.Location = new System.Drawing.Point(28, 40);
            this.lookUpKH.Margin = new System.Windows.Forms.Padding(4);
            this.lookUpKH.Name = "lookUpKH";
            this.lookUpKH.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lookUpKH.Properties.DisplayMember = "CUSNAME";
            this.lookUpKH.Properties.ValueMember = "ID";
            this.lookUpKH.Size = new System.Drawing.Size(311, 22);
            this.lookUpKH.TabIndex = 0;
            this.lookUpKH.Closed += new DevExpress.XtraEditors.Controls.ClosedEventHandler(this.lookUpKH_Closed);
            this.lookUpKH.EditValueChanged += new System.EventHandler(this.lookUpKH_EditValueChanged);
            // 
            // sidePanel2
            // 
            this.sidePanel2.Controls.Add(this.ControlNgayXH);
            this.sidePanel2.Dock = System.Windows.Forms.DockStyle.Right;
            this.sidePanel2.Location = new System.Drawing.Point(797, 0);
            this.sidePanel2.Name = "sidePanel2";
            this.sidePanel2.Size = new System.Drawing.Size(468, 97);
            this.sidePanel2.TabIndex = 4;
            this.sidePanel2.Text = "sidePanel2";
            // 
            // ControlNgayXH
            // 
            this.ControlNgayXH.Controls.Add(this.dateENXH);
            this.ControlNgayXH.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ControlNgayXH.Location = new System.Drawing.Point(1, 0);
            this.ControlNgayXH.Margin = new System.Windows.Forms.Padding(4);
            this.ControlNgayXH.Name = "ControlNgayXH";
            this.ControlNgayXH.Size = new System.Drawing.Size(467, 97);
            this.ControlNgayXH.TabIndex = 0;
            this.ControlNgayXH.Text = "Chọn Ngày Yêu Cầu Xuất (PLANNED_SHIP_DATE)";
            // 
            // dateENXH
            // 
            this.dateENXH.EditValue = null;
            this.dateENXH.Location = new System.Drawing.Point(43, 40);
            this.dateENXH.Margin = new System.Windows.Forms.Padding(4);
            this.dateENXH.Name = "dateENXH";
            this.dateENXH.Properties.Appearance.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dateENXH.Properties.Appearance.Options.UseFont = true;
            this.dateENXH.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateENXH.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateENXH.Properties.DateTimeChanged += new System.EventHandler(this.dateENXH_Properties_DateTimeChanged);
            this.dateENXH.Properties.EditValueChanged += new System.EventHandler(this.dateENXH_Properties_EditValueChanged);
            this.dateENXH.Size = new System.Drawing.Size(364, 28);
            this.dateENXH.TabIndex = 0;
            // 
            // sidePanel1
            // 
            this.sidePanel1.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.sidePanel1.Appearance.Options.UseBackColor = true;
            this.sidePanel1.Controls.Add(this.simpleButton2);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.sidePanel1.Location = new System.Drawing.Point(1265, 0);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(332, 97);
            this.sidePanel1.TabIndex = 3;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // simpleButton2
            // 
            this.simpleButton2.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton2.ImageOptions.Image")));
            this.simpleButton2.Location = new System.Drawing.Point(69, 29);
            this.simpleButton2.Margin = new System.Windows.Forms.Padding(4);
            this.simpleButton2.Name = "simpleButton2";
            this.simpleButton2.Size = new System.Drawing.Size(173, 33);
            this.simpleButton2.TabIndex = 1;
            this.simpleButton2.Text = "Xuất Phiếu";
            this.simpleButton2.Click += new System.EventHandler(this.simpleButton2_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.gridCDH);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 97);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1597, 670);
            this.panel2.TabIndex = 1;
            // 
            // gridCDH
            // 
            this.gridCDH.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCDH.Location = new System.Drawing.Point(0, 0);
            this.gridCDH.MainView = this.gridVDH;
            this.gridCDH.Name = "gridCDH";
            this.gridCDH.Size = new System.Drawing.Size(1597, 670);
            this.gridCDH.TabIndex = 3;
            this.gridCDH.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridVDH});
            // 
            // gridVDH
            // 
            this.gridVDH.GridControl = this.gridCDH;
            this.gridVDH.Name = "gridVDH";
            // 
            // PGH_XK
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1597, 767);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "PGH_XK";
            this.Text = "Phiếu Giao Hàng Xuất Khẩu";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.PGH_XK_Load);
            this.panel1.ResumeLayout(false);
            this.sidePanel4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).EndInit();
            this.groupControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.RChonTK.Properties)).EndInit();
            this.sidePanel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ControlKH)).EndInit();
            this.ControlKH.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.lookUpKH.Properties)).EndInit();
            this.sidePanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ControlNgayXH)).EndInit();
            this.ControlNgayXH.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dateENXH.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateENXH.Properties)).EndInit();
            this.sidePanel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridCDH)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVDH)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private DevExpress.XtraEditors.DateEdit dateENXH;
        private System.Windows.Forms.Panel panel2;
        private DevExpress.XtraEditors.SidePanel sidePanel4;
        private DevExpress.XtraEditors.GroupControl groupControl1;
        private DevExpress.XtraEditors.RadioGroup RChonTK;
        private DevExpress.XtraEditors.SidePanel sidePanel3;
        private DevExpress.XtraEditors.GroupControl ControlKH;
        private DevExpress.XtraEditors.LookUpEdit lookUpKH;
        private DevExpress.XtraEditors.SidePanel sidePanel2;
        private DevExpress.XtraEditors.GroupControl ControlNgayXH;
        private DevExpress.XtraEditors.SidePanel sidePanel1;
        private DevExpress.XtraEditors.SimpleButton simpleButton2;
        private DevExpress.XtraGrid.GridControl gridCDH;
        private DevExpress.XtraGrid.Views.Grid.GridView gridVDH;
    }
}