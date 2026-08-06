namespace PCTP.QRCODE_HVN.PGH
{
    partial class FRM_HTIN
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FRM_HTIN));
            this.sideIN = new DevExpress.XtraEditors.SidePanel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.checkBKGX = new System.Windows.Forms.CheckBox();
            this.checkBNM = new System.Windows.Forms.CheckBox();
            this.CMD_INPHIEUGIAO = new DevExpress.XtraEditors.SimpleButton();
            this.sideIN.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // sideIN
            // 
            this.sideIN.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(224)))), ((int)(((byte)(192)))));
            this.sideIN.Appearance.BorderColor = System.Drawing.Color.Yellow;
            this.sideIN.Appearance.Options.UseBackColor = true;
            this.sideIN.Appearance.Options.UseBorderColor = true;
            this.sideIN.Controls.Add(this.groupBox1);
            this.sideIN.Controls.Add(this.CMD_INPHIEUGIAO);
            this.sideIN.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sideIN.Location = new System.Drawing.Point(0, 0);
            this.sideIN.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.sideIN.Name = "sideIN";
            this.sideIN.Size = new System.Drawing.Size(354, 216);
            this.sideIN.TabIndex = 8;
            this.sideIN.Text = "sidePanel2";
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.groupBox1.Controls.Add(this.checkBKGX);
            this.groupBox1.Controls.Add(this.checkBNM);
            this.groupBox1.Location = new System.Drawing.Point(3, 7);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Size = new System.Drawing.Size(351, 138);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "IN PHIẾU GIAO HÀNG";
            // 
            // checkBKGX
            // 
            this.checkBKGX.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBKGX.AutoSize = true;
            this.checkBKGX.Location = new System.Drawing.Point(6, 47);
            this.checkBKGX.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.checkBKGX.Name = "checkBKGX";
            this.checkBKGX.Size = new System.Drawing.Size(201, 18);
            this.checkBKGX.TabIndex = 5;
            this.checkBKGX.Text = "THEO KHUNG GIỜ ĐANG CHỌN";
            this.checkBKGX.UseVisualStyleBackColor = true;
            // 
            // checkBNM
            // 
            this.checkBNM.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.checkBNM.AutoSize = true;
            this.checkBNM.Location = new System.Drawing.Point(7, 25);
            this.checkBNM.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.checkBNM.Name = "checkBNM";
            this.checkBNM.Size = new System.Drawing.Size(190, 18);
            this.checkBNM.TabIndex = 4;
            this.checkBNM.Text = "THEO NHÀ MÁY ĐANG CHỌN";
            this.checkBNM.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.checkBNM.UseVisualStyleBackColor = true;
            // 
            // CMD_INPHIEUGIAO
            // 
            this.CMD_INPHIEUGIAO.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.CMD_INPHIEUGIAO.Appearance.BackColor = DevExpress.LookAndFeel.DXSkinColors.FillColors.Success;
            this.CMD_INPHIEUGIAO.Appearance.Options.UseBackColor = true;
            this.CMD_INPHIEUGIAO.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("CMD_INPHIEUGIAO.ImageOptions.Image")));
            this.CMD_INPHIEUGIAO.Location = new System.Drawing.Point(119, 165);
            this.CMD_INPHIEUGIAO.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CMD_INPHIEUGIAO.Name = "CMD_INPHIEUGIAO";
            this.CMD_INPHIEUGIAO.Size = new System.Drawing.Size(112, 31);
            this.CMD_INPHIEUGIAO.TabIndex = 3;
            this.CMD_INPHIEUGIAO.Text = "IN PHIẾU";
            this.CMD_INPHIEUGIAO.Click += new System.EventHandler(this.CMD_INPHIEUGIAO_Click);
            // 
            // FRM_HTIN
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(354, 216);
            this.Controls.Add(this.sideIN);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FRM_HTIN";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FRM_HTIN";
            this.sideIN.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SidePanel sideIN;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox checkBKGX;
        private System.Windows.Forms.CheckBox checkBNM;
        private DevExpress.XtraEditors.SimpleButton CMD_INPHIEUGIAO;
    }
}