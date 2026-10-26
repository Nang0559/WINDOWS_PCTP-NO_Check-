namespace PCTP.UserControls
{
    partial class UCSTOCK
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UCSTOCK));
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.sidePanel2 = new DevExpress.XtraEditors.SidePanel();
            this.sidePanel3 = new DevExpress.XtraEditors.SidePanel();
            this.checkButton1 = new DevExpress.XtraEditors.CheckButton();
            this.gct_ListEmptyStock = new DevExpress.XtraGrid.GridControl();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.cmdOK = new DevExpress.XtraEditors.SimpleButton();
            this.cmdCancel = new DevExpress.XtraEditors.SimpleButton();
            this.sidePanel1.SuspendLayout();
            this.sidePanel2.SuspendLayout();
            this.sidePanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gct_ListEmptyStock)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // sidePanel1
            // 
            this.sidePanel1.Controls.Add(this.checkButton1);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.sidePanel1.Location = new System.Drawing.Point(0, 0);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(552, 40);
            this.sidePanel1.TabIndex = 0;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // sidePanel2
            // 
            this.sidePanel2.Controls.Add(this.cmdCancel);
            this.sidePanel2.Controls.Add(this.cmdOK);
            this.sidePanel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.sidePanel2.Location = new System.Drawing.Point(0, 338);
            this.sidePanel2.Name = "sidePanel2";
            this.sidePanel2.Size = new System.Drawing.Size(552, 46);
            this.sidePanel2.TabIndex = 1;
            this.sidePanel2.Text = "sidePanel2";
            // 
            // sidePanel3
            // 
            this.sidePanel3.Controls.Add(this.gct_ListEmptyStock);
            this.sidePanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidePanel3.Location = new System.Drawing.Point(0, 40);
            this.sidePanel3.Name = "sidePanel3";
            this.sidePanel3.Size = new System.Drawing.Size(552, 298);
            this.sidePanel3.TabIndex = 2;
            this.sidePanel3.Text = "sidePanel3";
            // 
            // checkButton1
            // 
            this.checkButton1.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("checkButton1.ImageOptions.Image")));
            this.checkButton1.Location = new System.Drawing.Point(142, 12);
            this.checkButton1.Name = "checkButton1";
            this.checkButton1.Size = new System.Drawing.Size(320, 22);
            this.checkButton1.TabIndex = 0;
            this.checkButton1.Text = "Nhập Nhiều Hàng Vào Cùng Vị Trí";
            // 
            // gct_ListEmptyStock
            // 
            this.gct_ListEmptyStock.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gct_ListEmptyStock.Location = new System.Drawing.Point(0, 0);
            this.gct_ListEmptyStock.MainView = this.gridView1;
            this.gct_ListEmptyStock.Name = "gct_ListEmptyStock";
            this.gct_ListEmptyStock.Size = new System.Drawing.Size(552, 298);
            this.gct_ListEmptyStock.TabIndex = 0;
            this.gct_ListEmptyStock.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridView1});
            // 
            // gridView1
            // 
            this.gridView1.GridControl = this.gct_ListEmptyStock;
            this.gridView1.Name = "gridView1";
            // 
            // cmdOK
            // 
            this.cmdOK.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton1.ImageOptions.Image")));
            this.cmdOK.Location = new System.Drawing.Point(78, 11);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(109, 34);
            this.cmdOK.TabIndex = 0;
            this.cmdOK.Text = "OK";
            // 
            // cmdCancel
            // 
            this.cmdCancel.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton2.ImageOptions.Image")));
            this.cmdCancel.Location = new System.Drawing.Point(218, 9);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(118, 36);
            this.cmdCancel.TabIndex = 1;
            this.cmdCancel.Text = "CanCel";
            // 
            // UCSTOCK
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.sidePanel3);
            this.Controls.Add(this.sidePanel2);
            this.Controls.Add(this.sidePanel1);
            this.Name = "UCSTOCK";
            this.Size = new System.Drawing.Size(552, 384);
            this.sidePanel1.ResumeLayout(false);
            this.sidePanel2.ResumeLayout(false);
            this.sidePanel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gct_ListEmptyStock)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SidePanel sidePanel1;
        private DevExpress.XtraEditors.CheckButton checkButton1;
        private DevExpress.XtraEditors.SidePanel sidePanel2;
        private DevExpress.XtraEditors.SimpleButton cmdCancel;
        private DevExpress.XtraEditors.SimpleButton cmdOK;
        private DevExpress.XtraEditors.SidePanel sidePanel3;
        private DevExpress.XtraGrid.GridControl gct_ListEmptyStock;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
    }
}
