using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using System.Data;
using System.Collections;

namespace PCTP.QRCODE_HVN.PGH
{
    public partial class Edit_DH : DevExpress.XtraGrid.Views.Grid.EditFormUserControl
    {
        ClassSQL.SQLPROVIDER sqlBRV = new ClassSQL.SQLPROVIDER();
        public Edit_DH()
        {
            InitializeComponent();

        }
      
        private GridView _MyView = null/* TODO Change to default(_) if this is not a reference type */;
        public GridView MyView
        {
            get
            {
                return _MyView;
            }
            set
            {
                _MyView = value;
            }
        }

        public Edit_DH(GridView view)
        {
            MyView = view;
            InitializeComponent();
        }
        

       
        private void load()
        {
            string MH = label2.Text;
            string sql = "select LOT,slconlai from stocktp where part =  '" + MH.Trim() + "' and slconlai >0";
            DataTable DML = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            lookUpEdit1.Properties.DataSource = DML;
            lookUpEdit1.Properties.ValueMember = "slconlai";
            lookUpEdit1.Properties.DisplayMember = "LOT";
            TackLot.Items.Clear();
            textEdit1.Text = "";
        }
        private void Edit_DH_Load(object sender, EventArgs e)
        {
            load();
        }

        private void simpleButton3_Click_1(object sender, EventArgs e)
        {
            string[] LOSL = lot.Text.Split(',');
            string[] SOLU;
            int SLX = 0;
            try
            {
                if (lot.Text != "")
                {
                    for (int i = 0; i < LOSL.Length; i++)
                    {
                        SOLU = LOSL[i].Split('-');
                        SLX = SLX + int.Parse(SOLU[1]);
                    }
                }
                if (int.Parse(slxuat.Text) == SLX)
                {
                    _MyView.PostEditor();
                    int STT;
                    string LOT, sql;
                    STT = int.Parse(MyView.GetFocusedRowCellDisplayText("STT").Trim());
                    LOT = MyView.GetFocusedRowCellDisplayText("LOT").Trim();
                    sql = "update tmpphieugiaohang set lot= '" + lot.Text + "' where stt = " + STT;
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    _MyView.CloseEditForm();
                }
                else
                    XtraMessageBox.Show("Error");
            }
            catch (Exception m)
            {
                throw m;
            }
        }

        private void simpleButton4_Click(object sender, EventArgs e)
        {
           MyView.CloseEditForm();
        }

        private void simpleButton1_Click_1(object sender, EventArgs e)
        {
            int slx = int.Parse(slxuat.Text);
            if (lookUpEdit1.Text != string.Empty && textEdit1.Text != "")
            {

                string MakeLot = lookUpEdit1.Text.ToString() + "-" + textEdit1.Text;

                if (TackLot.ItemCount == 0)
                {
                    if (slx >= int.Parse(textEdit1.Text))
                    {
                        TackLot.Items.Add(MakeLot);
                        lot.Text = TackLot.Items[0].ToString();
                    }
                    else
                    { XtraMessageBox.Show("Lỗi số lượng LOT > số lượng xuất !"); }

                }
                else
                {
                    string[] LOTSL = lot.Text.Split(',');
                    int SLX = 0;
                    if (LOTSL.Length > 1)
                    {

                        for (int j = 0; j < LOTSL.Length; j++)
                        {
                            string[] SL = LOTSL[j].Split('-');
                            SLX = SLX + int.Parse(SL[1].ToString());
                        }
                    }
                    else
                    {
                        string[] SL = LOTSL[0].Split('-');
                        SLX = SLX + int.Parse(SL[1].ToString());
                    }

                    if (lot.Text.Contains(lookUpEdit1.Text) == false && ((SLX + int.Parse(textEdit1.Text)) <= slx))
                    {
                        TackLot.Items.Add(MakeLot);
                        if (TackLot.ItemCount > 1)
                        {

                            lot.Text = lot.Text + "," + MakeLot;
                        }
                        if (TackLot.ItemCount == 1)
                        {
                            lot.Text = TackLot.Items[0].ToString();
                        }
                    }
                    else
                        XtraMessageBox.Show("Lỗi không thể dùng thiết lập , kiểm tra lại LOT hoặc số lượng xuất !");
                }
            }
        }

        private void simpleButton2_Click_1(object sender, EventArgs e)
        {
            ArrayList temp = new ArrayList();
            foreach (int index in TackLot.SelectedIndices)
                temp.Add(TackLot.Items[index]);
            foreach (object item in temp)
                TackLot.Items.Remove(item);
            lot.Text = "";
            for (int i = 0; i < TackLot.ItemCount; i++)
            {
                if (TackLot.ItemCount > 1)
                {
                    lot.Text = lot.Text + "," + TackLot.Items[1].ToString();
                }
                else
                {
                    lot.Text = TackLot.Items[0].ToString();
                }
            }
        }

        private void label2_TextChanged_1(object sender, EventArgs e)
        {
            load();
        }


        private void textEdit1_Properties_Validating(object sender, CancelEventArgs e)
        {
            if (lookUpEdit1.EditValue != null)
            {
                int slconlai = lookUpEdit1.EditValue != null ? int.Parse(lookUpEdit1.EditValue.ToString()) : 0;
                if (int.Parse((sender as TextEdit).Text) > slconlai)
                {
                    e.Cancel = true;
                    textEdit1.ErrorText = "Số lượng nhập không được lớn hơn tồn kho";
                }
            }
        }

      
    }

}
