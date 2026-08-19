
namespace PCTP.QRCODE_HVN.ComaprePart
{
    partial class ComaparePart
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ComaparePart));
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.label5 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.DateE_NgayAPP = new DevExpress.XtraEditors.DateEdit();
            this.LuKToPart = new DevExpress.XtraEditors.LookUpEdit();
            this.lup_Ma = new DevExpress.XtraEditors.LookUpEdit();
            this.sidePanel2 = new DevExpress.XtraEditors.SidePanel();
            this.cmd_Huy = new DevExpress.XtraEditors.SimpleButton();
            this.cmd_Tao = new DevExpress.XtraEditors.SimpleButton();
            this.sidePanel3 = new DevExpress.XtraEditors.SidePanel();
            this.GT_ListPart = new DevExpress.XtraGrid.GridControl();
            this.GV_ListPart = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.gCT_STT = new DevExpress.XtraGrid.Columns.GridColumn();
            this.GC_PartNo = new DevExpress.XtraGrid.Columns.GridColumn();
            this.GC_PartName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.GC_ToPartNo = new DevExpress.XtraGrid.Columns.GridColumn();
            this.GC_ToPartName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.GC_DateApprov = new DevExpress.XtraGrid.Columns.GridColumn();
            this.GCT = new DevExpress.XtraGrid.Columns.GridColumn();
            this.imageCollection1 = new DevExpress.Utils.ImageCollection(this.components);
            this.sidePanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DateE_NgayAPP.Properties.CalendarTimeProperties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.DateE_NgayAPP.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.LuKToPart.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.lup_Ma.Properties)).BeginInit();
            this.sidePanel2.SuspendLayout();
            this.sidePanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.GT_ListPart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_ListPart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection1)).BeginInit();
            this.SuspendLayout();
            // 
            // sidePanel1
            // 
            this.sidePanel1.Controls.Add(this.label5);
            this.sidePanel1.Controls.Add(this.label3);
            this.sidePanel1.Controls.Add(this.label4);
            this.sidePanel1.Controls.Add(this.label2);
            this.sidePanel1.Controls.Add(this.label1);
            this.sidePanel1.Controls.Add(this.DateE_NgayAPP);
            this.sidePanel1.Controls.Add(this.LuKToPart);
            this.sidePanel1.Controls.Add(this.lup_Ma);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.sidePanel1.Location = new System.Drawing.Point(0, 0);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(961, 115);
            this.sidePanel1.TabIndex = 1;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(462, 88);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(118, 19);
            this.label5.TabIndex = 5;
            this.label5.Text = "Date Approve :";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(111, 72);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(73, 19);
            this.label3.TabIndex = 5;
            this.label3.Text = "Mã HVN";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Times New Roman", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(431, 48);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(177, 19);
            this.label4.TabIndex = 5;
            this.label4.Text = "To Part No : (Mã FCC)";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.label2.Font = new System.Drawing.Font("Yu Gothic", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(323, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(86, 31);
            this.label2.TabIndex = 4;
            this.label2.Text = "≫≫≫";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Times New Roman", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(45, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(243, 23);
            this.label1.TabIndex = 3;
            this.label1.Text = "Chọn Mã Muốn Chuyển Đổi";
            // 
            // DateE_NgayAPP
            // 
            this.DateE_NgayAPP.EditValue = null;
            this.DateE_NgayAPP.Location = new System.Drawing.Point(589, 87);
            this.DateE_NgayAPP.Name = "DateE_NgayAPP";
            this.DateE_NgayAPP.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.DateE_NgayAPP.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.DateE_NgayAPP.Size = new System.Drawing.Size(318, 22);
            this.DateE_NgayAPP.TabIndex = 2;
            // 
            // LuKToPart
            // 
            this.LuKToPart.Location = new System.Drawing.Point(631, 47);
            this.LuKToPart.Name = "LuKToPart";
            this.LuKToPart.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.LuKToPart.Properties.DisplayMember = "code";
            this.LuKToPart.Properties.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoSuggest;
            this.LuKToPart.Properties.ValueMember = "code";
            this.LuKToPart.Size = new System.Drawing.Size(214, 22);
            this.LuKToPart.TabIndex = 0;
            // 
            // lup_Ma
            // 
            this.lup_Ma.Location = new System.Drawing.Point(49, 47);
            this.lup_Ma.Name = "lup_Ma";
            this.lup_Ma.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.lup_Ma.Properties.DisplayMember = "code";
            this.lup_Ma.Properties.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoSuggest;
            this.lup_Ma.Properties.ValueMember = "name";
            this.lup_Ma.Size = new System.Drawing.Size(214, 22);
            this.lup_Ma.TabIndex = 0;
            this.lup_Ma.EditValueChanged += new System.EventHandler(this.lup_Ma_EditValueChanged);
            // 
            // sidePanel2
            // 
            this.sidePanel2.Controls.Add(this.cmd_Huy);
            this.sidePanel2.Controls.Add(this.cmd_Tao);
            this.sidePanel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.sidePanel2.Location = new System.Drawing.Point(0, 397);
            this.sidePanel2.Name = "sidePanel2";
            this.sidePanel2.Size = new System.Drawing.Size(961, 91);
            this.sidePanel2.TabIndex = 2;
            this.sidePanel2.Text = "sidePanel2";
            // 
            // cmd_Huy
            // 
            this.cmd_Huy.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("cmd_Huy.ImageOptions.Image")));
            this.cmd_Huy.Location = new System.Drawing.Point(589, 30);
            this.cmd_Huy.Name = "cmd_Huy";
            this.cmd_Huy.Size = new System.Drawing.Size(158, 35);
            this.cmd_Huy.TabIndex = 0;
            this.cmd_Huy.Text = "Hủy Thao Tác";
            // 
            // cmd_Tao
            // 
            this.cmd_Tao.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("cmd_Tao.ImageOptions.Image")));
            this.cmd_Tao.Location = new System.Drawing.Point(205, 30);
            this.cmd_Tao.Name = "cmd_Tao";
            this.cmd_Tao.Size = new System.Drawing.Size(158, 35);
            this.cmd_Tao.TabIndex = 0;
            this.cmd_Tao.Text = "Tạo";
            this.cmd_Tao.Click += new System.EventHandler(this.cmd_Tao_Click);
            // 
            // sidePanel3
            // 
            this.sidePanel3.Controls.Add(this.GT_ListPart);
            this.sidePanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidePanel3.Location = new System.Drawing.Point(0, 115);
            this.sidePanel3.Name = "sidePanel3";
            this.sidePanel3.Size = new System.Drawing.Size(961, 282);
            this.sidePanel3.TabIndex = 3;
            this.sidePanel3.Text = "sidePanel3";
            // 
            // GT_ListPart
            // 
            this.GT_ListPart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GT_ListPart.Location = new System.Drawing.Point(0, 0);
            this.GT_ListPart.MainView = this.GV_ListPart;
            this.GT_ListPart.Name = "GT_ListPart";
            this.GT_ListPart.Size = new System.Drawing.Size(961, 282);
            this.GT_ListPart.TabIndex = 1;
            this.GT_ListPart.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GV_ListPart});
            // 
            // GV_ListPart
            // 
            this.GV_ListPart.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.gCT_STT,
            this.GC_PartNo,
            this.GC_PartName,
            this.GC_ToPartNo,
            this.GC_ToPartName,
            this.GC_DateApprov,
            this.GCT});
            this.GV_ListPart.GridControl = this.GT_ListPart;
            this.GV_ListPart.Name = "GV_ListPart";
            this.GV_ListPart.NewItemRowText = "i";
            this.GV_ListPart.OptionsBehavior.EditorShowMode = DevExpress.Utils.EditorShowMode.Click;
            this.GV_ListPart.OptionsSelection.MultiSelect = true;
           // this.GV_ListPart.ShowingPopupEditForm += new DevExpress.XtraGrid.Views.Grid.ShowingPopupEditFormEventHandler(this.GV_ListPart_ShowingPopupEditForm);
            this.GV_ListPart.PopupMenuShowing += new DevExpress.XtraGrid.Views.Grid.PopupMenuShowingEventHandler(this.GV_ListPart_PopupMenuShowing);
            this.GV_ListPart.RowUpdated += new DevExpress.XtraGrid.Views.Base.RowObjectEventHandler(this.GV_ListPart_RowUpdated);
            // 
            // gCT_STT
            // 
            this.gCT_STT.Caption = "STT";
            this.gCT_STT.FieldName = "STT";
            this.gCT_STT.MinWidth = 25;
            this.gCT_STT.Name = "gCT_STT";
            this.gCT_STT.Visible = true;
            this.gCT_STT.VisibleIndex = 0;
            this.gCT_STT.Width = 94;
            // 
            // GC_PartNo
            // 
            this.GC_PartNo.Caption = "Part No";
            this.GC_PartNo.FieldName = "PartNo";
            this.GC_PartNo.MinWidth = 25;
            this.GC_PartNo.Name = "GC_PartNo";
            this.GC_PartNo.Visible = true;
            this.GC_PartNo.VisibleIndex = 1;
            this.GC_PartNo.Width = 94;
            // 
            // GC_PartName
            // 
            this.GC_PartName.Caption = "Part Name";
            this.GC_PartName.FieldName = "PartName";
            this.GC_PartName.MinWidth = 25;
            this.GC_PartName.Name = "GC_PartName";
            this.GC_PartName.Visible = true;
            this.GC_PartName.VisibleIndex = 2;
            this.GC_PartName.Width = 94;
            // 
            // GC_ToPartNo
            // 
            this.GC_ToPartNo.Caption = "To Part No";
            this.GC_ToPartNo.FieldName = "ToPartNo";
            this.GC_ToPartNo.MinWidth = 25;
            this.GC_ToPartNo.Name = "GC_ToPartNo";
            this.GC_ToPartNo.Visible = true;
            this.GC_ToPartNo.VisibleIndex = 3;
            this.GC_ToPartNo.Width = 94;
            // 
            // GC_ToPartName
            // 
            this.GC_ToPartName.Caption = "To Part Name";
            this.GC_ToPartName.FieldName = "ToPartName";
            this.GC_ToPartName.MinWidth = 25;
            this.GC_ToPartName.Name = "GC_ToPartName";
            this.GC_ToPartName.Visible = true;
            this.GC_ToPartName.VisibleIndex = 4;
            this.GC_ToPartName.Width = 94;
            // 
            // GC_DateApprov
            // 
            this.GC_DateApprov.Caption = "Date Create";
            this.GC_DateApprov.FieldName = "timeSet";
            this.GC_DateApprov.MinWidth = 25;
            this.GC_DateApprov.Name = "GC_DateApprov";
            this.GC_DateApprov.Visible = true;
            this.GC_DateApprov.VisibleIndex = 5;
            this.GC_DateApprov.Width = 94;
            // 
            // GCT
            // 
            this.GCT.Caption = "Status";
            this.GCT.FieldName = "IsActive";
            this.GCT.MinWidth = 25;
            this.GCT.Name = "GCT";
            this.GCT.Visible = true;
            this.GCT.VisibleIndex = 6;
            this.GCT.Width = 94;
            // 
            // imageCollection1
            // 
            this.imageCollection1.ImageStream = ((DevExpress.Utils.ImageCollectionStreamer)(resources.GetObject("imageCollection1.ImageStream")));
            this.imageCollection1.Images.SetKeyName(1, "deletelist_16x16.png");
            this.imageCollection1.Images.SetKeyName(2, "reset_32x32.png");
            // 
            // ComaparePart
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(961, 488);
            this.Controls.Add(this.sidePanel3);
            this.Controls.Add(this.sidePanel2);
            this.Controls.Add(this.sidePanel1);
            this.Name = "ComaparePart";
            this.Text = "ComaparePart";
            this.sidePanel1.ResumeLayout(false);
            this.sidePanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DateE_NgayAPP.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.DateE_NgayAPP.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.LuKToPart.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.lup_Ma.Properties)).EndInit();
            this.sidePanel2.ResumeLayout(false);
            this.sidePanel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.GT_ListPart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GV_ListPart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.imageCollection1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private DevExpress.XtraEditors.SidePanel sidePanel1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private DevExpress.XtraEditors.DateEdit DateE_NgayAPP;
        private DevExpress.XtraEditors.LookUpEdit lup_Ma;
        private DevExpress.XtraEditors.SidePanel sidePanel2;
        private DevExpress.XtraEditors.SimpleButton cmd_Huy;
        private DevExpress.XtraEditors.SimpleButton cmd_Tao;
        private DevExpress.XtraEditors.SidePanel sidePanel3;
        private DevExpress.XtraGrid.GridControl GT_ListPart;
        private DevExpress.XtraGrid.Views.Grid.GridView GV_ListPart;
        private DevExpress.XtraGrid.Columns.GridColumn gCT_STT;
        private DevExpress.XtraGrid.Columns.GridColumn GC_PartNo;
        private DevExpress.XtraGrid.Columns.GridColumn GC_PartName;
        private DevExpress.XtraGrid.Columns.GridColumn GC_ToPartNo;
        private DevExpress.XtraGrid.Columns.GridColumn GC_ToPartName;
        private DevExpress.XtraGrid.Columns.GridColumn GC_DateApprov;
        private DevExpress.XtraGrid.Columns.GridColumn GCT;
        private DevExpress.Utils.ImageCollection imageCollection1;
        private DevExpress.XtraEditors.LookUpEdit LuKToPart;
        private System.Windows.Forms.Label label3;
    }
}