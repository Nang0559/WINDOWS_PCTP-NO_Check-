namespace PCTP.QRCODE_HVN
{
    partial class GIODAGIAHANG
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
            this.cmdOK = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.dateDXH = new DevExpress.XtraEditors.TimeSpanEdit();
            this.dateDH = new DevExpress.XtraEditors.DateEdit();
            ((System.ComponentModel.ISupportInitialize)(this.dateDXH.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateDH.Properties.CalendarTimeProperties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateDH.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // cmdOK
            // 
            this.cmdOK.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdOK.Location = new System.Drawing.Point(172, 77);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(92, 28);
            this.cmdOK.TabIndex = 1;
            this.cmdOK.Text = "OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("UD Digi Kyokasho NP-R", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.label1.Location = new System.Drawing.Point(98, -3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(298, 28);
            this.label1.TabIndex = 2;
            this.label1.Text = "Chọn Giờ Đã Xuất Hàng";
            // 
            // dateDXH
            // 
            this.dateDXH.EditValue = null;
            this.dateDXH.Location = new System.Drawing.Point(272, 34);
            this.dateDXH.Name = "dateDXH";
            this.dateDXH.Properties.AllowEditDays = false;
            this.dateDXH.Properties.AllowEditMinutes = false;
            this.dateDXH.Properties.AllowEditSeconds = false;
            this.dateDXH.Properties.Appearance.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dateDXH.Properties.Appearance.Options.UseFont = true;
            this.dateDXH.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateDXH.Properties.DisplayFormat.FormatString = "dd/MM/yyyy HH:00 00";
            this.dateDXH.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            this.dateDXH.Properties.EditFormat.FormatString = "d";
            this.dateDXH.Properties.EditFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            this.dateDXH.Properties.EditValueChangedFiringMode = DevExpress.XtraEditors.Controls.EditValueChangedFiringMode.Default;
            this.dateDXH.Properties.Mask.EditMask = "HH";
            this.dateDXH.Size = new System.Drawing.Size(143, 26);
            this.dateDXH.TabIndex = 0;
            // 
            // dateDH
            // 
            this.dateDH.EditValue = null;
            this.dateDH.Enabled = false;
            this.dateDH.Location = new System.Drawing.Point(57, 34);
            this.dateDH.Name = "dateDH";
            this.dateDH.Properties.AllowClickInactiveDays = false;
            this.dateDH.Properties.Appearance.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dateDH.Properties.Appearance.Options.UseFont = true;
            this.dateDH.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateDH.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateDH.Properties.DisplayFormat.FormatString = "dd/MM/yyyy";
            this.dateDH.Properties.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            this.dateDH.Properties.Mask.EditMask = "dd/MM/yyyy";
            this.dateDH.Size = new System.Drawing.Size(209, 26);
            this.dateDH.TabIndex = 0;
            // 
            // GIODAGIAHANG
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdOK;
            this.ClientSize = new System.Drawing.Size(498, 117);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmdOK);
            this.Controls.Add(this.dateDXH);
            this.Controls.Add(this.dateDH);
            this.Name = "GIODAGIAHANG";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Load += new System.EventHandler(this.GIODAGIAHANG_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dateDXH.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateDH.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateDH.Properties)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.Label label1;
        private DevExpress.XtraEditors.TimeSpanEdit dateDXH;
        private DevExpress.XtraEditors.DateEdit dateDH;
    }
}