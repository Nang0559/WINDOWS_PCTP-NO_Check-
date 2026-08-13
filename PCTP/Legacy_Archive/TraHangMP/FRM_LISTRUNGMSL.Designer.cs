namespace PCTP.QRCODE_HVN
{
    partial class FRM_LISTRUNGMSL
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
            this.listVTrungMaSL = new System.Windows.Forms.ListView();
            this.STT = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.GX = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.MH = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.TH = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SLX = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.TT = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // listVTrungMaSL
            // 
            this.listVTrungMaSL.CheckBoxes = true;
            this.listVTrungMaSL.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.STT,
            this.GX,
            this.MH,
            this.TH,
            this.SLX,
            this.TT});
            this.listVTrungMaSL.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listVTrungMaSL.Font = new System.Drawing.Font("Times New Roman", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listVTrungMaSL.FullRowSelect = true;
            this.listVTrungMaSL.GridLines = true;
            this.listVTrungMaSL.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listVTrungMaSL.HideSelection = false;
            this.listVTrungMaSL.Location = new System.Drawing.Point(0, 0);
            this.listVTrungMaSL.Name = "listVTrungMaSL";
            this.listVTrungMaSL.Size = new System.Drawing.Size(1060, 443);
            this.listVTrungMaSL.TabIndex = 0;
            this.listVTrungMaSL.UseCompatibleStateImageBehavior = false;
            this.listVTrungMaSL.View = System.Windows.Forms.View.Details;
            this.listVTrungMaSL.SelectedIndexChanged += new System.EventHandler(this.listVTrungMaSL_SelectedIndexChanged);
            this.listVTrungMaSL.DoubleClick += new System.EventHandler(this.listVTrungMaSL_DoubleClick);
            // 
            // STT
            // 
            this.STT.Text = "STT Phiếu";
            // 
            // GX
            // 
            this.GX.Text = "Giờ Xuất";
            this.GX.Width = 120;
            // 
            // MH
            // 
            this.MH.Text = "Mã Hàng";
            this.MH.Width = 200;
            // 
            // TH
            // 
            this.TH.Text = "Tên Hàng";
            this.TH.Width = 300;
            // 
            // SLX
            // 
            this.SLX.Text = "Số Lượng Xuất";
            this.SLX.Width = 150;
            // 
            // TT
            // 
            this.TT.Text = "Trạng Thái";
            this.TT.Width = 150;
            // 
            // FRM_LISTRUNGMSL
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1060, 443);
            this.Controls.Add(this.listVTrungMaSL);
            this.Name = "FRM_LISTRUNGMSL";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "LIST TRÙNG MÃ SỐ LƯỢNG";
            this.Load += new System.EventHandler(this.BAN_QRCODE_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listVTrungMaSL;
        private System.Windows.Forms.ColumnHeader STT;
        private System.Windows.Forms.ColumnHeader GX;
        private System.Windows.Forms.ColumnHeader MH;
        private System.Windows.Forms.ColumnHeader TH;
        private System.Windows.Forms.ColumnHeader SLX;
        private System.Windows.Forms.ColumnHeader TT;
    }
}