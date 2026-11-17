using System;
using DevExpress.XtraEditors;
using PCTP.Modules.GiaoHangKhach.HVN.Controls;
using PCTP.QRCODE_HVN;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    partial class HVN_PGH
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }
        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            DevExpress.XtraBars.Docking2010.WindowsUIButtonImageOptions windowsUIButtonImageOptions1 = new DevExpress.XtraBars.Docking2010.WindowsUIButtonImageOptions();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HVN_PGH));
            this.PN_MAIN_TOP = new DevExpress.XtraEditors.PanelControl();
            this.PN_DOCQR_TOP = new System.Windows.Forms.Panel();
            this.panelPhieu = new DevExpress.XtraEditors.PanelControl();
            this.sidePanel6 = new DevExpress.XtraEditors.SidePanel();
            this.btnUploadMilkrun = new DevExpress.XtraEditors.SimpleButton();
            this.dateNX = new DevExpress.XtraEditors.DateEdit();
            this.label2 = new System.Windows.Forms.Label();
            this.sidePanel1 = new DevExpress.XtraEditors.SidePanel();
            this.CheckGX = new DevExpress.XtraEditors.CheckedListBoxControl();
            this.tabPaneHVN = new DevExpress.XtraBars.Navigation.TabPane();
            this.tabHN = new DevExpress.XtraBars.Navigation.TabNavigationPage();
            this.groupControl1 = new DevExpress.XtraEditors.GroupControl();
            this.RDO_GXHN = new DevExpress.XtraEditors.RadioGroup();
            this.tabVP = new DevExpress.XtraBars.Navigation.TabNavigationPage();
            this.groupControl2 = new DevExpress.XtraEditors.GroupControl();
            this.radioGroup2 = new DevExpress.XtraEditors.RadioGroup();
            this.UIButtonHOME = new DevExpress.XtraBars.Docking2010.WindowsUIButtonPanel();
            this.lblDocQrcode = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.txt_DOCQRCODE = new System.Windows.Forms.TextBox();
            this.GCT_HT = new DevExpress.XtraGrid.GridControl();
            this.bandedGridView1 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
            this.gridBand2 = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            this.bandedGridColumn1 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn2 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn3 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn5 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.gridCtrDONHANG = new DevExpress.XtraGrid.GridControl();
            this.GridViewDONHANG = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridView();
            this.gridBandDH = new DevExpress.XtraGrid.Views.BandedGrid.GridBand();
            this.STT = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.GIO = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.cua = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.truyen = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.mahang = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.tenhang = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn9 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.LOT = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.dovi = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.soluong = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.XE = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.Hop = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.STATUSDOC = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.STATUSCNK = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn8 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn6 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn7 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.bandedGridColumn4 = new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn();
            this.gridCtrDOCQrCODE = new DevExpress.XtraGrid.GridControl();
            this.gridVDOCQRCODE = new DevExpress.XtraGrid.Views.Grid.GridView();
            this.NO = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_LOTFCC = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_MAHANGFCC = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_SLTEMFCC = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_LOTHVN = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_MAHANGHVN = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_SLTEMHVN = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_STTPHIEU = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_SUALOTHVN = new DevExpress.XtraGrid.Columns.GridColumn();
            this.CL_KETQUA = new DevExpress.XtraGrid.Columns.GridColumn();
            this.PN_DOCQR_SUASL1 = new DevExpress.XtraEditors.PanelControl();
            this.cmd_SuaLTemFCC = new DevExpress.XtraEditors.SimpleButton();
            this.TXT_FCCTU = new System.Windows.Forms.TextBox();
            this.LOTFCCVN = new System.Windows.Forms.Label();
            this.TXT_FCCTHANH = new System.Windows.Forms.TextBox();
            this.TXT_HVNTU = new System.Windows.Forms.TextBox();
            this.TXT_HVNTHANH = new System.Windows.Forms.TextBox();
            this.cmd_SuaSLHVN = new DevExpress.XtraEditors.SimpleButton();
            this.simpleButton5 = new DevExpress.XtraEditors.SimpleButton();
            this.simpleButton6 = new DevExpress.XtraEditors.SimpleButton();
            this.sidePanel3 = new DevExpress.XtraEditors.SidePanel();
            this.splitBottomLeftRight = new System.Windows.Forms.SplitContainer();
            this.splitBottomActionsHangThieu = new System.Windows.Forms.SplitContainer();
            this.UIButton = new PCTP.Modules.GiaoHangKhach.HVN.MyWindowsUIButtonPanel();
            this.sidePanel5 = new DevExpress.XtraEditors.SidePanel();
            this.imageBT = new DevExpress.Utils.ImageCollection(this.components);
            this.sidePanel2 = new DevExpress.XtraEditors.SidePanel();
            ((System.ComponentModel.ISupportInitialize)(this.PN_MAIN_TOP)).BeginInit();
            this.PN_MAIN_TOP.SuspendLayout();
            this.PN_DOCQR_TOP.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.panelPhieu)).BeginInit();
            this.panelPhieu.SuspendLayout();
            this.sidePanel6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dateNX.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateNX.Properties.CalendarTimeProperties)).BeginInit();
            this.sidePanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.CheckGX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tabPaneHVN)).BeginInit();
            this.tabPaneHVN.SuspendLayout();
            this.tabHN.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).BeginInit();
            this.groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RDO_GXHN.Properties)).BeginInit();
            this.tabVP.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2)).BeginInit();
            this.groupControl2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.radioGroup2.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCT_HT)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandedGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrDONHANG)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridViewDONHANG)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrDOCQrCODE)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVDOCQRCODE)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.PN_DOCQR_SUASL1)).BeginInit();
            this.PN_DOCQR_SUASL1.SuspendLayout();
            this.sidePanel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitBottomLeftRight)).BeginInit();
            this.splitBottomLeftRight.Panel2.SuspendLayout();
            this.splitBottomLeftRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitBottomActionsHangThieu)).BeginInit();
            this.splitBottomActionsHangThieu.Panel1.SuspendLayout();
            this.splitBottomActionsHangThieu.Panel2.SuspendLayout();
            this.splitBottomActionsHangThieu.SuspendLayout();
            this.sidePanel5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageBT)).BeginInit();
            this.sidePanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // PN_MAIN_TOP
            // 
            this.PN_MAIN_TOP.Controls.Add(this.PN_DOCQR_TOP);
            this.PN_MAIN_TOP.Dock = System.Windows.Forms.DockStyle.Top;
            this.PN_MAIN_TOP.Location = new System.Drawing.Point(0, 0);
            this.PN_MAIN_TOP.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.PN_MAIN_TOP.Name = "PN_MAIN_TOP";
            this.PN_MAIN_TOP.Size = new System.Drawing.Size(2024, 178);
            this.PN_MAIN_TOP.TabIndex = 0;
            // 
            // PN_DOCQR_TOP
            // 
            this.PN_DOCQR_TOP.Controls.Add(this.panelPhieu);
            this.PN_DOCQR_TOP.Controls.Add(this.UIButtonHOME);
            this.PN_DOCQR_TOP.Controls.Add(this.lblDocQrcode);
            this.PN_DOCQR_TOP.Controls.Add(this.label5);
            this.PN_DOCQR_TOP.Controls.Add(this.txt_DOCQRCODE);
            this.PN_DOCQR_TOP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PN_DOCQR_TOP.Location = new System.Drawing.Point(2, 2);
            this.PN_DOCQR_TOP.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.PN_DOCQR_TOP.Name = "PN_DOCQR_TOP";
            this.PN_DOCQR_TOP.Size = new System.Drawing.Size(2020, 174);
            this.PN_DOCQR_TOP.TabIndex = 3;
            // 
            // panelPhieu
            // 
            this.panelPhieu.Controls.Add(this.sidePanel6);
            this.panelPhieu.Controls.Add(this.sidePanel1);
            this.panelPhieu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPhieu.Location = new System.Drawing.Point(242, 0);
            this.panelPhieu.Name = "panelPhieu";
            this.panelPhieu.Size = new System.Drawing.Size(1778, 174);
            this.panelPhieu.TabIndex = 20;
            // 
            // sidePanel6
            // 
            this.sidePanel6.Controls.Add(this.btnUploadMilkrun);
            this.sidePanel6.Controls.Add(this.dateNX);
            this.sidePanel6.Controls.Add(this.label2);
            this.sidePanel6.Dock = System.Windows.Forms.DockStyle.Top;
            this.sidePanel6.Location = new System.Drawing.Point(2, 2);
            this.sidePanel6.Name = "sidePanel6";
            this.sidePanel6.Size = new System.Drawing.Size(1774, 36);
            this.sidePanel6.TabIndex = 23;
            this.sidePanel6.Text = "sidePanel6";
            // 
            // btnUploadMilkrun
            // 
            this.btnUploadMilkrun.Location = new System.Drawing.Point(50, 3);
            this.btnUploadMilkrun.Name = "btnUploadMilkrun";
            this.btnUploadMilkrun.Size = new System.Drawing.Size(178, 29);
            this.btnUploadMilkrun.TabIndex = 20;
            this.btnUploadMilkrun.Text = "UploadMikRun";
            // 
            // dateNX
            // 
            this.dateNX.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dateNX.EditValue = null;
            this.dateNX.Location = new System.Drawing.Point(1360, 7);
            this.dateNX.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.dateNX.Name = "dateNX";
            this.dateNX.Properties.Appearance.BackColor = System.Drawing.Color.Teal;
            this.dateNX.Properties.Appearance.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dateNX.Properties.Appearance.Options.UseBackColor = true;
            this.dateNX.Properties.Appearance.Options.UseFont = true;
            this.dateNX.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateNX.Properties.CalendarTimeProperties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.dateNX.Size = new System.Drawing.Size(409, 28);
            this.dateNX.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(1158, 10);
            this.label2.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(149, 21);
            this.label2.TabIndex = 3;
            this.label2.Text = "Ngày Xuất Hàng";
            // 
            // sidePanel1
            // 
            this.sidePanel1.Controls.Add(this.CheckGX);
            this.sidePanel1.Controls.Add(this.tabPaneHVN);
            this.sidePanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.sidePanel1.Location = new System.Drawing.Point(2, 40);
            this.sidePanel1.Name = "sidePanel1";
            this.sidePanel1.Size = new System.Drawing.Size(1774, 132);
            this.sidePanel1.TabIndex = 22;
            this.sidePanel1.Text = "sidePanel1";
            // 
            // CheckGX
            // 
            this.CheckGX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CheckGX.Location = new System.Drawing.Point(0, 1);
            this.CheckGX.Name = "CheckGX";
            this.CheckGX.Size = new System.Drawing.Size(1774, 131);
            this.CheckGX.TabIndex = 21;
            // 
            // tabPaneHVN
            // 
            this.tabPaneHVN.Appearance.BackColor = System.Drawing.Color.Gray;
            this.tabPaneHVN.Appearance.BorderColor = System.Drawing.Color.Black;
            this.tabPaneHVN.Appearance.Options.UseBackColor = true;
            this.tabPaneHVN.Appearance.Options.UseBorderColor = true;
            this.tabPaneHVN.Controls.Add(this.tabHN);
            this.tabPaneHVN.Controls.Add(this.tabVP);
            this.tabPaneHVN.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabPaneHVN.Location = new System.Drawing.Point(0, 1);
            this.tabPaneHVN.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.tabPaneHVN.Name = "tabPaneHVN";
            this.tabPaneHVN.Pages.AddRange(new DevExpress.XtraBars.Navigation.NavigationPageBase[] {
            this.tabHN,
            this.tabVP});
            this.tabPaneHVN.RegularSize = new System.Drawing.Size(1774, 131);
            this.tabPaneHVN.SelectedPage = this.tabHN;
            this.tabPaneHVN.Size = new System.Drawing.Size(1774, 131);
            this.tabPaneHVN.TabIndex = 19;
            this.tabPaneHVN.Text = "HNV HA NAM";
            // 
            // tabHN
            // 
            this.tabHN.Caption = "HNV HÀ NAM";
            this.tabHN.Controls.Add(this.groupControl1);
            this.tabHN.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.tabHN.Name = "tabHN";
            this.tabHN.Size = new System.Drawing.Size(1773, 85);
            // 
            // groupControl1
            // 
            this.groupControl1.Controls.Add(this.RDO_GXHN);
            this.groupControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupControl1.Location = new System.Drawing.Point(0, 0);
            this.groupControl1.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.groupControl1.Name = "groupControl1";
            this.groupControl1.Size = new System.Drawing.Size(1773, 85);
            this.groupControl1.TabIndex = 0;
            this.groupControl1.Text = "KHUNG GIỜ";
            // 
            // RDO_GXHN
            // 
            this.RDO_GXHN.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RDO_GXHN.Location = new System.Drawing.Point(2, 28);
            this.RDO_GXHN.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.RDO_GXHN.Name = "RDO_GXHN";
            this.RDO_GXHN.Size = new System.Drawing.Size(2211, 69);
            this.RDO_GXHN.TabIndex = 0;
            // 
            // tabVP
            // 
            this.tabVP.Caption = "HVN VĨNH PHÚC";
            this.tabVP.Controls.Add(this.groupControl2);
            this.tabVP.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.tabVP.Name = "tabVP";
            this.tabVP.Size = new System.Drawing.Size(1506, 122);
            // 
            // groupControl2
            // 
            this.groupControl2.Controls.Add(this.radioGroup2);
            this.groupControl2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupControl2.Location = new System.Drawing.Point(0, 0);
            this.groupControl2.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.groupControl2.Name = "groupControl2";
            this.groupControl2.Size = new System.Drawing.Size(1506, 122);
            this.groupControl2.TabIndex = 1;
            this.groupControl2.Text = "KHUNG GIỜ HVN";
            // 
            // radioGroup2
            // 
            this.radioGroup2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.radioGroup2.Location = new System.Drawing.Point(2, 28);
            this.radioGroup2.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.radioGroup2.Name = "radioGroup2";
            this.radioGroup2.Size = new System.Drawing.Size(1502, 92);
            this.radioGroup2.TabIndex = 0;
            // 
            // UIButtonHOME
            // 
            this.UIButtonHOME.Buttons.AddRange(new DevExpress.XtraEditors.ButtonPanel.IBaseButton[] {
            new DevExpress.XtraBars.Docking2010.WindowsUIButton("HOME", true, windowsUIButtonImageOptions1, DevExpress.XtraBars.Docking2010.ButtonStyle.PushButton, "", -1, true, null, true, false, true, null, -1, false)});
            this.UIButtonHOME.Dock = System.Windows.Forms.DockStyle.Left;
            this.UIButtonHOME.Location = new System.Drawing.Point(0, 0);
            this.UIButtonHOME.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.UIButtonHOME.Name = "UIButtonHOME";
            this.UIButtonHOME.Size = new System.Drawing.Size(242, 174);
            this.UIButtonHOME.TabIndex = 18;
            this.UIButtonHOME.Text = "windowsUIButtonPanel1";
            // 
            // lblDocQrcode
            // 
            this.lblDocQrcode.AutoSize = true;
            this.lblDocQrcode.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            this.lblDocQrcode.Font = new System.Drawing.Font("Times New Roman", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDocQrcode.ForeColor = System.Drawing.Color.Red;
            this.lblDocQrcode.Location = new System.Drawing.Point(704, 14);
            this.lblDocQrcode.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.lblDocQrcode.Name = "lblDocQrcode";
            this.lblDocQrcode.Size = new System.Drawing.Size(547, 32);
            this.lblDocQrcode.TabIndex = 16;
            this.lblDocQrcode.Text = "(Đọc QRCODE Theo Thứ Tự FCC --> HVN)";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Tahoma", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(263, 80);
            this.label5.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(169, 28);
            this.label5.TabIndex = 13;
            this.label5.Text = "ĐỌC QRCODE";
            // 
            // txt_DOCQRCODE
            // 
            this.txt_DOCQRCODE.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.txt_DOCQRCODE.Font = new System.Drawing.Font("Times New Roman", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txt_DOCQRCODE.Location = new System.Drawing.Point(640, 71);
            this.txt_DOCQRCODE.Margin = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.txt_DOCQRCODE.Name = "txt_DOCQRCODE";
            this.txt_DOCQRCODE.Size = new System.Drawing.Size(1001, 42);
            this.txt_DOCQRCODE.TabIndex = 2;
            // 
            // GCT_HT
            // 
            this.GCT_HT.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GCT_HT.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.GCT_HT.Location = new System.Drawing.Point(1, 0);
            this.GCT_HT.MainView = this.bandedGridView1;
            this.GCT_HT.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.GCT_HT.Name = "GCT_HT";
            this.GCT_HT.Size = new System.Drawing.Size(557, 209);
            this.GCT_HT.TabIndex = 7;
            this.GCT_HT.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.bandedGridView1});
            // 
            // bandedGridView1
            // 
            this.bandedGridView1.ActiveFilterEnabled = false;
            this.bandedGridView1.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.gridBand2});
            this.bandedGridView1.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] {
            this.bandedGridColumn1,
            this.bandedGridColumn2,
            this.bandedGridColumn3,
            this.bandedGridColumn5});
            this.bandedGridView1.GridControl = this.GCT_HT;
            this.bandedGridView1.GroupCount = 1;
            this.bandedGridView1.Name = "bandedGridView1";
            this.bandedGridView1.OptionsEditForm.PopupEditFormWidth = 914;
            this.bandedGridView1.OptionsSelection.MultiSelect = true;
            this.bandedGridView1.SortInfo.AddRange(new DevExpress.XtraGrid.Columns.GridColumnSortInfo[] {
            new DevExpress.XtraGrid.Columns.GridColumnSortInfo(this.bandedGridColumn1, DevExpress.Data.ColumnSortOrder.Ascending)});
            // 
            // gridBand2
            // 
            this.gridBand2.AppearanceHeader.BackColor = System.Drawing.Color.Red;
            this.gridBand2.AppearanceHeader.BorderColor = System.Drawing.Color.Yellow;
            this.gridBand2.AppearanceHeader.Font = new System.Drawing.Font("Times New Roman", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gridBand2.AppearanceHeader.Options.UseBackColor = true;
            this.gridBand2.AppearanceHeader.Options.UseBorderColor = true;
            this.gridBand2.AppearanceHeader.Options.UseFont = true;
            this.gridBand2.AppearanceHeader.Options.UseTextOptions = true;
            this.gridBand2.AppearanceHeader.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.gridBand2.AppearanceHeader.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            this.gridBand2.Caption = "THÔNG TIN HÀNG THIẾU";
            this.gridBand2.Columns.Add(this.bandedGridColumn1);
            this.gridBand2.Columns.Add(this.bandedGridColumn2);
            this.gridBand2.Columns.Add(this.bandedGridColumn3);
            this.gridBand2.Columns.Add(this.bandedGridColumn5);
            this.gridBand2.MinWidth = 30;
            this.gridBand2.Name = "gridBand2";
            this.gridBand2.VisibleIndex = 0;
            this.gridBand2.Width = 1218;
            // 
            // bandedGridColumn1
            // 
            this.bandedGridColumn1.Caption = "Mã Hàng";
            this.bandedGridColumn1.FieldName = "MH";
            this.bandedGridColumn1.MinWidth = 73;
            this.bandedGridColumn1.Name = "bandedGridColumn1";
            this.bandedGridColumn1.Visible = true;
            this.bandedGridColumn1.Width = 270;
            // 
            // bandedGridColumn2
            // 
            this.bandedGridColumn2.Caption = "Giờ Thiếu";
            this.bandedGridColumn2.FieldName = "GIOGIAO";
            this.bandedGridColumn2.MinWidth = 73;
            this.bandedGridColumn2.Name = "bandedGridColumn2";
            this.bandedGridColumn2.Visible = true;
            this.bandedGridColumn2.Width = 313;
            // 
            // bandedGridColumn3
            // 
            this.bandedGridColumn3.Caption = "Số Lượng Giao";
            this.bandedGridColumn3.FieldName = "SLGIAO";
            this.bandedGridColumn3.MinWidth = 73;
            this.bandedGridColumn3.Name = "bandedGridColumn3";
            this.bandedGridColumn3.Visible = true;
            this.bandedGridColumn3.Width = 489;
            // 
            // bandedGridColumn5
            // 
            this.bandedGridColumn5.Caption = "SỐ LƯỢNG THIẾU";
            this.bandedGridColumn5.FieldName = "SLTHIEU";
            this.bandedGridColumn5.MinWidth = 39;
            this.bandedGridColumn5.Name = "bandedGridColumn5";
            this.bandedGridColumn5.Visible = true;
            this.bandedGridColumn5.Width = 146;
            // 
            // gridCtrDONHANG
            // 
            this.gridCtrDONHANG.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCtrDONHANG.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrDONHANG.Location = new System.Drawing.Point(0, 0);
            this.gridCtrDONHANG.MainView = this.GridViewDONHANG;
            this.gridCtrDONHANG.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrDONHANG.Name = "gridCtrDONHANG";
            this.gridCtrDONHANG.Size = new System.Drawing.Size(2024, 332);
            this.gridCtrDONHANG.TabIndex = 4;
            this.gridCtrDONHANG.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.GridViewDONHANG});
            // 
            // GridViewDONHANG
            // 
            this.GridViewDONHANG.Bands.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.GridBand[] {
            this.gridBandDH});
            this.GridViewDONHANG.Columns.AddRange(new DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn[] {
            this.STT,
            this.GIO,
            this.cua,
            this.truyen,
            this.mahang,
            this.tenhang,
            this.LOT,
            this.dovi,
            this.soluong,
            this.XE,
            this.Hop,
            this.STATUSDOC,
            this.STATUSCNK,
            this.bandedGridColumn6,
            this.bandedGridColumn7,
            this.bandedGridColumn8,
            this.bandedGridColumn4,
            this.bandedGridColumn9});
            this.GridViewDONHANG.GridControl = this.gridCtrDONHANG;
            this.GridViewDONHANG.Name = "GridViewDONHANG";
            this.GridViewDONHANG.OptionsEditForm.PopupEditFormWidth = 914;
            this.GridViewDONHANG.OptionsFilter.AllowMultiSelectInCheckedFilterPopup = false;
            this.GridViewDONHANG.OptionsFilter.ShowAllTableValuesInCheckedFilterPopup = false;
            this.GridViewDONHANG.OptionsSelection.CheckBoxSelectorColumnWidth = 27;
            this.GridViewDONHANG.OptionsSelection.CheckBoxSelectorField = "CHON";
            this.GridViewDONHANG.OptionsSelection.MultiSelect = true;
            this.GridViewDONHANG.OptionsSelection.ShowCheckBoxSelectorInColumnHeader = DevExpress.Utils.DefaultBoolean.False;
            this.GridViewDONHANG.RowCellStyle += new DevExpress.XtraGrid.Views.Grid.RowCellStyleEventHandler(this.GridViewDONHANG_RowCellStyle);
            // 
            // gridBandDH
            // 
            this.gridBandDH.Caption = "gridBand3";
            this.gridBandDH.Columns.Add(this.STT);
            this.gridBandDH.Columns.Add(this.GIO);
            this.gridBandDH.Columns.Add(this.cua);
            this.gridBandDH.Columns.Add(this.truyen);
            this.gridBandDH.Columns.Add(this.mahang);
            this.gridBandDH.Columns.Add(this.tenhang);
            this.gridBandDH.Columns.Add(this.bandedGridColumn9);
            this.gridBandDH.Columns.Add(this.LOT);
            this.gridBandDH.Columns.Add(this.dovi);
            this.gridBandDH.Columns.Add(this.soluong);
            this.gridBandDH.Columns.Add(this.XE);
            this.gridBandDH.Columns.Add(this.Hop);
            this.gridBandDH.Columns.Add(this.STATUSDOC);
            this.gridBandDH.Columns.Add(this.STATUSCNK);
            this.gridBandDH.Columns.Add(this.bandedGridColumn8);
            this.gridBandDH.Columns.Add(this.bandedGridColumn6);
            this.gridBandDH.Columns.Add(this.bandedGridColumn7);
            this.gridBandDH.Columns.Add(this.bandedGridColumn4);
            this.gridBandDH.MinWidth = 14;
            this.gridBandDH.Name = "gridBandDH";
            this.gridBandDH.RowCount = 2;
            this.gridBandDH.VisibleIndex = 0;
            this.gridBandDH.Width = 1770;
            // 
            // STT
            // 
            this.STT.Caption = "GSTT";
            this.STT.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.STT.FieldName = "STT";
            this.STT.MinWidth = 14;
            this.STT.Name = "STT";
            this.STT.Visible = true;
            this.STT.Width = 39;
            // 
            // GIO
            // 
            this.GIO.Caption = "Giờ";
            this.GIO.DisplayFormat.FormatString = "HH";
            this.GIO.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            this.GIO.FieldName = "GIOGIAO";
            this.GIO.MinWidth = 27;
            this.GIO.Name = "GIO";
            this.GIO.Visible = true;
            this.GIO.Width = 103;
            // 
            // cua
            // 
            this.cua.Caption = "Cửa";
            this.cua.FieldName = "CUA";
            this.cua.MinWidth = 27;
            this.cua.Name = "cua";
            this.cua.Visible = true;
            this.cua.Width = 103;
            // 
            // truyen
            // 
            this.truyen.Caption = "Truyền";
            this.truyen.FieldName = "TRUYEN";
            this.truyen.MinWidth = 27;
            this.truyen.Name = "truyen";
            this.truyen.Visible = true;
            this.truyen.Width = 103;
            // 
            // mahang
            // 
            this.mahang.Caption = "Mã hàng";
            this.mahang.FieldName = "MAHANG";
            this.mahang.MinWidth = 27;
            this.mahang.Name = "mahang";
            this.mahang.Visible = true;
            this.mahang.Width = 103;
            // 
            // tenhang
            // 
            this.tenhang.Caption = "Tên Hàng";
            this.tenhang.FieldName = "TENHANG";
            this.tenhang.MinWidth = 27;
            this.tenhang.Name = "tenhang";
            this.tenhang.Visible = true;
            this.tenhang.Width = 103;
            // 
            // bandedGridColumn9
            // 
            this.bandedGridColumn9.Caption = "Gear Sử Dụng";
            this.bandedGridColumn9.FieldName = "GEAR";
            this.bandedGridColumn9.MinWidth = 29;
            this.bandedGridColumn9.Name = "bandedGridColumn9";
            this.bandedGridColumn9.Visible = true;
            this.bandedGridColumn9.Width = 107;
            // 
            // LOT
            // 
            this.LOT.Caption = "Số Lô";
            this.LOT.FieldName = "LOT";
            this.LOT.MinWidth = 27;
            this.LOT.Name = "LOT";
            this.LOT.Visible = true;
            this.LOT.Width = 103;
            // 
            // dovi
            // 
            this.dovi.Caption = "Đơn Vị";
            this.dovi.FieldName = "DV";
            this.dovi.MinWidth = 27;
            this.dovi.Name = "dovi";
            this.dovi.Visible = true;
            this.dovi.Width = 103;
            // 
            // soluong
            // 
            this.soluong.Caption = "Số Lượng";
            this.soluong.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.soluong.FieldName = "SOLUONG";
            this.soluong.MinWidth = 27;
            this.soluong.Name = "soluong";
            this.soluong.Visible = true;
            this.soluong.Width = 103;
            // 
            // XE
            // 
            this.XE.Caption = "Xe";
            this.XE.FieldName = "XE";
            this.XE.MinWidth = 27;
            this.XE.Name = "XE";
            this.XE.Visible = true;
            this.XE.Width = 90;
            // 
            // Hop
            // 
            this.Hop.Caption = "Hộp";
            this.Hop.DisplayFormat.FormatString = "n";
            this.Hop.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            this.Hop.FieldName = "HOP";
            this.Hop.MinWidth = 27;
            this.Hop.Name = "Hop";
            this.Hop.Visible = true;
            this.Hop.Width = 102;
            // 
            // STATUSDOC
            // 
            this.STATUSDOC.Caption = "Kết Quả Đọc QRcode";
            this.STATUSDOC.FieldName = "STATUSDOC";
            this.STATUSDOC.MinWidth = 27;
            this.STATUSDOC.Name = "STATUSDOC";
            this.STATUSDOC.Visible = true;
            this.STATUSDOC.Width = 73;
            // 
            // STATUSCNK
            // 
            this.STATUSCNK.Caption = "Trạng Thái Cập Nhập Kho";
            this.STATUSCNK.FieldName = "STATUS";
            this.STATUSCNK.MinWidth = 14;
            this.STATUSCNK.Name = "STATUSCNK";
            this.STATUSCNK.Visible = true;
            this.STATUSCNK.Width = 134;
            // 
            // bandedGridColumn8
            // 
            this.bandedGridColumn8.Caption = "TT Phiếu";
            this.bandedGridColumn8.FieldName = "TTPHIEU";
            this.bandedGridColumn8.MinWidth = 29;
            this.bandedGridColumn8.Name = "bandedGridColumn8";
            this.bandedGridColumn8.Visible = true;
            this.bandedGridColumn8.Width = 107;
            // 
            // bandedGridColumn6
            // 
            this.bandedGridColumn6.Caption = "PO No";
            this.bandedGridColumn6.FieldName = "PO_NO";
            this.bandedGridColumn6.MinWidth = 29;
            this.bandedGridColumn6.Name = "bandedGridColumn6";
            this.bandedGridColumn6.Visible = true;
            this.bandedGridColumn6.Width = 107;
            // 
            // bandedGridColumn7
            // 
            this.bandedGridColumn7.Caption = "PO_ITEM";
            this.bandedGridColumn7.FieldName = "PO_ITEM";
            this.bandedGridColumn7.MinWidth = 29;
            this.bandedGridColumn7.Name = "bandedGridColumn7";
            this.bandedGridColumn7.Visible = true;
            this.bandedGridColumn7.Width = 107;
            // 
            // bandedGridColumn4
            // 
            this.bandedGridColumn4.Caption = "Ghi Chú";
            this.bandedGridColumn4.FieldName = "Note";
            this.bandedGridColumn4.MinWidth = 33;
            this.bandedGridColumn4.Name = "bandedGridColumn4";
            this.bandedGridColumn4.Visible = true;
            this.bandedGridColumn4.Width = 80;
            // 
            // gridCtrDOCQrCODE
            // 
            this.gridCtrDOCQrCODE.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCtrDOCQrCODE.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrDOCQrCODE.Location = new System.Drawing.Point(0, 0);
            this.gridCtrDOCQrCODE.MainView = this.gridVDOCQRCODE;
            this.gridCtrDOCQrCODE.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.gridCtrDOCQrCODE.Name = "gridCtrDOCQrCODE";
            this.gridCtrDOCQrCODE.Size = new System.Drawing.Size(2024, 332);
            this.gridCtrDOCQrCODE.TabIndex = 3;
            this.gridCtrDOCQrCODE.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridVDOCQRCODE});
            // 
            // gridVDOCQRCODE
            // 
            this.gridVDOCQRCODE.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.NO,
            this.CL_LOTFCC,
            this.CL_MAHANGFCC,
            this.CL_SLTEMFCC,
            this.CL_LOTHVN,
            this.CL_MAHANGHVN,
            this.CL_SLTEMHVN,
            this.CL_STTPHIEU,
            this.CL_SUALOTHVN,
            this.CL_KETQUA});
            this.gridVDOCQRCODE.GridControl = this.gridCtrDOCQrCODE;
            this.gridVDOCQRCODE.Name = "gridVDOCQRCODE";
            this.gridVDOCQRCODE.OptionsEditForm.PopupEditFormWidth = 914;
            // 
            // NO
            // 
            this.NO.Caption = "Số Thứ Tự Bắn";
            this.NO.FieldName = "STT";
            this.NO.MinWidth = 27;
            this.NO.Name = "NO";
            this.NO.OptionsColumn.AllowEdit = false;
            this.NO.Visible = true;
            this.NO.VisibleIndex = 0;
            this.NO.Width = 107;
            // 
            // CL_LOTFCC
            // 
            this.CL_LOTFCC.Caption = "Lot FCC";
            this.CL_LOTFCC.FieldName = "LOTFCC";
            this.CL_LOTFCC.MinWidth = 27;
            this.CL_LOTFCC.Name = "CL_LOTFCC";
            this.CL_LOTFCC.OptionsColumn.AllowEdit = false;
            this.CL_LOTFCC.Visible = true;
            this.CL_LOTFCC.VisibleIndex = 1;
            this.CL_LOTFCC.Width = 107;
            // 
            // CL_MAHANGFCC
            // 
            this.CL_MAHANGFCC.Caption = "MÃ Hàng FCC";
            this.CL_MAHANGFCC.FieldName = "MAHANGFCC";
            this.CL_MAHANGFCC.MinWidth = 27;
            this.CL_MAHANGFCC.Name = "CL_MAHANGFCC";
            this.CL_MAHANGFCC.OptionsColumn.AllowEdit = false;
            this.CL_MAHANGFCC.Visible = true;
            this.CL_MAHANGFCC.VisibleIndex = 2;
            this.CL_MAHANGFCC.Width = 107;
            // 
            // CL_SLTEMFCC
            // 
            this.CL_SLTEMFCC.Caption = "Số Lượng Tem FCC";
            this.CL_SLTEMFCC.FieldName = "SLTEMFCC";
            this.CL_SLTEMFCC.MinWidth = 27;
            this.CL_SLTEMFCC.Name = "CL_SLTEMFCC";
            this.CL_SLTEMFCC.OptionsColumn.AllowEdit = false;
            this.CL_SLTEMFCC.Visible = true;
            this.CL_SLTEMFCC.VisibleIndex = 3;
            this.CL_SLTEMFCC.Width = 107;
            // 
            // CL_LOTHVN
            // 
            this.CL_LOTHVN.Caption = "Part Conten HVN";
            this.CL_LOTHVN.FieldName = "LOTHVN";
            this.CL_LOTHVN.MinWidth = 27;
            this.CL_LOTHVN.Name = "CL_LOTHVN";
            this.CL_LOTHVN.OptionsColumn.AllowEdit = false;
            this.CL_LOTHVN.Visible = true;
            this.CL_LOTHVN.VisibleIndex = 4;
            this.CL_LOTHVN.Width = 107;
            // 
            // CL_MAHANGHVN
            // 
            this.CL_MAHANGHVN.Caption = "MÃ Hàng HVN";
            this.CL_MAHANGHVN.FieldName = "MAHANGHVN";
            this.CL_MAHANGHVN.MinWidth = 27;
            this.CL_MAHANGHVN.Name = "CL_MAHANGHVN";
            this.CL_MAHANGHVN.OptionsColumn.AllowEdit = false;
            this.CL_MAHANGHVN.Visible = true;
            this.CL_MAHANGHVN.VisibleIndex = 5;
            this.CL_MAHANGHVN.Width = 107;
            // 
            // CL_SLTEMHVN
            // 
            this.CL_SLTEMHVN.Caption = "Số Lượng Tem HVN";
            this.CL_SLTEMHVN.FieldName = "SLTEMHVN";
            this.CL_SLTEMHVN.MinWidth = 27;
            this.CL_SLTEMHVN.Name = "CL_SLTEMHVN";
            this.CL_SLTEMHVN.OptionsColumn.AllowEdit = false;
            this.CL_SLTEMHVN.Visible = true;
            this.CL_SLTEMHVN.VisibleIndex = 6;
            this.CL_SLTEMHVN.Width = 107;
            // 
            // CL_STTPHIEU
            // 
            this.CL_STTPHIEU.Caption = "STT Trên Phiếu";
            this.CL_STTPHIEU.FieldName = "GIO";
            this.CL_STTPHIEU.MinWidth = 27;
            this.CL_STTPHIEU.Name = "CL_STTPHIEU";
            this.CL_STTPHIEU.OptionsColumn.AllowEdit = false;
            this.CL_STTPHIEU.Visible = true;
            this.CL_STTPHIEU.VisibleIndex = 7;
            this.CL_STTPHIEU.Width = 107;
            // 
            // CL_SUALOTHVN
            // 
            this.CL_SUALOTHVN.Caption = "Sửa LOT HVN";
            this.CL_SUALOTHVN.FieldName = "SUALOTHVN";
            this.CL_SUALOTHVN.MinWidth = 27;
            this.CL_SUALOTHVN.Name = "CL_SUALOTHVN";
            this.CL_SUALOTHVN.OptionsColumn.AllowEdit = false;
            this.CL_SUALOTHVN.Visible = true;
            this.CL_SUALOTHVN.VisibleIndex = 8;
            this.CL_SUALOTHVN.Width = 107;
            // 
            // CL_KETQUA
            // 
            this.CL_KETQUA.Caption = "Kết Quả";
            this.CL_KETQUA.FieldName = "STATUS";
            this.CL_KETQUA.MinWidth = 27;
            this.CL_KETQUA.Name = "CL_KETQUA";
            this.CL_KETQUA.Visible = true;
            this.CL_KETQUA.VisibleIndex = 9;
            this.CL_KETQUA.Width = 107;
            // 
            // PN_DOCQR_SUASL1
            // 
            this.PN_DOCQR_SUASL1.AutoSize = true;
            this.PN_DOCQR_SUASL1.Controls.Add(this.cmd_SuaLTemFCC);
            this.PN_DOCQR_SUASL1.Controls.Add(this.TXT_FCCTU);
            this.PN_DOCQR_SUASL1.Controls.Add(this.LOTFCCVN);
            this.PN_DOCQR_SUASL1.Controls.Add(this.TXT_FCCTHANH);
            this.PN_DOCQR_SUASL1.Controls.Add(this.TXT_HVNTU);
            this.PN_DOCQR_SUASL1.Controls.Add(this.TXT_HVNTHANH);
            this.PN_DOCQR_SUASL1.Controls.Add(this.cmd_SuaSLHVN);
            this.PN_DOCQR_SUASL1.Controls.Add(this.simpleButton5);
            this.PN_DOCQR_SUASL1.Controls.Add(this.simpleButton6);
            this.PN_DOCQR_SUASL1.Location = new System.Drawing.Point(1, 0);
            this.PN_DOCQR_SUASL1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.PN_DOCQR_SUASL1.Name = "PN_DOCQR_SUASL1";
            this.PN_DOCQR_SUASL1.Size = new System.Drawing.Size(557, 133);
            this.PN_DOCQR_SUASL1.TabIndex = 10;
            // 
            // cmd_SuaLTemFCC
            // 
            this.cmd_SuaLTemFCC.Location = new System.Drawing.Point(47, 37);
            this.cmd_SuaLTemFCC.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.cmd_SuaLTemFCC.Name = "cmd_SuaLTemFCC";
            this.cmd_SuaLTemFCC.Size = new System.Drawing.Size(201, 33);
            this.cmd_SuaLTemFCC.TabIndex = 7;
            this.cmd_SuaLTemFCC.Text = "SỐ LƯỢNG TRÊN TEM FCC";
            this.cmd_SuaLTemFCC.Click += new System.EventHandler(this.cmd_SuaLTemFCC_Click_1);
            // 
            // TXT_FCCTU
            // 
            this.TXT_FCCTU.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.TXT_FCCTU.Font = new System.Drawing.Font("Tahoma", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TXT_FCCTU.Location = new System.Drawing.Point(47, 71);
            this.TXT_FCCTU.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.TXT_FCCTU.Name = "TXT_FCCTU";
            this.TXT_FCCTU.Size = new System.Drawing.Size(60, 29);
            this.TXT_FCCTU.TabIndex = 11;
            // 
            // LOTFCCVN
            // 
            this.LOTFCCVN.AutoSize = true;
            this.LOTFCCVN.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.LOTFCCVN.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.LOTFCCVN.Font = new System.Drawing.Font("Tahoma", 12F);
            this.LOTFCCVN.Location = new System.Drawing.Point(62, 4);
            this.LOTFCCVN.Name = "LOTFCCVN";
            this.LOTFCCVN.Size = new System.Drawing.Size(2, 26);
            this.LOTFCCVN.TabIndex = 14;
            // 
            // TXT_FCCTHANH
            // 
            this.TXT_FCCTHANH.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.TXT_FCCTHANH.Font = new System.Drawing.Font("Tahoma", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TXT_FCCTHANH.Location = new System.Drawing.Point(186, 71);
            this.TXT_FCCTHANH.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.TXT_FCCTHANH.Name = "TXT_FCCTHANH";
            this.TXT_FCCTHANH.Size = new System.Drawing.Size(60, 29);
            this.TXT_FCCTHANH.TabIndex = 10;
            // 
            // TXT_HVNTU
            // 
            this.TXT_HVNTU.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.TXT_HVNTU.Font = new System.Drawing.Font("Tahoma", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TXT_HVNTU.Location = new System.Drawing.Point(272, 70);
            this.TXT_HVNTU.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.TXT_HVNTU.Name = "TXT_HVNTU";
            this.TXT_HVNTU.Size = new System.Drawing.Size(60, 29);
            this.TXT_HVNTU.TabIndex = 9;
            // 
            // TXT_HVNTHANH
            // 
            this.TXT_HVNTHANH.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.TXT_HVNTHANH.Font = new System.Drawing.Font("Tahoma", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TXT_HVNTHANH.Location = new System.Drawing.Point(414, 70);
            this.TXT_HVNTHANH.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.TXT_HVNTHANH.Name = "TXT_HVNTHANH";
            this.TXT_HVNTHANH.Size = new System.Drawing.Size(60, 29);
            this.TXT_HVNTHANH.TabIndex = 8;
            // 
            // cmd_SuaSLHVN
            // 
            this.cmd_SuaSLHVN.Location = new System.Drawing.Point(272, 37);
            this.cmd_SuaSLHVN.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.cmd_SuaSLHVN.Name = "cmd_SuaSLHVN";
            this.cmd_SuaSLHVN.Size = new System.Drawing.Size(201, 33);
            this.cmd_SuaSLHVN.TabIndex = 3;
            this.cmd_SuaSLHVN.Text = "SỐ LƯỢNG TRÊN TEM HVN";
            this.cmd_SuaSLHVN.Click += new System.EventHandler(this.cmd_SuaSLHVN_Click);
            // 
            // simpleButton5
            // 
            this.simpleButton5.Location = new System.Drawing.Point(137, 73);
            this.simpleButton5.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.simpleButton5.Name = "simpleButton5";
            this.simpleButton5.Size = new System.Drawing.Size(18, 27);
            this.simpleButton5.TabIndex = 6;
            this.simpleButton5.Text = "==>";
            // 
            // simpleButton6
            // 
            this.simpleButton6.Location = new System.Drawing.Point(363, 70);
            this.simpleButton6.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.simpleButton6.Name = "simpleButton6";
            this.simpleButton6.Size = new System.Drawing.Size(18, 27);
            this.simpleButton6.TabIndex = 5;
            this.simpleButton6.Text = "==>";
            // 
            // sidePanel3
            // 
            this.sidePanel3.Controls.Add(this.splitBottomLeftRight);
            this.sidePanel3.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.sidePanel3.Location = new System.Drawing.Point(0, 510);
            this.sidePanel3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.sidePanel3.Name = "sidePanel3";
            this.sidePanel3.Size = new System.Drawing.Size(2024, 210);
            this.sidePanel3.TabIndex = 3;
            this.sidePanel3.Text = "sidePanel3";
            // 
            // splitBottomLeftRight
            // 
            this.splitBottomLeftRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitBottomLeftRight.Location = new System.Drawing.Point(0, 1);
            this.splitBottomLeftRight.Name = "splitBottomLeftRight";
            this.splitBottomLeftRight.Panel1MinSize = 220;
            // 
            // splitBottomLeftRight.Panel2
            // 
            this.splitBottomLeftRight.Panel2.Controls.Add(this.splitBottomActionsHangThieu);
            this.splitBottomLeftRight.Panel2MinSize = 420;
            this.splitBottomLeftRight.Size = new System.Drawing.Size(2024, 209);
            this.splitBottomLeftRight.SplitterDistance = 447;
            this.splitBottomLeftRight.SplitterWidth = 5;
            this.splitBottomLeftRight.TabIndex = 0;
            // 
            // splitBottomActionsHangThieu
            // 
            this.splitBottomActionsHangThieu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitBottomActionsHangThieu.Location = new System.Drawing.Point(0, 0);
            this.splitBottomActionsHangThieu.Name = "splitBottomActionsHangThieu";
            // 
            // splitBottomActionsHangThieu.Panel1
            // 
            this.splitBottomActionsHangThieu.Panel1.Controls.Add(this.UIButton);
            this.splitBottomActionsHangThieu.Panel1MinSize = 260;
            // 
            // splitBottomActionsHangThieu.Panel2
            // 
            this.splitBottomActionsHangThieu.Panel2.Controls.Add(this.sidePanel5);
            this.splitBottomActionsHangThieu.Panel2MinSize = 300;
            this.splitBottomActionsHangThieu.Size = new System.Drawing.Size(1572, 209);
            this.splitBottomActionsHangThieu.SplitterDistance = 1023;
            this.splitBottomActionsHangThieu.SplitterWidth = 5;
            this.splitBottomActionsHangThieu.TabIndex = 0;
            // 
            // UIButton
            // 
            this.UIButton.AllowGlyphSkinning = false;
            this.UIButton.Buttons.AddRange(new DevExpress.XtraEditors.ButtonPanel.IBaseButton[] {
            new DevExpress.XtraBars.Docking2010.WindowsUIButton(),
            new DevExpress.XtraBars.Docking2010.WindowsUIButton(),
            new DevExpress.XtraBars.Docking2010.WindowsUIButton(),
            new DevExpress.XtraBars.Docking2010.WindowsUIButton()});
            this.UIButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.UIButton.Location = new System.Drawing.Point(0, 0);
            this.UIButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.UIButton.Name = "UIButton";
            this.UIButton.Size = new System.Drawing.Size(1023, 209);
            this.UIButton.TabIndex = 2;
            this.UIButton.Text = "myWindowsUIButtonPanel1";
            // 
            // sidePanel5
            // 
            this.sidePanel5.Controls.Add(this.PN_DOCQR_SUASL1);
            this.sidePanel5.Controls.Add(this.GCT_HT);
            this.sidePanel5.Dock = System.Windows.Forms.DockStyle.Right;
            this.sidePanel5.Location = new System.Drawing.Point(-14, 0);
            this.sidePanel5.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.sidePanel5.Name = "sidePanel5";
            this.sidePanel5.Size = new System.Drawing.Size(558, 209);
            this.sidePanel5.TabIndex = 1;
            this.sidePanel5.Text = "sidePanel5";
            // 
            // imageBT
            // 
            this.imageBT.ImageStream = ((DevExpress.Utils.ImageCollectionStreamer)(resources.GetObject("imageBT.ImageStream")));
            this.imageBT.Images.SetKeyName(0, "addfile_16x16.png");
            this.imageBT.Images.SetKeyName(1, "show_16x16.png");
            this.imageBT.Images.SetKeyName(2, "defaultprinter_16x16.png");
            this.imageBT.Images.SetKeyName(3, "printdialog_16x16.png");
            this.imageBT.Images.SetKeyName(4, "printer_16x16.png");
            this.imageBT.Images.SetKeyName(5, "showworktimeonly_16x16.png");
            this.imageBT.Images.SetKeyName(6, "addnewdatasource_16x16.png");
            this.imageBT.Images.SetKeyName(7, "deletedatasource2_16x16.png");
            this.imageBT.Images.SetKeyName(8, "editdatasource_16x16.png");
            this.imageBT.Images.SetKeyName(9, "exportmodeldifferences_16x16.png");
            this.imageBT.Images.SetKeyName(10, "apply_16x16.png");
            this.imageBT.Images.SetKeyName(11, "clear_16x16.png");
            this.imageBT.Images.SetKeyName(12, "editname_16x16.png");
            // 
            // sidePanel2
            // 
            this.sidePanel2.Controls.Add(this.gridCtrDONHANG);
            this.sidePanel2.Controls.Add(this.gridCtrDOCQrCODE);
            this.sidePanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidePanel2.Location = new System.Drawing.Point(0, 178);
            this.sidePanel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.sidePanel2.Name = "sidePanel2";
            this.sidePanel2.Size = new System.Drawing.Size(2024, 332);
            this.sidePanel2.TabIndex = 4;
            this.sidePanel2.Text = "sidePanel2";
            // 
            // HVN_PGH
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2024, 720);
            this.Controls.Add(this.sidePanel2);
            this.Controls.Add(this.sidePanel3);
            this.Controls.Add(this.PN_MAIN_TOP);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "HVN_PGH";
            this.Text = "HVN_PGH";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.HVN_PGH_Load);
            ((System.ComponentModel.ISupportInitialize)(this.PN_MAIN_TOP)).EndInit();
            this.PN_MAIN_TOP.ResumeLayout(false);
            this.PN_DOCQR_TOP.ResumeLayout(false);
            this.PN_DOCQR_TOP.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.panelPhieu)).EndInit();
            this.panelPhieu.ResumeLayout(false);
            this.sidePanel6.ResumeLayout(false);
            this.sidePanel6.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dateNX.Properties.CalendarTimeProperties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dateNX.Properties)).EndInit();
            this.sidePanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.CheckGX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tabPaneHVN)).EndInit();
            this.tabPaneHVN.ResumeLayout(false);
            this.tabHN.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.groupControl1)).EndInit();
            this.groupControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.RDO_GXHN.Properties)).EndInit();
            this.tabVP.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.groupControl2)).EndInit();
            this.groupControl2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.radioGroup2.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GCT_HT)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bandedGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrDONHANG)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.GridViewDONHANG)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCtrDOCQrCODE)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridVDOCQRCODE)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.PN_DOCQR_SUASL1)).EndInit();
            this.PN_DOCQR_SUASL1.ResumeLayout(false);
            this.PN_DOCQR_SUASL1.PerformLayout();
            this.sidePanel3.ResumeLayout(false);
            this.splitBottomLeftRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitBottomLeftRight)).EndInit();
            this.splitBottomLeftRight.ResumeLayout(false);
            this.splitBottomActionsHangThieu.Panel1.ResumeLayout(false);
            this.splitBottomActionsHangThieu.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitBottomActionsHangThieu)).EndInit();
            this.splitBottomActionsHangThieu.ResumeLayout(false);
            this.sidePanel5.ResumeLayout(false);
            this.sidePanel5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imageBT)).EndInit();
            this.sidePanel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion
        private DevExpress.XtraEditors.PanelControl PN_MAIN_TOP;
        private DevExpress.XtraGrid.GridControl GCT_HT;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridView bandedGridView1;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn1;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn2;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn3;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn5;
        private System.Windows.Forms.Panel PN_DOCQR_TOP;
        private System.Windows.Forms.Label lblDocQrcode;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txt_DOCQRCODE;
        private DevExpress.XtraGrid.GridControl gridCtrDOCQrCODE;
        public DevExpress.XtraGrid.Views.Grid.GridView gridVDOCQRCODE;
        private DevExpress.XtraGrid.Columns.GridColumn NO;
        private DevExpress.XtraGrid.Columns.GridColumn CL_LOTFCC;
        private DevExpress.XtraGrid.Columns.GridColumn CL_MAHANGFCC;
        private DevExpress.XtraGrid.Columns.GridColumn CL_SLTEMFCC;
        private DevExpress.XtraGrid.Columns.GridColumn CL_LOTHVN;
        private DevExpress.XtraGrid.Columns.GridColumn CL_MAHANGHVN;
        private DevExpress.XtraGrid.Columns.GridColumn CL_SLTEMHVN;
        private DevExpress.XtraGrid.Columns.GridColumn CL_STTPHIEU;
        private DevExpress.XtraGrid.Columns.GridColumn CL_SUALOTHVN;
        private DevExpress.XtraGrid.Columns.GridColumn CL_KETQUA;
        private DevExpress.XtraGrid.GridControl gridCtrDONHANG;
        private DevExpress.XtraEditors.PanelControl PN_DOCQR_SUASL1;
        private DevExpress.XtraEditors.SimpleButton cmd_SuaLTemFCC;
        private System.Windows.Forms.TextBox TXT_FCCTU;
        private System.Windows.Forms.Label LOTFCCVN;
        private System.Windows.Forms.TextBox TXT_FCCTHANH;
        private System.Windows.Forms.TextBox TXT_HVNTU;
        private System.Windows.Forms.TextBox TXT_HVNTHANH;
        private DevExpress.XtraEditors.SimpleButton cmd_SuaSLHVN;
        private DevExpress.XtraEditors.SimpleButton simpleButton5;
        private DevExpress.XtraEditors.SimpleButton simpleButton6;
        private DevExpress.XtraEditors.SidePanel sidePanel3;
        private System.Windows.Forms.SplitContainer splitBottomLeftRight;
        private System.Windows.Forms.SplitContainer splitBottomActionsHangThieu;
        private DevExpress.XtraEditors.SidePanel sidePanel5;
        private DevExpress.XtraBars.Docking2010.WindowsUIButtonPanel UIButtonHOME;
        private DevExpress.XtraEditors.SidePanel sidePanel2;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridView GridViewDONHANG;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn STT;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn GIO;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn cua;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn truyen;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn mahang;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn tenhang;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn LOT;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn dovi;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn soluong;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn XE;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn Hop;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn STATUSDOC;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn STATUSCNK;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBand2;
        private DevExpress.Utils.ImageCollection imageBT;
        private MyWindowsUIButtonPanel UIButton;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn4;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn6;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn7;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn8;
        private PanelControl panelPhieu;
        private DateEdit dateNX;
        private System.Windows.Forms.Label label2;
        private DevExpress.XtraBars.Navigation.TabPane tabPaneHVN;
        private DevExpress.XtraBars.Navigation.TabNavigationPage tabHN;
        private GroupControl groupControl1;
        private RadioGroup RDO_GXHN;
        private DevExpress.XtraBars.Navigation.TabNavigationPage tabVP;
        private GroupControl groupControl2;
        private RadioGroup radioGroup2;
        private SimpleButton btnUploadMilkrun;
        private SidePanel sidePanel6;
        private SidePanel sidePanel1;
        private CheckedListBoxControl CheckGX;
        private DevExpress.XtraGrid.Views.BandedGrid.BandedGridColumn bandedGridColumn9;
        private DevExpress.XtraGrid.Views.BandedGrid.GridBand gridBandDH;
        
    }
}
