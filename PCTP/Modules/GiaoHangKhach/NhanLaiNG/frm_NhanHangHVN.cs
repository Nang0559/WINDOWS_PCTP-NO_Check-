using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using DevExpress.XtraEditors.Repository;
using System.Data.SqlClient;
using System.Data.Linq;
using DevExpress.XtraGrid.EditForm.Helpers.Controls;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrintingLinks;
using System.Diagnostics;
using DevExpress.Export;
using DevExpress.Export.Xl;
using System.Reflection;
using DevExpress.Printing.ExportHelpers;
using DevExpress.XtraReports.UI;

using DevExpress.XtraReports.Parameters;

namespace PCTP.QRCODE_HVN.NhanLaiNG
{

    public partial class frm_NhanHangHVN : DevExpress.XtraEditors.XtraForm
    {
        public frm_NhanHangHVN()
        {
            InitializeComponent();
            // phiếu nhận
            GridVPHIEUNHAN.OptionsBehavior.EditingMode = GridEditingMode.EditForm;
            GridVPHIEUNHAN.OptionsEditForm.BindingMode = EditFormBindingMode.Cached;
            GridVPHIEUNHAN.EditFormPrepared += GridVPHIEUNHAN_EditFormPrepared;
            RepositoryItemDateEdit itemDateEdit = new RepositoryItemDateEdit();
            itemDateEdit.NullValuePrompt = DateTime.Today.ToString("dd/MM/yyyy");
            GridViewDONHANGDB.Columns["NGAYNHAN"].ColumnEdit = itemDateEdit;
            GridVPHIEUNHAN.Columns["NGAYLAP"].ColumnEdit = itemDateEdit;
            //GridVPHIEUNHAN.EditFormPrepared += GridVPHIEUNHAN_EditFormPrepared;
            GridVPHIEUNHAN.OptionsView.NewItemRowPosition = NewItemRowPosition.Top;
            // Chi Tiết
            GridViewDONHANGDB.OptionsBehavior.EditingMode = GridEditingMode.EditForm;
            GridViewDONHANGDB.OptionsEditForm.BindingMode = EditFormBindingMode.Cached;
            GridViewDONHANGDB.EditFormPrepared += GridViewDONHANG_EditFormPrepared;
            GridViewDONHANGDB.OptionsView.NewItemRowPosition = NewItemRowPosition.Top;

            // Thiết Lập
            string sql = "select ID,Code as MAHANG,Name as TENHANG,MinCloseQty from B20item where Name <> '' and ParentId>0 and IsActive = 1 and MinCloseQty <> 0 group by ID,Code,Name,MinCloseQty order by ID";

            PN = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            //BindingList<TACKMH> list = new BindingList<TACKMH>();

            //list = GetData();
            RepositoryItemComboBox riComboBox = new RepositoryItemComboBox();

            for (int i = 0; i <= 24; i++)
            {

                riComboBox.Items.Add(i.ToString("00"));
            }
            List<HVNCMPADD> CTYHVN = new List<HVNCMPADD>();
            CTYHVN.Add(new HVNCMPADD() { add = 1, HVNNAME = "HVN-(NHA MAY VINH PHUC)" });
            CTYHVN.Add(new HVNCMPADD() { add = 2, HVNNAME = "HVN-(NHA MAY HA NAM)" });
            RepositoryItemLookUpEdit NM = new RepositoryItemLookUpEdit();

            NM.DataSource = CTYHVN;
            NM.DisplayMember = "HVNNAME";
            NM.ValueMember = "add";
            GridViewDONHANGDB.Columns["GIOGIAO"].ColumnEdit = riComboBox;
            RepositoryItemMemoEdit NTE = new RepositoryItemMemoEdit();
            RepositoryItemLookUpEdit itemLookUpEdit = new RepositoryItemLookUpEdit();
            RepositoryItemLookUpEdit itemLookUpEdit1 = new RepositoryItemLookUpEdit();
            RepositoryItemCheckedComboBoxEdit lmh = new RepositoryItemCheckedComboBoxEdit();
            RepositoryItemGridLookUpEdit itemGridLookUpEdit = new RepositoryItemGridLookUpEdit();
            RepositoryItemTextEdit itemSTT = new RepositoryItemTextEdit();
            RepositoryItemTextEdit itpcs = new RepositoryItemTextEdit();
            itpcs.NullValuePrompt = "pcs";
            RepositoryItemLookUpEdit ItemIDNM = new RepositoryItemLookUpEdit();
            RepositoryItemTextEdit iTTTRa = new RepositoryItemTextEdit();
            RepositoryItemTextEdit IDHVN = new RepositoryItemTextEdit();
            iTTTRa.NullValuePrompt = "NG";
            itemLookUpEdit.DataSource = GetIds();
            //itemLookUpEdit1.DataSource = GetIds();
            lmh.DataSource = GetIds();
            itemGridLookUpEdit.DataSource = GetNames();
            GridViewDONHANGDB.Columns["Note"].ColumnEdit = NTE;
            GridViewDONHANGDB.Columns["MAHANG"].ColumnEdit = itemLookUpEdit;
            GridViewDONHANGDB.Columns["TENHANG"].ColumnEdit = itemGridLookUpEdit;
            GridViewDONHANGDB.Columns["STT"].ColumnEdit = itemSTT;
            GridViewDONHANGDB.Columns["DV"].ColumnEdit = itpcs;
            GridViewDONHANGDB.Columns["STATUS"].ColumnEdit = iTTTRa;
            GridVPHIEUNHAN.Columns["NAME"].ColumnEdit = itpcs;
            GridVPHIEUNHAN.Columns["NHAMAY"].ColumnEdit = IDHVN;
            //GridVPHIEUNHAN.Columns["MAHANGN"].ColumnEdit = itemLookUpEdit1;
            GridVPHIEUNHAN.Columns["NOTE"].ColumnEdit = NTE;
            GridVPHIEUNHAN.Columns["NHAMAYNAME"].ColumnEdit = NM;
            sql = "select DOCK_CODE,SUB_DOCK_CODE from cust_sched_line_tab where LINE_TYPE_ID = 'FIX' and customer_no = '100001' and  dock_code is not null group by DOCK_CODE,SUB_DOCK_CODE ";
            IFSVAL = ifs.ExecuteQuery(sql);
            RepositoryItemComboBox TRUYEN = new RepositoryItemComboBox();
            RepositoryItemComboBox CUA = new RepositoryItemComboBox();

            loadGDHDB();
            
            //gridControl1.DataSource = list;


            for (int i = 0; i < IFSVAL.Rows.Count; i++)
            {

                TRUYEN.Items.Add(IFSVAL.Rows[i]["DOCK_CODE"].ToString());
                if (IFSVAL.Rows[i]["SUB_DOCK_CODE"].ToString() != "")
                {
                    CUA.Items.Add(IFSVAL.Rows[i]["SUB_DOCK_CODE"].ToString());
                }
            }
            GridViewDONHANGDB.Columns["TRUYEN"].ColumnEdit = TRUYEN;

            GridViewDONHANGDB.Columns["CUA"].ColumnEdit = CUA;
            // loadGDHDB();
            GridVPHIEUNHAN.ValidateRow += GridVPHIEUNHAN_ValidateRow;
            GridViewDONHANGDB.ValidateRow += GridViewDONHANG_ValidateRow;
            // GridVPHIEUNHAN.CellValueChanged += GridVPHIEUNHAN_CellValueChanged;
            GridVPHIEUNHAN.RowUpdated += GridVPHIEUNHAN_RowUpdated;
            GridViewDONHANGDB.RowUpdated += GridViewDONHANG_RowUpdated;

            GridVPHIEUNHAN.ShowingPopupEditForm += GridVPHIEUNHAN_ShowingPopupEditForm;
            GridViewDONHANGDB.ShowingPopupEditForm += GridViewDONHANG_ShowingPopupEditForm;
            // This line of code is generated by Data Source Configuration Wizard
            gridCPHIEUNHAN.DataSource = new PCTP.QRCODE_HVN.NhanLaiNG.NHAN_HVN_NGDataContext().TMPPHIEUNHANDBs;
            // This line of code is generated by Data Source Configuration Wizard
            //gridCtrDONHANG.DataSource = new PCTP.QRCODE_HVN.NhanLaiNG.NHAN_HVN_NGDataContext().TMPPHIEUGIAOHANGDBs.Where(t => t.IDP == 1);
            // This line of code is generated by Data Source Configuration Wizard
            gridCtrDONHANGDB.DataSource = new PCTP.QRCODE_HVN.NhanLaiNG.NHAN_HVN_NGDataContext().TMPPHIEUGIAOHANGDBCTs;
            GridVPHIEUNHAN.InitNewRow += GridVPHIEUNHAN_InitNewRow;
            GridViewDONHANGDB.InitNewRow += GridViewDONHANGDB_InitNewRow;


            // This line of code is generated by Data Source Configuration Wizard
            gridCPHIEUNHAN.DataSource = new PCTP.QRCODE_HVN.NhanLaiNG.NHAN_HVN_NGDataContext().TMPPHIEUNHANDBs;
        }
        DataTable PN = new DataTable();
        DataTable IFSVAL = new DataTable();
        DataTable IDPN = new DataTable();
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        IFSPROVIDER ifs = new IFSPROVIDER();
        LookUpEdit lookUp,LKNM, TENNMHVN;
        ComboBoxEdit LKIDPN = new ComboBoxEdit();
        CheckedComboBoxEdit CBMH;
        GridLookUpEdit gridLookUp;
        TextEdit Stt, IDNM, pcs,TTTRA,Note,IDP,name;
        DateTimePicker NN;
        DateEdit NGAYLAPP;
        public void loadBinding()
        {
            Binding noteBinding = new Binding("Text", IDNM + "" + name + "" + NGAYLAPP, "Text");
            //Note.DataBindings.Add(noteBinding);
        }
        private void GridVPHIEUNHAN_InitNewRow(object sender, InitNewRowEventArgs e)
        {
            GridView view = sender as GridView;
            view.SetRowCellValue(e.RowHandle, view.Columns["NGAYLAP"], DateTime.Today);
           
        }
        private void GridViewDONHANGDB_InitNewRow(object sender, InitNewRowEventArgs e)
        {
            GridView view = sender as GridView;
            view.SetRowCellValue(e.RowHandle, view.Columns["DV"], "pcs");

            view.SetRowCellValue(e.RowHandle, view.Columns["STATUS"], "NG");
        }
        private void loadGDHDB()
        {
            string sql = "select IDP,NAME,NGAYLAP from TMPPHIEUNHANDB";
            IDPN = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
           
            RepositoryItemComboBox LITEMIDPN = new RepositoryItemComboBox();
            // GridViewDONHANGDB.Columns["Note"].ColumnEdit = NTE;
            for (int i = 0; i < IDPN.Rows.Count; i++)
            {
                LITEMIDPN.Items.Add(IDPN.Rows[i]["IDP"].ToString());
            }
           
            
            //gridCtrDONHANGDB.DataSource = list;
            GridViewDONHANGDB.Columns["IDP"].ColumnEdit = LITEMIDPN;
        }
        private void GridViewDONHANG_EditFormPrepared(object sender, EditFormPreparedEventArgs e)
        {
            loadGDHDB();
            foreach (Control c in e.BindableControls)
            {
                string columnName = c.Tag.ToString().Replace(@"EditValue/", "");
                if (columnName == "NGAYGIAO")
                {
                    BaseEdit edit = c as BaseEdit;
                    edit.EditValueChanged += edit_EditValueChanged;
                }
                //if(columnName=="DV")
                //{
                //    BaseEdit edit = c as BaseEdit;
                //    edit.Visible = false;
                //}
                
            }
            
            e.FocusField("MAHANG");
            
        }
        private void GridVPHIEUNHAN_EditFormPrepared(object sender, EditFormPreparedEventArgs e)
        {
            //foreach (Control c in e.BindableControls)
            //{
            //    string columnName = c.Tag.ToString().Replace(@"EditValue/", "");
            //    if (columnName == "NGAYNHAN")
            //    {
            //        DateTimePicker edit = c as DateTimePicker;
            //        edit.Value = DateTime.Now.ToShortDateString();
            //    }

            //}
            loadBinding();
            e.FocusField("NAME");
        }
        private void edit_EditValueChanged(object sender, EventArgs e)
        {
            DateEdit edit = (sender as DateEdit);
            string columnName = edit.Tag.ToString().Replace(@"EditValue/", "");
            string TC = columnName;
            string OL = edit.OldEditValue.ToString();
            string NE = edit.EditValue.ToString();
            if (OL == "" || OL != NE)
            {
                edit.DataBindings[0].WriteValue();

                string GTC = edit.DateTime.ToString("yyyyMMdd");
                string sql = " select case when max(STT) is null then 0 else max(STT) end  from tmpphieugiaohangdb where CONVERT(VARCHAR(10),  NGAYGIAO, 112)   = '" + GTC + "'";
                int STT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql)) + 1;
                Stt.Text = STT.ToString();

            }
        }
        private void LookUpEdit1_ProcessNewValue(object sender, DevExpress.XtraEditors.Controls.ProcessNewValueEventArgs e)
        {
            MessageBox.Show(e.DisplayValue.ToString());
        }
        private void GridViewDONHANG_ShowingPopupEditForm(object sender, ShowingPopupEditFormEventArgs e)
        {
            //loadGDHDB();
            LKIDPN = e.BindableControls["IDP"] as ComboBoxEdit;
            //LKIDPN.EditValueChanged += LKIDPN_EditValueChanged;
            int SELECTPID = GridVPHIEUNHAN.GetFocusedRowCellValue("IDP") != null ? (int)GridVPHIEUNHAN.GetFocusedRowCellValue("IDP") : 0;
            if(SELECTPID !=0)
            {
                LKIDPN.EditValue = SELECTPID;
                LKIDPN.DataBindings[0].WriteValue();
                LKIDPN.ReadOnly = true;
            }
            else
            {
                LKIDPN.ReadOnly = false;
            }
            lookUp = e.BindableControls["MAHANG"] as LookUpEdit;
            lookUp.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard; 
            lookUp.ProcessNewValue += LookUpEdit1_ProcessNewValue;
            lookUp.EditValueChanged += LookUp_EditValueChanged;

            gridLookUp = e.BindableControls["TENHANG"] as GridLookUpEdit;
            gridLookUp.ReadOnly = true;

            Stt = e.BindableControls["STT"] as TextEdit;
            
            Stt.ReadOnly = true;
            pcs = e.BindableControls["DV"] as TextEdit;
            pcs.Text = "pcs";
            pcs.DataBindings[0].WriteValue();
            pcs.ReadOnly = true;
            TTTRA = e.BindableControls["STATUS"] as TextEdit;
            TTTRA.Text = "NG";
            TTTRA.DataBindings[0].WriteValue();
            TTTRA.ReadOnly = true;
            //e.EditForm.FormClosing += EditForm_FormClosing;
            foreach (Control control in e.EditForm.Controls)
            {
                if (!(control is EditFormContainer))
                {
                    continue;
                }
                foreach (Control nestedControl in control.Controls)
                {
                    if (!(nestedControl is PanelControl))
                    {
                        continue;
                    }
                    foreach (Control button in nestedControl.Controls)
                    {
                        if (!(button is SimpleButton))
                        {
                            continue;
                        }
                        var simpleButton = button as SimpleButton;
                        //simpleButton.Click -= editFormUpdateButton_Click;
                        //simpleButton.Click += editFormUpdateButton_Click;
                    }
                }
            }
        }
        private void GridVPHIEUNHAN_ShowingPopupEditForm(object sender, ShowingPopupEditFormEventArgs e)
        {
            TENNMHVN = e.BindableControls["NHAMAYNAME"] as LookUpEdit;

            TENNMHVN.EditValueChanged += TENNMHVN_EditValueChanged;
          
            IDNM = e.BindableControls["NHAMAY"] as TextEdit;
            IDNM.ReadOnly = true;
            //IDNM.EditValueChanged += IDNM_EditValueChanged;
            IDP = e.BindableControls["IDP"] as TextEdit;
            // IDP.EditValueChanged += IDP_EditValueChanged;
            name = e.BindableControls["NAME"] as TextEdit;
            //lookUp.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            //lookUp.ProcessNewValue += LookUpEdit1_ProcessNewValue;
            //lookUp.EditValueChanged += LookUp_EditValueChanged;
            NGAYLAPP = e.BindableControls["NGAYLAP"] as DateEdit;
            NGAYLAPP.EditValueChanged += NGAYLAPP_EditValueChanged;
            //CBMH = e.BindableControls["NAME"] as CheckedComboBoxEdit;
            //CBMH.EditValueChanged += CBMH_EditValueChanged;
            IDP.ReadOnly = true;
            
           
            Note = e.BindableControls["NOTE"] as MemoEdit;
            //e.EditForm.FormClosing += EditForm_FormClosing;
            foreach (Control control in e.EditForm.Controls)
            {
                if (!(control is EditFormContainer))
                {
                    continue;
                }
                foreach (Control nestedControl in control.Controls)
                {
                    if (!(nestedControl is PanelControl))
                    {
                        continue;
                    }
                    foreach (Control button in nestedControl.Controls)
                    {
                        if (!(button is SimpleButton))
                        {
                            continue;
                        }
                        var simpleButton = button as SimpleButton;
                        //simpleButton.Click -= editFormUpdateButton_Click;
                        //simpleButton.Click += editFormUpdateButton_Click;
                    }
                }
            }
            
        }

        private void TENNMHVN_EditValueChanged(object sender, EventArgs e)
        {
            var IDNMVL = TENNMHVN.EditValue;
            TENNMHVN.DataBindings[0].WriteValue();
            IDNM.EditValue = IDNMVL.ToString();
            IDNM.DataBindings[0].WriteValue();
            //
        }

      

        private void NGAYLAPP_EditValueChanged(object sender, EventArgs e)
        {
            var MAHANG = name.EditValue;
            string NGL = NGAYLAPP.DateTime.ToString("dd/MM/yyyy");
           
            NGAYLAPP.DataBindings[0].WriteValue();
             Note.EditValue = "Nhận Hàng : " + MAHANG.ToString() + " từ HVN ngày  " + NGL;
            Note.DataBindings[0].WriteValue();
        }

        private void CBMH_EditValueChanged(object sender, EventArgs e)
        {
            var MAHANG = CBMH.EditValue;
            string NGL = NGAYLAPP.DateTime.ToString("dd/MM/yyyy");
            Note.EditValue = "Nhận Hàng : " + MAHANG.ToString() + " từ HVN ngày  " + NGL;
            Note.DataBindings[0].WriteValue();
        }

       
        private void LookUp_EditValueChanged(object sender, EventArgs e)
        {
            var MAHANG = lookUp.EditValue;
            for (int i = 0; i < PN.Rows.Count; i++)
            {

                if (PN.Rows[i]["MAHANG"].ToString() == MAHANG)
                {
                    lookUp.DataBindings[0].WriteValue();
                    gridLookUp.EditValue = PN.Rows[i]["TENHANG"];
                    gridLookUp.DataBindings[0].WriteValue();
                }
                if (PN.Rows[i]["MAHANG"].ToString() == MAHANG)
                {
                    lookUp.DataBindings[0].WriteValue();
                    gridLookUp.EditValue = PN.Rows[i]["TENHANG"];
                    gridLookUp.DataBindings[0].WriteValue();
                }
            }
            //string N_T = Nt.DateTime.ToString("YYYYMMDD");
            //string sql = "select max(STT) from tmpphieugiaohangdb where CONVERT(VARCHAR(10),  NGAYTRA, 112) AS [YYYYMMDD]  = '" + N_T + "'";
            //int STT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            //Stt.Text = STT.ToString();
        }
        private void frm_NhanHangHVN_Load(object sender, EventArgs e)
        {

            //gridCPHIEUNHAN.ForceInitialize();
        }

        private BindingList<string> GetNames()
        {
            BindingList<string> list = new BindingList<string>();

            for (int i = 0; i < PN.Rows.Count; i++)
                list.Add(PN.Rows[i]["TENHANG"].ToString());
            return list;
        }

        private BindingList<string> GetIds()
        {
            BindingList<string> list = new BindingList<string>();

            for (int i = 0; i < PN.Rows.Count; i++)
                list.Add(PN.Rows[i]["MAHANG"].ToString());

            return list;
        }
       
        private void GridVPHIEUNHAN_ValidateRow(object sender, DevExpress.XtraGrid.Views.Base.ValidateRowEventArgs e)
        {
            //GridView view = (GridView)sender;
            //e.ErrorText = "";
            //string NameP = (string)GridVPHIEUNHAN.GetRowCellValue(e.RowHandle, "NAME");
            ////string NgayNha = (string)GridVPHIEUNHAN.GetRowCellValue(e.RowHandle, "NGAYNHAN");
            ////if (DateTime.TryParse(NgayNha) == true)
            ////    e.ErrorText += "Invalid product price. ";

            //if (NameP == " " || NameP==null)
            //    e.ErrorText += "Invalid product status. ";
            //if (e.ErrorText != "")
            //    e.Valid = false;
        }
        private void GridViewDONHANG_ValidateRow(object sender, DevExpress.XtraGrid.Views.Base.ValidateRowEventArgs e)
        {
            GridView view = (GridView)sender;
            e.ErrorText = "";
            int IDPN = 0;
            if (GridViewDONHANGDB.GetRowCellValue(e.RowHandle, "IDP") != "" && GridViewDONHANGDB.GetRowCellValue(e.RowHandle, "IDP") != null)
            {
                IDPN = (int)GridViewDONHANGDB.GetRowCellValue(e.RowHandle, "IDP");
            }
            
            string MH = (string)GridViewDONHANGDB.GetRowCellValue(e.RowHandle, "MAHANG");
            if (MH == "" || MH == null)
                e.ErrorText += "Chưa chọn mã hàng ! ";
            int SL = GridViewDONHANGDB.GetRowCellValue(e.RowHandle, "SOLUONG") != null ? (Int16)GridViewDONHANGDB.GetRowCellValue(e.RowHandle, "SOLUONG"):0;
            if (SL == 0 )
                e.ErrorText += "Chưa chọn số lượng ! ";
            if (IDPN == 0)
                e.ErrorText += "Hãy chọn số phiếu ! ";
            if (e.ErrorText != "")
                e.Valid = false;
            
             
        }

        private void GridVPHIEUNHAN_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            int ID = GridVPHIEUNHAN.GetRowCellValue(e.FocusedRowHandle, "IDP") != null ? (int)GridVPHIEUNHAN.GetRowCellValue(e.FocusedRowHandle, "IDP"):0; 
            GridViewDONHANGDB.Columns["IDP"].FilterInfo = new ColumnFilterInfo("[IDP] = " + ID );
        }

        private void controlNavigator2_ButtonClick(object sender, NavigatorButtonClickEventArgs e)
        {
            if (e.Button.ButtonType == NavigatorButtonType.Remove )
            {
                if (MessageBox.Show("Delete row?", "Confirmation", MessageBoxButtons.YesNo) !=
                 DialogResult.Yes)
                    return;
                GridView view = GridVPHIEUNHAN as GridView;
                view.DeleteSelectedRows();
                (gridCPHIEUNHAN.DataSource as Table<TMPPHIEUNHANDB>).Context.SubmitChanges();
            }
        }

        private void GridVPHIEUNHAN_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            (gridCPHIEUNHAN.DataSource as Table<TMPPHIEUNHANDB>).Context.SubmitChanges();
            loadGDHDB();
        }

        private void GridViewDONHANGDB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.Control)
            {
                if (MessageBox.Show("Delete row?", "Confirmation", MessageBoxButtons.YesNo) !=
                  DialogResult.Yes)
                    return;

                GridView view = sender as GridView;
                
                view.DeleteRow(view.FocusedRowHandle);
                (gridCtrDONHANGDB.DataSource as Table<TMPPHIEUGIAOHANGDBCT>).Context.SubmitChanges();
                
            }
        }

        

        private void GridVPHIEUNHAN_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.Control)
            {
                if (MessageBox.Show("Xóa Dòng được chọn ?", "Confirmation", MessageBoxButtons.YesNo) !=
                  DialogResult.Yes)
                    return;
                GridView view = sender as GridView;
                int IDP = view.GetFocusedRowCellValue("IDP") != null ? (int)view.GetFocusedRowCellValue("IDP") : 0;
                string sql = "select count(*) from TMPPHIEUGIAOHANGDBCT where IDP = " + IDP;
                if (sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql) != "0")
                {
                    MessageBox.Show("Hãy xóa phần chi tiết phiếu hết trước ! ", "Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                view.DeleteRow(view.FocusedRowHandle);
                (gridCPHIEUNHAN.DataSource as Table<TMPPHIEUNHANDB>).Context.SubmitChanges();
                loadGDHDB();
            }
        }

        private void GridViewDONHANG_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            (gridCtrDONHANGDB.DataSource as Table<TMPPHIEUGIAOHANGDBCT>).Context.SubmitChanges();
        }

        public void exportToExcel()
        {
            GridViewDONHANGDB.ExpandAllGroups();
            GridVPHIEUNHAN.ExpandAllGroups();
            string filePath = "";
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Excel(.xlsx) | *.xlsx";
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    var printingSystem = new PrintingSystemBase();
                    var compositeLink = new CompositeLinkBase();
                    compositeLink.PrintingSystemBase = printingSystem;
                    var link1 = new PrintableComponentLinkBase();
                    link1.Component = gridCtrDONHANGDB;
                    var link2 = new PrintableComponentLinkBase();
                    link2.Component = gridCPHIEUNHAN;
                    
                    compositeLink.Links.Add(link1);
                    compositeLink.Links.Add(link2);
                 
                    var options = new XlsxExportOptions();
                    options.ExportMode = XlsxExportMode.SingleFile;
                    //options.ExportMode = XlsxExportMode.SingleFilePageByPage;
                    options.SheetName = "Thông Tin Tìm Kiếm";
                    //compositeLink.
                    //compositeLink.CreatePageForEachLink();
                    compositeLink.ExportToXlsx(saveDialog.FileName, options);
                    filePath = saveDialog.FileName;
                    DialogResult dlr = MessageBox.Show("Bạn có muốn mở file?", "Xuất file thành công!", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dlr == DialogResult.Yes)
                    {
                        Process.Start(filePath);
                    }
                }
            }
        }
        #region Export excell 
        //void gridView1_CustomUnboundColumnData(object sender, CustomColumnDataEventArgs e)
        //{
        //    GridView view = sender as GridView;
        //    if (e.Column == categoryName)
        //        if (e.IsGetData)
        //        {
        //            int id = (int)view.GetListSourceRowCellValue(e.ListSourceRowIndex, colCategoryID);
        //            e.Value = nwindDataSet.Categories.FindByCategoryID(id).CategoryName;
        //        }
        //}

        //private void cmdExport_Click(object sender, EventArgs e)
        //{
        //     Report_sub.nhanNGHVN  F= new Report_sub.nhanNGHVN();
          
            
        //    ReportPrintTool printTool = new ReportPrintTool(F);
        //    printTool.ShowPreviewDialog();
          

            
        //}

        #region #AfterAddRowEvent
        void options_AfterAddRow(AfterAddRowEventArgs e)
        {
            // Merge cells in rows that correspond to the grid's group rows.
            if (e.DataSourceRowIndex < 0)
            {
                e.ExportContext.MergeCells(new XlCellRange(new XlCellPosition(0, e.DocumentRow - 1), new XlCellPosition(5, e.DocumentRow - 1)));
            }
        }
        #endregion #AfterAddRowEvent

        #region #CustomizeCellEvent
        // Specify the value alignment for Discontinued field.
        XlCellAlignment aligmentForDiscontinuedColumn = new XlCellAlignment()
        {
            HorizontalAlignment = XlHorizontalAlignment.Center,
            VerticalAlignment = XlVerticalAlignment.Center
        };

        void options_CustomizeCell(CustomizeCellEventArgs e)
        {
            // Substitute Boolean values within the Discontinued column by special symbols.
            if (e.ColumnFieldName == "Discontinued")
            {
                if (e.Value is bool)
                {
                    e.Handled = true;
                    e.Formatting.Alignment = aligmentForDiscontinuedColumn;
                    e.Value = ((bool)e.Value) ? "☑" : "☐";
                }
            }
        }
        #endregion #CustomizeCellEvent

        #region #CustomizeSheetHeaderEvent
        delegate void AddCells(ContextEventArgs e, XlFormattingObject formatFirstCell, XlFormattingObject formatSecondCell);

        Dictionary<int, AddCells> methods = CreateMethodSet();

        static Dictionary<int, AddCells> CreateMethodSet()
        {
            var dictionary = new Dictionary<int, AddCells>();
            dictionary.Add(9, AddAddressRow);
            dictionary.Add(10, AddAddressLocationCityRow);
            dictionary.Add(11, AddPhoneRow);
            dictionary.Add(12, AddFaxRow);
            dictionary.Add(13, AddEmailRow);
            return dictionary;
        }
        Bitmap imageToHeader;
        void options_CustomizeSheetHeader(ContextEventArgs e)
        {
            // Specify cell formatting. 
            var formatFirstCell = CreateXlFormattingObject(true, 24);
            var formatSecondCell = CreateXlFormattingObject(true, 18);
            // Add new rows displaying custom information. 
            for (var i = 0; i < 15; i++)
            {
                AddCells addCellMethod;
                if (methods.TryGetValue(i, out addCellMethod))
                    addCellMethod(e, formatFirstCell, formatSecondCell);
                else e.ExportContext.AddRow();
            }
            // Merge specific cells.
            MergeCells(e);
            // Add an image to the top of the document.
            if (imageToHeader == null)
            {

                using (var fileStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GridDataAwareExportCustomization.Resources.1.jpg"))
                    if (fileStream != null)
                        imageToHeader = new Bitmap(Image.FromStream(fileStream));
            }
            var imageToHeaderRange = new XlCellRange(new XlCellPosition(0, 0), new XlCellPosition(5, 7));
            e.ExportContext.MergeCells(imageToHeaderRange);
            e.ExportContext.InsertImage(imageToHeader, imageToHeaderRange);
            e.ExportContext.MergeCells(new XlCellRange(new XlCellPosition(0, 8), new XlCellPosition(5, 8)));
        }

        static void AddEmailRow(ContextEventArgs e, XlFormattingObject formatFirstCell,
            XlFormattingObject formatSecondCell)
        {
            var emailCellName = CreateCell("Email :", formatFirstCell);
            var emailCellLocation = CreateCell("corpsales@devav.com", formatSecondCell);
            emailCellLocation.Hyperlink = "corpsales@devav.com";
            e.ExportContext.AddRow(new[] { emailCellName, null, emailCellLocation });
        }
        static void AddFaxRow(ContextEventArgs e, XlFormattingObject formatFirstCell,
            XlFormattingObject formatSecondCell)
        {
            var faxCellName = CreateCell("Fax :", formatFirstCell);
            var faxCellLocation = CreateCell("+ 1 (213) 555-1824", formatSecondCell);
            e.ExportContext.AddRow(new[] { faxCellName, null, faxCellLocation });
        }
        static void AddPhoneRow(ContextEventArgs e, XlFormattingObject formatFirstCell,
            XlFormattingObject formatSecondCell)
        {
            var phoneCellName = CreateCell("Phone :", formatFirstCell);
            var phoneCellLocation = CreateCell("+ 1 (213) 555-2828", formatSecondCell);
            e.ExportContext.AddRow(new[] { phoneCellName, null, phoneCellLocation });
        }
        static void AddAddressLocationCityRow(ContextEventArgs e, XlFormattingObject formatFirstCell,
            XlFormattingObject formatSecondCell)
        {
            var AddressLocationCityCell = CreateCell("Los Angeles CA 90731 USA", formatSecondCell);
            e.ExportContext.AddRow(new[] { null, null, AddressLocationCityCell });
        }
        static void AddAddressRow(ContextEventArgs e, XlFormattingObject formatFirstCell,
            XlFormattingObject formatSecondCell)
        {
            var AddressCellName = CreateCell("Address: ", formatFirstCell);
            var AddresssCellLocation = CreateCell("807 West Paseo Del Mar", formatSecondCell);
            e.ExportContext.AddRow(new[] { AddressCellName, null, AddresssCellLocation });
        }

        // Create a new cell with a specified value and format settings.
        static CellObject CreateCell(object value, XlFormattingObject formatCell)
        {
            return new CellObject { Value = value, Formatting = formatCell };
        }

        // Merge specific cells.
        static void MergeCells(ContextEventArgs e)
        {
            MergeCells(e, 2, 9, 5, 9);
            MergeCells(e, 0, 9, 1, 10);
            MergeCells(e, 2, 10, 5, 10);
            MergeCells(e, 0, 11, 1, 11);
            MergeCells(e, 2, 11, 5, 11);
            MergeCells(e, 0, 12, 1, 12);
            MergeCells(e, 2, 12, 5, 12);
            MergeCells(e, 0, 13, 1, 13);
            MergeCells(e, 2, 13, 5, 13);
            MergeCells(e, 0, 14, 5, 14);
        }

       

        static void MergeCells(ContextEventArgs e, int left, int top, int right, int bottom)
        {
            e.ExportContext.MergeCells(new XlCellRange(new XlCellPosition(left, top), new XlCellPosition(right, bottom)));
        }

        // Specify a cell's alignment and font settings. 
        static XlFormattingObject CreateXlFormattingObject(bool bold, double size)
        {
            var cellFormat = new XlFormattingObject
            {
                Font = new XlCellFont
                {
                    Bold = bold,
                    Size = size
                },
                Alignment = new XlCellAlignment
                {
                    RelativeIndent = 10,
                    HorizontalAlignment = XlHorizontalAlignment.Center,
                    VerticalAlignment = XlVerticalAlignment.Center
                }
            };
            return cellFormat;
        }
        #endregion #CustomizeSheetHeaderEvent

        #region #CustomizeSheetFooterEvent
        void options_CustomizeSheetFooter(ContextEventArgs e)
        {
            // Add an empty row to the document's footer.
            e.ExportContext.AddRow();

            // Create a new row.
            var firstRow = new CellObject();
            // Specify row values.
            firstRow.Value = @"The report is generated from the NorthWind database.";
            // Specify the cell content alignment and font settings.
            var rowFormatting = CreateXlFormattingObject(true, 18);
            rowFormatting.Alignment.HorizontalAlignment = XlHorizontalAlignment.Left;
            firstRow.Formatting = rowFormatting;
            // Add the created row to the output document. 
            e.ExportContext.AddRow(new[] { firstRow });

            // Create one more row.
            var secondRow = new CellObject();
            // Specify the row value. 
            secondRow.Value = @"The addresses and phone numbers are fictitious.";
            // Change the row's font settings.
            rowFormatting.Font.Size = 14;
            rowFormatting.Font.Bold = false;
            rowFormatting.Font.Italic = true;
            secondRow.Formatting = rowFormatting;
            // Add this row to the output document.
            e.ExportContext.AddRow(new[] { secondRow });
        }
        #endregion #CustomizeSheetFooterEvent

        #region #CustomizeSheetSettingsEvent
        void options_CustomizeSheetSettings(CustomizeSheetEventArgs e)
        {
            // Anchor the output document's header to the top and set its fixed height. 
            const int lastHeaderRowIndex = 15;
            e.ExportContext.SetFixedHeader(lastHeaderRowIndex);
            // Add the AutoFilter button to the document's cells corresponding to the grid column headers.
            e.ExportContext.AddAutoFilter(new XlCellRange(new XlCellPosition(0, lastHeaderRowIndex), new XlCellPosition(5, 100)));
        }
        #endregion #CustomizeSheetSettingsEvent
        #endregion
    }
    public class HVNCMPADD
    {
        public int add { get; set; }
        public string  HVNNAME { get; set; }
    }
}