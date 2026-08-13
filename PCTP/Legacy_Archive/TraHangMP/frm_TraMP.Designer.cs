
namespace PCTP.QRCODE_HVN.TraHangMP
{
    partial class frm_TraMP
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.GCTPhieuGiao = new DevExpress.XtraGrid.GridControl();
            this.GCTQRCode = new DevExpress.XtraGrid.GridControl();
            this.txt_Qrcode = new DevExpress.XtraEditors.TextEdit();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            this.simpleButton2 = new DevExpress.XtraEditors.SimpleButton();
            this.simpleButton3 = new DevExpress.XtraEditors.SimpleButton();
            this.panel3 = new System.Windows.Forms.Panel();
            this.GCTMain = new DevExpress.XtraGrid.GridControl();
            this.GVMain = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.GvQRPhieuGiao = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
            this.GVQRCode = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
            this.GBandPhieuGiao = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            this.GBandQrcode = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            this.fcc = new System.Windows.Forms.Label();
            this.hvn = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GCTPhieuGiao)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCTQRCode)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txt_Qrcode.Properties)).BeginInit();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GCTMain)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GVMain)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GvQRPhieuGiao)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GVQRCode)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.hvn);
            this.panel1.Controls.Add(this.fcc);
            this.panel1.Controls.Add(this.simpleButton3);
            this.panel1.Controls.Add(this.simpleButton2);
            this.panel1.Controls.Add(this.simpleButton1);
            this.panel1.Controls.Add(this.txt_Qrcode);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1384, 85);
            this.panel1.TabIndex = 0;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.GCTQRCode);
            this.panel2.Controls.Add(this.GCTPhieuGiao);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 85);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1384, 153);
            this.panel2.TabIndex = 1;
            // 
            // GCTPhieuGiao
            // 
            this.GCTPhieuGiao.Dock = System.Windows.Forms.DockStyle.Left;
            this.GCTPhieuGiao.Location = new System.Drawing.Point(0, 0);
            this.GCTPhieuGiao.MainView = this.GvQRPhieuGiao;
            this.GCTPhieuGiao.Name = "GCTPhieuGiao";
            this.GCTPhieuGiao.Size = new System.Drawing.Size(650, 153);
            this.GCTPhieuGiao.TabIndex = 0;
            this.GCTPhieuGiao.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GvQRPhieuGiao});
            // 
            // GCTQRCode
            // 
            this.GCTQRCode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GCTQRCode.Location = new System.Drawing.Point(650, 0);
            this.GCTQRCode.MainView = this.GVQRCode;
            this.GCTQRCode.Name = "GCTQRCode";
            this.GCTQRCode.Size = new System.Drawing.Size(734, 153);
            this.GCTQRCode.TabIndex = 1;
            this.GCTQRCode.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GVQRCode});
            // 
            // txt_Qrcode
            // 
            this.txt_Qrcode.Location = new System.Drawing.Point(169, 19);
            this.txt_Qrcode.Name = "txt_Qrcode";
            this.txt_Qrcode.Properties.Appearance.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txt_Qrcode.Properties.Appearance.Options.UseFont = true;
            this.txt_Qrcode.Size = new System.Drawing.Size(481, 26);
            this.txt_Qrcode.TabIndex = 0;
            this.txt_Qrcode.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txt_Qrcode_KeyPress);
            // 
            // simpleButton1
            // 
            this.simpleButton1.Location = new System.Drawing.Point(870, 22);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(150, 40);
            this.simpleButton1.TabIndex = 1;
            this.simpleButton1.Text = "simpleButton1";
            // 
            // simpleButton2
            // 
            this.simpleButton2.Location = new System.Drawing.Point(1046, 22);
            this.simpleButton2.Name = "simpleButton2";
            this.simpleButton2.Size = new System.Drawing.Size(150, 40);
            this.simpleButton2.TabIndex = 1;
            this.simpleButton2.Text = "simpleButton1";
            // 
            // simpleButton3
            // 
            this.simpleButton3.Location = new System.Drawing.Point(1222, 22);
            this.simpleButton3.Name = "simpleButton3";
            this.simpleButton3.Size = new System.Drawing.Size(150, 40);
            this.simpleButton3.TabIndex = 1;
            this.simpleButton3.Text = "simpleButton1";
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.GCTMain);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 238);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1384, 508);
            this.panel3.TabIndex = 2;
            // 
            // GCTMain
            // 
            this.GCTMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GCTMain.Location = new System.Drawing.Point(0, 0);
            this.GCTMain.MainView = this.GVMain;
            this.GCTMain.Name = "GCTMain";
            this.GCTMain.Size = new System.Drawing.Size(1384, 508);
            this.GCTMain.TabIndex = 0;
            this.GCTMain.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GVMain});
            // 
            // GVMain
            // 
            this.GVMain.GridControl = this.GCTMain;
            this.GVMain.Name = "GVMain";
            // 
            // GvQRPhieuGiao
            // 
            this.GvQRPhieuGiao.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.GBandPhieuGiao});
            this.GvQRPhieuGiao.GridControl = this.GCTPhieuGiao;
            this.GvQRPhieuGiao.Name = "GvQRPhieuGiao";
            // 
            // GVQRCode
            // 
            this.GVQRCode.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.GBandQrcode});
            this.GVQRCode.GridControl = this.GCTQRCode;
            this.GVQRCode.Name = "GVQRCode";
            // 
            // GBandPhieuGiao
            // 
            this.GBandPhieuGiao.Caption = "Phiếu Giao Của Tem";
            this.GBandPhieuGiao.MinWidth = 12;
            this.GBandPhieuGiao.Name = "GBandPhieuGiao";
            this.GBandPhieuGiao.VisibleIndex = 0;
            this.GBandPhieuGiao.Width = 82;
            // 
            // GBandQrcode
            // 
            this.GBandQrcode.Caption = "Dữ Liệu Đọc Qrcode Của Tem";
            this.GBandQrcode.MinWidth = 12;
            this.GBandQrcode.Name = "GBandQrcode";
            this.GBandQrcode.VisibleIndex = 0;
            this.GBandQrcode.Width = 82;
            // 
            // fcc
            // 
            this.fcc.AutoSize = true;
            this.fcc.Location = new System.Drawing.Point(50, 55);
            this.fcc.Name = "fcc";
            this.fcc.Size = new System.Drawing.Size(49, 17);
            this.fcc.TabIndex = 2;
            this.fcc.Text = "temfcc";
            // 
            // hvn
            // 
            this.hvn.AutoSize = true;
            this.hvn.Location = new System.Drawing.Point(418, 55);
            this.hvn.Name = "hvn";
            this.hvn.Size = new System.Drawing.Size(54, 17);
            this.hvn.TabIndex = 2;
            this.hvn.Text = "temhvn";
            // 
            // frm_TraMP
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1384, 746);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "frm_TraMP";
            this.Text = "frm_TraMP";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GCTPhieuGiao)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCTQRCode)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txt_Qrcode.Properties)).EndInit();
            this.panel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GCTMain)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GVMain)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GvQRPhieuGiao)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GVQRCode)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label hvn;
        private System.Windows.Forms.Label fcc;
        private DevExpress.XtraEditors.SimpleButton simpleButton3;
        private DevExpress.XtraEditors.SimpleButton simpleButton2;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
        private DevExpress.XtraEditors.TextEdit txt_Qrcode;
        private System.Windows.Forms.Panel panel2;
        private DevExpress.XtraGrid.GridControl GCTQRCode;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridView GVQRCode;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand GBandQrcode;
        private DevExpress.XtraGrid.GridControl GCTPhieuGiao;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridView GvQRPhieuGiao;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand GBandPhieuGiao;
        private System.Windows.Forms.Panel panel3;
        private DevExpress.XtraGrid.GridControl GCTMain;
        private DevExpress.XtraGrid.Views.Grid.GridView GVMain;
    }
}