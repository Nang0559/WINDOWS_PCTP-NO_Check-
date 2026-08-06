
namespace PCTP.QRCODE_HVN.PGH.FuctionQR
{
    partial class UF_QRCodeERR
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
            this.components = new System.ComponentModel.Container();
            DevExpress.XtraBars.Docking2010.WindowsUIButtonImageOptions windowsUIButtonImageOptions1 = new DevExpress.XtraBars.Docking2010.WindowsUIButtonImageOptions();
            DevExpress.XtraBars.Docking2010.WindowsUIButtonImageOptions windowsUIButtonImageOptions2 = new DevExpress.XtraBars.Docking2010.WindowsUIButtonImageOptions();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UF_QRCodeERR));
            this.UIP_BT = new DevExpress.XtraBars.Docking2010.WindowsUIButtonPanel();
            this.imageCollection1 = new DevExpress.Utils.ImageCollection(this.components);
            this.panelControl1 = new DevExpress.XtraEditors.PanelControl();
            this.GT_QR = new DevExpress.XtraGrid.GridControl();
            this.GV_QR = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.imageCollection2 = new DevExpress.Utils.ImageCollection(this.components);
            this.dateEdit1 = new DevExpress.XtraEditors.DateEdit();
            this.UIP_BT.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).BeginInit();
            this.panelControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GT_QR)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_QR)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties.CalendarTimeProperties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // UIP_BT
            // 
            windowsUIButtonImageOptions1.ImageUri.Uri = "Cut";
            windowsUIButtonImageOptions2.ImageUri.Uri = "Paste";
            this.UIP_BT.Buttons.AddRange(new DevExpress.XtraEditors.ButtonPanel.IBaseButton[] {
            new DevExpress.XtraBars.Docking2010.WindowsUIButton("CUT", true, windowsUIButtonImageOptions1, DevExpress.XtraBars.Docking2010.ButtonStyle.PushButton, "", -1, true, null, true, false, true, null, -1, false),
            new DevExpress.XtraBars.Docking2010.WindowsUIButton("PASTE", true, windowsUIButtonImageOptions2, DevExpress.XtraBars.Docking2010.ButtonStyle.PushButton, "", -1, true, null, true, false, true, null, -1, false)});
            this.UIP_BT.Controls.Add(this.dateEdit1);
            this.UIP_BT.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.UIP_BT.Images = this.imageCollection1;
            this.UIP_BT.Location = new System.Drawing.Point(0, 602);
            this.UIP_BT.Name = "UIP_BT";
            this.UIP_BT.Size = new System.Drawing.Size(1559, 86);
            this.UIP_BT.TabIndex = 1;
            this.UIP_BT.Text = "windowsUIButtonPanel1";
            this.UIP_BT.ButtonClick += new DevExpress.XtraBars.Docking2010.ButtonEventHandler(this.UIP_BT_ButtonClick);
            // 
            // imageCollection1
            // 
            this.imageCollection1.ImageStream = ((DevExpress.Utils.ImageCollectionStreamer)(resources.GetObject("imageCollection1.ImageStream")));
            this.imageCollection1.Images.SetKeyName(0, "cut_32x32.png");
            this.imageCollection1.Images.SetKeyName(1, "pastespecial_32x32.png");
            // 
            // panelControl1
            // 
            this.panelControl1.Controls.Add(this.GT_QR);
            this.panelControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelControl1.Location = new System.Drawing.Point(0, 0);
            this.panelControl1.Name = "panelControl1";
            this.panelControl1.Size = new System.Drawing.Size(1559, 602);
            this.panelControl1.TabIndex = 2;
            // 
            // GT_QR
            // 
            this.GT_QR.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GT_QR.Location = new System.Drawing.Point(2, 2);
            this.GT_QR.MainView = this.GV_QR;
            this.GT_QR.Name = "GT_QR";
            this.GT_QR.Size = new System.Drawing.Size(1555, 598);
            this.GT_QR.TabIndex = 1;
            this.GT_QR.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GV_QR});
            // 
            // GV_QR
            // 
            this.GV_QR.GridControl = this.GT_QR;
            this.GV_QR.Name = "GV_QR";
            // 
            // imageCollection2
            // 
            this.imageCollection2.ImageStream = ((DevExpress.Utils.ImageCollectionStreamer)(resources.GetObject("imageCollection2.ImageStream")));
            this.imageCollection2.Images.SetKeyName(0, "cut_16x16.png");
            this.imageCollection2.Images.SetKeyName(1, "pastespecial_16x16.png");
            // 
            // dateEdit1
            // 
            this.dateEdit1.EditValue = null;
            this.dateEdit1.Location = new System.Drawing.Point(164, 23);
            this.dateEdit1.Name = "dateEdit1";
            this.dateEdit1.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateEdit1.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateEdit1.Size = new System.Drawing.Size(229, 22);
            this.dateEdit1.TabIndex = 0;
            // 
            // UF_QRCodeERR
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1559, 688);
            this.Controls.Add(this.panelControl1);
            this.Controls.Add(this.UIP_BT);
            this.Name = "UF_QRCodeERR";
            this.Text = "UF_QRCodeERR";
            this.UIP_BT.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.panelControl1)).EndInit();
            this.panelControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GT_QR)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_QR)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private DevExpress.XtraBars.Docking2010.WindowsUIButtonPanel UIP_BT;
        private DevExpress.XtraEditors.PanelControl panelControl1;
        private DevExpress.XtraGrid.GridControl GT_QR;
        private DevExpress.XtraGrid.Views.Grid.GridView GV_QR;
        private DevExpress.Utils.ImageCollection imageCollection1;
        private DevExpress.Utils.ImageCollection imageCollection2;
        private DevExpress.XtraEditors.DateEdit dateEdit1;
    }
}