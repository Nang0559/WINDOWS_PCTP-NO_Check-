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
using PCTP.ClassSQL;
using System.Diagnostics;
using DevExpress.XtraGrid.Drawing;
using DevExpress.XtraPrinting;
using System.Web.Mvc;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraPrintingLinks;
using DevExpress.Utils.Menu;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.Utils.Extensions;
using System.Data.SqlClient;

namespace PCTP
{
    public partial class TONKHOTP : Form
    {
        clsResize _form_resize;
        public TONKHOTP()
        {

            InitializeComponent();
            InitializeContextMenu();
            _form_resize = new clsResize(this);
            this.Load += _Load;
            this.Resize += _Resize;
        }
        private void _Load(object sender, EventArgs e)
        {
            _form_resize._get_initial_size();
        }

        private void _Resize(object sender, EventArgs e)
        {
            _form_resize._resize();
        }
        public TONKHOTP(string LOTNHAP)
        {
            InitializeComponent();
            DataTable TK1, TK = new DataTable();
            gridCtrTTTK.DataSource = null;
            gridCTTCT.DataSource = null;

            string sql1;
            gridCtrTTTK.DataSource = null;
            sql1 = "select PART,NAME ,sum(slconlai) as SLCONLAI from STOCKTP where slconlai > 0 and LOT in (" + LOTNHAP + ") group by PART,NAME";
            TK = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql1);
            gridCtrTTTK.RefreshDataSource();
            gridCtrTTTK.DataSource = TK;
            sql1 = "select lot,part,name,ngaysx,casx,slsx,ngaynhap,slnhap,ngayxuat,slxuat,slconlai as SLCONLAI , slconlaitmp as SOLUONGDANGGIAO from STOCKTP where  slconlai > 0 and LOT in (" + LOTNHAP + ") order by LOT ASC ";
            TK1 = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql1);
            gridCTTCT.DataSource = TK1;

