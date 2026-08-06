namespace PCTP.QRCODE_HVN
{
    partial class THEMPDB
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(THEMPDB));
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.GGFCC = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtTP = new DevExpress.XtraEditors.TextEdit();
            this.sidePanel2 = new DevExpress.XtraEditors.SidePanel();
            this.simpleButton5 = new DevExpress.XtraEditors.SimpleButton();
            this.CMDOK = new DevExpress.XtraEditors.SimpleButton();
            this.lookUpEdit1 = new DevExpress.XtraEditors.LookUpEdit();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            this.sidePanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.txtTP.Properties)).BeginInit();
            this.sidePanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEdit1.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // sidePanel1
            // 
            this.sidePanel1.Controls.Add(this.GGFCC);
            this.sidePanel1.Controls.Add(this.label2);
            this.sidePanel1.Controls.Add(this.label1);
            this.sidePanel1.Controls.Add(this.txtTP);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.sidePanel1.Location = new System.Drawing.Point(406, 0);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(472, 181);
            this.sidePanel1.TabIndex = 4;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // GGFCC
            // 
            this.GGFCC.FormattingEnabled = true;
            this.GGFCC.Location = new System.Drawing.Point(220, 91);
            this.GGFCC.Name = "GGFCC";
            this.GGFCC.Size = new System.Drawing.Size(216, 21);
            this.GGFCC.TabIndex = 10;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Times New Roman", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(54, 91);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(160, 26);
            this.label2.TabIndex = 6;
            this.label2.Text = "Giờ Giao FCC";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Times New Roman", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(54, 39);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(116, 26);
            this.label1.TabIndex = 7;
            this.label1.Text = "Tên Phiếu";
            // 
            // txtTP
            // 
            this.txtTP.Location = new System.Drawing.Point(190, 44);
            this.txtTP.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtTP.Name = "txtTP";
            this.txtTP.Properties.Appearance.Font = new System.Drawing.Font("Times New Roman", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtTP.Properties.Appearance.Options.UseFont = true;
            this.txtTP.Size = new System.Drawing.Size(253, 26);
            this.txtTP.TabIndex = 4;
            // 
            // sidePanel2
            // 
            this.sidePanel2.Controls.Add(this.simpleButton5);
            this.sidePanel2.Controls.Add(this.CMDOK);
            this.sidePanel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.sidePanel2.Location = new System.Drawing.Point(0, 138);
            this.sidePanel2.Name = "sidePanel2";
            this.sidePanel2.Size = new System.Drawing.Size(406, 43);
            this.sidePanel2.TabIndex = 5;
            this.sidePanel2.Text = "sidePanel2";
            // 
            // simpleButton5
            // 
            this.simpleButton5.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton5.ImageOptions.Image")));
            this.simpleButton5.Location = new System.Drawing.Point(222, 2);
            this.simpleButton5.Name = "simpleButton5";
            this.simpleButton5.Size = new System.Drawing.Size(142, 39);
            this.simpleButton5.TabIndex = 13;
            this.simpleButton5.Text = "Xem Lại";
            this.simpleButton5.Click += new System.EventHandler(this.simpleButton5_Click_1);
            // 
            // CMDOK
            // 
            this.CMDOK.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("CMDOK.ImageOptions.Image")));
            this.CMDOK.Location = new System.Drawing.Point(25, 3);
            this.CMDOK.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CMDOK.Name = "CMDOK";
            this.CMDOK.Size = new System.Drawing.Size(112, 37);
            this.CMDOK.TabIndex = 3;
            this.CMDOK.Text = "OK";
            this.CMDOK.Click += new System.EventHandler(this.CMDOK_Click_1);
            // 
            // lookUpEdit1
            // 
            this.lookUpEdit1.Location = new System.Drawing.Point(25, 31);
            this.lookUpEdit1.Name = "lookUpEdit1";
            this.lookUpEdit1.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lookUpEdit1.Properties.DisplayMember = "IDP";
            this.lookUpEdit1.Properties.ValueMember = "IDP";
            this.lookUpEdit1.Size = new System.Drawing.Size(375, 22);
            this.lookUpEdit1.TabIndex = 6;
            this.lookUpEdit1.EditValueChanged += new System.EventHandler(this.lookUpEdit1_EditValueChanged);
            // 
            // simpleButton1
            // 
            this.simpleButton1.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("simpleButton1.ImageOptions.Image")));
            this.simpleButton1.Location = new System.Drawing.Point(134, 59);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(113, 36);
            this.simpleButton1.TabIndex = 7;
            this.simpleButton1.Text = "Thêm Mới";
            this.simpleButton1.Click += new System.EventHandler(this.simpleButton1_Click);
            // 
            // THEMPDB
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(878, 181);
            this.Controls.Add(this.simpleButton1);
            this.Controls.Add(this.lookUpEdit1);
            this.Controls.Add(this.sidePanel2);
            this.Controls.Add(this.sidePanel1);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "THEMPDB";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "THÊM PHIẾU GIAO ĐẶC BIỆT";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.THEMPDB_FormClosing);
            this.Load += new System.EventHandler(this.THEMPDB_Load);
            this.sidePanel1.ResumeLayout(false);
            this.sidePanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.txtTP.Properties)).EndInit();
            this.sidePanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEdit1.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SidePanel sidePanel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private DevExpress.XtraEditors.TextEdit txtTP;
        private DevExpress.XtraEditors.SidePanel sidePanel2;
        private DevExpress.XtraEditors.SimpleButton CMDOK;
        private DevExpress.XtraEditors.LookUpEdit lookUpEdit1;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
        private DevExpress.XtraEditors.SimpleButton simpleButton5;
        private System.Windows.Forms.ComboBox GGFCC;
    }
}