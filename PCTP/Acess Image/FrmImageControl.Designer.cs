namespace PCTP.Acess_Image
{
    partial class FrmImageControl
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
            this.imagesControl1 = new PCTP.ImagesControl.ImagesControl();
            this.SuspendLayout();
            // 
            // imagesControl1
            // 
            this.imagesControl1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.imagesControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imagesControl1.Location = new System.Drawing.Point(0, 0);
            this.imagesControl1.Margin = new System.Windows.Forms.Padding(4, 2, 4, 2);
            this.imagesControl1.Name = "imagesControl1";
            this.imagesControl1.Size = new System.Drawing.Size(1262, 632);
            this.imagesControl1.TabIndex = 0;
            this.imagesControl1.Load += new System.EventHandler(this.imagesControl1_Load);
            // 
            // FrmImageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1262, 632);
            this.Controls.Add(this.imagesControl1);
            this.Name = "FrmImageControl";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Frm Ảnh Cho Sản Phẩm";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.ResumeLayout(false);

        }

        #endregion

        private ImagesControl.ImagesControl imagesControl1;
    }
}