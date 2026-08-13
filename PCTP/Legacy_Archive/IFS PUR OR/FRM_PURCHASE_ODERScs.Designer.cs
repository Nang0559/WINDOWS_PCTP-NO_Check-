namespace PCTP.IFS_PUR_OR
{
    partial class FRM_PURCHASE_ODERScs
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FRM_PURCHASE_ODERScs));
            this.MAIN_PANEL = new DevExpress.XtraEditors.PanelControl();
            this.gridCDDH = new DevExpress.XtraGrid.GridControl();
            this.gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.documentGroup1 = new DevExpress.XtraBars.Docking2010.Views.Tabbed.DocumentGroup(this.components);
            this.document1 = new DevExpress.XtraBars.Docking2010.Views.Tabbed.Document(this.components);
            this.dockManager1 = new DevExpress.XtraBars.Docking.DockManager(this.components);
            this.hideContainerTop = new DevExpress.XtraBars.Docking.AutoHideContainer();
            this.dockPanel2 = new DevExpress.XtraBars.Docking.DockPanel();
            this.dockPanel2_Container = new DevExpress.XtraBars.Docking.ControlContainer();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.lookUpEditPartNo = new DevExpress.XtraEditors.LookUpEdit();
            this.SQLPartNo = new DevExpress.XtraEditors.LookUpEdit();
            this.lookUpEditSuplier = new DevExpress.XtraEditors.LookUpEdit();
            this.SQLSupplier = new DevExpress.XtraEditors.LookUpEdit();
            this.textPartNo = new DevExpress.XtraEditors.TextEdit();
            this.lookUpEditOrderNo = new DevExpress.XtraEditors.LookUpEdit();
            this.SQLOrderNo = new DevExpress.XtraEditors.LookUpEdit();
            this.textSuplier = new DevExpress.XtraEditors.TextEdit();
            this.dateEdit1 = new DevExpress.XtraEditors.DateEdit();
            this.textOderNo = new DevExpress.XtraEditors.TextEdit();
            this.lookUpDate1 = new DevExpress.XtraEditors.LookUpEdit();
            this.textWanteddeliverydate = new DevExpress.XtraEditors.TextEdit();
            ((System.ComponentModel.ISupportInitialize)(this.MAIN_PANEL)).BeginInit();
            this.MAIN_PANEL.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridCDDH)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.documentGroup1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.document1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dockManager1)).BeginInit();
            this.hideContainerTop.SuspendLayout();
            this.dockPanel2.SuspendLayout();
            this.dockPanel2_Container.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEditPartNo.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SQLPartNo.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEditSuplier.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SQLSupplier.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textPartNo.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEditOrderNo.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SQLOrderNo.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textSuplier.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties.CalendarTimeProperties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textOderNo.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpDate1.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textWanteddeliverydate.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // MAIN_PANEL
            // 
            this.MAIN_PANEL.Controls.Add(this.gridCDDH);
            this.MAIN_PANEL.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MAIN_PANEL.Location = new System.Drawing.Point(0, 48);
            this.MAIN_PANEL.Name = "MAIN_PANEL";
            this.MAIN_PANEL.Size = new System.Drawing.Size(1372, 560);
            this.MAIN_PANEL.TabIndex = 1;
            // 
            // gridCDDH
            // 
            this.gridCDDH.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCDDH.Location = new System.Drawing.Point(2, 2);
            this.gridCDDH.MainView = this.gridView1;
            this.gridCDDH.Name = "gridCDDH";
            this.gridCDDH.Size = new System.Drawing.Size(1368, 556);
            this.gridCDDH.TabIndex = 0;
            this.gridCDDH.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridView1});
            // 
            // gridView1
            // 
            this.gridView1.GridControl = this.gridCDDH;
            this.gridView1.Name = "gridView1";
            // 
            // document1
            // 
            this.document1.Caption = "dockPanel1";
            this.document1.ControlName = "dockPanel1";
            this.document1.FloatLocation = new System.Drawing.Point(0, 0);
            this.document1.FloatSize = new System.Drawing.Size(200, 200);
            // 
            // dockManager1
            // 
            this.dockManager1.AutoHideContainers.AddRange(new DevExpress.XtraBars.Docking.AutoHideContainer[] {
            this.hideContainerTop});
            this.dockManager1.Form = this;
            this.dockManager1.TopZIndexControls.AddRange(new string[] {
            "DevExpress.XtraBars.BarDockControl",
            "DevExpress.XtraBars.StandaloneBarDockControl",
            "System.Windows.Forms.StatusBar",
            "System.Windows.Forms.MenuStrip",
            "System.Windows.Forms.StatusStrip",
            "DevExpress.XtraBars.Ribbon.RibbonStatusBar",
            "DevExpress.XtraBars.Ribbon.RibbonControl",
            "DevExpress.XtraBars.Navigation.OfficeNavigationBar",
            "DevExpress.XtraBars.Navigation.TileNavPane",
            "DevExpress.XtraBars.TabFormControl",
            "DevExpress.XtraBars.FluentDesignSystem.FluentDesignFormControl",
            "DevExpress.XtraBars.ToolbarForm.ToolbarFormControl"});
            // 
            // hideContainerTop
            // 
            this.hideContainerTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(248)))));
            this.hideContainerTop.Controls.Add(this.dockPanel2);
            this.hideContainerTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.hideContainerTop.Location = new System.Drawing.Point(0, 0);
            this.hideContainerTop.Name = "hideContainerTop";
            this.hideContainerTop.Size = new System.Drawing.Size(1372, 48);
            // 
            // dockPanel2
            // 
            this.dockPanel2.Appearance.BackColor = System.Drawing.Color.White;
            this.dockPanel2.Appearance.BackColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(224)))), ((int)(((byte)(192)))));
            this.dockPanel2.Appearance.Options.UseBackColor = true;
            this.dockPanel2.Controls.Add(this.dockPanel2_Container);
            this.dockPanel2.Dock = DevExpress.XtraBars.Docking.DockingStyle.Top;
            this.dockPanel2.ID = new System.Guid("cdaeeae3-2eae-4841-ad37-4d62b8681d29");
            this.dockPanel2.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("dockPanel2.ImageOptions.Image")));
            this.dockPanel2.Location = new System.Drawing.Point(0, 0);
            this.dockPanel2.Name = "dockPanel2";
            this.dockPanel2.OriginalSize = new System.Drawing.Size(200, 227);
            this.dockPanel2.SavedDock = DevExpress.XtraBars.Docking.DockingStyle.Top;
            this.dockPanel2.SavedIndex = 0;
            this.dockPanel2.Size = new System.Drawing.Size(1372, 227);
            this.dockPanel2.Visibility = DevExpress.XtraBars.Docking.DockVisibility.AutoHide;
            // 
            // dockPanel2_Container
            // 
            this.dockPanel2_Container.Controls.Add(this.label4);
            this.dockPanel2_Container.Controls.Add(this.label3);
            this.dockPanel2_Container.Controls.Add(this.label2);
            this.dockPanel2_Container.Controls.Add(this.label1);
            this.dockPanel2_Container.Controls.Add(this.lookUpEditPartNo);
            this.dockPanel2_Container.Controls.Add(this.SQLPartNo);
            this.dockPanel2_Container.Controls.Add(this.lookUpEditSuplier);
            this.dockPanel2_Container.Controls.Add(this.SQLSupplier);
            this.dockPanel2_Container.Controls.Add(this.textPartNo);
            this.dockPanel2_Container.Controls.Add(this.lookUpEditOrderNo);
            this.dockPanel2_Container.Controls.Add(this.SQLOrderNo);
            this.dockPanel2_Container.Controls.Add(this.textSuplier);
            this.dockPanel2_Container.Controls.Add(this.dateEdit1);
            this.dockPanel2_Container.Controls.Add(this.textOderNo);
            this.dockPanel2_Container.Controls.Add(this.lookUpDate1);
            this.dockPanel2_Container.Controls.Add(this.textWanteddeliverydate);
            this.dockPanel2_Container.Location = new System.Drawing.Point(4, 56);
            this.dockPanel2_Container.Name = "dockPanel2_Container";
            this.dockPanel2_Container.Size = new System.Drawing.Size(1364, 165);
            this.dockPanel2_Container.TabIndex = 0;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(667, 93);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 18);
            this.label4.TabIndex = 2;
            this.label4.Text = "PART NO :";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(667, 28);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(80, 18);
            this.label3.TabIndex = 2;
            this.label3.Text = "Supplier :";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(8, 90);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(99, 18);
            this.label2.TabIndex = 2;
            this.label2.Text = "ORDER NO :";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(3, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(211, 18);
            this.label1.TabIndex = 2;
            this.label1.Text = " WANTED DELIVERY DATE :";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lookUpEditPartNo
            // 
            this.lookUpEditPartNo.Location = new System.Drawing.Point(1120, 89);
            this.lookUpEditPartNo.Name = "lookUpEditPartNo";
            this.lookUpEditPartNo.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lookUpEditPartNo.Properties.NullText = "[Chọn Part No]";
            this.lookUpEditPartNo.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            this.lookUpEditPartNo.Size = new System.Drawing.Size(236, 22);
            this.lookUpEditPartNo.TabIndex = 1;
            // 
            // SQLPartNo
            // 
            this.SQLPartNo.Location = new System.Drawing.Point(1073, 90);
            this.SQLPartNo.Name = "SQLPartNo";
            this.SQLPartNo.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.SQLPartNo.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            this.SQLPartNo.Size = new System.Drawing.Size(41, 22);
            this.SQLPartNo.TabIndex = 1;
            // 
            // lookUpEditSuplier
            // 
            this.lookUpEditSuplier.Location = new System.Drawing.Point(1120, 25);
            this.lookUpEditSuplier.Name = "lookUpEditSuplier";
            this.lookUpEditSuplier.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lookUpEditSuplier.Properties.NullText = "[Chọn Supplier]";
            this.lookUpEditSuplier.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            this.lookUpEditSuplier.Size = new System.Drawing.Size(236, 22);
            this.lookUpEditSuplier.TabIndex = 1;
            // 
            // SQLSupplier
            // 
            this.SQLSupplier.Location = new System.Drawing.Point(1073, 26);
            this.SQLSupplier.Name = "SQLSupplier";
            this.SQLSupplier.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.SQLSupplier.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            this.SQLSupplier.Size = new System.Drawing.Size(41, 22);
            this.SQLSupplier.TabIndex = 1;
            // 
            // textPartNo
            // 
            this.textPartNo.Location = new System.Drawing.Point(783, 89);
            this.textPartNo.Name = "textPartNo";
            this.textPartNo.Properties.Appearance.Options.UseTextOptions = true;
            this.textPartNo.Properties.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.textPartNo.Size = new System.Drawing.Size(284, 22);
            this.textPartNo.TabIndex = 1;
            // 
            // lookUpEditOrderNo
            // 
            this.lookUpEditOrderNo.Location = new System.Drawing.Point(418, 86);
            this.lookUpEditOrderNo.Name = "lookUpEditOrderNo";
            this.lookUpEditOrderNo.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lookUpEditOrderNo.Properties.NullText = "[Chọn OrderNo]";
            this.lookUpEditOrderNo.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            this.lookUpEditOrderNo.Size = new System.Drawing.Size(160, 22);
            this.lookUpEditOrderNo.TabIndex = 1;
            // 
            // SQLOrderNo
            // 
            this.SQLOrderNo.Location = new System.Drawing.Point(371, 85);
            this.SQLOrderNo.Name = "SQLOrderNo";
            this.SQLOrderNo.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.SQLOrderNo.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            this.SQLOrderNo.Size = new System.Drawing.Size(41, 22);
            this.SQLOrderNo.TabIndex = 1;
            // 
            // textSuplier
            // 
            this.textSuplier.Location = new System.Drawing.Point(783, 27);
            this.textSuplier.Name = "textSuplier";
            this.textSuplier.Properties.Appearance.Options.UseTextOptions = true;
            this.textSuplier.Properties.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.textSuplier.Size = new System.Drawing.Size(284, 22);
            this.textSuplier.TabIndex = 1;
            // 
            // dateEdit1
            // 
            this.dateEdit1.EditValue = null;
            this.dateEdit1.Location = new System.Drawing.Point(523, 22);
            this.dateEdit1.Name = "dateEdit1";
            this.dateEdit1.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateEdit1.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateEdit1.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            this.dateEdit1.Properties.EditValueChanged += new System.EventHandler(this.dateEdit1_Properties_EditValueChanged);
            this.dateEdit1.Size = new System.Drawing.Size(51, 22);
            this.dateEdit1.TabIndex = 0;
            // 
            // textOderNo
            // 
            this.textOderNo.Location = new System.Drawing.Point(125, 86);
            this.textOderNo.Name = "textOderNo";
            this.textOderNo.Properties.Appearance.Options.UseTextOptions = true;
            this.textOderNo.Properties.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.textOderNo.Size = new System.Drawing.Size(240, 22);
            this.textOderNo.TabIndex = 1;
            // 
            // lookUpDate1
            // 
            this.lookUpDate1.Location = new System.Drawing.Point(482, 22);
            this.lookUpDate1.Name = "lookUpDate1";
            this.lookUpDate1.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lookUpDate1.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.HideTextEditor;
            this.lookUpDate1.Properties.EditValueChanged += new System.EventHandler(this.lookUpDate1_Properties_EditValueChanged);
            this.lookUpDate1.Size = new System.Drawing.Size(41, 22);
            this.lookUpDate1.TabIndex = 1;
            // 
            // textWanteddeliverydate
            // 
            this.textWanteddeliverydate.Location = new System.Drawing.Point(241, 22);
            this.textWanteddeliverydate.Name = "textWanteddeliverydate";
            this.textWanteddeliverydate.Properties.Appearance.Options.UseTextOptions = true;
            this.textWanteddeliverydate.Properties.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.textWanteddeliverydate.Size = new System.Drawing.Size(241, 22);
            this.textWanteddeliverydate.TabIndex = 1;
            // 
            // FRM_PURCHASE_ODERScs
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1372, 608);
            this.Controls.Add(this.MAIN_PANEL);
            this.Controls.Add(this.hideContainerTop);
            this.Name = "FRM_PURCHASE_ODERScs";
            this.Text = "PURCHASE ODERS";
            ((System.ComponentModel.ISupportInitialize)(this.MAIN_PANEL)).EndInit();
            this.MAIN_PANEL.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridCDDH)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.documentGroup1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.document1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dockManager1)).EndInit();
            this.hideContainerTop.ResumeLayout(false);
            this.dockPanel2.ResumeLayout(false);
            this.dockPanel2_Container.ResumeLayout(false);
            this.dockPanel2_Container.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEditPartNo.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.SQLPartNo.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEditSuplier.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.SQLSupplier.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textPartNo.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpEditOrderNo.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.SQLOrderNo.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textSuplier.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateEdit1.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textOderNo.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.lookUpDate1.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textWanteddeliverydate.Properties)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private DevExpress.XtraEditors.PanelControl MAIN_PANEL;
        private DevExpress.XtraGrid.GridControl gridCDDH;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraBars.Docking2010.Views.Tabbed.DocumentGroup documentGroup1;
        private DevExpress.XtraBars.Docking2010.Views.Tabbed.Document document1;
        private DevExpress.XtraBars.Docking.DockManager dockManager1;
        private DevExpress.XtraBars.Docking.DockPanel dockPanel2;
        private DevExpress.XtraBars.Docking.ControlContainer dockPanel2_Container;
        private DevExpress.XtraEditors.DateEdit dateEdit1;
        private DevExpress.XtraEditors.LookUpEdit lookUpDate1;
        private DevExpress.XtraEditors.TextEdit textWanteddeliverydate;
        private DevExpress.XtraEditors.LookUpEdit SQLPartNo;
        private DevExpress.XtraEditors.LookUpEdit SQLSupplier;
        private DevExpress.XtraEditors.TextEdit textPartNo;
        private DevExpress.XtraEditors.LookUpEdit SQLOrderNo;
        private DevExpress.XtraEditors.TextEdit textSuplier;
        private DevExpress.XtraEditors.TextEdit textOderNo;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private DevExpress.XtraEditors.LookUpEdit lookUpEditSuplier;
        private DevExpress.XtraEditors.LookUpEdit lookUpEditPartNo;
        private DevExpress.XtraEditors.LookUpEdit lookUpEditOrderNo;
        private DevExpress.XtraBars.Docking.AutoHideContainer hideContainerTop;
    }
}