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
using DevExpress.Utils;
using DevExpress.XtraGrid.Editors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraEditors.Controls;
using System.Web.UI.Design;

namespace PCTP.QRCODE_HVN
{
    public partial class PHIEUGIAOHANG : DevExpress.XtraEditors.XtraForm
    {
        public PHIEUGIAOHANG()
        {
            InitializeComponent();
            dateNX.DateTime = DateTime.Now;
        }
        IFSPROVIDER iFSPROVIDER = new IFSPROVIDER();
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public string GIOXUAT ;
        // Lấy thông tin đơn hàng theo ngày .
        private DataTable TT_HANG_MA = new DataTable();
        private DataTable TT_HANG_GIO = new DataTable();
        private DataTable TT_HANG_GIO_MA = new DataTable();
        private DataGridView DHTTGIO = new DataGridView();
        public static DateEdit NGAYXUAT_DT = new DateEdit();
        int addHVN;
        string N_XH;
        private void LoadDL()
        {
            DataTable DONHANG = new DataTable();
            N_XH = dateNX.DateTime.ToString("ddMMyyyy");


            //dateNX.Properties.DisplayFormat.FormatString = "ddmmyyyy";


            string sql = "select '' as STT,CUSTOMER_NO,WANTED_DELIVERY_DATE,BUY_QTY_DUE,customer_part_unit_meas,SHIP_ADDR_NO,SUB_DOCK_CODE,CUSTOMER_PART_NO,CATALOG_DESC,CUSTOMER_PO_REL_NO,DOCK_CODE,ORDER_NO, " +
                   "CATALOG_NO,PLANNED_SHIP_DATE,CUSTOMER_PO_NO,'' as HOP, '' as LOTNO " +
                    " from CUSTOMER_ORDER_JOIN " +
                   " where " +
                   " CUSTOMER_NO = '100001' and " +
                   " SHIP_ADDR_NO = " + addHVN + " and " +
                   " (OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )and " +
                   " to_char( WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "' and " +
                   " CUSTOMER_PO_REL_NO is not null" +
                   " and  to_char(WANTED_DELIVERY_DATE, 'HH24') in (" + GIOXUAT + ")" +
                   " Order by WANTED_DELIVERY_DATE ";


            DONHANG =  iFSPROVIDER.ExecuteQuery(sql);
            gridCtrDONHANG.DataSource = DONHANG;
            for (int i = 0; i< gridView2.RowCount;i++)
            {
                string MaHang = gridView2.GetRowCellValue(i, "CUSTOMER_PART_NO").ToString();
                string sql1 = "SELECT cast(MinCloseQty as int)  from B20Item where Code= '" + MaHang + "'";
                string QCDg = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1);
                DateTime GIO_XUAT_LIST = DateTime.Parse(gridView2.GetRowCellValue(i, "WANTED_DELIVERY_DATE").ToString()) ;
                int GXH = int.Parse(GIO_XUAT_LIST.ToString("HH"));
                int SLXUAT = int.Parse(gridView2.GetRowCellValue(i, "BUY_QTY_DUE").ToString());
                //gridView2.SetRowCellValue(i, "HOP") = QCDg.ToString();
                gridView2.SetRowCellValue(i, "HOP", QCDg);
                gridView2.SetRowCellValue(i, "STT", i+1);

                //string sqltongkho = "select sum(slconlaitmp) as slconlaitmp from stocktp where part = '" + MaHang + "' and slconlaitmp > 0";
                //int TTTONKHO;
                //try
                //{
                //    TTTONKHO = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqltongkho));
                //}
                //catch
                //{
                //    TTTONKHO = 0;
                //}
                //// Tong Giao Hang cua ma
                //int TTGIAOHANG = TTGIAOTHEOMA(MaHang);

                //if (TTTONKHO < TTGIAOHANG)
                //{
                //    ADDLISTHT(MaHang, TTGIAOHANG, TTTONKHO, TTGIAOHANG - TTTONKHO, 6);
                //}
            }
            gridView2.RefreshData();
            // Load TT ghep lot

        }
        private void GHEP_LOT()
        {

            string sql;
            string PartNo;
            int GioGiao;
            int  SLGIAO;
            int TCDG;
            string LOTDUYET;
            int SLTKCONLAI_LOTDUYET;
            object TONKHOTEM;
            object TONKHOCOLAITMP;
            int slle = 0;
            string LOTGHEP = "";
            int SLCANGHEP = 0;
            DataTable TONKHOTHEOMA1 = new DataTable();
            DataTable TONKHOTHEOMATMP = new DataTable();
            // Lay don hang theo gio
            N_XH = dateNX.DateTime.ToString("ddMMyyyy");
            
            //---------------------------------------------------------------------------------------------------------------------------------
            //string sqlTK = "select LOT,SLCONLAI from STOCKTP where SLCONLAI > 0 and PART = '" + MaHang + "' order by lot";
            DataTable TBTK = new DataTable();
            //TBTK = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sqlTK);
            //TK.DataSource = TBTK;
            //string LOT_GHEP = "";
            Tong_Hang_Gio();
            // Duyệt Bảng Grid Đơn Hàng Theo Giờ : DHTTGIO
            CAPNHAPTMP("%", 0);
            for (int j = 0; j < TT_HANG_GIO.Rows.Count; j++)
            {
                PartNo = TT_HANG_GIO.Rows[j].Field<string>("PART_NO");
                GioGiao = int.Parse(TT_HANG_GIO.Rows[j].Field<string>("GIOGIAO"));
                object O_SLGIAO =  TT_HANG_GIO.Rows[j][3];
                SLGIAO = Convert.ToInt32(O_SLGIAO);
                // Lấy tiêu chuẩn đóng gói .của mã hàng .
                sql = "SELECT cast(isnull(MinCloseQty,0) as int)  from B20Item where Code= '" + PartNo + "'";
                string sTCDG = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                if (sTCDG == "")
                {
                    TCDG = -1;
                }
                else
                {
                    
                    TCDG = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                }
                // Lấy tồn kho theo LOT của mã hàng .
                string sql1 = "select lot,part,isnull(slconlai,0) as slconlai ,slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0 order by lot";
                TONKHOTHEOMA1.Clear();
                TONKHOTHEOMA1 = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql1);
                // Lấy tồn kho tổng của mã hàng.
                
                //Duyệt Đơn hàng và với kho .
            
                if (TONKHOTHEOMA1.Rows.Count > 0)// Nếu có tồn kho
                {
                    for(int i = 0; i <TONKHOTHEOMA1.Rows.Count;i++)// duyệt tồn kho
                    {
                        // Cập Nhập tạm thông tin tồn kho vào trường TMP
                        
                           TONKHOTEM = TONKHOTHEOMA1.Rows[i][3];
                            if(TONKHOTEM == null)
                            {
                            TONKHOTEM = 0;
                             }
     
                            if (Convert.ToInt32(TONKHOTEM) == 0)
                            {
                                LOTDUYET = TONKHOTHEOMA1.Rows[i].Field<string>("lot");
                                object TONKHOCOLAI = TONKHOTHEOMA1.Rows[i][2];
                                SLTKCONLAI_LOTDUYET = Convert.ToInt32(TONKHOCOLAI);
                                CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET);
                            }
                            // Nếu tổng tồn kho ít hơn số lượng cần giao thì cho vào danh sách hàng thiếu
                    }
                    // Lấy tồn kho theo LOTTMP của mã hàng .
                    string sqlTMP = "select lot,part,isnull(slconlai,0) as slconlai ,isnull(slconlaitmp,0) as slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0 order by lot";
                    TONKHOTHEOMATMP = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sqlTMP);
                    // Duyệt Hàng theo TMP tồn kho .
                    