            gridCTTCT.RefreshDataSource();
        }
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        private void TONKHOTP_Load(object sender, EventArgs e)
        {
            MAHANG();
            if (NHAP_TP.LOTNHAP == "")
            {
                LOAD_DL();
            }


        }
        private void MAHANG()
        {

            DataTable MH;


            //"SELECT PART,NAME FROM B GROUP BY PART,NAME";

            string SQL = "SELECT K.Code,K.Name FROM B20Item K left join STOCKTP TP " +
            "on k.Code = TP.PART " +
            "where k.IsGroup = 0   group BY k.Code,k.NAME";
            MH = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, SQL);
            lookUpMHSX.Properties.DataSource = MH;
            lookUpMHSX.Properties.DisplayMember = "Code";
            lookUpMHSX.Properties.ValueMember = "Code";



        }
        private void LOAD_DL()
        {
            DataTable TK = new DataTable();
            string sql;
            sql = "select PART,NAME ,sum(slconlai) as SLCONLAI from STOCKTP where slconlai > 0  group by PART,NAME";
            TK = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCtrTTTK.DataSource = TK;
            sql = "select lot,part,name,ngaysx,casx,slsx,ngaynhap,slnhap,ngayxuat,slxuat,slconlai as SLCONLAI , slconlaitmp as SOLUONGDANGGIAO from STOCKTP where  slconlai > 0 order by LOT ASC";
            TK = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCTTCT.DataSource = TK;
        }


        #region Updaet Stock
        DXMenuCheckItem CreateMenuItemCellMerging(GridView view, int rowHandle)
        {
            DXMenuCheckItem checkItem = new DXMenuCheckItem("Làm mới dữ liệu",
              view.OptionsView.AllowCellMerge, null, new EventHandler(OnRefreshClick));
            checkItem.Tag = new RowInfo(view, rowHandle);
            checkItem.ImageOptions.Image = imageCollection1.Images[1];
            return checkItem;
        }
        DXMenuCheckItem CreateMenuItemUpdateStok(GridView view, int rowHandle)
        {
            DXMenuCheckItem checkItem = new DXMenuCheckItem("Cập Nhập tồn kho dòng đã chọn",
              view.OptionsView.AllowCellMerge, null, new EventHandler(UpdateItem_Click));
            checkItem.Tag = new RowInfo(view, rowHandle);
            checkItem.ImageOptions.Image = imageCollection1.Images[0];
            return checkItem;
        }
        private void UpdateItem_Click(object sender, EventArgs e)
        {
            List<DataRow> selectedRows = new List<DataRow>();
            foreach (int rowHandle in gridVCTK.GetSelectedRows())
            {
                DataRow row = gridVCTK.GetDataRow(rowHandle);
                selectedRows.Add(row);
            }

            // Hiển thị form cập nhật với danh sách các hàng đã chọn
            UpdateStockPOP updateForm = new UpdateStockPOP(selectedRows);
            if (selectedRows.Count != 0)
            {
                if (updateForm.ShowDialog() == DialogResult.OK)
                {
                    int[] selectedRowHandles = gridVCTK.GetSelectedRows();

                    foreach (int rowHandle in selectedRowHandles)
                    {
                        if (rowHandle >= 0)
                        {
                            var row = gridVCTK.GetRow(rowHandle) as DataRowView;
                            string lotno = row["Lot"].ToString();
                            int slconlai = Convert.ToInt32(row["SLCONLAI"].ToString());

                            // Cập nhật dữ liệu vào cơ sở dữ liệu
                            using (SqlConnection con = new SqlConnection(sqlBRV.B7R2_FCCdb))
                            {
                                SqlCommand cmd = new SqlCommand("UPDATE STOCKTP SET SLCONLAI=@SLCONLAI  WHERE Lot=@LotNo", con);
                                cmd.Parameters.AddWithValue("@LOTNO", lotno);
                                cmd.Parameters.AddWithValue("@SLCONLAI", slconlai);
                                con.Open();
                                cmd.ExecuteNonQuery();
                                con.Close();
                            }
                        }
                    }
                }
            }
            else
            {
                XtraMessageBox.Show("Bạn chưa chọn dòng muốn update dữ liệu ?", "Thông Báo", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
        }
        #endregion



        private void LOAD_DK(string MAHANG)
        {
            string sql;
            DataTable TMP = new DataTable();


            //sql = "select PART,NAME ,sum(slconlai) as SLCONLAI from STOCKTP where slconlai > 0 and PART = '" + MAHANG + "'  group by PART,NAME";
            //TMP = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            //gridCtrTTTK.DataSource = TMP;

            sql = "select lot,part,name,ngaysx,casx,slsx,ngaynhap,slnhap,ngayxuat,slxuat,slconlai as SLCONLAI , slconlaitmp as SOLUONGDANGGIAO from STOCKTP where part = '" + MAHANG + "' and slconlai > 0  order by LOT ASC";
            TMP = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCTTCT.DataSource = TMP;
        }
        private void gridCtrTTTK_DoubleClick(object sender, EventArgs e)
        {


            string MAHANG = gridVTTK.GetFocusedRowCellValue("PART").ToString();
            LOAD_DK(MAHANG);
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
           
        }
        public void exportToExcel()
        {
            string filePath = "";
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Excel(.xlsx) | *.xlsx";
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {



                    CompositeLink complink = new CompositeLink(new PrintingSystem());
                    PrintableComponentLink link1 = new PrintableComponentLink();
                    PrintableComponentLink link = new PrintableComponentLink();
                    link1.Component = gridCtrTTTK;
                    complink.Links.Add(link1);
                    link.Component = gridCTTCT;
                    complink.Links.Add(link);
                    complink.CreatePageForEachLink();
                    complink.ExportToXlsx(saveDialog.FileName, new XlsxExportOptions() { ExportMode = XlsxExportMode.SingleFilePageByPage });
                    filePath = saveDialog.FileName;
                    DialogResult dlr = MessageBox.Show("Bạn có muốn mở file?", "Xuất file thành công!", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dlr == DialogResult.Yes)
                    {
                        Process.Start(filePath);
                    }
                }
            }
        }
        private void btExportExcel_Click(object sender, EventArgs e)
        {
            exportToExcel();
        }

        private void gridCtrTTTK_Click(object sender, EventArgs e)
        {

        }

        private void gridCTTCT_Click(object sender, EventArgs e)
        {

        }

        private void gridCTTCT_DoubleClick(object sender, EventArgs e)
        {
            string LOTNO = gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "lot").ToString();
            FRM_LOTNO_UPDATE_INFOR.LOTNO = LOTNO;
            FRM_LOTNO_UPDATE_INFOR F_UD = new FRM_LOTNO_UPDATE_INFOR();

            F_UD.ShowDialog();
        }

        private void gridCTTCT_MouseClick(object sender, MouseEventArgs e)
        {



        }
        private void InitializeContextMenu()
        {
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem updateItem = new ToolStripMenuItem("Update");
            ToolStripMenuItem refresh = new ToolStripMenuItem("Refresh");
            updateItem.Click += UpdateItem_Click;
            updateItem.Image = imageCollection1.Images[0];
            refresh.Click += OnRefreshClick;
            refresh.Image = imageCollection1.Images[1];
            contextMenu.Items.Add(updateItem);
            contextMenu.Items.Add(refresh);
            gridVCTK.GridControl.ContextMenuStrip = contextMenu;
            if (gridVCTK.GridControl != null)
            {
                gridVCTK.GridControl.ContextMenuStrip = contextMenu;
            }
            else
            {
                MessageBox.Show("GridControl is not initialized.");
            }
        }
        //private void gridVCTK_PopupMenuShowing(object sender, DevExpress.XtraGrid.Views.Grid.PopupMenuShowingEventArgs e)
        //{
        //    GridView view = sender as GridView;
        //    if (e.MenuType == DevExpress.XtraGrid.Views.Grid.GridMenuType.Row)
        //    {
        //        int rowHandle = e.HitInfo.RowHandle;
        //        // Delete existing menu items, if any.
        //        e.Menu.Items.Clear();
        //        // Add the Rows submenu with the 'Delete Row' command
        //        e.Menu.Items.Add(CreateSubMenuRows(view, rowHandle));
        //        // Add the 'Cell Merging' check menu item.
        //        DXMenuItem item = CreateMenuItemCellMerging(view, rowHandle);
        //        item.BeginGroup = true;
        //        e.Menu.Items.Add(item);
        //    }
        //}
        DXMenuItem CreateSubMenuRows(GridView view, int rowHandle)
        {
            DXSubMenuItem subMenu = new DXSubMenuItem("Rows");
            string deleteRowsCommandCaption;
            if (view.IsGroupRow(rowHandle))
                deleteRowsCommandCaption = "&Sửa Tồn Kho in this group";
            else
                deleteRowsCommandCaption = "&Sửa Tồn Kho";
            DXMenuItem menuItemDeleteRow = new DXMenuItem(deleteRowsCommandCaption, new EventHandler(EditTKRowClick), imageCollection1.Images[0]);
            menuItemDeleteRow.Tag = new RowInfo(view, rowHandle);
            menuItemDeleteRow.Enabled = view.IsDataRow(rowHandle) || view.IsGroupRow(rowHandle);
            subMenu.Items.Add(menuItemDeleteRow);
            return subMenu;
        }

        //DXMenuCheckItem CreateMenuItemCellMerging(GridView view, int rowHandle)
        //{
        //    DXMenuCheckItem checkItem = new DXMenuCheckItem("Làm mới dữ liệu",
        //      view.OptionsView.AllowCellMerge, null, new EventHandler(OnRefreshClick));
        //    checkItem.Tag = new RowInfo(view, rowHandle);
        //    checkItem.ImageOptions.Image = imageCollection1.Images[1];
        //    return checkItem;
        //}

        void EditTKRowClick(object sender, EventArgs e)
        {
            DXMenuItem menuItem = sender as DXMenuItem;
            RowInfo ri = menuItem.Tag as RowInfo;
            if (ri != null)
            {
                string LOTNO = gridVCTK.GetRowCellValue(gridVCTK.FocusedRowHandle, "lot").ToString();
                string message = menuItem.Caption.Replace("&", "");
                if (XtraMessageBox.Show(message + ": " + LOTNO + " ?", "Confirm operation", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return;
                
                FRM_LOTNO_UPDATE_INFOR.LOTNO = LOTNO;
                FRM_LOTNO_UPDATE_INFOR F_UD = new FRM_LOTNO_UPDATE_INFOR();

                F_UD.ShowDialog();
                //ri.View.DeleteRow(ri.RowHandle);
            }
        }

        void OnRefreshClick(object sender, EventArgs e)
        {
            LOAD_DL();
            //DXMenuCheckItem item = sender as DXMenuCheckItem;
            //RowInfo info = item.Tag as RowInfo;
            //info.View.OptionsView.AllowCellMerge = item.Checked;
        }

        class RowInfo
        {
            public RowInfo(GridView view, int rowHandle)
            {
                this.RowHandle = rowHandle;
                this.View = view;
            }
            public GridView View;
            public int RowHandle;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string sql;
            DataTable TMP = new DataTable();
            string MAHANG = lookUpMHSX.Text.ToString().Trim();
            sql = "select PART,NAME ,sum(slconlai) as SLCONLAI from STOCKTP where slconlai > 0 and PART = '" + MAHANG + "'  group by PART,NAME";
            TMP = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCtrTTTK.DataSource = TMP;
            LOAD_DK(MAHANG);
        }

        private void btExportExcel_Click_1(object sender, EventArgs e)
        {
            exportToExcel();
        }
    }
}