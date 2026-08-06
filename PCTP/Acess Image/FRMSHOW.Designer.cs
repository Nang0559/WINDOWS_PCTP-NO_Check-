namespace PCTP.Acess_Image
{
    partial class FRMSHOW
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
            this.imageSliderShow = new DevExpress.XtraEditors.Controls.ImageSlider();
            ((System.ComponentModel.ISupportInitialize)(this.imageSliderShow)).BeginInit();
            this.SuspendLayout();
            // 
            // imageSliderShow
            // 
            this.imageSliderShow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageSliderShow.LayoutMode = DevExpress.Utils.Drawing.ImageLayoutMode.ZoomInside;
            this.imageSliderShow.Location = new System.Drawing.Point(0, 0);
            this.imageSliderShow.Name = "imageSliderShow";
            this.imageSliderShow.Size = new System.Drawing.Size(1278, 587);
            this.imageSliderShow.TabIndex = 0;
            this.imageSliderShow.Text = "imageSlider1";
            // 
            // FRMSHOW
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1278, 587);
            this.Controls.Add(this.imageSliderShow);
            this.Name = "FRMSHOW";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FRMSHOW";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.FRMSHOW_Load);
            ((System.ComponentModel.ISupportInitialize)(this.imageSliderShow)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.Controls.ImageSlider imageSliderShow;
    }
}