                    Boolean canghep = false;
                    for (int i = 0; i < TONKHOTHEOMATMP.Rows.Count; i++)
                    {
                        LOTDUYET = TONKHOTHEOMATMP.Rows[i].Field<string>("lot");
                        TONKHOCOLAITMP = TONKHOTHEOMATMP.Rows[i][3];
                        SLTKCONLAI_LOTDUYET = Convert.ToInt32(TONKHOCOLAITMP);
                        if (Convert.ToInt32(TONKHOCOLAITMP) > 0)
                        {
                            if (SLTKCONLAI_LOTDUYET < SLGIAO)
                            {
                                slle = SLTKCONLAI_LOTDUYET % TCDG;
                                if (slle == 0)
                                {

                                    CAPNHAPTMP(LOTDUYET, -1);
                                    SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;


                                }
                                else
                                {
                                    if (canghep == true)
                                    {
                                        if (SLCANGHEP > SLTKCONLAI_LOTDUYET)
                                        {
                                            LOTGHEP = LOTGHEP + "," + LOTDUYET + "-" + SLTKCONLAI_LOTDUYET + ",";
                                            SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
                                            SLCANGHEP = SLCANGHEP - SLTKCONLAI_LOTDUYET;
                                            canghep = true;
                                            CAPNHAPTMP(LOTDUYET, -1);


                                        }
                                        else
                                        {
                                            LOTGHEP = LOTGHEP + "," + LOTDUYET + "-" + SLCANGHEP;
                                            SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
                                            SLCANGHEP = 0;
                                            canghep = false;
                                            CAPNHAPTMP(LOTDUYET, -1);

                                            break;
                                        }
                                    }
                                    else
                                    {
                                        LOTGHEP = LOTDUYET + "-" + slle + ",";
                                        SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
                                        SLCANGHEP = TCDG - slle;
                                        canghep = true;
                                        CAPNHAPTMP(LOTDUYET, -1);
                                    }

                                }
                            }
                            else
                            {

                                if (canghep == true)
                                {
                                    LOTGHEP = LOTGHEP + LOTDUYET + "-" + SLCANGHEP;
                                    ADDLIST_GHEPLOT(PartNo, GioGiao, LOTGHEP);
                                    CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET - SLGIAO);
                                    canghep = false;
                                    SLCANGHEP = 0;
                                    break;
                                }
                                else
                                {

                                    CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET - SLGIAO);
                                    canghep = false;
                                    SLCANGHEP = 0;
                                    break;
                                }
                            }

                        }

                    }
                }
                else
                {
                    //MessageBox.Show("Không có tồn kho của mã hàng : " + PartNo, "THÔNG BÁO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }

        }
        // Cập NHập LOTTMP
        private void DuyetTTHangThieu()
        {
            string Mahang;
            int TT_GIAO;
            int TONGTON_THEOMA;
            string sql;
            int GIOGIO;
            int GIOTHIEU = 9999;
            int SLGIAO = 0;
            TTGIAOTHEOMA();
            for (int i= 0;i< TT_HANG_MA.Rows.Count;i++)
            {
                Mahang = TT_HANG_MA.Rows[i].Field<string>("PART_NO");
                object TLg = TT_HANG_MA.Rows[i][1];
                TT_GIAO = Convert.ToInt32(TLg);
                // Lấy tổng tồn kho theo mã 
                sql = "select isnull(sum(slconlai),0) as TT_TK from stocktp where part = '" + Mahang + "' and slconlai > 0 ";
                TONGTON_THEOMA = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                // Tìm giờ thiếu hàng .
                Tong_Hang_Gio_Ma(Mahang);
                for(int j = 0;j< TT_HANG_GIO_MA.Rows.Count;j++)
                {
                   GIOGIO = int.Parse(TT_HANG_GIO_MA.Rows[j].Field<string>("GIOGIAO"));
                    object slg = TT_HANG_GIO_MA.Rows[j][2];
                    SLGIAO = SLGIAO + Convert.ToInt32(slg);
                    if(SLGIAO > TONGTON_THEOMA)
                    {
                        GIOTHIEU = GIOGIO;
                        ADDLISTHT(Mahang, TT_GIAO, TONGTON_THEOMA, TONGTON_THEOMA - TT_GIAO, GIOTHIEU);
                        SLGIAO = 0;
                        break;

                    }
                    else
                    {
                        SLGIAO = 0;
                    }
                }

            }
        }
        private void ADDLISTHT(string patno, int slgiao, int sltonkho , int slthieu , int giothieu)
        {
            listHANGTHIEU.View = View.Details;
            listHANGTHIEU.GridLines = true;
          
            

            ListViewItem item1 = new ListViewItem(patno);
            item1.SubItems.Add(slgiao.ToString());
            item1.SubItems.Add(sltonkho.ToString());
            item1.SubItems.Add(slthieu.ToString());
            item1.SubItems.Add(giothieu.ToString());
            listHANGTHIEU.Items.Add(item1);
        }
        private void ADDLIST_GHEPLOT(string MAHANG, int GIOGIAO, string LOTGHEP)
        {
            listVGHEPLOT.View = View.Details;
            listVGHEPLOT.GridLines = true;
            ListViewItem item1 = new ListViewItem(MAHANG);
            item1.SubItems.Add(GIOGIAO.ToString());
            item1.SubItems.Add(LOTGHEP.ToString());
            listVGHEPLOT.Items.Add(item1);
        }
        private void CAPNHAPTMP(string LOT, int SLCONLAITMP)
        {
            string SQL = "update stocktp set slconlaitmp = " + SLCONLAITMP + " where lot like '" + LOT + "'";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, SQL);
        }



        // Lấy tổng đơn hàng theo ngày có tách giờ  TT_HANG_GIO
        private void Tong_Hang_Gio()
        {
            int GIOXUATH = int.Parse(GIOXUAT.Substring(1, 2));
            string sql = "select * from " +
                    " (select CUSTOMER_PART_NO as PART_NO,CATALOG_DESC as NAME,to_char(WANTED_DELIVERY_DATE, 'HH24') as GIOGIAO,sum(BUY_QTY_DUE) as TTSLG " +
                    "from CUSTOMER_ORDER_JOIN " +
                    "where " +
                    " CUSTOMER_NO = '100001' and " +
                    " SHIP_ADDR_NO = " + addHVN + " and " +
                    "(OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )and " +
                   " CUSTOMER_PO_REL_NO is not null and " +
                   " to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy') = '" + N_XH + "'" +
                 " group by CUSTOMER_PART_NO,CATALOG_DESC,WANTED_DELIVERY_DATE ) TTDH" +
                  " where GIOGIAO >  " + GIOXUATH +
                  " Order by PART_NO, GIOGIAO ";
            TT_HANG_GIO = iFSPROVIDER.ExecuteQuery(sql);
            //bindingSource = new BindingSource { DataSource = TT_HANG_GIO };
            //DHTTGIO.DataSource = bindingSource;
            //DHTTGIO.EndEdit();
        }
        // Giao theo gio va theo ma 
        private void Tong_Hang_Gio_Ma(string MAHang)
        {
            int GIOXUATH = int.Parse(GIOXUAT.Substring(1, 2));
            string sql = " select CUSTOMER_PART_NO as PART_NO,to_char(WANTED_DELIVERY_DATE, 'HH24') as GIOGIAO,sum(BUY_QTY_DUE) as TTSLG " +
                    "from CUSTOMER_ORDER_JOIN " +
                    "where " +
                    " CUSTOMER_NO = '100001' and " +
                    " SHIP_ADDR_NO = " + addHVN + " and " +
                    "(OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )and " +
                   " CUSTOMER_PO_REL_NO is not null and " +
                   " to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy') = '" + N_XH + "' and to_char(WANTED_DELIVERY_DATE, 'HH24') > " + GIOXUATH + " and CUSTOMER_PART_NO = '" + MAHang + "'" +
                 " group by CUSTOMER_PART_NO,CATALOG_DESC,WANTED_DELIVERY_DATE " +
                  " Order by GIOGIAO ";
            TT_HANG_GIO_MA = iFSPROVIDER.ExecuteQuery(sql);
            //bindingSource = new BindingSource { DataSource = TT_HANG_GIO };
            //DHTTGIO.DataSource = bindingSource;
            //DHTTGIO.EndEdit();
        }
        // Tổng số lượng giao theo mã trong ngày theo khung giờ chọn
        private void TTGIAOTHEOMA()
        {
            
            int GIOXUATH =  int.Parse(GIOXUAT.Substring(1, 2));
            String sql = "select PART_NO,sum(TTSLG) as TTSLG " +
                " from" +
                " (select * from " +
                        " (select CUSTOMER_PART_NO as PART_NO,to_char(WANTED_DELIVERY_DATE, 'HH24') as GIOGIAO,sum(BUY_QTY_DUE) as TTSLG " +
                        "from CUSTOMER_ORDER_JOIN " +
                        "where " +
                        " CUSTOMER_NO = '100001' and " +
                        " SHIP_ADDR_NO = " + addHVN + " and " +
                        "(OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) or " +
                        " OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )and " +
                       " CUSTOMER_PO_REL_NO is not null and " +
                       " to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy') = '" + N_XH + "'" +
                     " group by CUSTOMER_PART_NO,CATALOG_DESC,WANTED_DELIVERY_DATE ) TTDH" +
                      " where GIOGIAO >  " + GIOXUATH + ") B1" +
                      " group by PART_NO ";
                TT_HANG_MA = iFSPROVIDER.ExecuteQuery(sql);
                
            
        }

        private void DOCQRCODE_Load(object sender, EventArgs e)
        {
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                //GIOXUAT = "'06'";
                GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName; 
            }
            LoadDL();
            listHANGTHIEU.Items.Clear();
            DuyetTTHangThieu();
            listVGHEPLOT.Items.Clear();
            GHEP_LOT();
        }

        private void cmd_CheckGhep_Click(object sender, EventArgs e)
        {
            NGAYXUAT_DT.DateTime = dateNX.DateTime;
            GIODAGIAHANG UF_GIODAXUAT = new GIODAGIAHANG();
            UF_GIODAXUAT.ShowDialog();
            Tong_Hang_Gio();
            listHANGTHIEU.Items.Clear();
            DuyetTTHangThieu();
            listVGHEPLOT.Items.Clear();
            GHEP_LOT();
        }
        private Boolean KT_GHEP_LOT(string partno,int slxuat)
        {
            Boolean KT = true;
            return KT;
        }

        private void RDO_GXHN_SelectedIndexChanged(object sender, EventArgs e)
        {
            RadioGroup edit = sender as RadioGroup;
            if (edit.SelectedIndex == 0) GIOXUAT = "'06'" ;
            if (edit.SelectedIndex == 1) GIOXUAT= "'08'";
            if (edit.SelectedIndex == 2) GIOXUAT= "'11'";
            if (edit.SelectedIndex == 3) GIOXUAT= "'12','13'";
            if (edit.SelectedIndex == 4) GIOXUAT = "'14','15'";
            
            if (edit.SelectedIndex == 5) GIOXUAT = "'16','17'";
            if (edit.SelectedIndex == 6) GIOXUAT = "'18','19'";
            if (edit.SelectedIndex == 7) GIOXUAT = "'21','22'";
            if (edit.SelectedIndex == 7) GIOXUAT = "'00'";
            LoadDL();
            listHANGTHIEU.Items.Clear();
            DuyetTTHangThieu();
            listVGHEPLOT.Items.Clear();
            GHEP_LOT();

        }

        private void radioGroup2_SelectedIndexChanged(object sender, EventArgs e)
        {
            RadioGroup edit = sender as RadioGroup;
            if (edit.SelectedIndex == 0) GIOXUAT = "'06','07','08'";
            if (edit.SelectedIndex == 1) GIOXUAT = "'09','10','11'";
            if (edit.SelectedIndex == 2) GIOXUAT = "'12','13'";
            if (edit.SelectedIndex == 3) GIOXUAT = "'14','15'";
            if (edit.SelectedIndex == 4) GIOXUAT = "'16','17','18'";

            if (edit.SelectedIndex == 5) GIOXUAT = "'19','20'";
            if (edit.SelectedIndex == 6) GIOXUAT = "'21','22'";
            if (edit.SelectedIndex == 7) GIOXUAT = "'00'";

            LoadDL();
            listHANGTHIEU.Items.Clear();
            DuyetTTHangThieu();
            listVGHEPLOT.Items.Clear();
            GHEP_LOT();
        }

        private void tabPaneHVN_Click(object sender, EventArgs e)
        {

            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                //GIOXUAT = "'06'";
                GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
                
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName;
                
            }
            LoadDL();
            listHANGTHIEU.Items.Clear();
            DuyetTTHangThieu();
            listVGHEPLOT.Items.Clear();
            GHEP_LOT();
        }

        private void dateNX_EditValueChanged(object sender, EventArgs e)
        {
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                GIOXUAT = "'06'";
                //GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName;
            }
            LoadDL();
            listHANGTHIEU.Items.Clear();
            DuyetTTHangThieu();
            listVGHEPLOT.Items.Clear();
            GHEP_LOT();
        }
    }
}