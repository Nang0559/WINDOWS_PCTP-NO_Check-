using DevExpress.Utils.Menu;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Localization;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN.ComaprePart
{
    public partial class ComaparePart : DevExpress.XtraEditors.XtraForm
    {
        public ComaparePart()
        {
            InitializeComponent();
            GV_ListPart.Appearance.FocusedRow.BackColor = Color.FromArgb(255, 255, 192);
            GV_ListPart.Appearance.SelectedRow.BackColor = Color.FromArgb(255, 255, 192);
              GV_ListPart.Appearance.SelectedRow.Options.UseBackColor = true;
            LoaDL();
        }
        SQLPROVIDER SQLPROVIDER = new SQLPROVIDER();
        DataSet ChangePart = new DataSet();
        SqlDataAdapter adapter = new SqlDataAdapter();
        //Create DataTable objects for representing database's tables
        public DataTable ListPart,GV = new DataTable();
        private void LoaDL()
        {
            string sql = "select distinct code,name from B20Item";
            lup_Ma.Properties.DataSource = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
            lup_Ma.Properties.ValueMember = "code";
            lup_Ma.Properties.DisplayMember = "code";
            sql = "select STT,PartNo,PartName,PartNoCompare as ToPartNo,PartName as ToPartName,timeSet,IsActive,timeSet as DateApp from  tbl_QR_ComparePart";
             GV =  SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
            GT_ListPart.DataSource = GV;
        }
        private bool ListPartCP ()
        {
            bool KQ = false;
            string sql= "select distinct code,name from B20Item where code like '" + lup_Ma.EditValue + "%'";
            ListPart = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
            if (ListPart.Rows.Count > 0)
                KQ = true;
            return KQ;
        }
        private void lup_Ma_EditValueChanged(object sender, EventArgs e)
        {
           
            if(ListPartCP()==true)
            LuKToPart.Properties.DataSource = ListPart;
        }

        private void GV_ListPart_PopupMenuShowing(object sender, PopupMenuShowingEventArgs e)
        {
            GridView view = sender as GridView;
            if(e.MenuType== DevExpress.XtraGrid.Views.Grid.GridMenuType.Row)
            {
                int rowHandle = e.HitInfo.RowHandle;
                e.Menu.Items.Clear();
                DXMenuItem item1 = CreateMenuItemDelete(view, rowHandle);
                //item.BeginGroup = true;
                e.Menu.Items.Add(item1);
                DXMenuItem item2 = CreateMenuItemRefresh(view, rowHandle);
                //item.BeginGroup = true;
                e.Menu.Items.Add(item2);
                //DXMenuItem item3 = CreateMenuItemBackRC(view, rowHandle);
                ////item.BeginGroup = true;
                //e.Menu.Items.Add(item3);

            }
        }

        private DXMenuItem CreateMenuItemBackRC(GridView view, int rowHandle)
        {
            DXMenuCheckItem checkItem = new DXMenuCheckItem("Lấy Lại Tem ", view.OptionsMenu.EnableColumnMenu,
           null, new EventHandler(OnBackClick));
            checkItem.Tag = new RowInfo(view, rowHandle);
            checkItem.ImageOptions.Image = imageCollection1.Images[3];
            return checkItem;
        }

        private void OnBackClick(object sender, EventArgs e)
        {
            
        }

        private DXMenuItem CreateMenuItemRefresh(GridView view, int rowHandle)
        {
            DXMenuCheckItem checkItem = new DXMenuCheckItem("Làm Mới", view.OptionsMenu.EnableColumnMenu,
            null, new EventHandler(OnRefreshClick));
            checkItem.Tag = new RowInfo(view, rowHandle);
            checkItem.ImageOptions.Image = imageCollection1.Images[1];
            return checkItem;
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            LoaDL();
        }

        private DXMenuItem CreateMenuItemDelete(GridView view, int rowHandle)
        {
            DXMenuCheckItem checkItem = new DXMenuCheckItem("Xóa Dòng Chọn",
            view.OptionsView.AllowCellMerge, null, new EventHandler(OnDeleClick));
            checkItem.Tag = new RowInfo(view, rowHandle);
            checkItem.ImageOptions.Image = imageCollection1.Images[0];
            return checkItem;
        }

        private void OnDeleClick(object sender, EventArgs e)
        {
            DXMenuItem menuItem = sender as DXMenuItem;
            RowInfo ri = menuItem.Tag as RowInfo;

            if (ri != null)
            {
                string message = menuItem.Caption.Replace("&", "");
                if (XtraMessageBox.Show(message + " ?", "Confirm operation", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return;

                int[] seletcted = GV_ListPart.GetSelectedRows();
                    foreach (int i in seletcted)
                    {
                        DataRow row = GV_ListPart.GetDataRow(i);
                    string s = "delete from tbl_QR_ComparePart where STT = " + int.Parse(row["STT"].ToString());
                        SQLPROVIDER.ExecuteNonQuery(SQLPROVIDER.B7R2_FCCdb, s) ;
                        ri.View.DeleteRow(i);

                        
                    }
                
            }
        }
        string sql = "";
        private void GV_ListPart_ShowingPopupEditForm(object sender, ShowingPopupEditFormEventArgs e)
        {
            
            List<TextEdit> textEdits = new List<TextEdit>();
            FindChildrenByType(e.EditForm, textEdits);
            foreach(TextEdit txt in textEdits)
            {
                sql = sql + txt.Name + txt.EditValue;
            }
            List<SimpleButton> buttons = new List<SimpleButton>();
            FindChildrenByType(e.EditForm, buttons);
            foreach (SimpleButton btn in buttons)
                if (btn.Text == GridLocalizer.Active.GetLocalizedString(GridStringId.EditFormUpdateButton))
                {
                    btn.Click += UpdateClick;
                }
        }

        private void UpdateClick(object sender, EventArgs e)
        {
            MessageBox.Show(sql);
        }
        void FindChildrenByType<T>(Control parent, List<T> list) where T : class
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T)
                    list.Add(child as T);
                if (child.HasChildren)
                    FindChildrenByType<T>(child, list);
            }
        }

        private void GV_ListPart_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            DevExpress.XtraGrid.Views.Base.BaseView view = GT_ListPart.FocusedView;
            if (!(view.PostEditor() && view.UpdateCurrentRow()))
            {
                //e.Cancel = true;
                return;
            }
           // (GT_ListPart.DataSource as Table<TMPPHIEUNHANDB>).Context.SubmitChanges();
            adapter.Update(ChangePart);
        }

       

        private void cmd_Tao_Click(object sender, EventArgs e)
        {
            if(lup_Ma.EditValue != LuKToPart.EditValue)
            {
                DateTime ApproveDate = new DateTime();
                string S_ApproveDate = DateE_NgayAPP.DateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    string  STT = string.IsNullOrEmpty(SQLPROVIDER.ExecuteReader(SQLPROVIDER.B7R2_FCCdb, "select max(STT) from tbl_QR_ComparePart ")) ? "0" : SQLPROVIDER.ExecuteReader(SQLPROVIDER.B7R2_FCCdb, "select max(STT) from tbl_QR_ComparePart ");
                int STTF = int.Parse(STT) + 1;   
                string ss = "insert into tbl_QR_ComparePart (STT,PartNo,PartNoCompare,timeSet,IsActive) values (" + STTF + ",'" + lup_Ma.EditValue + "', '" + LuKToPart.EditValue + "','" + S_ApproveDate + "'," + 1 + ")";
                    SQLPROVIDER.ExecuteNonQuery(SQLPROVIDER.B7R2_FCCdb,ss );
                
            }
            LoaDL();
        }
    }
}