namespace PCTP
{
    partial class UF_NHAPLAI_NG
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
            this.GW_NG = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.GW_NG)).BeginInit();
            this.SuspendLayout();
            // 
            // GW_NG
            // 
            this.GW_NG.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.GW_NG.Location = new System.Drawing.Point(12, 22);
            this.GW_NG.Name = "GW_NG";
            this.GW_NG.RowHeadersWidth = 51;
            this.GW_NG.RowTemplate.Height = 24;
            this.GW_NG.Size = new System.Drawing.Size(916, 277);
            this.GW_NG.TabIndex = 0;
            this.GW_NG.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.GW_NG_CellContentClick);
            this.GW_NG.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.GW_NG_CellContentDoubleClick);
            this.GW_NG.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.GW_NG_CellEndEdit);
            this.GW_NG.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.GW_NG_CellEnter);
            this.GW_NG.DoubleClick += new System.EventHandler(this.GW_NG_DoubleClick);
            // 
            // UF_NHAPLAI_NG
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(940, 315);
            this.Controls.Add(this.GW_NG);
            this.Name = "UF_NHAPLAI_NG";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NHẬP LẠI NG";
            this.Load += new System.EventHandler(this.UF_NHAPLAI_NG_Load);
            ((System.ComponentModel.ISupportInitialize)(this.GW_NG)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView GW_NG;
    }
}