using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.EditForm.Helpers.Controls;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using System;
using System.ComponentModel;
using System.Data;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN.NhanLaiNG
{
    public partial class frm_NhanNGHVN : DevExpress.XtraEditors.XtraForm
    {
        public frm_NhanNGHVN()
        {
            InitializeComponent();
            gridNhanNGHVN.OptionsBehavior.EditingMode = GridEditingMode.EditForm;
            gridNhanNGHVN.OptionsEditForm.BindingMode = EditFormBindingMode.Cached;
            gridNhanNGHVN.EditFormPrepared += gridNhanNGHVN_EditFormPrepared;
            gridNhanNGHVN.OptionsView.NewItemRowPosition = NewItemRowPosition.Top;
            gridNhanNGHVN.InitNewRow += (s, e) => {
                GridView view = s as GridView;
                // Set the new row cell value
                view.SetRowCellValue(e.RowHandle, view.Columns["RecordDate"], DateTime.Today);
                view.SetRowCellValue(e.RowHandle, view.Columns["Name"], "CustomName");
                // Obtain the new row cell value 
                int newRowID = Convert.ToInt32(view.GetRowCellValue(e.RowHandle, "ID"));
                view.SetRowCellValue(e.RowHandle, view.Columns["Notes"], string.Format("Row ID: {0}", newRowID));
            };
            string sql = "select ID,Code as MAHANG,Name as TENHANG,MinCloseQty from B20item where Name <> '' and ParentId>0 and IsActive = 1 and MinCloseQty <> 0 group by ID,Code,Name,MinCloseQty order by ID";

            PN = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            //BindingList<TACKMH> list = new BindingList<TACKMH>();

            //list = GetData();

            RepositoryItemLookUpEdit itemLookUpEdit = new RepositoryItemLookUpEdit();
            RepositoryItemGridLookUpEdit itemGridLookUpEdit = new RepositoryItemGridLookUpEdit();
            RepositoryItemTextEdit itemSTT = new RepositoryItemTextEdit();


            itemLookUpEdit.DataSource = GetIds();

            itemGridLookUpEdit.DataSource = GetNames();

            //gridControl1.DataSource = list;

            gridNhanNGHVN.Columns["MAHANG"].ColumnEdit = itemLookUpEdit;
            gridNhanNGHVN.Columns["TENHANG"].ColumnEdit = itemGridLookUpEdit;
            gridNhanNGHVN.Columns["STT"].ColumnEdit = itemSTT;


            gridNhanNGHVN.ShowingPopupEditForm += gridNhanNGHVN_ShowingPopupEditForm;
        }
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public DataTable PN = new DataTable();
        LookUpEdit lookUp;
        GridLookUpEdit gridLookUp;

        TextEdit Stt,IDP;
        private void gridNhanNGHVN_EditFormPrepared(object sender, EditFormPreparedEventArgs e)
        {
            foreach (Control c in e.BindableControls)
            {
                string columnName = c.Tag.ToString().Replace(@"EditValue/", "");
                if (columnName == "NGAYGIAO")
                {
                    BaseEdit edit = c as BaseEdit;
                    edit.EditValueChanged += edit_EditValueChanged;
                }
                if (columnName == "STT")
                {
                    BaseEdit STT = c as BaseEdit;
                    STT.EditValueChanged += STT_EditValueChanged;
                }
            }
        }
        private void STT_EditValueChanged(object sender, EventArgs e)
        {
            Stt.DataBindings[0].WriteValue();
        }
        private void edit_EditValueChanged(object sender, EventArgs e)
        {
            DateEdit edit = (sender as DateEdit);
            string columnName = edit.Tag.ToString().Replace(@"EditValue/", "");
            string TC = columnName;
            string OL = edit.OldEditValue.ToString();
            string NE = edit.EditValue.ToString();
            if (OL =="" || OL != NE)
            {
                edit.DataBindings[0].WriteValue();

                string GTC = edit.DateTime.ToString("yyyyMMdd");
                string sql = " select case when max(STT) is null then 0 else max(STT) end  from tmpphieugiaohangdb where CONVERT(VARCHAR(10),  NGAYGIAO, 112)   = '" + GTC + "'";
                int STT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql)) + 1;
                Stt.Text = STT.ToString();

            }
        }
        private void gridNhanNGHVN_ShowingPopupEditForm(object sender, ShowingPopupEditFormEventArgs e)
        {
            lookUp = e.BindableControls["MAHANG"] as LookUpEdit;
            lookUp.EditValueChanged += LookUp_EditValueChanged;

            gridLookUp = e.BindableControls["TENHANG"] as GridLookUpEdit;
            gridLookUp.ReadOnly = true;

            Stt = e.BindableControls["STT"] as TextEdit;
            Stt.ReadOnly = true;
            IDP = e.BindableControls["IDP"] as TextEdit;
            IDP.ReadOnly = true;
            e.EditForm.FormClosing += EditForm_FormClosing;
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
                        simpleButton.Click -= editFormUpdateButton_Click;
                        simpleButton.Click += editFormUpdateButton_Click;
                    }
                }
            }
        }
        private void editFormUpdateButton_Click(object sender, EventArgs e)
        {
            GridView GV =  sender as GridView;
            int ID = int.Parse(GV.GetFocusedRowCellValue("IDP").ToString());
            string sql = "if exists  (select * from TMPPHIEUGIAOHANGDB where IDP =  "+ID+ ") begin update TMPPHIEUGIAOHANGDB set  ";
            sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);

        }

        private void EditForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            lookUp.EditValueChanged -= LookUp_EditValueChanged;
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
            }
            //string N_T = Nt.DateTime.ToString("YYYYMMDD");
            //string sql = "select max(STT) from tmpphieugiaohangdb where CONVERT(VARCHAR(10),  NGAYTRA, 112) AS [YYYYMMDD]  = '" + N_T + "'";
            //int STT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            //Stt.Text = STT.ToString();
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

        private BindingList<TACKMH> GetData()
        {
            BindingList<TACKMH> list = new BindingList<TACKMH>();
            string sql = "select ID,Code as MAHANG,Name as TENHANG,MinCloseQty from B20item where Name <> '' and ParentId>0 and IsActive = 1 and MinCloseQty <> 0 group by ID,Code,Name,MinCloseQty order by ID";
            PN = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            for (int i = 0; i < PN.Rows.Count; i++)
                list.Add(new TACKMH() { MAHANG = PN.Rows[i]["MAHANG"].ToString(), TENHANG = PN.Rows[i]["TENHANG"].ToString() });
            return list;
        }
        private void load()
        {
            DataTable DONHANG = new DataTable();
            string sql = "select IDP, NAME, GGFCC, STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU, NHAMAY, ADDNM, HOP, STATUSDOC, NGAYNHAN, TRANGTHAINHAN, NOTE FROM TMPPHIEUGIAOHANGDB ";

            DONHANG = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            gridCNhanNGHVN.DataSource = DONHANG;

            sql = "select ID,Code as MAHANG,Name as TENHANG,MinCloseQty from B20item where Name <> '' and ParentId>0 and IsActive = 1 and MinCloseQty <> 0 group by ID,Code,Name,MinCloseQty order by ID";

            PN = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            //BindingList<TACKMH> list = new BindingList<TACKMH>();

            //list = GetData();

            RepositoryItemLookUpEdit itemLookUpEdit = new RepositoryItemLookUpEdit();
            RepositoryItemGridLookUpEdit itemGridLookUpEdit = new RepositoryItemGridLookUpEdit();
            RepositoryItemTextEdit itemSTT = new RepositoryItemTextEdit();


            itemLookUpEdit.DataSource = GetIds();

            itemGridLookUpEdit.DataSource = GetNames();

            //gridControl1.DataSource = list;

            gridNhanNGHVN.Columns["MAHANG"].ColumnEdit = itemLookUpEdit;
            gridNhanNGHVN.Columns["TENHANG"].ColumnEdit = itemGridLookUpEdit;
            gridNhanNGHVN.Columns["STT"].ColumnEdit = itemSTT;


            gridNhanNGHVN.ShowingPopupEditForm += gridNhanNGHVN_ShowingPopupEditForm;
        }

        private void frm_NhanNGHVN_Load(object sender, EventArgs e)
        {
            // TODO: This line of code loads data into the 'b7R2_FCCDataSet.TMPPHIEUGIAOHANGDB' table. You can move, or remove it, as needed.
         
           // load();
        }

        private void windowsUIButtonPanel1_ButtonClick(object sender, DevExpress.XtraBars.Docking2010.ButtonEventArgs e)
        {
            string tag = ((WindowsUIButton)e.Button).Caption.ToString();
            switch (tag)
            {
                case "Thêm":
                    gridNhanNGHVN.AddNewRow();
                    break;
            }
        }

    }
}