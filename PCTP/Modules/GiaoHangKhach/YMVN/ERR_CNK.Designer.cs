namespace PCTP.YMN
{
    partial class ERR_CNK
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
            this.GT_ERR_CNK = new DevExpress.XtraGrid.GridControl();
            this.GV_ERR_CNK = new DevExpress.XtraGrid.Views.Grid.GridView();
            ((System.ComponentModel.ISupportInitialize)(this.GT_ERR_CNK)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_ERR_CNK)).BeginInit();
            this.SuspendLayout();
            // 
            // GT_ERR_CNK
            // 
            this.GT_ERR_CNK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GT_ERR_CNK.Location = new System.Drawing.Point(0, 0);
            this.GT_ERR_CNK.MainView = this.GV_ERR_CNK;
            this.GT_ERR_CNK.Name = "GT_ERR_CNK";
            this.GT_ERR_CNK.Size = new System.Drawing.Size(574, 260);
            this.GT_ERR_CNK.TabIndex = 0;
            this.GT_ERR_CNK.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GV_ERR_CNK});
            // 
            // GV_ERR_CNK
            // 
            this.GV_ERR_CNK.GridControl = this.GT_ERR_CNK;
            this.GV_ERR_CNK.Name = "GV_ERR_CNK";
            // 
            // ERR_CNK
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(574, 260);
            this.Controls.Add(this.GT_ERR_CNK);
            this.Name = "ERR_CNK";
            this.Text = "ERR_CNK";
            ((System.ComponentModel.ISupportInitialize)(this.GT_ERR_CNK)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_ERR_CNK)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraGrid.GridControl GT_ERR_CNK;
        private DevExpress.XtraGrid.Views.Grid.GridView GV_ERR_CNK;
    }
}