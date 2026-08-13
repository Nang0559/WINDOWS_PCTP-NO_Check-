namespace PCTP.QRCODE_HVN.PGH
{
    partial class LOT_EDIT
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LOT_EDIT));
            this.panelControl1 = new DevExpress.XtraEditors.PanelControl();
            this.panelControl2 = new DevExpress.XtraEditors.PanelControl();
            this.simpleButton4 = new DevExpress.XtraEditors.SimpleButton();
            this.simpleButton3 = new DevExpress.XtraEditors.SimpleButton();
            this.memoExEdit1 = new DevExpress.XtraEditors.MemoExEdit();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).BeginInit();
            this.panelControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl2)).BeginInit();
            this.panelControl2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.memoExEdit1.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // panelControl1
            // 
            this.SetBoundPropertyName(this.panelControl1, "");
            this.panelControl1.Controls.Add(this.label1);
            this.panelControl1.Controls.Add(this.memoExEdit1);
            this.panelControl1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelControl1.Location = new System.Drawing.Point(0, 0);
            this.panelControl1.Name = "panelControl1";
            this.panelControl1.Size = new System.Drawing.Size(749, 112);
            this.panelControl1.TabIndex = 0;
            // 
            // panelControl2
            // 
            this.SetBoundPropertyName(this.panelControl2, "");
            this.panelControl2.Controls.Add(this.simpleButton4);
            this.panelControl2.Controls.Add(this.simpleButton3);
            this.panelControl2.Location = new System.Drawing.Point(0, 118);
            this.panelControl2.Name = "panelControl2";
            this.panelControl2.Size = new System.Drawing.Size(749, 56);
            this.panelControl2.TabIndex = 3;
            // 
            // simpleButton4
            // 
            this.SetBoundPropertyName(this.simpleButton4, "");
            this.simpleButton4.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton4.ImageOptions.Image")));
            this.simpleButton4.Location = new System.Drawing.Point(418, 16);
            this.simpleButton4.Name = "simpleButton4";
            this.simpleButton4.Size = new System.Drawing.Size(95, 31);
            this.simpleButton4.TabIndex = 4;
            this.simpleButton4.Text = "Cancel";
            this.simpleButton4.Click += new System.EventHandler(this.simpleButton4_Click);
            // 
            // simpleButton3
            // 
            this.SetBoundPropertyName(this.simpleButton3, "");
            this.simpleButton3.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton3.ImageOptions.Image")));
            this.simpleButton3.Location = new System.Drawing.Point(260, 16);
            this.simpleButton3.Name = "simpleButton3";
            this.simpleButton3.Size = new System.Drawing.Size(95, 31);
            this.simpleButton3.TabIndex = 4;
            this.simpleButton3.Text = "Update";
            this.simpleButton3.Click += new System.EventHandler(this.simpleButton3_Click);
            // 
            // memoExEdit1
            // 
            this.SetBoundFieldName(this.memoExEdit1, "Note");
            this.SetBoundPropertyName(this.memoExEdit1, "EditValue");
            this.memoExEdit1.Location = new System.Drawing.Point(30, 57);
            this.memoExEdit1.Name = "memoExEdit1";
            this.memoExEdit1.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.memoExEdit1.Size = new System.Drawing.Size(692, 22);
            this.memoExEdit1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.SetBoundPropertyName(this.label1, "");
            this.label1.Font = new System.Drawing.Font("Times New Roman", 22.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(263, 11);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(211, 43);
            this.label1.TabIndex = 2;
            this.label1.Text = "GHI CHÚ :";
            // 
            // LOT_EDIT
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelControl2);
            this.Controls.Add(this.panelControl1);
            this.Name = "LOT_EDIT";
            this.Size = new System.Drawing.Size(749, 182);
            this.Load += new System.EventHandler(this.LOT_EDIT_Load);
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).EndInit();
            this.panelControl1.ResumeLayout(false);
            this.panelControl1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl2)).EndInit();
            this.panelControl2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.memoExEdit1.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.PanelControl panelControl1;
        private DevExpress.XtraEditors.PanelControl panelControl2;
        private DevExpress.XtraEditors.SimpleButton simpleButton4;
        private DevExpress.XtraEditors.SimpleButton simpleButton3;
        private System.Windows.Forms.Label label1;
        private DevExpress.XtraEditors.MemoExEdit memoExEdit1;
    }
}
