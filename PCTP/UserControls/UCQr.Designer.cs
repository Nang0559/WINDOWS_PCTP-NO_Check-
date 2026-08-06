namespace PCTP.UserControls
{
    partial class UCQr
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
            this.txtQr = new DevExpress.XtraEditors.TextEdit();
            ((System.ComponentModel.ISupportInitialize)(this.txtQr.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // txtQr
            // 
            this.txtQr.Location = new System.Drawing.Point(14, 3);
            this.txtQr.Name = "txtQr";
            this.txtQr.Size = new System.Drawing.Size(626, 22);
            this.txtQr.TabIndex = 0;
            //this.txtQr.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtQr_KeyPress);
            // 
            // UCQr
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.txtQr);
            this.Name = "UCQr";
            this.Size = new System.Drawing.Size(653, 30);
            ((System.ComponentModel.ISupportInitialize)(this.txtQr.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.TextEdit txtQr;
    }
}
