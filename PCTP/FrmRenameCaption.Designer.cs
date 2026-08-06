namespace PCTP
{
    partial class FrmRenameCaption
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
            this.txtnewName = new DevExpress.XtraEditors.TextEdit();
            this.simpleButton1 = new DevExpress.XtraEditors.SimpleButton();
            this.FrmRenameCaptionlayoutControl1ConvertedLayout = new DevExpress.XtraLayout.LayoutControl();
            this.layoutControlGroup1 = new DevExpress.XtraLayout.LayoutControlGroup();
            this.groupControl1item = new DevExpress.XtraLayout.LayoutControlGroup();
            this.groupControl2item = new DevExpress.XtraLayout.LayoutControlGroup();
            this.txtnewNameitem = new DevExpress.XtraLayout.LayoutControlItem();
            this.simpleButton1item = new DevExpress.XtraLayout.LayoutControlItem();
            ((System.ComponentModel.ISupportInitialize)(this.txtnewName.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.FrmRenameCaptionlayoutControl1ConvertedLayout)).BeginInit();
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1item)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2item)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtnewNameitem)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.simpleButton1item)).BeginInit();
            this.SuspendLayout();
            // 
            // txtnewName
            // 
            this.txtnewName.Location = new System.Drawing.Point(87, 50);
            this.txtnewName.Name = "txtnewName";
            this.txtnewName.Size = new System.Drawing.Size(335, 22);
            this.txtnewName.StyleController = this.FrmRenameCaptionlayoutControl1ConvertedLayout;
            this.txtnewName.TabIndex = 4;
            // 
            // simpleButton1
            // 
            this.simpleButton1.Location = new System.Drawing.Point(24, 76);
            this.simpleButton1.Name = "simpleButton1";
            this.simpleButton1.Size = new System.Drawing.Size(398, 27);
            this.simpleButton1.StyleController = this.FrmRenameCaptionlayoutControl1ConvertedLayout;
            this.simpleButton1.TabIndex = 5;
            this.simpleButton1.Text = "OK";
            // 
            // FrmRenameCaptionlayoutControl1ConvertedLayout
            // 
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.Controls.Add(this.txtnewName);
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.Controls.Add(this.simpleButton1);
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.Location = new System.Drawing.Point(0, 0);
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.Name = "FrmRenameCaptionlayoutControl1ConvertedLayout";
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.Root = this.layoutControlGroup1;
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.Size = new System.Drawing.Size(446, 131);
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.TabIndex = 1;
            // 
            // layoutControlGroup1
            // 
            this.layoutControlGroup1.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
            this.layoutControlGroup1.GroupBordersVisible = false;
            this.layoutControlGroup1.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.groupControl1item});
            this.layoutControlGroup1.Name = "layoutControlGroup1";
            this.layoutControlGroup1.Size = new System.Drawing.Size(446, 131);
            this.layoutControlGroup1.TextVisible = false;
            // 
            // groupControl1item
            // 
            this.groupControl1item.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.groupControl2item});
            this.groupControl1item.Location = new System.Drawing.Point(0, 0);
            this.groupControl1item.Name = "groupControl1item";
            this.groupControl1item.Size = new System.Drawing.Size(426, 111);
            this.groupControl1item.Text = "Change Header";
            // 
            // groupControl2item
            // 
            this.groupControl2item.GroupBordersVisible = false;
            this.groupControl2item.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.txtnewNameitem,
            this.simpleButton1item});
            this.groupControl2item.Location = new System.Drawing.Point(0, 0);
            this.groupControl2item.Name = "groupControl2item";
            this.groupControl2item.Size = new System.Drawing.Size(402, 61);
            this.groupControl2item.Text = "Root";
            // 
            // txtnewNameitem
            // 
            this.txtnewNameitem.Control = this.txtnewName;
            this.txtnewNameitem.Location = new System.Drawing.Point(0, 0);
            this.txtnewNameitem.Name = "txtnewNameitem";
            this.txtnewNameitem.Size = new System.Drawing.Size(402, 26);
            this.txtnewNameitem.Text = "New Name";
            this.txtnewNameitem.TextLocation = DevExpress.Utils.Locations.Left;
            this.txtnewNameitem.TextSize = new System.Drawing.Size(51, 13);
            // 
            // simpleButton1item
            // 
            this.simpleButton1item.Control = this.simpleButton1;
            this.simpleButton1item.Location = new System.Drawing.Point(0, 26);
            this.simpleButton1item.Name = "simpleButton1item";
            this.simpleButton1item.Size = new System.Drawing.Size(402, 35);
            this.simpleButton1item.TextSize = new System.Drawing.Size(0, 0);
            this.simpleButton1item.TextVisible = false;
            // 
            // FrmRenameCaption
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(446, 131);
            this.Controls.Add(this.FrmRenameCaptionlayoutControl1ConvertedLayout);
            this.MaximizeBox = false;
            this.Name = "FrmRenameCaption";
            this.Text = "FrmRenameCaption";
            ((System.ComponentModel.ISupportInitialize)(this.txtnewName.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.FrmRenameCaptionlayoutControl1ConvertedLayout)).EndInit();
            this.FrmRenameCaptionlayoutControl1ConvertedLayout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroup1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1item)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2item)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtnewNameitem)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.simpleButton1item)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.TextEdit txtnewName;
        private DevExpress.XtraLayout.LayoutControl FrmRenameCaptionlayoutControl1ConvertedLayout;
        private DevExpress.XtraEditors.SimpleButton simpleButton1;
        private DevExpress.XtraLayout.LayoutControlGroup layoutControlGroup1;
        private DevExpress.XtraLayout.LayoutControlGroup groupControl1item;
        private DevExpress.XtraLayout.LayoutControlGroup groupControl2item;
        private DevExpress.XtraLayout.LayoutControlItem txtnewNameitem;
        private DevExpress.XtraLayout.LayoutControlItem simpleButton1item;
    }
}