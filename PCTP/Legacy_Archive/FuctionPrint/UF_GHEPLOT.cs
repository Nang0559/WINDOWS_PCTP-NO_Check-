using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PCTP.FuctionMain;
using PCTP.ClassSQL;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraReports.UI;
using PCTP.QRCODE_HVN.Report;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using DevExpress.Utils.Menu;
using DevExpress.XtraVerticalGrid.Rows;

namespace PCTP.FuctionPrint
{
    public partial class UF_GHEPLOT : DevExpress.XtraEditors.XtraForm
    {
        FuctionGridView GV = new FuctionGridView();
        public  BindingList<Record> records = new BindingList<Record>();
        BindingList<DetailGL> recordsGL = new BindingList<DetailGL>();
        BindingList<DetailGL> recordsIN = new BindingList<DetailGL>();
        private DataTable tblKQGL,tblKQQR = new DataTable();
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        List<int> DSCLick = new List<int>();
        ClassFunction fctQR = new ClassFunction();
        public UF_GHEPLOT()
        {
            InitializeComponent();
          //  GV.AddColumn(GV_ReadQR);
            GCT_DOCQR.DataSource = records;
            GCT_GEPLOT.DataSource = recordsGL;
            GV_ReadQR.ClearSorting();
            
            GV_ReadQR.Columns["ItemCode"].SortOrder = DevExpress.Data.ColumnSortOrder.Ascending;
            foreach (GridColumn column in GV_ReadQR.VisibleColumns)
            {
                if (column.FieldName == "SLG")
                {
                    column.AppearanceHeader.BackColor = Color.CornflowerBlue;
                    column.OptionsColumn.ReadOnly = false;
                }
                else
                {
                    column.OptionsColumn.ReadOnly = true;
                    column.AppearanceHeader.BackColor = Color.AliceBlue;
                }
                }
        }
        List<string> DL = new List<string>();
        private List<string> LoadQCDG(string PartNo)
        {
            DataTable tbl = new DataTable();
            
            string sql = "select Name,cast(MinCloseQty as int) as Qty,Model from B20Item where Code='" + PartNo + "'";

            tbl  = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            DL.Add(tbl.Rows[0]["Name"].ToString());
            DL.Add(tbl.Rows[0]["Qty"].ToString());
            DL.Add(tbl.Rows[0]["Model"].ToString());

            return DL;
        }
        private string LoadGear(int Code)
        {
            string Gear = "";

            string sql = "select Name from B20Gear where Code = " + Code ;
             Gear = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);

