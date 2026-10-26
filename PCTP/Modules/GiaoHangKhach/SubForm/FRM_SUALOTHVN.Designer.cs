namespace PCTP.QRCODE_HVN
{
    partial class FRM_SUALOTHVN
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
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            this.LOTBD = new System.Windows.Forms.Label();
            this.sidePanel2 = new DevExpress.XtraEditors.SidePanel();
            this.gridCtrSUALOTHVN = new DevExpress.XtraGrid.GridControl();
            this.gridVSUALOTHVN = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.sidePanel1.SuspendLayout();
            this.sidePanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrSUALOTHVN)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVSUALOTHVN)).BeginInit();
            this.SuspendLayout();
            // 
            // sidePanel1
            // 
            this.sidePanel1.Controls.Add(this.simpleButton1);
            this.sidePanel1.Controls.Add(this.LOTBD);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.sidePanel1.Location = new System.Drawing.Point(0, 539);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(1212, 48);
            this.sidePanel1.TabIndex = 3;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // simpleButton1
            // 
            this.simpleButton1.Location = new System.Drawing.Point(1007, 4);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(205, 45);
            this.simpleButton1.TabIndex = 4;
            this.simpleButton1.Text = "SỬA LOT HVN";
            this.simpleButton1.Click += new System.EventHandler(this.simpleButton1_Click);
            // 
            // LOTBD
            // 
            this.LOTBD.AutoSize = true;
            this.LOTBD.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.LOTBD.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.LOTBD.Font = new System.Drawing.Font("Tahoma", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LOTBD.Location = new System.Drawing.Point(0, 12);
            this.LOTBD.Name = "LOTBD";
            this.LOTBD.Size = new System.Drawing.Size(2, 36);
            this.LOTBD.TabIndex = 3;
            this.LOTBD.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // sidePanel2
            // 
            this.sidePanel2.Controls.Add(this.gridCtrSUALOTHVN);
            this.sidePanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidePanel2.Location = new System.Drawing.Point(0, 0);
            this.sidePanel2.Name = "sidePanel2";
            this.sidePanel2.Size = new System.Drawing.Size(1212, 539);
            this.sidePanel2.TabIndex = 4;
            this.sidePanel2.Text = "sidePanel2";
            // 
            // gridCtrSUALOTHVN
            // 
            this.gridCtrSUALOTHVN.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCtrSUALOTHVN.Location = new System.Drawing.Point(0, 0);
            this.gridCtrSUALOTHVN.MainView = this.gridVSUALOTHVN;
            this.gridCtrSUALOTHVN.Name = "gridCtrSUALOTHVN";
            this.gridCtrSUALOTHVN.Size = new System.Drawing.Size(1212, 539);
            this.gridCtrSUALOTHVN.TabIndex = 1;
            this.gridCtrSUALOTHVN.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridVSUALOTHVN});
            this.gridCtrSUALOTHVN.DoubleClick += new System.EventHandler(this.gridCtrSUALOTHVN_DoubleClick);
            // 
            // gridVSUALOTHVN
            // 
            this.gridVSUALOTHVN.GridControl = this.gridCtrSUALOTHVN;
            this.gridVSUALOTHVN.Name = "gridVSUALOTHVN";
            this.gridVSUALOTHVN.OptionsBehavior.Editable = false;
            this.gridVSUALOTHVN.OptionsSelection.MultiSelect = true;
            this.gridVSUALOTHVN.OptionsView.ShowFooter = true;
            // 
            // FRM_SUALOTHVN
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1212, 587);
            this.Controls.Add(this.sidePanel2);
            this.Controls.Add(this.sidePanel1);
            this.Name = "FRM_SUALOTHVN";
            this.Text = "FRM_SUALOTHVN";
            this.Load += new System.EventHandler(this.FRM_SUALOTHVN_Load);
            this.sidePanel1.ResumeLayout(false);
            this.sidePanel1.PerformLayout();
            this.sidePanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrSUALOTHVN)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVSUALOTHVN)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SidePanel sidePanel1;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
        private System.Windows.Forms.Label LOTBD;
        private DevExpress.XtraEditors.SidePanel sidePanel2;
        private DevExpress.XtraGrid.GridControl gridCtrSUALOTHVN;
        private DevExpress.XtraGrid.Views.Grid.GridView gridVSUALOTHVN;
    }
}