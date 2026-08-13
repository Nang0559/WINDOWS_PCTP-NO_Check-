namespace PCTP.YMN
{
    partial class YMVN_CHONGIAO
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(YMVN_CHONGIAO));
            this.YMVN_MP_SP = new DevExpress.XtraEditors.RadioGroup();
            this.label1 = new System.Windows.Forms.Label();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.YMVN_MP_SP.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // YMVN_MP_SP
            // 
            this.YMVN_MP_SP.Location = new System.Drawing.Point(77, 84);
            this.YMVN_MP_SP.Name = "YMVN_MP_SP";
            this.YMVN_MP_SP.Properties.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(255)))));
            this.YMVN_MP_SP.Properties.Appearance.Font = new System.Drawing.Font("Times New Roman", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.YMVN_MP_SP.Properties.Appearance.Options.UseBackColor = true;
            this.YMVN_MP_SP.Properties.Appearance.Options.UseFont = true;
            this.YMVN_MP_SP.Properties.Items.AddRange(new DevExpress.XtraEditors.Controls.RadioGroupItem[] {
            new DevExpress.XtraEditors.Controls.RadioGroupItem(null, "GIAO MP", true, null, "MP"),
            new DevExpress.XtraEditors.Controls.RadioGroupItem(null, "GIAO SP", true, null, "SP")});
            this.YMVN_MP_SP.Size = new System.Drawing.Size(309, 62);
            this.YMVN_MP_SP.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.Color.White;
            this.label1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label1.Font = new System.Drawing.Font("Times New Roman", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(10, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(467, 37);
            this.label1.TabIndex = 1;
            this.label1.Text = "Chọn Loại Hình Giao Hàng YMVN";
            // 
            // simpleButton1
            // 
            this.simpleButton1.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton1.ImageOptions.Image")));
            this.simpleButton1.Location = new System.Drawing.Point(155, 180);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(120, 38);
            this.simpleButton1.TabIndex = 2;
            this.simpleButton1.Text = "OK";
            this.simpleButton1.Click += new System.EventHandler(this.simpleButton1_Click);
            // 
            // YMVN_CHONGIAO
            // 
            this.Appearance.BackColor = System.Drawing.Color.Gray;
            this.Appearance.Options.UseBackColor = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(483, 253);
            this.Controls.Add(this.simpleButton1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.YMVN_MP_SP);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "YMVN_CHONGIAO";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "YMVN_CHONGIAO";
            ((System.ComponentModel.ISupportInitialize)(this.YMVN_MP_SP.Properties)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.RadioGroup YMVN_MP_SP;
        private System.Windows.Forms.Label label1;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
    }
}