            return Gear;
        }
        private void txtDocQR_KeyPress(object sender, KeyPressEventArgs e)
        {
            DL.Clear();
            string Gear = "";
            if (e.KeyChar == 13)
            {
                string[] LOTNO = fctQR.LOT(txtDocQR.Text);
                DL = LoadQCDG(LOTNO[1]);
                string NSX = LOTNO[0].Substring(0, 2) + "/" + LOTNO[0].Substring(2, 2) + "/" + LOTNO[0].Substring(4, 2);
                DateTime NgaSX = DateTime.ParseExact(NSX, "yy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);
                Record DocQR = new Record();
                if (ISYAMH(LOTNO[1]))
                {
                    List<string> DLL = fctQR.DLLOT(LOTNO[0],true);
                    Gear = LoadGear(int.Parse(LOTNO[0].Substring(12, 1)));
                   
                    AddRC(DocQR, LOTNO[0], LOTNO[1], DL[0], Gear, NgaSX, int.Parse(DLL[1]), int.Parse(LOTNO[3]), int.Parse(DL[1]), false, txtDocQR.Text);
                }
                else
                {
                    List<string> DLL = fctQR.DLLOT(LOTNO[0],false);
                    //DL = LoadQCDG(LOTNO[1]);

                    AddRC(DocQR, LOTNO[0], LOTNO[1], DL[0], "", NgaSX, int.Parse(DLL[1]), int.Parse(LOTNO[3]), int.Parse(DL[1]), false, txtDocQR.Text);
                }
                bool KTTT = records.Contains(DocQR) ;
                bool KT = records.Any(item => item.ItemLotCode == LOTNO[0]);
                if (KT == false)
                {
                    records.Add(DocQR);

                }
                
                txtDocQR.Text = "";
            }
            //tbl.Columns.Add("Selected", typeof(bool));
           
               
            
           // GV.AddColumn(GV_ReadQR);
        }
        private void AddRC(Record newRecord, string _Lot,string _Part, string _Name, string _Model,DateTime _DocDate,int _CSX, int _SL,int _QC,bool TT,string QR)
        {
            int vt = records.Count;
           // Record newRecord = new Record();
            newRecord.STT = vt + 1;
            newRecord.ItemCode = _Part;
            newRecord.ItemLotCode = _Lot;
            newRecord.ItemName = _Name;
            newRecord.DocDate = _DocDate;
            newRecord.ShiftCode = _CSX;
            newRecord.Model = _Model;
            newRecord.QCDG = _QC;
            newRecord.Quantity9 = _SL;
            newRecord.State = TT;
            newRecord.QRCODE = QR;
            //records.Add(newRecord);
          
        }
        private void btGL_Click(object sender, EventArgs e)
        {
            int[] selectedRows = GV_ReadQR.GetSelectedRows();
            int SLLOTG = 0;
            
            string _PartNo = "", _PartName = "", _LotNo = "",LotTach ="", _Model = "",_QR ="";
            int STTB, STT,_Csx=0 ,_SLLOT=0,_SLG = 0,QCDG = 0;
            DateTime _NSX = DateTime.MinValue;
            //Record RCGL = new Record();
            if (selectedRows.Length < 2)
                XtraMessageBox.Show("Bạn chưa chọn đủ Lot để ghép . Vui lòng chọn từ 2 Lot trở nên !");
            else
            {
                if (CheckSLLotGhep())
                {
                    foreach (int index in selectedRows)
                    {
                        Record record = GV_ReadQR.GetRow(index) as Record;
                        
                        Record Tach = new Record();

                        if (record.Quantity9 >= record.SLG && record.SLG > 0)
                        {
                            record.State = false;
                            STTB = record.STT;
                            if (record.Quantity9 - record.SLG != 0)
                            {


                                //LotTach = string.Format("{0000}", record.SLG.ToString().PadLeft(4,'0');
                                LotTach = record.ItemLotCode.Substring(0, 23) + (record.Quantity9 - record.SLG).ToString().PadLeft(4, '0');
                                _QR = LotTach + ":" + record.ItemCode + ":" + record.DocDate.ToString("dd/MM/yyyy") + ":" + record.SLG.ToString();

                                AddRC(Tach, LotTach, record.ItemCode, record.ItemName, record.Model, record.DocDate, record.ShiftCode, record.Quantity9 - record.SLG, int.Parse(LoadQCDG(record.ItemCode)[1]), true, _QR);
                                records.Add(Tach);
                            }

                            _PartNo = record.ItemCode;
                            _PartName = record.ItemName;
                            _Model = record.Model;
                            _Csx = record.ShiftCode;
                            _NSX = record.DocDate;
                            QCDG = record.QCDG;
                            
                              
                                 
                                if (_LotNo.Length <= 0)
                                    _LotNo += fctQR.DLLOT(record.ItemLotCode, ISYAMH(record.ItemCode))[0] + "-" + record.SLG.ToString();
                                else
                                    _LotNo += "," + fctQR.DLLOT(record.ItemLotCode, ISYAMH(record.ItemCode))[0] + "-" + record.SLG.ToString();
                             
                            
                            _SLLOT += record.SLG;
                            DSCLick.Add(STTB);
                            btGL.Visible = false;
                            GV_ReadQR.ClearSelection();
                        }
                        else
                            GV_ReadQR.ClearSelection();
                    }
                   
                        STT = recordsGL.Count;
                        DetailGL newRecord = new DetailGL();
                        newRecord.STT = STT + 1;
                        newRecord.ItemCode = _PartNo;
                        newRecord.ItemName = _PartName;
                        newRecord.Model = _Model;
                        newRecord.ItemLotCode += _LotNo;
                        newRecord.DocDate = _NSX;
                        newRecord.ShiftCode = _Csx;
                        newRecord.Quantity9 += _SLLOT;
                        newRecord.QRCODE = _LotNo + ":" + _PartNo + ":" + _NSX.ToString("dd/mm/yyyy") + ":" + _SLLOT.ToString();

                    recordsGL.Add(newRecord);
                    
                }
                else
                    XtraMessageBox.Show("KT lai");
            }
        }
        private bool CheckSLLotGhep()
        {
            bool _GTKT = true;
            int[] selectedRows = GV_ReadQR.GetSelectedRows();
            int _ItemSLG = 0, _SLTT = 0,_QC = 0;
            foreach (int item in selectedRows)
            {
                _QC = int.Parse(GV_ReadQR.GetRowCellValue(item, "QCDG").ToString());
                _ItemSLG = int.Parse(GV_ReadQR.GetRowCellValue(item, "SLG").ToString());
                _SLTT += _ItemSLG;
            }
            if (_SLTT == _QC)
                _GTKT = true;
            return _GTKT;
        }
        private bool ShouldBeReadonly(int rowHandle, DevExpress.XtraGrid.Columns.GridColumn gridColumn)
        {
            bool isReadOnly;
            // int[] selectedRows = GV_ReadQR.GetSelectedRows();
            int STT = int.Parse(GV_ReadQR.GetRowCellValue(rowHandle, "STT").ToString());
            if (DSCLick.Contains(STT) == true)
                isReadOnly = true;
            else
                isReadOnly = false;
            return isReadOnly;
        }
        private void GV_ReadQR_ShowingEditor(object sender, CancelEventArgs e)
        {
            if (ShouldBeReadonly(GV_ReadQR.FocusedRowHandle, GV_ReadQR.FocusedColumn))
            {
                e.Cancel = true;
                btGL.Visible = false;
            }
            else
                btGL.Visible = true;
        }

        private void GV_ReadQR_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
        {
            if (e.RowHandle >= 0)
            {
                var STT = GV_ReadQR.GetRowCellValue(e.RowHandle, "STT");
                if (DSCLick.Contains(int.Parse(STT.ToString())) == true)
                    e.Appearance.BackColor = Color.Salmon;
            }
        }

        private void GV_ReadQR_ValidatingEditor(object sender, DevExpress.XtraEditors.Controls.BaseContainerValidateEditorEventArgs e)
        {
            ColumnView view = sender as ColumnView;
            GridColumn column = (e as EditFormValidateEditorEventArgs)?.Column ?? view.FocusedColumn;
            int index = GV_ReadQR.FocusedRowHandle;
            Record record = GV_ReadQR.GetRow(index) as Record;
            int _sltem = record.Quantity9;
            if (column.FieldName == "SLG")
            {
                double SLG = 0;
                if (!Double.TryParse(e.Value as String, out SLG))
                {
                    e.Valid = false;
                    e.ErrorText = "Số lượng ghép phải khác không  ";
                }
                else if (SLG < 0 || SLG > _sltem)
                {
                    e.Valid = false;
                    e.ErrorText = "Số lượng muốn ghép phải nhỏ hơn số lượng tem !";
                }
            }
        }

        private void bt_Print_Click(object sender, EventArgs e)
        {
            GHEPLOT GL = new GHEPLOT();
            recordsIN.Clear();
            foreach (DetailGL IT in recordsGL)
            {
                if (ISYAMH(IT.ItemCode))
                {
                    IT.MO = "GEAR";

                }
                else
                    IT.MO = "";

                recordsIN.Add(IT);
            }
            for (int i = 0; i < records.Count; i++)
            {

                if (DSCLick.Contains(records[i].STT) == false && records[i].State == true)
                {
                    Record record = GV_ReadQR.GetRow(i) as Record;
                    DetailGL newRecord = new DetailGL();
                    newRecord.STT = recordsIN.Count + 1;
                    newRecord.ItemCode = record.ItemCode;
                    newRecord.ItemName = record.ItemName;
                  
                    newRecord.Model = record.Model;
                    if (ISYAMH(record.ItemCode))
                        newRecord.MO = "GEAR";
                    else
                        newRecord.MO = "";
                    newRecord.ItemLotCode = record.ItemLotCode;
                    newRecord.DocDate = record.DocDate;
                    newRecord.ShiftCode = record.ShiftCode;
                    newRecord.Quantity9 = record.Quantity9;
                    newRecord.QRCODE = record.QRCODE;

                    recordsIN.Add(newRecord);
                }
            }
            GL.DataSource = recordsIN;
           
            ReportPrintTool printTool = new ReportPrintTool(GL);
            
            printTool.ShowPreviewDialog();
        }
       
        private void GV_ReadQR_MouseUp(object sender, MouseEventArgs e)
        {
            GridView gridView = sender as GridView;
             GridHitInfo hitInfo = gridView.CalcHitInfo(e.Location);
            List<string> ListPartNo = new List<string>() ;
            int[] selectedRows = GV_ReadQR.GetSelectedRows();

            //row Click이 아니라면 return
            if (!(hitInfo.InRow || hitInfo.InDataRow))
            {
                return;
            }
            else
            {
                string PartNo = GV_ReadQR.GetRowCellValue(hitInfo.RowHandle, "ItemCode").ToString();
                if (gridView.GetSelectedRows().Contains(hitInfo.RowHandle))
                {
                    if (int.Parse(GV_ReadQR.GetRowCellValue(hitInfo.RowHandle, "SLG").ToString()) == 0)
                        gridView.UnselectRow(hitInfo.RowHandle);
                    else
                    {
                        foreach (int index in selectedRows)
                        {

                            if (index != hitInfo.RowHandle)
                            {
                                ListPartNo.Add(gridView.GetRowCellValue(index, "ItemCode").ToString());
                                if (!ListPartNo.Contains(PartNo))
                                    gridView.UnselectRow(hitInfo.RowHandle);
                            }

                        }
                    }
                }
                else
                {
                    //gridView.SelectRow(hitInfo.RowHandle);
                }
            }
        }

        private void GV_ReadQR_MouseDown(object sender, MouseEventArgs e)
        {
            GridView gridView = sender as GridView;

            GridHitInfo hitInfo = gridView.CalcHitInfo(e.Location);
            if (!(hitInfo.InRow || hitInfo.InDataRow))
            {
                return;
            }
            if(int.Parse(gridView.GetRowCellValue(hitInfo.RowHandle,"SLG").ToString())==0)
            gridView.UnselectRow(hitInfo.RowHandle);
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            records.Clear();
            recordsGL.Clear();
            recordsIN.Clear();
        }

        private void GV_ReadQR_PopupMenuShowing(object sender, PopupMenuShowingEventArgs e)
        {
            GridView view = sender as GridView;
            if (e.MenuType == DevExpress.XtraGrid.Views.Grid.GridMenuType.Row)
            {
                int rowHandle = e.HitInfo.RowHandle;
                e.Menu.Items.Clear();
              
                DXMenuItem item1 = CreateMenuItemBackRC(view, rowHandle);
                //item.BeginGroup = true;
                e.Menu.Items.Add(item1);

            }
        }

        private DXMenuItem CreateMenuItemBackRC(GridView view, int rowHandle)
        {
            DXMenuCheckItem checkItem = new DXMenuCheckItem("Lấy Lại Tem ", view.OptionsMenu.EnableColumnMenu,
           null, new EventHandler(OnBackClick));
            checkItem.Tag = new RowInfo(view, rowHandle);
            checkItem.ImageOptions.Image = imageCollection1.Images[0];
            return checkItem;
        }
        private bool ISYAMH (string Part)
        {
            bool KQ = false;
            string sql = "select CustomerCode from B20ItemQuyCach where ItemCode = '" + Part + "'";
            string MaCUS = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (MaCUS == "0100002")
                KQ = true;
            return KQ;
        }
        
        private void OnBackClick(object sender, EventArgs e)
        {
            string QRC = GV_ReadQR.GetRowCellValue(GV_ReadQR.FocusedRowHandle, "QRCODE").ToString();
            txtDocQR.Text = QRC;
        }

        private void GV_ReadQR_CellMerge(object sender, DevExpress.XtraGrid.Views.Grid.CellMergeEventArgs e)
        {
           
            if (e.Column.FieldName == "QCDG")
            {
                string id1 = (string)GV_ReadQR.GetRowCellValue(e.RowHandle1, GV_ReadQR.Columns[0]);
                string id2 = (string)GV_ReadQR.GetRowCellValue(e.RowHandle2, GV_ReadQR.Columns[0]);
                e.Merge = id1 == id2;
                e.Handled = true;
            }
            if (e.Column.FieldName == "Qty")
                e.Merge = false;
        }

        
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
}