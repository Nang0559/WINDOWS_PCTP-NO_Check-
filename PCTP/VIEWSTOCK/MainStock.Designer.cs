namespace PCTP.VIEWSTOCK
{
    partial class MainStock
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainStock));
            this.panelTop = new DevExpress.XtraEditors.SidePanel();
            this.btnDKMa = new DevExpress.XtraEditors.SimpleButton();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.PEditInput = new DevExpress.XtraEditors.GridLookUpEdit();
            this.gridLookUpEdit1View = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.btnReset = new DevExpress.XtraEditors.SimpleButton();
            this.btnEnterItem = new DevExpress.XtraEditors.SimpleButton();
            this.btnRegisterRack = new DevExpress.XtraEditors.SimpleButton();
            this.pnlMain = new System.Windows.Forms.FlowLayoutPanel();
            this.btnHisCheck = new DevExpress.XtraEditors.SimpleButton();
            this.panelTop.SuspendLayout();
            this.sidePanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PEditInput.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridLookUpEdit1View)).BeginInit();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.btnHisCheck);
            this.panelTop.Controls.Add(this.btnDKMa);
            this.panelTop.Controls.Add(this.simpleButton1);
            this.panelTop.Controls.Add(this.sidePanel1);
            this.panelTop.Controls.Add(this.btnReset);
            this.panelTop.Controls.Add(this.btnEnterItem);
            this.panelTop.Controls.Add(this.btnRegisterRack);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1291, 51);
            this.panelTop.TabIndex = 0;
            this.panelTop.Text = "sidePanel1";
            // 
            // btnDKMa
            // 
            this.btnDKMa.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnDKMa.Location = new System.Drawing.Point(407, 0);
            this.btnDKMa.Name = "btnDKMa";
            this.btnDKMa.Size = new System.Drawing.Size(119, 50);
            this.btnDKMa.TabIndex = 6;
            this.btnDKMa.Text = "ĐK Mã (KT Nhập)";
            this.btnDKMa.Click += new System.EventHandler(this.btnDKMa_Click);
            // 
            // simpleButton1
            // 
            this.simpleButton1.Dock = System.Windows.Forms.DockStyle.Left;
            this.simpleButton1.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton1.ImageOptions.Image")));
            this.simpleButton1.Location = new System.Drawing.Point(238, 0);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(169, 50);
            this.simpleButton1.TabIndex = 5;
            this.simpleButton1.Text = "Báo cáo Nhập Xuất";
            this.simpleButton1.Click += new System.EventHandler(this.simpleButton1_Click);
            // 
            // sidePanel1
            // 
            this.sidePanel1.Controls.Add(this.PEditInput);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.sidePanel1.Location = new System.Drawing.Point(529, 0);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(636, 50);
            this.sidePanel1.TabIndex = 4;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // PEditInput
            // 
            this.PEditInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PEditInput.Location = new System.Drawing.Point(1, 0);
            this.PEditInput.Name = "PEditInput";
            this.PEditInput.Properties.Appearance.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(163)));
            this.PEditInput.Properties.Appearance.Options.UseFont = true;
            this.PEditInput.Properties.AutoHeight = false;
            this.PEditInput.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.PEditInput.Properties.NullText = "Nhập Mã Hàng Tìm Kiếm / Bắn Tem Đã Nhập Kho";
            this.PEditInput.Properties.PopupView = this.gridLookUpEdit1View;
            this.PEditInput.Size = new System.Drawing.Size(635, 50);
            this.PEditInput.TabIndex = 2;
            this.PEditInput.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PEditInput_MouseClick);
            // 
            // gridLookUpEdit1View
            // 
            this.gridLookUpEdit1View.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.gridLookUpEdit1View.Name = "gridLookUpEdit1View";
            this.gridLookUpEdit1View.OptionsSelection.EnableAppearanceFocusedCell = false;
            this.gridLookUpEdit1View.OptionsView.ShowGroupPanel = false;
            // 
            // btnReset
            // 
            this.btnReset.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnReset.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnReset.ImageOptions.Image")));
            this.btnReset.Location = new System.Drawing.Point(1165, 0);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(126, 50);
            this.btnReset.TabIndex = 3;
            this.btnReset.Text = "ResetSlotDisplay";
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // btnEnterItem
            // 
            this.btnEnterItem.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnEnterItem.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnEnterItem.ImageOptions.Image")));
            this.btnEnterItem.Location = new System.Drawing.Point(119, 0);
            this.btnEnterItem.Name = "btnEnterItem";
            this.btnEnterItem.Size = new System.Drawing.Size(119, 50);
            this.btnEnterItem.TabIndex = 1;
            this.btnEnterItem.Text = "Nhập Kho";
            this.btnEnterItem.Click += new System.EventHandler(this.btnEnterItem_Click);
            // 
            // btnRegisterRack
            // 
            this.btnRegisterRack.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnRegisterRack.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnRegisterRack.ImageOptions.Image")));
            this.btnRegisterRack.Location = new System.Drawing.Point(0, 0);
            this.btnRegisterRack.Name = "btnRegisterRack";
            this.btnRegisterRack.Size = new System.Drawing.Size(119, 50);
            this.btnRegisterRack.TabIndex = 0;
            this.btnRegisterRack.Text = "Đăng ký Rack";
            this.btnRegisterRack.Click += new System.EventHandler(this.btnRegisterRack_Click);
            // 
            // pnlMain
            // 
            this.pnlMain.AutoScroll = true;
            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMain.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlMain.Location = new System.Drawing.Point(0, 51);
            this.pnlMain.Name = "pnlMain";
            this.pnlMain.Size = new System.Drawing.Size(1291, 562);
            this.pnlMain.TabIndex = 1;
            this.pnlMain.WrapContents = false;
            // 
            // btnHisCheck
            // 
            this.btnHisCheck.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnHisCheck.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton2.ImageOptions.Image")));
            this.btnHisCheck.Location = new System.Drawing.Point(526, 0);
            this.btnHisCheck.Name = "btnHisCheck";
            this.btnHisCheck.Size = new System.Drawing.Size(119, 50);
            this.btnHisCheck.TabIndex = 7;
            this.btnHisCheck.Text = "LS Kiểm Tra";
            this.btnHisCheck.Click += new System.EventHandler(this.btnHisCheck_Click);
            // 
            // MainStock
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1291, 613);
            this.Controls.Add(this.pnlMain);
            this.Controls.Add(this.panelTop);
            this.Name = "MainStock";
            this.Text = "MainStock";
            this.Load += new System.EventHandler(this.MainStock_Load);
            this.Shown += new System.EventHandler(this.MainStock_Shown);
            this.Resize += new System.EventHandler(this.MainStock_Resize);
            this.panelTop.ResumeLayout(false);
            this.sidePanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.PEditInput.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridLookUpEdit1View)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SidePanel panelTop;
        private DevExpress.XtraEditors.SimpleButton btnRegisterRack;
        private DevExpress.XtraEditors.SimpleButton btnEnterItem;

        private DevExpress.XtraEditors.GridLookUpEdit PEditInput;
        private DevExpress.XtraGrid.Views.Grid.GridView gridLookUpEdit1View;
        private DevExpress.XtraEditors.SimpleButton btnReset;
        private DevExpress.XtraEditors.SidePanel sidePanel1;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
        private System.Windows.Forms.FlowLayoutPanel pnlMain;
        private DevExpress.XtraEditors.SimpleButton btnDKMa;
        private DevExpress.XtraEditors.SimpleButton btnHisCheck;
    }
}