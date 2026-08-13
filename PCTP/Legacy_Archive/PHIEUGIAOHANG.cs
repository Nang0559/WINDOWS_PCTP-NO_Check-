//using DevExpress.CodeRush.StructuralParser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using DevExpress.Utils;
using DevExpress.XtraGrid.Editors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraEditors.Controls;
using System.Web.UI.Design;
using DevExpress.XtraGrid;
using DevExpress.XtraReports.UI;
using DevExpress.Xpf;
using DevExpress.XtraSplashScreen;

using DevExpress.XtraGrid.Views.Grid;
using System.Collections;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.XtraLayout.Utils;
using DevExpress.XtraGrid.Menu;
using DevExpress.Utils.Menu;
using DevExpress.XtraGrid.Columns;

namespace PCTP.QRCODE_HVN
{
    public partial class PHIEUGIAOHANG : DevExpress.XtraBars.TabForm
    {
        clsResize _form_resize;
        public PHIEUGIAOHANG()
        {
            InitializeComponent();
            loadKGXSQL();
            loadKGXHNSQL();
            _form_resize = new clsResize(this);
            this.Load += _Load;
            this.Resize += _Resize;
            //PHIEUGIAOHANG FR = new PHIEUGIAOHANG();
            lvwColumnSorter = new ListViewColumnSorter();
            this.listVGHEPLOT.ListViewItemSorter = lvwColumnSorter;
            dateNX.DateTime = DateTime.Now;
            this.pnbanqrcode3.Size = new Size(1657, 250);
            this.PanelDLGHEP.Size = new Size(1657, 250);
        }
        private void _Load(object sender, EventArgs e)
        {
            _form_resize._get_initial_size();
        }

        private void _Resize(object sender, EventArgs e)
        {
            _form_resize._resize();
        }
        IFSPROVIDER iFSPROVIDER = new IFSPROVIDER();
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public string GIOXUAT, GIOXUATH;
        public static DataGridView G_PHIEUGIAOHANG = new DataGridView();
        // Lấy thông tin đơn hàng theo ngày .
        private DataTable TT_HANG_MA = new DataTable();
        private DataTable TT_HANG_GIO = new DataTable();
        private DataTable TT_HANG_GIO_MA = new DataTable();
        private DataGridView DHTTGIO = new DataGridView();
        public static DateEdit NGAYXUAT_DT = new DateEdit();
        public static ListView KQTRUNGMASL = new ListView();
        private string _MAFCC;
        private DataTable DOCQRCODETMP = new DataTable();
        private DataTable DSTRUNG = new DataTable();
        private int lodappp = 0;
        public static int TTFOR_WAIT = 0;
        int addHVN;
        string N_XH;
        private void loadKGXSQL()
        {
            radioGroup2.Properties.Items.Clear();
            string sql = "select A.GioFCCVP  " +
                        " FROM " +
                        " ( " +
                        " select GioFCCVP, MAX(ID) AS GIOHVN " +
                        " from QRCODE_CHANGETIME " +
                        " group by GioFCCVP " +
                        " ) " +
                        " A " +
                        " order by A.GIOHVN ";
            string GT = "", GT1 = "";
            string[] Giatri;
            DataTable R = new DataTable();
            R = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            for (int i = 0; i < R.Rows.Count; i++)
            {
                string VL = R.Rows[i]["GioFCCVP"].ToString();
                if (VL == "(O TYPE #)")
                {
                    GT = "'01'";
                }
                else
                {
                    if (VL == "(O TYPE 6)")
                    {
                        GT = "'00'";
                    }
                    else
                    {
                        if (VL == "(GIAO DB)")
                        {
                            GT = "#";
                        }
                        else
                        {
                            GT1 = "";
                            GT = "";
                            Giatri = VL.Split('+');
                            for (int j = 0; j < Giatri.Length; j++)
                            {
                                GT1 = Giatri[j].Replace("(", "");
                                GT1 = GT1.Replace(")", "");
                                GT1 = GT1.Replace("+", "");
                                GT1 = GT1.Replace("H", "");
                                if (GT1.Length == 1)
                                {
                                    GT1 = "0" + GT1;
                                }
                                if (GT == "")
                                {
                                    GT = "'" + GT1 + "'";
                                }
                                else
                                {
                                    GT = GT + "," + GT1;
                                }
                            }
                        }
                    }
                }
                RadioGroupItem item = new RadioGroupItem(i, VL, true);
                item.AccessibleName = GT;
                item.Description = VL;

                radioGroup2.Properties.Items.Add(item);
            }
            radioGroup2.EditValue = 0;
        }
        private void loadKGXHNSQL()
        {
            RDO_GXHN.Properties.Items.Clear();
            string sql = "select A.GioFCCHN  " +
                        " FROM " +
                        " ( " +
                        " select GioFCCHN, MAX(ID) AS GIOHVN " +
                        " from QRCODE_CHANGETIME " +
                        " group by GioFCCHN " +
                        " ) " +
                        " A " +
                        " order by A.GIOHVN ";
            string GT1 = "", GT = "";
            string[] Giatri;
            DataTable R = new DataTable();
            R = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            for (int i = 0; i < R.Rows.Count; i++)
            {
                string VL = R.Rows[i]["GioFCCHN"].ToString();
                if (VL == "(O TYPE #)")
                {
                    GT = "'01'";
                }
                else
                {
                    if (VL == "(O TYPE 6)")
                    {
                        GT = "'00'";
                    }
                    else
                    {
                        if (VL == "(GIAO DB)")
                        {
                            GT = "#";
                        }
                        else
                        {
                            GT1 = "";
                            GT = "";
                            Giatri = VL.Split('+');
                            for (int j = 0; j < Giatri.Length; j++)
                            {
                                GT1 = Giatri[j].Replace("(", "");
                                GT1 = GT1.Replace(")", "");
                                GT1 = GT1.Replace("+", "");
                                GT1 = GT1.Replace("H", "");
                                if (GT1.Length == 1)
                                {
                                    GT1 = "0" + GT1;
                                }
                                if (GT == "")
                                {
                                    GT = "'" + GT1 + "'";
                                }
                                else
                                {
                                    GT = GT + "," + GT1;
                                }
                            }
                        }
                    }
                }
                RadioGroupItem item = new RadioGroupItem(i, VL, true);
                item.AccessibleName = GT;
                item.Description = VL;

                RDO_GXHN.Properties.Items.Add(item);
            }
            RDO_GXHN.EditValue = 0;
        }


        #region Xét phiếu đã bắn 
        private int Trangthaigiao()
        {

            int KQ = 0;
            string NHAMAY, sql, NXH = dateNX.DateTime.ToString("yyyy-MM-dd");
            DataTable STATAUS = new DataTable();
            if (addHVN == 1)
            {
                NHAMAY = "(NHA MAY VP)";
            }
            else
            {
                NHAMAY = "(NHA MAY HA NAM)";
            }
            sql = "select status from LUUPHIEUGIAOHANG where NGAYGIAO= '" + NXH + "' and GIOGIAOFCC = '" + GIOXUATH + "' and NHAMAY like '%" + NHAMAY + "' group by STATUS";
            STATAUS = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            if (STATAUS.Rows.Count == 0)
            {
                KQ = 0;
            }
            else
            {
                if (STATAUS.Rows.Count == 1)
                {
                    if (STATAUS.Rows[0]["STATUS"].ToString() == "NG")
                    {
                        KQ = 1;
                    }
                    else
                    {
                        KQ = 2;
                    }
                }
                else

                {
                    KQ = 1;
                }
            }
            return KQ;
        }

        private void loadPHIEU()
        {
            Boolean DANGBAN = false;
            string CUA, TRUYEN, DV, TENHANG, LOT;
            if (GIOXUAT != "#")
            {
                LoadDL();
            }
            WaitForm2.SO = 1;
            splashScreenManager2.ShowWaitForm();

            string NGAYGIAO = dateNX.DateTime.ToString("yyyy-MM-dd");
            setstatusbootomDOCQ9RCODE();
            //try
            //{
            //    // handle = ShowProgressPanel();
            //    int TTBDL = Trangthaigiao();
            //    if (TTBDL == 0)
            //    {

            //        if (gridVDONHANG.RowCount != 0)
            //        {
            //            listHANGTHIEU.Items.Clear();
            //            DuyetTTHangThieu();
            //            listVGHEPLOT.Items.Clear();
            //            GHEP_LOT();
            //            listVGHEPLOT.Sorting = SortOrder.Ascending;
            //            //sidePaTTDOCQRCODE.Enabled = true;
            //            if (KT_DANGBANQRCODE() == true)
            //            {
            //                DANGBAN = true;
            //            }
            //            for (int i = 0; i < gridVDONHANG.RowCount; i++)
            //            {
            //                string MaHang = gridVDONHANG.GetRowCellValue(i, "MAHANG").ToString();
            //                DateTime GIO_XUAT_LIST = DateTime.Parse(gridVDONHANG.GetRowCellValue(i, "NGAYGIAO").ToString());
            //                int GXH = int.Parse(GIO_XUAT_LIST.ToString("HH"));

            //                int SLXUAT = int.Parse(gridVDONHANG.GetRowCellValue(i, "SOLUONG").ToString());
            //                CUA = gridVDONHANG.GetRowCellValue(i, "CUA").ToString();
            //                TRUYEN = gridVDONHANG.GetRowCellValue(i, "TRUYEN").ToString();
            //                TENHANG = gridVDONHANG.GetRowCellValue(i, "TENHANG").ToString();
            //                DV = "";
            //                string sql1 = "SELECT cast(MinCloseQty as int)  from B20Item where Code= '" + MaHang + "'";
            //                string QCDg = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1);


            //                gridVDONHANG.SetRowCellValue(i, "HOP", QCDg);
            //                gridVDONHANG.SetRowCellValue(i, "STT", i + 1);
            //                if (LoadTTDANGDOCQR() == true)
            //                {

            //                    string LOTNO_PUT = "select lot from tmpphieugiaohang where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                    string __l = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, LOTNO_PUT);
            //                    gridVDONHANG.SetRowCellValue(i, "LOT", __l);
            //                    string Statuss = "select STATUS from tmpphieugiaohang where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                    string __S = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, Statuss);
            //                    gridVDONHANG.SetRowCellValue(i, "STATUS", __S);
            //                    string StatusDoc = "select TTPHIEU from tmpphieugiaohang where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                    string __SD = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, StatusDoc);
            //                    if (__SD != "")
            //                    {
            //                        gridVDONHANG.SetRowCellValue(i, "STATUSDOC", __SD);
            //                    }
            //                    else
            //                    {
            //                        gridVDONHANG.SetRowCellValue(i, "STATUSDOC", "NG");
            //                    }
            //                }



            //            }
            //            if (KT_DANGBANQRCODE() == true)
            //            {
            //                if (LoadTTDANGDOCQR() == true)
            //                {
            //                    LoadDLTMP();
            //                }
            //            }
            //        }
            //        else
            //        {
            //            if (LoadTTDANGDOCQR() == true)
            //            {
            //                LoadDLTMP();
            //            }
            //        }
            //    }

            //    else
            //    {
            //        //listHANGTHIEU.Items.Clear();
            //        //listVGHEPLOT.Items.Clear();
            //        if (gridVDONHANG.RowCount != 0)
            //        {
            //            int demslok = 0;
            //            for (int i = 0; i < gridVDONHANG.RowCount; i++)
            //            {
            //                string MaHang = gridVDONHANG.GetRowCellValue(i, "MAHANG").ToString();
            //                DateTime GIO_XUAT_LIST = DateTime.Parse(gridVDONHANG.GetRowCellValue(i, "NGAYGIAO").ToString());
            //                int GXH = int.Parse(GIO_XUAT_LIST.ToString("HH"));

            //                int SLXUAT = int.Parse(gridVDONHANG.GetRowCellValue(i, "SOLUONG").ToString());
            //                CUA = gridVDONHANG.GetRowCellValue(i, "CUA").ToString();
            //                TRUYEN = gridVDONHANG.GetRowCellValue(i, "TRUYEN").ToString();
            //                TENHANG = gridVDONHANG.GetRowCellValue(i, "TENHANG").ToString();
            //                DV = "";
            //                string sql1 = "SELECT cast(MinCloseQty as int)  from B20Item where Code= '" + MaHang + "'";
            //                string QCDg = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1);


            //                gridVDONHANG.SetRowCellValue(i, "HOP", QCDg);
            //                gridVDONHANG.SetRowCellValue(i, "STT", i + 1);
            //                string LOTNO_PUT = "select lot from LUUPHIEUGIAOHANG where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                string __l = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, LOTNO_PUT);
            //                string Statuss = "select STATUS from LUUPHIEUGIAOHANG where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                string __S = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, Statuss);
            //                string StatusDoc = "select TTPHIEU from LUUPHIEUGIAOHANG where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                string __SD = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, StatusDoc);
            //                if (__l == "")
            //                {
            //                    LOTNO_PUT = "select lot from tmpphieugiaohang where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                    __l = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, LOTNO_PUT);
            //                    Statuss = "select STATUS from tmpphieugiaohang where MAHANG = '" + MaHang + "' AND CUA= '" + CUA + "' AND TRUYEN = '" + TRUYEN + "' AND SOLUONG = " + SLXUAT + " AND NGAYGIAO = '" + NGAYGIAO + "' AND GIOGIAO like '%" + GXH + "%'";
            //                    __S = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, Statuss);
            //                }

            //                if (__l != "")
            //                {
            //                    gridVDONHANG.SetRowCellValue(i, "LOT", __l);

            //                    demslok = demslok + 1;
            //                }

            //                if (__S != "")
            //                {
            //                    gridVDONHANG.SetRowCellValue(i, "STATUS", __S);
            //                }
            //                if (__SD != "")
            //                {
            //                    gridVDONHANG.SetRowCellValue(i, "STATUSDOC", __SD);
            //                }
            //                else
            //                {
            //                    gridVDONHANG.SetRowCellValue(i, "STATUSDOC", "NG");
            //                }
            //            }
            //            // if (demslok == gridVDONHANG.RowCount )
            //            // {
            //            if (LoadTTDANGDOCQR() == false)
            //            {
            //                if (KT_DANGBANQRCODE() == false)
            //                {
            //                    sidePaTTDOCQRCODE.Enabled = true;
            //                }
            //                else
            //                {
            //                    sidePaTTDOCQRCODE.Enabled = false;
            //                }
            //            }
            //            else
            //            {
            //                sidePaTTDOCQRCODE.Enabled = true;
            //            }
            //            //}
            //            //else
            //            //{
            //            //    sidePaTTDOCQRCODE.Enabled = true;
            //            //}
            //        }
            //        else
            //        {
            //            LoadDLDB();

            //        }
            //    }
            //    gridVDONHANG.RowCellStyle += new DevExpress.XtraGrid.Views.Grid.RowCellStyleEventHandler(gridVDONHANG_RowCellStyle);
            //}
            //finally
            //{
               splashScreenManager2.CloseWaitForm();
            //}
        }
        private void loadPHIEUBD()
        {
            THEMPDB TP = new THEMPDB(1);
            TP.ShowDialog();
            GIOXUAT = TP.GG;
            loadPHIEU();
            sidePTHEMPDB.Visible = true;
        }
        private void LoadDLDB()
        {
            string NHAMAY, NXH = dateNX.DateTime.ToString("yyyy-MM-dd");
            DataTable PGH = new DataTable();
            if (addHVN == 1)
            {
                NHAMAY = "(NHA MAY VP)";
            }
            else
            {
                NHAMAY = "(NHA MAY HA NAM)";
            }
            string sql = "select STT,left(GIOGIAO,2),SOLUONG,DV,NHAMAY,TRUYEN,MAHANG ,TENHANG,CUA, " +
                   " '' AS HOP,LOT,STATUS,'OK' as STATUSDOC " +
                   " from LUUPHIEUGIAOHANG " +
                    " where NGAYGIAO= '" + NXH + "' and GIOGIAOFCC = '" + GIOXUATH + "' and NHAMAY like '%" + NHAMAY + "' order by STT";
            PGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            gridCtrDONHANG.DataSource = PGH;
        }

        #region set trang thai
        private Boolean KTUPDATE()
        {
            Boolean KQ = false;
            string sql = "select count(STATUS) from TMPPHIEUGIAOHANG where LOT <> '' and status = 'NG'";
            string SL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (int.Parse(SL) > 0)
            {
                KQ = true;
            }
            else
            {
                KQ = false;
            }
            return KQ;

        }
        #endregion
        #region Phiếu giao hàng
       
        private void LoadDLTMP()
        {
            string sql = " select STT, GIOGIAO,SOLUONG,DV,NHAMAY,TRUYEN,MAHANG,TENHANG,CUA, " +
                   " '' AS HOP,LOT,STATUS,TTPHIEU as STATUSDOC  from tmpphieugiaohang ";

            DataTable TMPPHIEUGH = new DataTable();
            TMPPHIEUGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            gridCtrDONHANG.DataSource = TMPPHIEUGH;
        }
        private Boolean LoadTTDANGDOCQR()
        {
            string sql = "select addnm,(substring(CONVERT(VARCHAR(10),ngaygiao,103),1,2) + substring(CONVERT(VARCHAR(10),ngaygiao,103),4,2) + " +
                            " substring(CONVERT(VARCHAR(10), ngaygiao, 103), 7, 4)) as ngaygiao,giogiao from TMPPHIEUGIAOHANG group by addnm, ngaygiao, giogiao ";

            DataTable TTGH = new DataTable();
            int ADDNM = 0;
            Boolean KQ = false;
            string N_GIAOH = "";
            string G_GIAOHANG = "";
            //if (KT_DANGBANQRCODE() == true)
            //{

            TTGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            if (TTGH.Rows.Count > 0)
            {
                ADDNM = int.Parse(TTGH.Rows[0]["ADDNM"].ToString());

                N_GIAOH = TTGH.Rows[0]["ngaygiao"].ToString();
                G_GIAOHANG = TTGH.Rows[0]["giogiao"].ToString();
                G_GIAOHANG = G_GIAOHANG.Replace("h", "");
                if (G_GIAOHANG.Length == 1)
                {
                    G_GIAOHANG = "'0" + G_GIAOHANG.Trim() + "'";
                }
                else
                {
                    G_GIAOHANG = "'" + G_GIAOHANG.Trim() + "'";
                }
                if (addHVN == ADDNM && N_XH == N_GIAOH && GIOXUAT.Contains(G_GIAOHANG) == true)
                {
                    KQ = true;

                }
                else
                {
                    KQ = false;

                }

            }
            else
            {
                KQ = true;
            }
            return KQ;
        }
        private void LOADDOCQRCODE()
        {
            string sq = "select STT,LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,SLTEMHVN,GIO,SUALOTHVN,KETQUA from DOCQRCODE order by STT";
            DataTable DOCQR = new DataTable();
            DOCQR = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sq);
            gridCtrDOCQrCODE.DataSource = DOCQR;
            //if (kiemtramatrungsl() == true)
            //{
            //    if (KQTRUNGMASL.Items.Count > 0)
            //    {
            //        FRM_LISTRUNGMSL fRM_LISTRUNGMSL = new FRM_LISTRUNGMSL(KQTRUNGMASL);
            //        fRM_LISTRUNGMSL.ShowDialog();
            //    }
            //}
        }
        private Boolean KTTTGH()
        {
            Boolean KQ;
            string sql = "select lot,status from tmpphieugiaohang where lot <> ''";
            DataTable SKQ = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            if (SKQ.Rows.Count > 1)
            {
                KQ = true;
            }
            else
            {
                KQ = false;
            }
            return KQ;
        }
        #endregion
        #region Bắn QRcode
        private Boolean CHOPHEPBANQRCODE()
        {
            Boolean KQ = false;
            string sql = "select count(*) from docqrcode ";
            string Sl = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);

            if (int.Parse(Sl) > 0)
            {
                KQ = true;

            }
            else
            {
                KQ = false;
            }

            return KQ;

        }
        private Boolean ChoPhepCNK()
        {
            Boolean KQ = false;
            string LOT;
            for (int i = 0; i < gridVDONHANG.RowCount; i++)
            {
                LOT = gridVDONHANG.GetRowCellValue(i, "LOT").ToString();
                if (LOT != "")
                {
                    KQ = true;
                }
            }
            return KQ;
        }
        private void cmd_DOCQRCODE_Click(object sender, EventArgs e)
        {
            //if (CHOPHEPBANQRCODE() == true)
            //{
                LoadPhieu_GridView();
            //}

            //else
            //{
            //    MessageBox.Show("Không thể DocQRCODE , Kiểm tra lại phiếu ! ", "Thông Báo!", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            //}
        }
        private void LoadPhieu_GridView()
        {

            pnbanqrcode1.Visible = true;
            pnbanqrcode2.Visible = true;
            pnbanqrcode3.Visible = true;
            sideIN.Visible = false;
            sidePChonKGX.Visible = false;
            
            sidePGL.Visible = false;
            DataSet DH = new DataSet();
            string NHAMAY;
            string NGAYGIAO = dateNX.DateTime.ToString("yyyy-MM-dd");
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                NHAMAY = "HANAM";
            }
            else
            {
                NHAMAY = "VP";
            }
            if (KT_DANGBANQRCODE() == false)
            {
                
                    LOADDOCQRCODE();
            }
            else
            {


                DH = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_LOAD_PHIEU_DOCQR");
                //////////////////////////

            }
            txt_DOCQRCODE.Focus();
            this.pnbanqrcode3.Size = new Size(1657, 60);
            this.PanelDLGHEP.Size = new Size(1657, 60);
            gridCtrSUASL.Visible = false;

        }
        private void setstatusbootomDOCQ9RCODE()
        {

            if (KT_DANGBANQRCODE() == false)
            {

                cmd_DOCQRCODE.Enabled = true;


            }
        }
        private DataTable TableGL()
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("MH", typeof(string));
            tbl.Columns.Add("GG", typeof(string));
            tbl.Columns.Add("LG", typeof(string));


            //for (int i = 0; i < RowCount; i++)
            //    tbl.Rows.Add(new object[] { String.Format("{1}Name{0}", i, prefix), i, i, DateTime.Now.AddDays(i) });
            return tbl;
        }
        private void GHEP_LOT()
        {

            //string sql;
            //string PartNo;
            //int GioGiao;
            //int SLGIAO;
            //int TCDG;
            //DataRow row;
            //string LOTDUYET;
            //int SLTKCONLAI_LOTDUYET;
            //object TONKHOTEM;
            //object TONKHOCOLAITMP;
            //int slle = 0;
            //string LOTGHEP = "";
            //int SLCANGHEP = 0;
            //DataTable TONKHOTHEOMA1 = new DataTable();
            //DataTable TONKHOTHEOMATMP = new DataTable();

            //// Lay don hang theo gio
            //N_XH = dateNX.DateTime.ToString("ddMMyyyy");
            //DataTable tbl = new DataTable();
            //tbl.Columns.Add("MH", typeof(string));
            //tbl.Columns.Add("GG", typeof(string));
            //tbl.Columns.Add("LG", typeof(string));
            ////---------------------------------------------------------------------------------------------------------------------------------
            ////string sqlTK = "select LOT,SLCONLAI from STOCKTP where SLCONLAI > 0 and PART = '" + MaHang + "' order by lot";
            //DataTable TBTK = new DataTable();
            ////TBTK = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sqlTK);
            ////TK.DataSource = TBTK;
            ////string LOT_GHEP = "";
            //Tong_Hang_Gio();
            //// Duyệt Bảng Grid Đơn Hàng Theo Giờ : DHTTGIO
            //CAPNHAPTMP("%", 0);
            //for (int j = 0; j < TT_HANG_GIO.Rows.Count; j++)
            //{
            //    PartNo = TT_HANG_GIO.Rows[j].Field<string>("PART_NO");
            //    if (PartNo == "23010-K12-V010-M1")
            //    {
            //        int stops = 1;
            //    }
            //    GioGiao = int.Parse(TT_HANG_GIO.Rows[j].Field<string>("GIOGIAO"));
            //    object O_SLGIAO = TT_HANG_GIO.Rows[j][3];
            //    SLGIAO = Convert.ToInt32(O_SLGIAO);
            //    // Lấy tiêu chuẩn đóng gói .của mã hàng .
            //    sql = "SELECT cast(isnull(MinCloseQty,0) as int)  from B20Item where Code= '" + PartNo + "'";
            //    string sTCDG = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            //    if (sTCDG == "")
            //    {
            //        TCDG = -1;
            //    }
            //    else
            //    {

            //        TCDG = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            //        if (TCDG == 0)
            //        {
            //            TCDG = -1;
            //        }
            //    }
            //    // Lấy tồn kho theo LOT của mã hàng .
            //    string sql1 = "select lot,part,isnull(slconlai,0) as slconlai ,slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0 order by lot";
            //    TONKHOTHEOMA1.Clear();
            //    TONKHOTHEOMA1 = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql1);
            //    // Lấy tồn kho tổng của mã hàng.

            //    //Duyệt Đơn hàng và với kho .

            //    if (TONKHOTHEOMA1.Rows.Count > 0)// Nếu có tồn kho
            //    {
            //        for (int i = 0; i < TONKHOTHEOMA1.Rows.Count; i++)// duyệt tồn kho
            //        {
            //            // Cập Nhập tạm thông tin tồn kho vào trường TMP

            //            TONKHOTEM = TONKHOTHEOMA1.Rows[i][3];
            //            if (TONKHOTEM == null)
            //            {
            //                TONKHOTEM = 0;
            //            }

            //            if (Convert.ToInt32(TONKHOTEM) == 0)
            //            {
            //                LOTDUYET = TONKHOTHEOMA1.Rows[i].Field<string>("lot");
            //                object TONKHOCOLAI = TONKHOTHEOMA1.Rows[i][2];
            //                SLTKCONLAI_LOTDUYET = Convert.ToInt32(TONKHOCOLAI);
            //                CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET);
            //            }
            //            // Nếu tổng tồn kho ít hơn số lượng cần giao thì cho vào danh sách hàng thiếu
            //        }
            //        // Lấy tồn kho theo LOTTMP của mã hàng .
            //        string sqlTMP = "select lot,part,isnull(slconlai,0) as slconlai ,isnull(slconlaitmp,0) as slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0 order by lot";
            //        TONKHOTHEOMATMP = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sqlTMP);
            //        // Duyệt Hàng theo TMP tồn kho .

            //        Boolean canghep = false;
            //        for (int i = 0; i < TONKHOTHEOMATMP.Rows.Count; i++)
            //        {
            //            LOTDUYET = TONKHOTHEOMATMP.Rows[i].Field<string>("lot");
            //            TONKHOCOLAITMP = TONKHOTHEOMATMP.Rows[i][3];
            //            SLTKCONLAI_LOTDUYET = Convert.ToInt32(TONKHOCOLAITMP);
            //            if (Convert.ToInt32(TONKHOCOLAITMP) > 0)
            //            {
            //                if (SLTKCONLAI_LOTDUYET < SLGIAO)
            //                {
            //                    slle = SLTKCONLAI_LOTDUYET % TCDG;
            //                    if (slle == 0)
            //                    {

            //                        CAPNHAPTMP(LOTDUYET, -1);
            //                        SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;


            //                    }
            //                    else
            //                    {
            //                        if (canghep == true)
            //                        {
            //                            if (SLCANGHEP > SLTKCONLAI_LOTDUYET)
            //                            {
            //                                LOTGHEP = LOTGHEP + LOTDUYET + "-" + SLTKCONLAI_LOTDUYET + ",";
            //                                SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
            //                                SLCANGHEP = SLCANGHEP - SLTKCONLAI_LOTDUYET;
            //                                canghep = true;
            //                                CAPNHAPTMP(LOTDUYET, -1);


            //                            }
            //                            else
            //                            {
            //                                LOTGHEP = LOTGHEP + "," + LOTDUYET + "-" + SLCANGHEP;
            //                                SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
            //                                SLCANGHEP = 0;
            //                                canghep = false;
            //                                CAPNHAPTMP(LOTDUYET, -1);

            //                                break;
            //                            }
            //                        }
            //                        else
            //                        {
            //                            LOTGHEP = LOTDUYET + "-" + slle + ",";
            //                            SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
            //                            SLCANGHEP = TCDG - slle;
            //                            canghep = true;
            //                            CAPNHAPTMP(LOTDUYET, -1);
            //                        }

            //                    }
            //                }
            //                else
            //                {

            //                    if (canghep == true)
            //                    {
            //                        LOTGHEP = LOTGHEP + LOTDUYET + "-" + SLCANGHEP;
            //                        ADDLIST_GHEPLOT(PartNo, GioGiao, LOTGHEP);
            //                        //for (int i = 0; i < 10; i++)
            //                        //{
            //                        row = tbl.NewRow();
            //                        row["MH"] = PartNo;
            //                        row["GG"] = GioGiao;
            //                        row["LG"] = LOTGHEP;
            //                        tbl.Rows.Add(row);
            //                        //}
            //                        CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET - SLGIAO);
            //                        canghep = false;
            //                        SLCANGHEP = 0;
            //                        break;
            //                    }
            //                    else
            //                    {

            //                        CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET - SLGIAO);
            //                        canghep = false;
            //                        SLCANGHEP = 0;
            //                        break;
            //                    }
            //                }

            //            }

            //        }
            //    }
            //    else
            //    {
            //        //MessageBox.Show("Không có tồn kho của mã hàng : " + PartNo, "THÔNG BÁO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    }

           // }

           // gridCTTGL.DataSource = tbl;
        }
        private void ADDLIST_GHEPLOT(string MAHANG, int GIOGIAO, string LOTGHEP)
        {
            listVGHEPLOT.View = View.Details;
            listVGHEPLOT.GridLines = true;
            ListViewItem item1 = new ListViewItem(MAHANG);
            item1.SubItems.Add(GIOGIAO.ToString().Trim());
            item1.SubItems.Add(LOTGHEP.ToString());
            listVGHEPLOT.Items.Add(item1);
        }
        // Cập NHập LOTTMP
        
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
                  " where GIOGIAO >=  " + GIOXUATH +
                  " Order by PART_NO, GIOGIAO ";
            TT_HANG_GIO = iFSPROVIDER.ExecuteQuery(sql);
            sql = "IF EXISTS(SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TBL_TT_GIO]') AND type in (N'U')) ";
            sql += " begin ";
            sql += " Drop table TBL_TT_GIO ";
            sql += " end ";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            /// Tao Bang SQL Va copy dl
            string createtablehangthieu = SqlTableCreator.GetCreateFromDataTableSQL("TBL_TT_GIO", TT_HANG_GIO);
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, createtablehangthieu);
            string sqldelete = "delete from TBL_TT_GIO";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldelete);
            SqlTableCreator.BulkInsertDataTable(sqlBRV.B7R2_FCCdb, "TBL_TT_GIO", TT_HANG_GIO);
            //////////////////////////
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
            sql = "IF EXISTS(SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TBL_TT_MA_GIO]') AND type in (N'U')) ";
             sql +=   " begin ";
            sql += " Drop table TBL_TT_MA_GIO ";
            sql += " end ";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            /// Tao Bang SQL Va copy dl
            string createtablehangthieu = SqlTableCreator.GetCreateFromDataTableSQL("TBL_TT_MA_GIO", TT_HANG_GIO_MA);
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, createtablehangthieu);
            string sqldelete = "delete from TBL_TT_MA_GIO";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldelete);
            SqlTableCreator.BulkInsertDataTable(sqlBRV.B7R2_FCCdb, "TBL_TT_MA_GIO", TT_HANG_GIO_MA);
            //////////////////////////
        }
        // Tổng số lượng giao theo mã trong ngày theo khung giờ chọn
      

        private void DOCQRCODE_Load(object sender, EventArgs e)
        {

        }

        private void cmd_CheckGhep_Click(object sender, EventArgs e)
        {
            NGAYXUAT_DT.DateTime = dateNX.DateTime;
            GIODAGIAHANG UF_GIODAXUAT = new GIODAGIAHANG();
            UF_GIODAXUAT.ShowDialog();
            Tong_Hang_Gio();
            
           
            listVGHEPLOT.Items.Clear();
            GHEP_LOT();
        }
        private Boolean KT_GHEP_LOT(string partno, int slxuat)
        {
            Boolean KT = true;
            return KT;
        }


        private void PHIEUGIAOHANG_Load(object sender, EventArgs e)
        {
            //_form_resize._get_initial_size();
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                //GIOXUAT = "'06'";
                GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
                GIOXUATH = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].Description;
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName;
                GIOXUATH = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].Description;
            }
            loadPHIEU();
            //LoadDL();
            lodappp = 1;

            // gridCtrDOCQrCODE.DataSource = CreateTable();
            pnbanqrcode1.Visible = false;
            pnbanqrcode2.Visible = false;
            pnbanqrcode3.Visible = false;
            sidePTHEMPDB.Visible = false;
        }

        private void dateNX_EditValueChanged_1(object sender, EventArgs e)
        {

            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                GIOXUAT = "'06'";
                //GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
                GIOXUATH = "(06)H";
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName;
                GIOXUATH = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].Description;
            }
            //LoadDL();
            if (lodappp != 0 && GIOXUAT != "#")
            {
                loadPHIEU();
            }
            if (GIOXUAT == "#")
            {
                loadPHIEUBD();
            }
            //listHANGTHIEU.Items.Clear();
            //DuyetTTHangThieu();
            //listVGHEPLOT.Items.Clear();
            //GHEP_LOT();
        }

        private void tabPaneHVN_Click_1(object sender, EventArgs e)
        {
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                //GIOXUAT = "'06'";
                GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
                GIOXUATH = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].Description;
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName;
                GIOXUATH = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].Description;
            }
            //LoadDL();
            if (lodappp != 0 && GIOXUAT != "#")
            {
                loadPHIEU();
            }
            if (GIOXUAT == "#")
            {
                loadPHIEUBD();
            }
            //listHANGTHIEU.Items.Clear();
            //DuyetTTHangThieu();
            //listVGHEPLOT.Items.Clear();
            //GHEP_LOT();
        }

        private void RDO_GXHN_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                //GIOXUAT = "'06'";
                GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
                GIOXUATH = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].Description;
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName;
                GIOXUATH = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].Description;
            }
            //LoadDL();
            if (lodappp != 0 && GIOXUAT != "#")
            {
                loadPHIEU();
            }
            if (GIOXUAT == "#")
            {
                loadPHIEUBD();
            }
            
        }

        private void radioGroup2_SelectedIndexChanged(object sender, EventArgs e)
        {
            //RadioGroup edit = sender as RadioGroup;
            //if (edit.SelectedIndex == 0) GIOXUAT = "'06','07','08'";
            //if (edit.SelectedIndex == 1) GIOXUAT = "'09','10','11'";
            //if (edit.SelectedIndex == 2) GIOXUAT = "'12','13'";
            //if (edit.SelectedIndex == 3) GIOXUAT = "'14','15'";
            //if (edit.SelectedIndex == 4) GIOXUAT = "'16','17','18'";

            //if (edit.SelectedIndex == 5) GIOXUAT = "'19','20'";
            //if (edit.SelectedIndex == 6) GIOXUAT = "'21','22'";
            //if (edit.SelectedIndex == 7) GIOXUAT = "'00'";
            if (tabPaneHVN.SelectedPage == tabHN)
            {
                addHVN = 2;
                //GIOXUAT = "'06'";
                GIOXUAT = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].AccessibleName;
                GIOXUATH = RDO_GXHN.Properties.Items[RDO_GXHN.SelectedIndex].Description;
            }
            else
            {
                addHVN = 1;
                GIOXUAT = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].AccessibleName;
                GIOXUATH = radioGroup2.Properties.Items[radioGroup2.SelectedIndex].Description;
            }
            //LoadDL();
            if (lodappp != 0 && GIOXUAT != "#")
            {
                loadPHIEU();
            }
            if (GIOXUAT == "#")
            {
                loadPHIEUBD();
            }
            //listHANGTHIEU.Items.Clear();
            //DuyetTTHangThieu();
            // listVGHEPLOT.Items.Clear();
            //GHEP_LOT();

        }


        private DataTable CreateTable()
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("STT", typeof(int));
            tbl.Columns.Add("LOTFCC", typeof(string));
            tbl.Columns.Add("MAHANGFCC", typeof(string));
            tbl.Columns.Add("SLTEMFCC", typeof(int));
            tbl.Columns.Add("LOTHVN", typeof(string));
            tbl.Columns.Add("MAHANGHVN", typeof(string));
            tbl.Columns.Add("SLTEMHVN", typeof(int));
            tbl.Columns.Add("STTPHIEU", typeof(int));
            tbl.Columns.Add("SUALOTHVN", typeof(string));
            tbl.Columns.Add("KQ", typeof(string));
            //for (int i = 0; i < RowCount; i++)
            //    tbl.Rows.Add(new object[] { String.Format("{1}Name{0}", i, prefix), i, i, DateTime.Now.AddDays(i) });
            return tbl;
        }
        private Boolean KT_DANGBANQRCODE()
        {
            Boolean KQ;
            string sql = "select count(*) from DOCQRCODE";
            string S_KQ = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (S_KQ == "0")
            {
                KQ = true;
            }
            else
            {
                KQ = false;
            }
            return KQ;
        }
        private void ADDROWDATA(string CTY, int STT, String LOT, String MAHANG, int SLTEM, string KQ)
        {

            LISTV_BANQRCODE.View = View.Details;
            LISTV_BANQRCODE.GridLines = true;
            int vt;

            if (CTY == "FCC")
            {
                ListViewItem item1 = new ListViewItem(STT.ToString());
                item1.SubItems.Add(LOT);
                item1.SubItems.Add(MAHANG);
                item1.SubItems.Add(SLTEM.ToString());
                item1.SubItems.Add("");
                item1.SubItems.Add("");
                item1.SubItems.Add("");
                item1.SubItems.Add("");
                item1.SubItems.Add("");
                LISTV_BANQRCODE.Items.Add(item1);
            }
            else
            {
                vt = LISTV_BANQRCODE.Items.Count;
                LISTV_BANQRCODE.Items[vt - 1].SubItems.Add(LOT);
                LISTV_BANQRCODE.Items[vt - 1].SubItems.Add(MAHANG);
                LISTV_BANQRCODE.Items[vt - 1].SubItems.Add(SLTEM.ToString());
            }
        }
        #endregion
        #region KIểm tra Bắn
        private Boolean KTHANGDABAN_DANGBANQR(string MH, int SLBAN, string SP)
        {
            Boolean KQ = true;
            int SLDABAN;
            string SL_DB, sql;
            int SLCANGIAO;
            int DANGDOCKOTRUNG = 0;
            DataTable TB_CONLAI = new DataTable();
            if (SP == "" || SP == null)
            {
                sql = "select MAHANG,sum(SOLUONG) as SOLUONG ,STATUS  from  TMPPHIEUGIAOHANG where ( STATUS <> '1' or STATUS is null ) and MAHANG = '" + MH + "' and  TTPHIEU is null group by MAHANG ,STATUS ";
                DANGDOCKOTRUNG = 1;
            }
            else
            {
                sql = "select MAHANG,SOLUONG,STATUS  from  TMPPHIEUGIAOHANG where ( STATUS <> '1' or STATUS is null ) and MAHANG = '" + MH + "' and TTPHIEU = '" + SP + "'";
            }
            TB_CONLAI = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            string sql1;
            if (TB_CONLAI.Rows.Count > 0)
            {
                SLCANGIAO = int.Parse(TB_CONLAI.Rows[0]["SOLUONG"].ToString());
                if (DANGDOCKOTRUNG == 1)
                {
                    sql1 = "select sum(SLTEMFCC) from docqrcode where MAHANGFCC = '" + MH + "' and Gio is null";
                }
                else
                {
                    sql1 = "select sum(SLTEMFCC) from docqrcode where MAHANGFCC = '" + MH + "' and GIO = '" + SP + "'";
                }
                SL_DB = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1);
                if (SL_DB != "")
                {
                    SLDABAN = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1));
                    if (SLDABAN + SLBAN > SLCANGIAO)
                    {
                        KQ = false;
                    }
                    else
                    {
                        if ((SLDABAN + SLBAN) == SLCANGIAO)
                        {

                        }
                        KQ = true;
                    }
                }
                else
                {
                    if (SLBAN > SLCANGIAO)
                    {
                        KQ = false;
                    }
                    else
                    {
                        KQ = true;
                    }
                }
            }
            else
            {
                KQ = true;
            }

            return KQ;

        }
        private Boolean KTTRUNGMASLXUAT()
        {
            Boolean KQ;
            string sql;
            string dem;
            sql = "select T1.TRUNG from " +
           " (select count(mahang + cast(soluong as nvarchar)) as TRUNG from tmpphieugiaohang  group by mahang + cast(soluong as nvarchar)) as T1 " +
             "where t1.TRUNG > 1 ";
            dem = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (dem != "")
            {
                KQ = true;
            }
            else
            {
                KQ = false;
            }
            return KQ;
        }
        private Boolean kiemtramatrungsl()
        {

            Boolean KQ = false;
            string sql, STATUS, sqlupdatephieugiaohang;
            string STT, GIOXUAT, MA, TEN, SLXUAT;
            if (KTTRUNGMASLXUAT() == true)
            {
                KQTRUNGMASL.Items.Clear();
                sql = " select t3.STT,t3.CUA,t3.TRUYEN,t3.MAHANG,t3.TENHANG,t3.SOLUONG,t3.GIOGIAO,t3.TTPHIEU,t3.STATUS " +
                        " from " +
                         " (select t1.MAHANG, t1.TRUNG, T1.MSL " +
                        " from(select mahang, count(mahang + cast(soluong as nvarchar)) as TRUNG, (mahang + cast(soluong as nvarchar)) as MSL " +
                        " from TMPPHIEUGIAOHANG group by mahang, mahang + cast(soluong as nvarchar)) as T1 where T1.TRUNG > 1) " +
                        " T2 , TMPPHIEUGIAOHANG as T3 " +
                        " where T2.MSL = (T3.MAHANG + cast(t3.SOLUONG as nvarchar)) and (t3.STATUS = '0' or  t3.STATUS is null) ";
                DSTRUNG = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
                for (int i = 0; i < DSTRUNG.Rows.Count; i++)
                {
                    STT = DSTRUNG.Rows[i]["STT"].ToString();
                    MA = DSTRUNG.Rows[i]["MAHANG"].ToString();

                    TEN = DSTRUNG.Rows[i]["TENHANG"].ToString();
                    GIOXUAT = DSTRUNG.Rows[i]["GIOGIAO"].ToString();
                    SLXUAT = DSTRUNG.Rows[i]["SOLUONG"].ToString();
                    STATUS = DSTRUNG.Rows[i]["STATUS"].ToString();
                    if (STATUS == "")
                    {
                        STATUS = "Chưa Bắn QRCODE";
                    }
                    if (STATUS == "0")
                    {
                        STATUS = "Đang Bắn QRCODE";
                    }
                    if (STATUS == "1")
                    {
                        STATUS = "Đã Bắn QRCODE";
                    }
                    KQTRUNGMASL.Items.Add(new ListViewItem(new[] { STT, GIOXUAT, MA, TEN, SLXUAT, STATUS }));
                    sqlupdatephieugiaohang = "update tmpphieugiaohang set TTPHIEU = '" + STT + "' where STT = " + int.Parse(STT) + "";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdatephieugiaohang);

                }
                KQ = true;
            }

            return KQ;
        }
        private Boolean KTRAMA(string MaHang)
        {
            Boolean KQ = false;
            string sql = "select count(*) from TMPPHIEUGIAOHANG where MAHANG = '" + MaHang + "'";
            string KQSQL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            int INTSQL = int.Parse(KQSQL);
            if (INTSQL == 0)
            {
                KQ = false;
            }
            else
            {
                KQ = true;
            }
            return KQ;
        }
        private Boolean KIEMTRATHUTUBANFCC()
        {
            Boolean KQ = false;
            int i = gridVDOCQRCODE.RowCount;
            if (i == 0)
            {
                KQ = true;
            }
            else
            {
                string LOTHVN = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "LOTHVN").ToString();

                if (LOTHVN == "")
                {
                    KQ = false;
                }
                else
                {
                    KQ = true;
                }
            }
            return KQ;
        }
        private Boolean KIEMTRATHUTUBANHVN()
        {
            Boolean KQ = false;
            int i = gridVDOCQRCODE.RowCount;
            if (i == 0)
            {
                KQ = false;
            }
            else
            {
                string LOTFCC = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "LOTFCC").ToString();

                if (LOTFCC == "")
                {
                    KQ = false;
                }
                else
                {
                    KQ = true;
                }
            }
            return KQ;
        }
        private Boolean KIEMTRATT2TEMMA(string MAHVN)
        {
            Boolean KQ = false;
            string MAHANGFCC;
            int SLTEMFCC;

            int i = gridVDOCQRCODE.RowCount;
            if (i == 0)
            {
                KQ = true;
            }
            else
            {
                MAHANGFCC = "Select MAHANGFCC from DOCQRCODE where LOTHVN is null ";
                MAHANGFCC = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, MAHANGFCC).Trim();

                if (MAHANGFCC == "22660-KWB-6014-M1-FU")
                {
                    MAHANGFCC = "22660-KWB-6014-M1";
                }

                //SLTEMFCC = int.Parse(gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "SLTEMFCC").ToString());


                if (MAHANGFCC.Replace("-", "") != MAHVN)
                {
                    KQ = false;
                }
                else
                {
                    KQ = true;
                }
            }
            return KQ;
        }
        private Boolean KIEMTRATT2TEMSL(int SLTEMHVN)
        {
            Boolean KQ = false;

            int SLTEMFCC;

            int i = gridVDOCQRCODE.RowCount;
            if (i == 0)
            {
                KQ = true;
            }
            else
            {

                string SLTEM = "select SLTEMFCC from DOCQRCODE where LOTHVN is null";
                SLTEMFCC = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, SLTEM));
                // int.Parse(gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "SLTEMFCC").ToString());


                //SLTEMFCC = int.Parse(gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "SLTEMFCC").ToString());


                if (SLTEMFCC != SLTEMHVN)
                {
                    KQ = false;
                }
                else
                {
                    KQ = true;
                }
            }
            return KQ;
        }
        private Boolean KIEMTRATTRUNGTEM(string LOTHVN)
        {
            Boolean KQ = false;
            string S_LOTHVN;
            int rowHandle = gridVDOCQRCODE.LocateByValue("LOTHVN", LOTHVN);
            int i = gridVDOCQRCODE.RowCount;

            if (i == 0)
            {
                KQ = true;
            }
            else
            {


                for (int j = 0; j < i; j++)
                {
                    S_LOTHVN = gridVDOCQRCODE.GetRowCellValue(j, "LOTHVN").ToString().Trim();
                    if (LOTHVN != S_LOTHVN)
                    {
                        KQ = true;

                    }
                    else
                    {
                        KQ = false;
                        break;
                    }
                }

            }
            return KQ;
        }
        #endregion
        #region Function Click
        private void ADDGRIDV(string CTY, int STT, String LOT, String MAHANG, int SLTEM, string STTP, string KQ)
        {
            int i = gridVDOCQRCODE.RowCount;
            if (CTY == "FCC")
            {
                gridVDOCQRCODE.AddNewRow();
                // int i = gridVDOCQRCODE.RowCount;
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["STT"], STT);
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["LOTFCC"], LOT);
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["MAHANGFCC"], MAHANG);
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["SLTEMFCC"], SLTEM);
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["GIO"], STTP);
                gridVDOCQRCODE.UpdateCurrentRow();
            }
            else
            {
                if (gridVDOCQRCODE.FocusedRowHandle < 0)
                {
                    gridVDOCQRCODE.SetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, gridVDOCQRCODE.Columns["LOTHVN"], LOT);
                    gridVDOCQRCODE.SetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, gridVDOCQRCODE.Columns["MAHANGHVN"], MAHANG);
                    gridVDOCQRCODE.SetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, gridVDOCQRCODE.Columns["SLTEMHVN"], SLTEM);
                    gridVDOCQRCODE.SetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, gridVDOCQRCODE.Columns["KETQUA"], KQ);
                }
                else
                {
                    gridVDOCQRCODE.SetRowCellValue(i - 1, gridVDOCQRCODE.Columns["LOTHVN"], LOT);
                    gridVDOCQRCODE.SetRowCellValue(i - 1, gridVDOCQRCODE.Columns["MAHANGHVN"], MAHANG);
                    gridVDOCQRCODE.SetRowCellValue(i - 1, gridVDOCQRCODE.Columns["SLTEMHVN"], SLTEM);
                    gridVDOCQRCODE.SetRowCellValue(i - 1, gridVDOCQRCODE.Columns["KETQUA"], KQ);
                }
            }
            gridVDOCQRCODE.RefreshData();
        }
        private void luuDQCQRCODE(string CTY, int STT, String LOT, string MAFCC, String MAHANG, int SLTEM, string STTP, string KQ)
        {
            string sql;
            if (CTY == "FCC")
            {
                sql = "insert into DOCQRCODE (STT,LOTFCC,MAHANGFCC,MAFCC,SLTEMFCC,GIO) " +
                "VALUES " +
                "('" + STT + "' , '" + LOT + "','" + MAHANG + "','" + MAHANG + "'," + SLTEM + ",'" + STTP + "')";
            }
            else
            {

                sql = "update DOCQRCODE set LOTHVN = '" + LOT + "',MAHANGHVN = '" + MAHANG + "',SLTEMHVN = " + SLTEM + ", STATUS = 1 , KETQUA = '" + KQ + "'" +
                  "WHERE " +
                 " STT = " + STT + " ";
            }
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
        }
        private void KTSLBANCUAMATRUNG(string MA, int STTPHIEU, int TTSL)
        {

            string sql, s_TTSL;

            sql = "select sum(sltemfcc) from DOCQRCODE where mahangfcc= '" + MA + "' and GIO = '" + STTPHIEU + "'";
            s_TTSL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (s_TTSL == "")
            {
                sql = "update TMPPHIEUGIAOHANG set STATUS = ''  where STT = '" + STTPHIEU + "'";
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            }
            else
            {
                if (int.Parse(s_TTSL) == TTSL)
                {

                    sql = "update TMPPHIEUGIAOHANG set STATUS = '1'  where STT = '" + STTPHIEU + "'";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    FRM_LISTRUNGMSL.MAHANG = null;
                    FRM_LISTRUNGMSL.STTPHIEU = null;
                    FRM_LISTRUNGMSL.SL = null;
                }
                else
                {
                    sql = "update TMPPHIEUGIAOHANG set STATUS = '0'  where STT = '" + STTPHIEU + "'";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                }
            }

        }

        private void txt_DOCQRCODE_KeyPress(object sender, KeyPressEventArgs e)
        {
            string Cty = "";
            string LOTSLFCC, MHSL;
            string LOTFCC;
            string LOTHVN;
            string MAHANGFCC = "";
            string MAHANGHVN;
            int SLTEMFCC;
            int SLTEMHVN;
            int STTBAN = 0;
            string sqlTIMSTTBAN, TIMSTTBAN;


            if (e.KeyChar == 13)
            {
                sqlTIMSTTBAN = "select max(STT) from DOCQRCODE";
                TIMSTTBAN = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlTIMSTTBAN);
                if (TIMSTTBAN != "")
                {
                    STTBAN = int.Parse(TIMSTTBAN);
                }
                //LISTV_BANQRCODE.Items[1].SubItems["LOTHVN"].Text == "")


                string QRFCC = txt_DOCQRCODE.Text.Trim();
                string[] arrQRFCC = QRFCC.Split(':');

                if (arrQRFCC.Length == 4)
                {
                    if (KIEMTRATHUTUBANFCC() == true)
                    {

                        Cty = "FCC";
                        LOTSLFCC = arrQRFCC[0];
                        MAHANGFCC = arrQRFCC[1];
                        string LOTT = "", IDMH = "select ID from B20Item where code = '" + MAHANGFCC + "'";
                        IDMH = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, IDMH);
                        string[] arrQRFCC_GHEP = LOTSLFCC.Split(',');
                        string[] LOTSLGHEP;
                        SLTEMFCC = int.Parse(arrQRFCC[3]);

                        #region Bỏ
                        if (arrQRFCC_GHEP.Length > 1)
                        {
                            for (int t = 0; t < arrQRFCC_GHEP.Length; t++)
                            {
                                LOTSLGHEP = arrQRFCC_GHEP[t].Split('-');
                                if (LOTSLGHEP[0].Length > 10)
                                {
                                    if (LOTT == "")
                                    {
                                        LOTT = LOTSLGHEP[0].Substring(0, 6) + IDMH + LOTSLGHEP[0].Substring(11, 1) + "-" + LOTSLGHEP[1];
                                    }
                                    else
                                    {
                                        LOTT = LOTT + "," + LOTSLGHEP[0].Substring(0, 6) + IDMH + LOTSLGHEP[0].Substring(10, 1) + "-" + LOTSLGHEP[1];
                                    }
                                }
                                else
                                {
                                    if (LOTT == "")
                                    {
                                        LOTT = arrQRFCC_GHEP[t];
                                    }
                                    else
                                    {
                                        LOTT = LOTT + "," + arrQRFCC_GHEP[t];
                                    }
                                }
                            }
                            LOTFCC = LOTT;
                        }
                        else
                        {
                            // LOTSLGHEP = arrQRFCC_GHEP[0];
                            if (LOTSLFCC.Length > 10)
                            {
                                LOTT = LOTSLFCC.Substring(0, 6) + IDMH + LOTSLFCC.Substring(11, 1);
                            }
                            else
                            {
                                LOTT = LOTSLFCC;
                            }
                            LOTFCC = LOTT;
                        }

                        #endregion 
                        if (KTRAMA(MAHANGFCC) == true)
                        {
                            if (KTHANGDABAN_DANGBANQR(MAHANGFCC, SLTEMFCC, FRM_LISTRUNGMSL.STTPHIEU) == true)
                            {

                                if (FRM_LISTRUNGMSL.MAHANG == null)
                                {
                                    FRM_LISTRUNGMSL.MAHANG = "";
                                    _MAFCC = MAHANGFCC;
                                    ADDGRIDV(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, SLTEMFCC, "", "");
                                    luuDQCQRCODE(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, MAHANGFCC, SLTEMFCC, "", "");
                                    LoadDOCQR();
                                    txt_DOCQRCODE.Text = "";
                                }
                                else
                                {
                                    if (MAHANGFCC != FRM_LISTRUNGMSL.MAHANG.Trim())
                                    {
                                        _MAFCC = MAHANGFCC;
                                        ADDGRIDV(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, SLTEMFCC, "", "");
                                        luuDQCQRCODE(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, MAHANGFCC, SLTEMFCC, "", "");
                                        txt_DOCQRCODE.Text = "";
                                    }
                                    else
                                    {
                                        _MAFCC = MAHANGFCC;
                                        ADDGRIDV(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, SLTEMFCC, FRM_LISTRUNGMSL.STTPHIEU, "");
                                        luuDQCQRCODE(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, MAHANGFCC, SLTEMFCC, FRM_LISTRUNGMSL.STTPHIEU, "");
                                        LoadDOCQR();
                                        txt_DOCQRCODE.Text = "";
                                        KTSLBANCUAMATRUNG(MAHANGFCC, int.Parse(FRM_LISTRUNGMSL.STTPHIEU), int.Parse(FRM_LISTRUNGMSL.SL));


                                    }
                                }
                            }
                            else
                            {
                                MessageBox.Show("Số lượng bắn đang vượt quá số lượng giao ! Hãy kiểm tra lại ", "Thông Báo FCC",
                                              MessageBoxButtons.OK,
                                              MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            MessageBox.Show("không tồn tại mã trong phiếu giao ! ", "Thông Báo FCC",
                                              MessageBoxButtons.OK,
                                              MessageBoxIcon.Error);
                        }
                        //}
                        //else
                        //{
                        //    FRM_LISTRUNGMSL fRM_LISTRUNGMSL = new FRM_LISTRUNGMSL();
                        //    fRM_LISTRUNGMSL.ShowDialog();
                        //}
                    }
                    else
                    {
                        MessageBox.Show("Sai Thứ tự bắn! ", "Thông Báo FCC",
                                             MessageBoxButtons.OK,
                                             MessageBoxIcon.Error);
                    }

                }
                else
                {
                    if (KIEMTRATHUTUBANHVN() == true)
                    {

                        Cty = "HVN";
                        LOTHVN = arrQRFCC[0];

                        if (KIEMTRATTRUNGTEM(LOTHVN) == true)
                        {
                            MAHANGHVN = arrQRFCC[1].Replace(" ", "");

                            if (KIEMTRATT2TEMMA(MAHANGHVN) == true)
                            {

                                SLTEMHVN = int.Parse(arrQRFCC[3]);
                                if (KIEMTRATT2TEMSL(SLTEMHVN) == true)
                                {
                                    ADDGRIDV(Cty, STTBAN, LOTHVN, MAHANGHVN, SLTEMHVN, "", "OK");
                                    luuDQCQRCODE(Cty, STTBAN, LOTHVN, _MAFCC, MAHANGHVN, SLTEMHVN, "", "OK");
                                    LoadDOCQR();
                                    txt_DOCQRCODE.Text = "";
                                }
                                else
                                {
                                    DialogResult re = MessageBox.Show("Số lượng TEM không khớp bạn có muốn nhập ? ! ", "Thông Báo FCC",
                                          MessageBoxButtons.YesNo,
                                          MessageBoxIcon.Warning);
                                    if (re == DialogResult.Yes)
                                    {
                                        ADDGRIDV(Cty, STTBAN + 1, LOTHVN, MAHANGHVN, SLTEMHVN, "", "KHAC SLTEM");
                                        luuDQCQRCODE(Cty, STTBAN + 1, LOTHVN, _MAFCC, MAHANGHVN, SLTEMHVN, "", "KHAC SLTEM");
                                        LoadDOCQR();
                                        txt_DOCQRCODE.Text = "";
                                    }

                                }

                            }
                            else
                            {
                                MessageBox.Show("Mã Hàng HVN không khớp với FCC ! ", "Thông Báo FCC",
                                                 MessageBoxButtons.OK,
                                                 MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Trùng Tem! ", "Thông Báo FCC",
                                               MessageBoxButtons.OK,
                                               MessageBoxIcon.Error);
                        }
                    }
                    else

                    {
                        MessageBox.Show("Sai Thứ tự bắn! ", "Thông Báo FCC",
                                             MessageBoxButtons.OK,

                                             MessageBoxIcon.Error);
                    }
                }

            }
        }
        private void LoadDOCQR()
        {
            string sql;
            DataTable Tbl_QR;
            sql = "select * from DocQRCode";
            Tbl_QR = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            gridCtrDOCQrCODE.DataSource = Tbl_QR;
            int newRowHandle = gridVDOCQRCODE.FocusedRowHandle;
            object newRow = gridVDOCQRCODE.GetRow(newRowHandle);
            for (int n = 0; n < gridVDOCQRCODE.DataRowCount; n++)
            {
                if (gridVDOCQRCODE.GetRow(n).Equals(newRow))
                {
                    gridVDOCQRCODE.FocusedRowHandle = n;
                    break;
                }
            }
        }
        private void CMD_HOANTHANH_Click(object sender, EventArgs e)
        {
            pnbanqrcode1.Visible = false;
            pnbanqrcode2.Visible = false;
            pnbanqrcode3.Visible = false;
            sidePTHEMPDB.Visible = false;
            sidePChonKGX.Visible = true;
            this.pnbanqrcode3.Size = new Size(1657, 250);
            this.PanelDLGHEP.Size = new Size(1657, 250);
            string sql = "select count(*) from DOCQRCODE where KETQUA <> 'DG'";
            string SLKQ = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (int.Parse(SLKQ) > 0)
            {
                TTFOR_WAIT = 2;
                splashScreenManager2.ShowWaitForm();
                TinhTong();
                LoadDLTMP();
                splashScreenManager2.CloseWaitForm();
            }
            setstatusbootomDOCQ9RCODE();
            sideIN.Visible = true;

            
            sidePGL.Visible = true;
        }

        private void gridVDOCQRCODE_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {

        }


        private void simpleButton1_Click(object sender, EventArgs e)
        {
            string STT = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "STT").ToString().Trim();
            string STTPHIEU = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "GIO").ToString().Trim();
            string M = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "MAHANGFCC").ToString().Trim();
            int TTSL;
            gridVDOCQRCODE.DeleteSelectedRows();
            string sql = "delete DOCQRCODE where STT =  " + int.Parse(STT) + "";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            if (STTPHIEU != "")
            {
                sql = "select SOLUONG from TMPPHIEUGIAOHANG where stt = " + int.Parse(STTPHIEU) + "";
                TTSL = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                if (STTPHIEU != "")
                {

                    KTSLBANCUAMATRUNG(M, int.Parse(STTPHIEU), TTSL);
                }
            }
            LOADDOCQRCODE();
            setstatusbootomDOCQ9RCODE();
        }
        #endregion
        private void cmd_loaddstrungsl_Click(object sender, EventArgs e)
        {
            LOADDOCQRCODE();
        }

        private void TinhTong()
        {
            string MH, LOT, S_DEM, sqlupdate, STTT, TEN, SLXUAT, STATUS, MA, GIOXUAT;

            //params  
            int SLGIAO, STTPHIEU, DEM, STT;
            DataTable BangTam = new DataTable();

            DataTable DSTRUNG = new DataTable();
            string sqldem, sqldocqrcode, sql = " select STT, MAHANG,lot,SOLUONG from TMPPHIEUGIAOHANG where LOT =  '' order by STT";
            BangTam = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            for (int i = 0; i < BangTam.Rows.Count; i++)
            {
                DataTable BangTamLOTNO = new DataTable();
                BangTamLOTNO.Clear();
                MH = BangTam.Rows[i]["MAHANG"].ToString().Trim();
                SLGIAO = int.Parse(BangTam.Rows[i]["SOLUONG"].ToString().Trim());
                sqldem = "select * from TMPPHIEUGIAOHANG where MAHANG = '" + MH + "' and SOLUONG = " + SLGIAO + " and LOT=''";
                DSTRUNG = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sqldem);
                DEM = DSTRUNG.Rows.Count;
                LOT = "";
                STT = int.Parse(BangTam.Rows[i]["STT"].ToString().Trim());
                KQTRUNGMASL.Items.Clear();
                if (DEM > 1)
                {

                    for (int t = 0; t < DSTRUNG.Rows.Count; t++)
                    {
                        STTT = DSTRUNG.Rows[t]["STT"].ToString();
                        MA = DSTRUNG.Rows[t]["MAHANG"].ToString();
                        TEN = DSTRUNG.Rows[t]["TENHANG"].ToString();
                        GIOXUAT = DSTRUNG.Rows[t]["GIOGIAO"].ToString();
                        SLXUAT = DSTRUNG.Rows[t]["SOLUONG"].ToString();
                        STATUS = DSTRUNG.Rows[t]["STATUS"].ToString();
                        if (STATUS == "")
                        {
                            STATUS = "Chưa Bắn QRCODE";
                        }
                        if (STATUS == "0")
                        {
                            STATUS = "Đang Bắn QRCODE";
                        }
                        if (STATUS == "1")
                        {
                            STATUS = "Đã Bắn QRCODE";
                        }
                        KQTRUNGMASL.Items.Add(new ListViewItem(new[] { STTT, GIOXUAT, MH, TEN, SLXUAT, STATUS }));
                    }
                    FRM_LISTRUNGMSL _LISTRUNGMSL = new FRM_LISTRUNGMSL(KQTRUNGMASL);
                    _LISTRUNGMSL.ShowDialog();
                    if (FRM_LISTRUNGMSL.STTPHIEU != null)
                    {
                        STT = int.Parse(FRM_LISTRUNGMSL.STTPHIEU);
                        LOT = GET_LOTNO(BangTamLOTNO, MH, STT, DEM, SLGIAO);
                    }
                }
                else
                {
                    LOT = GET_LOTNO(BangTamLOTNO, MH, STT, DEM, SLGIAO);
                }
                if (LOT != "")
                {
                    sqlupdate = "update tmpphieugiaohang set LOT= '" + LOT + "' where STT = " + STT + "";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);

                }
            }
        }
        private string GET_LOTNO(DataTable BangTamLOTNO, string MH, int STT, int DEM, int SLGIAO)
        {
            string sqlupdate, LOT = "";
            int STTDOC, TTSL = 0;
            DataTable BTUPDATE = new DataTable();
            BangTamLOTNO = sqlBRV.LoadData(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_Take_Lot",
                       new System.Data.SqlClient.SqlParameter("@_MaFCC", MH),
                       new System.Data.SqlClient.SqlParameter("@_STTP", STT),
                       new System.Data.SqlClient.SqlParameter("@_DeM", DEM),
                       new System.Data.SqlClient.SqlParameter("@_SLGIAO", SLGIAO));
            for (int j = 0; j < BangTamLOTNO.Rows.Count; j++)
            {
                if (LOT == "")
                {
                    LOT = BangTamLOTNO.Rows[j]["LOTFCC"].ToString().Trim() + "-" + BangTamLOTNO.Rows[j]["FCC"].ToString().Trim();
                }
                else
                {
                    LOT = LOT + "," + BangTamLOTNO.Rows[j]["LOTFCC"].ToString().Trim() + "-" + BangTamLOTNO.Rows[j]["FCC"].ToString().Trim();
                }
            }

            return LOT;
        }
        #region Boỏ không tính theo C# sử dụng tổng hợp trên sql
        private ListView LOTSL = new ListView();

        private void TINHTONG_KOGHEPLOT(string MA, int GIO, int SLGIAO, int DEM)
        {
            string sql, sql1, LOTFCC, LOTTONGHOP = "", LOT = "";
            int STTPHIEU, TTSLBAN, SLTEMFCC, SL = 0;

            DataTable BangTam = new DataTable();
            if (GIO == 0)
            {
                if (MA == "22660-KWB-6014-M1")
                {
                    sql1 = " select sum(SLTEMHVN) from DOCQRCODE where MAHANGFCC like '" + MA + "%' and KETQUA <> 'DG'";
                }
                else
                {
                    sql1 = " select sum(SLTEMHVN) from DOCQRCODE where MAHANGFCC = '" + MA + "' and KETQUA <> 'DG'";
                }
            }
            else
            {
                if (MA == "22660-KWB-6014-M1")
                {
                    sql1 = " select sum(SLTEMHVN) from DOCQRCODE where MAHANGFCC like '" + MA + "%' and KETQUA <> 'DG' and GIO = '" + GIO + "'";
                }
                else
                {
                    sql1 = " select sum(SLTEMHVN) from DOCQRCODE where MAHANGFCC = '" + MA + "' and KETQUA <> 'DG' and GIO = '" + GIO + "'";
                }
            }
            if (sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1) != "")
            {
                TTSLBAN = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1));
                //sql = "select STT,MAHANG,SOLUONG from TMPPHIEUGIAOHANG where MAHANG = '" + MA + "' and LOT <> ''";

                if (DEM >= 2)
                {
                    if (GIO == 0)
                    {
                        //SLBANTHEOLOTHVN(string MA, int STTPHIEU, int SLGIAO)
                    }
                    else
                    {

                        string sqlupdate = "update tmpphieugiaohang set LOT= '" + TT_SUB1(MA, SLGIAO, TTSLBAN) + "'  where STT = " + GIO + "";
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                        if (MA == "22660-KWB-6014-M1")
                        {
                            sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC like '" + MA + "%' and GIO = '" + GIO.ToString() + "'";
                        }
                        else
                        {
                            sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC = '" + MA + "' and GIO = '" + GIO.ToString() + "'";
                        }

                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                    }
                }
                else
                {
                    string sqlupdate = "update tmpphieugiaohang set LOT= '" + TT_SUB1(MA, SLGIAO, TTSLBAN) + "' where MAHANG = '" + MA + "' and SOLUONG = " + SLGIAO + "";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                    if (MA == "22660-KWB-6014-M1")
                    {
                        sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC like '" + MA + "'%";
                    }
                    else
                    {
                        sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC = '" + MA + "'";
                    }

                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                }
            }

        }
        private string TT_SUB1(string MH, int SLG, int SLB)
        {
            string sql, LOTFCC, SLTEM, LOTTTC = "";
            sql = "select ID from B20ITEM where code = '" + MH + "'";
            int IDMH = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            DataTable BangTam = new DataTable();
            if (SLG == SLB)
            {
                if (MH == "22660-KWB-6014-M1")
                {
                    sql = "select lotfcc,sltemfcc from DOCQRCODE where mahangfcc like '" + MH + "%' and KETQUA <> 'DG'";
                }
                else
                {
                    sql = "select lotfcc,sltemfcc from DOCQRCODE where mahangfcc= '" + MH + "' and KETQUA <> 'DG'";
                }
                BangTam = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
                LOTSL.Items.Clear();
                for (int j = 0; j < BangTam.Rows.Count; j++)
                {

                    LOTFCC = BangTam.Rows[j]["LOTFCC"].ToString().Trim();
                    SLTEM = BangTam.Rows[j]["sltemfcc"].ToString().Trim();
                    string[] arrLOTFCC = LOTFCC.Split(',');
                    if (arrLOTFCC.Length == 1)
                    {
                        TT_SUB(IDMH, LOTFCC + "-" + SLTEM);
                    }
                    else
                    {
                        for (int m = 0; m < arrLOTFCC.Length; m++)
                        {
                            TT_SUB(IDMH, arrLOTFCC[m]);
                        }
                    }
                }
                for (int j = 0; j < LOTSL.Items.Count; j++)
                {
                    if (j == 0)
                    {
                        LOTTTC = LOTSL.Items[j].SubItems[0].Text + "-" + LOTSL.Items[j].SubItems[1].Text;
                    }
                    else
                    {
                        LOTTTC = LOTTTC + "," + LOTSL.Items[j].SubItems[0].Text + "-" + LOTSL.Items[j].SubItems[1].Text;
                    }
                }
            }
            else
            {
                LOTTTC = "";
            }
            return LOTTTC;
        }
        private void TT_SUB(int MH, string LOTFCC)
        {
            string LOTTMP;
            int SLTMP;
            Boolean KQ = false;
            string[] arrLOTFCC = LOTFCC.Split('-');
            LOTTMP = arrLOTFCC[0].ToString().Trim();

            if (LOTTMP.Length > 11)
            {
                string CA = LOTTMP.Substring(12, 1);

                LOTTMP = LOTTMP.Substring(0, 6) + MH.ToString() + CA;

            }

            SLTMP = int.Parse(arrLOTFCC[1].ToString().Trim());
            if (LOTSL.Items.Count > 0)
            {
                for (int k = 0; k < LOTSL.Items.Count; k++)
                {
                    if (LOTSL.Items[k].SubItems[0].Text == LOTTMP)
                    {
                        LOTSL.Items[k].SubItems[1].Text = (int.Parse(LOTSL.Items[k].SubItems[1].Text) + SLTMP).ToString();
                        KQ = true;
                        break;
                    }
                    else
                    {

                        KQ = false;
                    }


                }
                if (KQ == false)
                {
                    LOTSL.Items.Add(new ListViewItem(new[] { LOTTMP, SLTMP.ToString() }));
                    return;
                }
            }
            else
            {
                LOTSL.Items.Add(new ListViewItem(new[] { LOTTMP, SLTMP.ToString() }));
                return;
            }
        }
        private ListView LOTSL1 = new ListView();

        private void SLBANTHEOLOTHVN(string MA, int STTPHIEU, int SLGIAO)
        {
            string LOTHVN, STTBAN, LOTFCC, SLTEMHVN, SLTEMFCC, sqlupdate;
            string sql = "select ID from B20ITEM where code = '" + MA + "'";
            int IDMH = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            string LOTTTC = "";
            string SQLDOCQR = " select STT,LOTHVN,SUALOTHVN,MAHANGHVN,mafcc,SLTEMHVN,LOTFCC,SLTEMFCC,GIO from DOCQRCODE where MAHANGFCC = '" + MA + "' and KETQUA <> 'DG' order by MAHANGHVN ,LOTHVN,GIO ";
            DataTable BANGTAM = new DataTable();
            BANGTAM = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, SQLDOCQR);
            int TTSLB = 0;
            LOTSL1.Items.Clear();
            LOTSL.Items.Clear();
            for (int i = 0; i < BANGTAM.Rows.Count; i++)
            {
                if (i == 20)
                {
                    int okk = 1;
                }
                STTBAN = BANGTAM.Rows[i]["STT"].ToString().Trim();
                LOTFCC = BANGTAM.Rows[i]["LOTFCC"].ToString().Trim();
                //LOTHVN = BANGTAM.Rows[i]["LOTHVN"].ToString();
                SLTEMHVN = BANGTAM.Rows[i]["SLTEMHVN"].ToString().Trim();
                SLTEMFCC = BANGTAM.Rows[i]["SLTEMFCC"].ToString().Trim();

                if (BANGTAM.Rows[i]["SUALOTHVN"].ToString().Trim() != "")
                {
                    LOTHVN = BANGTAM.Rows[i]["SUALOTHVN"].ToString().Trim();
                }
                else
                {
                    LOTHVN = BANGTAM.Rows[i]["LOTHVN"].ToString().Trim();
                }
                string LHVN = LOTHVN.ToString();
                if (i == 0)
                {

                    TTSLB = TTSLB + int.Parse(SLTEMHVN);
                    LOTSL1.Items.Add(new ListViewItem(new[] { STTBAN, LOTHVN, LOTFCC, SLTEMHVN, SLTEMFCC }));
                    sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC = '" + MA + "' and STT = '" + STTBAN.ToString() + "'";

                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);

                }
                else
                {
                    string SQL1, sqlcheck = "select count(*) from docqrcode where  KETQUA = 'DG' and SUALOTHVN <> ''";
                    string SL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlcheck);
                    if (BANGTAM.Rows[i]["SUALOTHVN"].ToString().Trim() != "" && SL != "0")
                    {
                        SQL1 = "select STT,LOTHVN,SUALOTHVN,MAHANGHVN,mafcc,SLTEMHVN,LOTFCC,SLTEMFCC,GIO from DOCQRCODE where MAHANGFCC = '" + MA + "' and SUALOTHVN = " + (Double.Parse(LOTHVN) - 1) + " and KETQUA = 'DG'";
                    }
                    else
                    {
                        SQL1 = "select STT,LOTHVN,SUALOTHVN,MAHANGHVN,mafcc,SLTEMHVN,LOTFCC,SLTEMFCC,GIO from DOCQRCODE where MAHANGFCC = '" + MA + "' and LOTHVN = " + (Double.Parse(LOTHVN) - 1) + " and KETQUA = 'DG'";
                    }

                    DataTable BANGTAM1 = new DataTable();
                    BANGTAM1 = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, SQL1);
                    if (BANGTAM1.Rows.Count > 0)
                    {
                        TTSLB = TTSLB + int.Parse(SLTEMHVN);
                        //STTBAN = BANGTAM1.Rows[0]["STT"].ToString().Trim();
                        //LOTFCC = BANGTAM1.Rows[0]["LOTFCC"].ToString().Trim();
                        //SLTEMHVN = BANGTAM1.Rows[0]["SLTEMHVN"].ToString().Trim();
                        //SLTEMFCC = BANGTAM.Rows[0]["SLTEMFCC"].ToString().Trim();
                        LOTSL1.Items.Add(new ListViewItem(new[] { STTBAN, LOTHVN, LOTFCC, SLTEMHVN, SLTEMFCC }));
                        sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC = '" + MA + "' and STT = '" + STTBAN.ToString() + "'";

                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                    }
                    else

                    {
                        break;

                    }
                }
            }
            if (LOTSL1.Items.Count != 1 && TTSLB == SLGIAO)
            {

                for (int j = 0; j < LOTSL1.Items.Count; j++)
                {
                    STTBAN = LOTSL1.Items[j].SubItems[0].Text.Trim();

                    LOTFCC = LOTSL1.Items[j].SubItems[2].Text.Trim();

                    SLTEMFCC = LOTSL1.Items[j].SubItems[4].Text.Trim();

                    string[] arrLOTFCC = LOTFCC.Split(',');
                    if (arrLOTFCC.Length == 1)
                    {
                        TT_SUB(IDMH, LOTFCC + "-" + SLTEMFCC);
                    }
                    else
                    {
                        for (int m = 0; m < arrLOTFCC.Length; m++)
                        {

                            TT_SUB(IDMH, arrLOTFCC[m]);
                        }
                    }
                    sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC = '" + MA + "' and STT = '" + STTBAN.ToString() + "'";

                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                }
                for (int j = 0; j < LOTSL.Items.Count; j++)
                {
                    if (j == 0)
                    {
                        LOTTTC = LOTSL.Items[j].SubItems[0].Text + "-" + LOTSL.Items[j].SubItems[1].Text;
                    }
                    else
                    {
                        LOTTTC = LOTTTC + "," + LOTSL.Items[j].SubItems[0].Text + "-" + LOTSL.Items[j].SubItems[1].Text;
                    }
                }
                sqlupdate = "update tmpphieugiaohang set LOT= '" + LOTTTC + "' where STT = " + STTPHIEU + "";
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);

            }
            else
            {
                ////TTSLB = TTSLB + int.Parse(SLTEMHVN);
                if (TTSLB == SLGIAO)
                {
                    STTBAN = LOTSL1.Items[0].SubItems[0].Text.Trim();

                    LOTFCC = LOTSL1.Items[0].SubItems[2].Text.Trim();

                    SLTEMFCC = LOTSL1.Items[0].SubItems[4].Text.Trim();
                    string[] arrLOTFCC = LOTFCC.Split(',');
                    if (arrLOTFCC.Length == 1)
                    {
                        LOTTTC = LOTFCC + "-" + SLTEMFCC;
                    }
                    else
                    {
                        LOTTTC = LOTFCC.Trim();
                    }
                    sqlupdate = "update docqrcode set KETQUA = 'DG' where MAHANGFCC = '" + MA + "' and STT = '" + int.Parse(STTBAN.ToString().Trim()) + "'";

                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                    sqlupdate = "update tmpphieugiaohang set LOT= '" + LOTTTC + "'  where STT = " + STTPHIEU + "";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                    return;
                }
                else
                {
                    sqlupdate = "update docqrcode set KETQUA = 'OK' where MAHANGFCC = '" + MA + "'";

                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlupdate);
                }
            }
        }

        #endregion 
        #region in ghep lot
        DataTable tblData = new DataTable();

        private DataTable loadDATArt()
        {

            DataSet DTS = new DataSet();
            DTS = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_BarCodeView_ThongKe_Tmp3");
            return DTS.Tables[0];



        }
        private void CMD_INGHEPLOT_Click(object sender, EventArgs e)
        {
            string LOT, MA, GIO, sqlinsert;
            sqlinsert = "delete from TMPLOTGHEP";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlinsert);
            foreach (int i in GridVTTGL.GetSelectedRows())
            {
                DataRow row = GridVTTGL.GetDataRow(i);
                MA = row[0].ToString();
                LOT = row[2].ToString();
                GIO = row[1].ToString();
                sqlinsert = "insert into TMPLOTGHEP (LOT,MAHANG,GIOXUAT,flag) values ( '" + LOT + "','" + MA + "'," + GIO + ",0 )";
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlinsert);
            }
            
            Report.GHEPLOT report = new Report.GHEPLOT();
            report.DataSource = loadDATArt();

            ReportPrintTool printTool = new ReportPrintTool(report);
            printTool.ShowPreviewDialog();
        }

        #endregion



        private void CMD_INPHIEUGIAO_Click_1(object sender, EventArgs e)
        {
            DataTable PGH = new DataTable();
            DataRow row;
            N_XH = dateNX.DateTime.ToString("ddMMyyyy");

            string CUA, TRUYEN, DV, NM, LOT, NGAYGIAO, NX, GXX;
            NGAYGIAO = dateNX.DateTime.ToString("MM/dd/yyyy");
            NX = dateNX.DateTime.ToString("yyyy-MM-dd");
            int ADD_NM, SL;
            string GX, MH, sql1, QCDg;
            //dateNX.Properties.DisplayFormat.FormatString = "ddmmyyyy";


            string sql = "select '' as STT,CONCAT(CUSTOMER_PART_NO , cast(BUY_QTY_DUE as VARCHAR(10))) as FIND, to_char(WANTED_DELIVERY_DATE,'HH24') as GIOGIAO,CUSTOMER_NO,WANTED_DELIVERY_DATE as NGAYGIAO,BUY_QTY_DUE as SOLUONG,customer_part_unit_meas as DV, case " +
                    " when SHIP_ADDR_NO = 1 then 'HON DA -VIET NAM- (NHA MAY VINH PHUC)' " +
                    " else 'HON DA -VIET NAM- (NHA MAY HA NAM)'" +
                    " end as NHAMAY,SUB_DOCK_CODE as CUA,CUSTOMER_PART_NO as MAHANG,CATALOG_DESC as TENHANG,CUSTOMER_PO_REL_NO,DOCK_CODE as TRUYEN,ORDER_NO, " +
                   "CATALOG_NO,PLANNED_SHIP_DATE as NGAYGIAO,CUSTOMER_PO_NO,'' as HOP, '' as LOT ,'' as KGX ,SHIP_ADDR_NO" +
                    " from CUSTOMER_ORDER_JOIN " +
                   " where " +
                   " CUSTOMER_NO = '100001' and " +
                   " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual) )and " +
                   " to_char( WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "' and " +
                   " CUSTOMER_PO_REL_NO is not null ";





            if (checkBNM.Checked == true)
            {
                if (checkBKGX.Checked == true)
                {
                    sql = sql + " and  SHIP_ADDR_NO  = " + addHVN + "" +
                        " and  to_char(WANTED_DELIVERY_DATE, 'HH24') in (" + GIOXUAT + ") " +
                       " Order by SHIP_ADDR_NO ASC,GIOGIAO ASC ";
                }
                else
                {
                    sql = sql + " and  SHIP_ADDR_NO  = " + addHVN + "" +


                       " Order by SHIP_ADDR_NO ASC,GIOGIAO ASC ";
                }
            }
            else
            {
                if (checkBKGX.Checked == true)
                {
                    sql = sql +
                   " and  to_char(WANTED_DELIVERY_DATE, 'HH24') in (" + GIOXUAT + ")" +

                   " Order by SHIP_ADDR_NO ASC,GIOGIAO ASC ";
                }
                else
                {
                    sql = sql +
                    " Order by SHIP_ADDR_NO ASC,GIOGIAO ASC ";
                }
            }
            PGH = iFSPROVIDER.ExecuteQuery(sql);
            for (int i = 0; i < PGH.Rows.Count; i++)
            {

                CUA = PGH.Rows[i]["CUA"].ToString();
                TRUYEN = PGH.Rows[i]["TRUYEN"].ToString();
                ADD_NM = int.Parse(PGH.Rows[i]["SHIP_ADDR_NO"].ToString());
                if (ADD_NM == 2)
                {
                    NM = "HA NAM";
                }
                else
                {
                    NM = "VP";
                }
                GX = PGH.Rows[i]["GIOGIAO"].ToString();
                GXX = GX + "h";
                MH = PGH.Rows[i]["MAHANG"].ToString();
                SL = int.Parse(PGH.Rows[i]["SOLUONG"].ToString());
                sql1 = "SELECT cast(MinCloseQty as int)  from B20Item where Code= '" + MH + "'";
                QCDg = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1);
                PGH.Rows[i]["HOP"] = QCDg;
                if (ADD_NM == 1 && (GX == "00"))
                {
                    PGH.Rows[i]["KGX"] = "Oder Type 6";
                }
                if (ADD_NM == 1 && (GX == "01"))
                {
                    PGH.Rows[i]["KGX"] = "(Type #)";
                }
                if (ADD_NM == 1 && (GX == "06" || GX == "07" || GX == "08"))
                {
                    PGH.Rows[i]["KGX"] = "(06+07+08)h";
                }
                if (ADD_NM == 1 && (GX == "09" || GX == "10" || GX == "11"))
                {
                    PGH.Rows[i]["KGX"] = "(09+10+11)h";
                }
                if (ADD_NM == 1 && (GX == "12" || GX == "13"))
                {
                    PGH.Rows[i]["KGX"] = "(12+13)h";
                }
                if (ADD_NM == 1 && (GX == "14" || GX == "15"))
                {
                    PGH.Rows[i]["KGX"] = "(14+15)h";
                }
                if (ADD_NM == 1 && (GX == "16" || GX == "17" || GX == "18"))
                {
                    PGH.Rows[i]["KGX"] = "(16+17+18)h";
                }
                if (ADD_NM == 1 && (GX == "19" || GX == "20"))
                {
                    PGH.Rows[i]["KGX"] = "(19+20)h";
                }
                if (ADD_NM == 1 && (GX == "21" || GX == "22"))
                {
                    PGH.Rows[i]["KGX"] = "(21+22)h";
                }
                // Khung gio Ha Nam
                if (ADD_NM == 2 && (GX == "06"))
                {
                    PGH.Rows[i]["KGX"] = "(06)h";
                }
                if (ADD_NM == 2 && (GX == "07" || GX == "08"))
                {
                    PGH.Rows[i]["KGX"] = "(07+08)h";
                }
                if (ADD_NM == 2 && (GX == "11"))
                {
                    PGH.Rows[i]["KGX"] = "(11)h";
                }
                if (ADD_NM == 2 && (GX == "12" || GX == "13"))
                {
                    PGH.Rows[i]["KGX"] = "(12+13)h";
                }
                if (ADD_NM == 2 && (GX == "14" || GX == "15"))
                {
                    PGH.Rows[i]["KGX"] = "(14+15)h";
                }
                if (ADD_NM == 2 && (GX == "16" || GX == "17"))
                {
                    PGH.Rows[i]["KGX"] = "(16+17)h";
                }
                if (ADD_NM == 2 && (GX == "18" || GX == "19"))
                {
                    PGH.Rows[i]["KGX"] = "(18+19)h";
                }
                if (ADD_NM == 2 && (GX == "21" || GX == "22"))
                {
                    PGH.Rows[i]["KGX"] = "(21+22)h";
                }
                if (MH == "22010-K96-V000")
                {
                    string atm = "x";
                }
                sql = "select lot from luuphieugiaohang where CUA = '" + CUA + "' and TRUYEN = '" + TRUYEN + "' and MAHANG = '" + MH + "' and SOLUONG = " + SL + " and NGAYGIAO = '" + NX + "' and GIOGIAO = '" + GXX + "' and NHAMAY like '%" + NM + "%'";
                LOT = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                PGH.Rows[i]["LOT"] = LOT;
            }
            //int L = PGH.Rows.Count % 15;
            //if ( L != 0)
            //{
            //    for (int m = 1; m <= L; m++)
            //    {
            //        row = PGH.NewRow();

            //        PGH.Rows.Add(row);
            //    }
            //}
            Report.temphieugh report = new Report.temphieugh();
            report.DataSource = PGH;

            ReportPrintTool printTool = new ReportPrintTool(report);
            printTool.ShowPreviewDialog();
        }
        private string STTBANTEM;

        private void gridVDOCQRCODE_DoubleClick(object sender, EventArgs e)
        {
            DataTable tbl = new DataTable();

            tbl.Columns.Add("STT", typeof(int));
            tbl.Columns.Add("LOTFCC", typeof(string));

            tbl.Columns.Add("SLTEMFCC", typeof(int));

            tbl.Columns.Add("SUATHANH", typeof(int));
            STTBANTEM = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "STT").ToString().Trim();
            LOTFCCVN.Text = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "LOTFCC").ToString().Trim();
            TXT_FCCTU.Text = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "SLTEMFCC").ToString().Trim();
            TXT_HVNTU.Text = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "SLTEMHVN").ToString().Trim();
            string _LOT, LOTFCC = LOTFCCVN.Text.Trim();
            int SL;

            string[] LOT, _LOTSL;
            LOT = LOTFCC.Split(',');
            if (LOT.Length > 1)
            {
                gridCtrSUASL.Visible = true;

                for (int i = 0; i < LOT.Length; i++)
                {
                    _LOTSL = LOT[i].Split('-');
                    _LOT = _LOTSL[0].ToString();
                    SL = int.Parse(_LOTSL[1].ToString());
                    DataRow row = tbl.NewRow();
                    row["STT"] = i;
                    row["LOTFCC"] = _LOT;
                    row["SLTEMFCC"] = SL;
                    row["SUATHANH"] = 0;

                    tbl.Rows.Add(row);

                }
                gridCtrSUASL.DataSource = tbl;
            }
            else
            {
                gridCtrSUASL.Visible = false;
                TXT_FCCTHANH.Enabled = true;

            }
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            string LOTGHEP = "", sql, _LOT, LOTFCC = LOTFCCVN.Text.Trim();
            int SL;
            string[] LOT, _LOTSL;

            LOT = LOTFCC.Split(',');


            if (TXT_FCCTU.Text != "" && TXT_FCCTHANH.Text != "")
            {

                if (LOT.Length == 1)
                {
                    _LOTSL = LOTFCC.Split('-');
                    _LOT = _LOTSL[0].ToString();

                    sql = "update docqrcode set SLTEMFCC = " + int.Parse(TXT_FCCTHANH.Text) + " , LOTFCC = '" + _LOT + "-" + TXT_FCCTHANH.Text + "' where STT = " + STTBAN + "";
                }
                else
                {
                    for (int i = 0; i < gridVSUASL.RowCount; i++)
                    {
                        if (i == 0)
                        {
                            LOTGHEP = gridVSUASL.GetRowCellValue(i, "LOTFCC").ToString() + "-" + gridVSUASL.GetRowCellValue(i, "SUATHANH").ToString();
                        }
                        else
                        {
                            LOTGHEP = LOTGHEP + "," + gridVSUASL.GetRowCellValue(i, "LOTFCC").ToString() + "-" + gridVSUASL.GetRowCellValue(i, "SUATHANH").ToString();
                        }
                    }
                    sql = "update docqrcode set SLTEMFCC = " + int.Parse(TXT_FCCTHANH.Text) + " , LOTFCC = '" + LOTGHEP + "' where STT = " + STTBANTEM + "";
                }

                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                LOADDOCQRCODE();
                gridCtrSUASL.DataSource = null;
                gridVSUASL.Columns.Clear();
                TXT_FCCTHANH.Text = "";
                TXT_FCCTU.Text = "";
                TXT_HVNTU.Text = "";
                TXT_HVNTHANH.Text = "";
                LOTFCCVN.Text = "";
            }
            else
            {
                MessageBox.Show("Chưa chọn số lượng thay đổi !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            string sql;


            if (TXT_HVNTU.Text != "" && TXT_HVNTHANH.Text != "")
            {

                int SLHVN = int.Parse(TXT_HVNTHANH.Text);

                sql = "update docqrcode set SLTEMHVN = " + SLHVN + " where STT = '" + STTBANTEM + "'";


                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                LOADDOCQRCODE();
            }
            else
            {
                MessageBox.Show("Chưa chọn số lượng thay đổi !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #region Cập Nhập Kho

        private void CAPNHAPK2()
        {
            UPDQATESTOCK = "";
            string sql, TTLOTSL, MAH;
            int STT, dem = 0;
            DataTable BANGTAM = new DataTable();
            sql = "select STT,LOT,MAHANG,STATUS from TMPPHIEUGIAOHANG where LOT <> '' and (STATUS != 'OK' or STATUS is null )  order by STT";
            BANGTAM = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            if (BANGTAM.Rows.Count == 0)
            {
                MessageBox.Show("Bạn chưa cập nhập được kho vì chưa hoàn thành phiếu !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                string NGAYGIAO = dateNX.DateTime.ToString("MM/dd/yyyy");
                string GIOGIAO = GIOXUATH;
                string NHAMAY;
                if (addHVN == 1)
                {
                    NHAMAY = "HON DA - VIET NAM(NHA MAY VP)";
                }
                else { NHAMAY = "HON DA - VIET NAM(NHA MAY HA NAM)"; }

                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);

                #region TMP CN
                for (int i = 0; i < BANGTAM.Rows.Count; i++)
                {
                    TTLOTSL = BANGTAM.Rows[i]["LOT"].ToString();

                    STT = int.Parse(BANGTAM.Rows[i]["STT"].ToString());
                    MAH = BANGTAM.Rows[i]["MAHANG"].ToString();
                    if (TTLOTSL != "")
                    {
                        if (KTLOT_TONKHO(TTLOTSL, STT, MAH) == true)
                        {
                            dem = dem + 1;
                            sql = "update TMPPHIEUGIAOHANG set STATUS ='OK' where STT = " + STT + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                            sql = "INSERT INTO LUUPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,TTPHIEU,NHAMAY,GIOGIAOFCC)" +
                                  " SELECT STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,'OK' as STATUS,TTPHIEU,'" + NHAMAY + "','" + GIOGIAO + "'" +
                                    " FROM TMPPHIEUGIAOHANG where LOT <> '' and STATUS = 'OK' and STT = " + STT + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);

                            sql = "delete from TMPPHIEUGIAOHANG where STATUS = 'OK' and STT =" + STT + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);

                        }
                        else
                        {

                            return;

                        }
                    }


                }
                #endregion

                if (dem == BANGTAM.Rows.Count)
                {
                    sql = "INSERT INTO LUUDOCQRCODE (LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,SLTEMHVN,STATUS,MAFCC,STT,KETQUA,NGAYXUAT,GIOXUAT,NHAMAY,GIOGIAO)" +
                                " SELECT LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,SLTEMHVN,STATUS,MAFCC,STT,KETQUA ,'" + NGAYGIAO + "', '" + GIOGIAO + "' ,'" + NHAMAY + "',CUA" +
                                  " FROM DOCQRCODE where KETQUA = 'DG'";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    sql = "delete from DOCQRCODE where KETQUA = 'DG'";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                }
                sql = "select count(*) from docqrcode ";
                string SL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                if (int.Parse(SL) == 0)
                {
                    sql = "delete from tmpphieugiaohang ";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    MessageBox.Show("Đấ cập nhập kho !!!!!", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    DialogResult RS = MessageBox.Show("Đấ cập nhập kho, Dữ liệu đọc QRcode vẫn còn . Bạn muốn bỏ qua ?", "Thông Báo !", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (RS == DialogResult.Yes)
                    {
                        sql = "delete from tmpphieugiaohang ";
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        sql = "delete from docqrcode ";
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        gridCtrDOCQrCODE.RefreshDataSource();
                    }

                }

            }
        }
        private void CAPNHAPK()
        {

            UPDQATESTOCK = "";
            string sql, TTLOTSL, MAH, sqldocqr, sqlluudocqr;
            int dem1 = 0, dem = 0;
            DataTable BANGTAM = new DataTable();
            DataSet LOTOK_NG = new DataSet();
            sql = "select STT,LOT,MAHANG,STATUS from TMPPHIEUGIAOHANG where LOT <> '' and (STATUS != 'OK' or STATUS is null )  order by STT";
            BANGTAM = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            if (BANGTAM.Rows.Count == 0)
            {
                MessageBox.Show("Bạn chưa cập nhập được kho vì chưa có dữ liệu !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                string NGAYGIAO = dateNX.DateTime.ToString("MM/dd/yyyy");
                string MS, LOT, SL, SLC, STS, GIOGIAO = GIOXUATH;
                string NHAMAY;
                if (addHVN == 1)
                {
                    NHAMAY = "HON DA - VIET NAM(NHA MAY VP)";
                }
                else { NHAMAY = "HON DA - VIET NAM(NHA MAY HA NAM)"; }

                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                LOTOK_NG = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_Update_Stock");
                dem = LOTOK_NG.Tables[0].Rows.Count;
                if (dem > 0)
                {
                    MS = "";
                    for (int i = 0; i < dem; i++)
                    {
                        LOT = LOTOK_NG.Tables[0].Rows[i]["LOT"].ToString();
                        SL = LOTOK_NG.Tables[0].Rows[i]["SOLUONG"].ToString();
                        STS = LOTOK_NG.Tables[0].Rows[i]["STATUS"].ToString();
                        MS = MS + "\n" + LOT.Trim() + " | Số lượng xuất : " + SL + " | Trạng Thái :" + STS;

                    }
                    XtraMessageBox.Show("Cập Nhập : " + dem + " LOT " + "\n" + MS + Environment.NewLine);

                }
                dem1 = LOTOK_NG.Tables[1].Rows.Count;
                if (dem1 > 0)
                {
                    dem = LOTOK_NG.Tables[1].Rows.Count;
                    MS = "";
                    for (int i = 0; i < dem; i++)
                    {
                        LOT = LOTOK_NG.Tables[1].Rows[i]["LOT"].ToString();
                        SL = LOTOK_NG.Tables[1].Rows[i]["SLCAN"].ToString();
                        SLC = LOTOK_NG.Tables[1].Rows[i]["SLCO"].ToString();
                        STS = LOTOK_NG.Tables[1].Rows[i]["STATUS"].ToString();
                        MS = MS + "\n" + LOT.Trim() + " | Số lượng Cần Xuất : " + SL + " | Số Lượng tồng kho : " + SLC + " | Lỗi :" + STS;

                    }
                    XtraMessageBox.Show("Kiểm tra lại " + dem + " LOT" + "\n" + MS + Environment.NewLine);
                }
                BANGTAM = sqlBRV.LoadData(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_LUU_PHIEU", new System.Data.SqlClient.SqlParameter("@GIOGIAOFCC", GIOXUAT));
                LoadDLTMP();
                #region Bỏ
                //for (int i = 0; i < BANGTAM.Rows.Count; i++)
                //{
                //    TTLOTSL = BANGTAM.Rows[i]["LOT"].ToString();

                //    STT = int.Parse(BANGTAM.Rows[i]["STT"].ToString());
                //    MAH = BANGTAM.Rows[i]["MAHANG"].ToString();
                //    #region Cap nhập

                //    if (KTLOT_TONKHO(TTLOTSL, STT, MAH) == true)
                //    {

                //        string[] _TACH = TTLOTSL.Split(',');
                //        if (_TACH.Length == 1)
                //        {
                //            sql = TruTK(TTLOTSL, STT);
                //            if (UPDQATESTOCK == "")
                //            {
                //                UPDQATESTOCK = sql;
                //            }
                //            else
                //            {
                //                UPDQATESTOCK = UPDQATESTOCK + ";" + sql;
                //            }
                //        }
                //        else
                //        {
                //            for (int m = 0; m < _TACH.Length; m++)
                //            {
                //                sql = TruTK(_TACH[m], STT);

                //                if (UPDQATESTOCK == "")
                //                {
                //                    UPDQATESTOCK = sql;
                //                }
                //                else
                //                {
                //                    UPDQATESTOCK = UPDQATESTOCK + ";" + sql;
                //                }
                //            }
                //        }

                //        /// lưu và xóa đọc QCcode


                //        sqldocqr = sql = "INSERT INTO LUUDOCQRCODE (LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,SLTEMHVN,STATUS,MAFCC,STT,KETQUA,NGAYXUAT,GIOXUAT,NHAMAY,GIOGIAO)" +
                //            " SELECT LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,SLTEMHVN,STATUS,MAFCC,STT,KETQUA ,'" + NGAYGIAO + "', '" + GIOGIAO + "' ,'" + NHAMAY + "',CUA" +
                //              " FROM DOCQRCODE where KETQUA = 'DG'";


                //        if (sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldocqr) != -1)
                //        {

                //            sqlluudocqr = "delete from DOCQRCODE where KETQUA = 'DG'";

                //            // sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldocqr);
                //        }
                //        else
                //        {
                //            MessageBox.Show("Có lỗi xảy ra không lưu được đọc Qrcode ! Không thể cập nhập Kho ", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //            return;
                //        }
                //        /// lưu và xóa phiếu

                //        sql = "INSERT INTO LUUPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,TTPHIEU,NHAMAY,GIOGIAOFCC)" +
                //             " SELECT STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,'OK' as STATUS,TTPHIEU,'" + NHAMAY + "','" + GIOGIAO + "'" +
                //               " FROM TMPPHIEUGIAOHANG where LOT <> '' and STATUS = 'OK' and STT = " + STT + "";



                //        if (sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql) != -1)
                //        {
                //            #region
                //            dem = dem + 1;
                //            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlluudocqr);
                //            #endregion
                //        }
                //        else
                //        {
                //            MessageBox.Show("Có lỗi xảy ra luu phiếu giao ! Không thể cập nhập Kho ", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //            return;
                //        }
                //    }
                //    else
                //    {
                //        return;
                //    }

                //}
                //#endregion
                //if (dem > 0)
                //{
                //    string upd = "";
                //    string[] SQLUPDATESTOCK;
                //    SQLUPDATESTOCK = UPDQATESTOCK.Split(';');
                //    for (int i = 0; i < SQLUPDATESTOCK.Length; i++)
                //    {
                //        upd = SQLUPDATESTOCK[i];
                //        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, upd);
                //    }

                //    UPDQATESTOCK = "";
                //    MessageBox.Show("Đã cập nhập : " + dem + " Mã của tổng số " + BANGTAM.Rows.Count + " Mã ", " Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Information);



                //    sqldocqr = "select count(*) from DOCQRCODE ";
                //    string KQBangQrcode = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqldocqr);
                //    if (int.Parse(KQBangQrcode) > 0)
                //    {
                //        DialogResult rs = MessageBox.Show("Dữ liệu đọc Qrcode vẫn còn ! bạn có muốn bỏ qua ?", "Thông Báo !", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                //        if (rs == DialogResult.Yes)
                //        {
                //            sqldocqr = "delete from DOCQRCODE ";
                //            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldocqr);
                //        }
                //    }

                //}
                //else
                //{
                //    MessageBox.Show("Không thể cập nhập Kho ", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //}
                #endregion
            }
        }
        public string UPDQATESTOCK;
        private string TruTK(string LOTSL, int STT)
        {
            string sql;
            string[] _TACH = LOTSL.Split('-');
            string LOT = _TACH[0];
            int SL = int.Parse(_TACH[1]);

            sql = "update TMPPHIEUGIAOHANG set status = 'OK' where STT= " + STT + "";

            gridVDONHANG.SetRowCellValue(STT - 1, gridVDONHANG.Columns["STATUS"], "OK");
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            sql = "update stocktp set slxuat = slxuat +" + SL + ",slconlai = slconlai - " + SL + " where LOT = '" + LOT + "'";

            return sql;

        }
        private Boolean KTLOT_TONKHO2(string LOTSL, int STT, string MH)
        {
            Boolean KQ;
            string NGAYGIAO = dateNX.DateTime.ToString("MM/dd/yyyy");
            string GIOGIAO = GIOXUATH;
            int SL, dem = 0;
            string LOT, sql;
            string[] _LOT, _TACH;
            _LOT = LOTSL.Split(',');
            if (_LOT.Length == 1)
            {
                if (KIEMTRA(LOTSL, STT, MH) == true)
                {
                    _TACH = LOTSL.Split('-');
                    LOT = _TACH[0];
                    SL = int.Parse(_TACH[1]);
                    if (LOT.Length > 20)
                    {
                        LOT = LOT.Substring(0, 6) + int.Parse(LOT.Substring(7, 5)).ToString() + LOT.Substring(12, 1);
                    }
                    KQ = true;
                    sql = "update tmpphieugiaohang set status = 'OK' where STT= " + STT + "";
                    gridVDONHANG.SetRowCellValue(STT - 1, gridVDONHANG.Columns["STATUS"], "OK");
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    sql = "update stocktp set slxuat = slxuat +" + SL + ",slconlai = slconlai - " + SL + " where LOT = '" + LOT + "'";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    sql = "INSERT INTO GIAO_HANG (LOT,NGAYGIAO,GIOGIAO,SLGIAO,STATUS)" +
                            " Values ('" + LOT + "','" + NGAYGIAO + "','" + GIOGIAO + "'," + SL + ",1) ";

                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                }
                else
                {
                    KQ = false;
                }
            }
            else
            {
                for (int i = 0; i < _LOT.Length; i++)
                {
                    if (KIEMTRA(_LOT[i], STT, MH) == true)
                    {
                        dem = dem + 1;
                    }
                    else
                    {
                        return false;
                    }
                }
                if (dem == _LOT.Length)
                {
                    KQ = true;

                    sql = "update tmpphieugiaohang set status = 'OK' where STT= " + STT + "";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    gridVDONHANG.SetRowCellValue(STT - 1, gridVDONHANG.Columns["STATUS"], "OK");
                    for (int i = 0; i < _LOT.Length; i++)
                    {
                        if (KIEMTRA(_LOT[i], STT, MH) == true)
                        {
                            _TACH = _LOT[i].Split('-');
                            LOT = _TACH[0];
                            SL = int.Parse(_TACH[1]);
                            sql = "update stocktp set slxuat = slxuat + " + SL + ",slconlai = slconlai - " + SL + " where LOT = '" + LOT + "'";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                            sql = "INSERT INTO GIAO_HANG (LOT,NGAYGIAO,GIOGIAO,SLGIAO,STATUS)" +
                             " Values ('" + LOT + "','" + NGAYGIAO + "','" + GIOGIAO + "'," + SL + ",1)";

                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    KQ = false;
                }
            }
            return KQ;
        }

        private Boolean KTLOT_TONKHO(string LOTSL, int STT, string MH)
        {
            Boolean KQ;
            string NGAYGIAO = dateNX.DateTime.ToString("MM/dd/yyyy");
            string GIOGIAO = GIOXUATH;
            int SL, dem = 0;
            string LOT, sql;
            string[] _LOT, _TACH;
            _LOT = LOTSL.Split(',');
            if (_LOT.Length == 1)
            {
                if (KIEMTRA(LOTSL, STT, MH) == true)
                {
                    _TACH = LOTSL.Split('-');
                    LOT = _TACH[0];
                    SL = int.Parse(_TACH[1]);
                    KQ = true;

                }
                else
                {
                    KQ = false;
                }
            }
            else
            {
                for (int i = 0; i < _LOT.Length; i++)
                {
                    if (KIEMTRA(_LOT[i], STT, MH) == true)
                    {
                        dem = dem + 1;
                    }
                    else
                    {
                        return false;
                    }
                }
                if (dem == _LOT.Length)
                {
                    KQ = true;


                }
                else
                {
                    KQ = false;
                }
            }
            return KQ;
        }
        private Boolean KIEMTRA(string LOTSL, int STT, string MH)
        {
            Boolean KQ;
            int SLCONLAI;

            string[] _LOTSL;
            _LOTSL = LOTSL.Split('-');
            string _SL, LOT = _LOTSL[0];
            int SL = int.Parse(_LOTSL[1]);
            string sql = "select slconlai from STOCKTP where LOT = '" + LOT + "'";
            _SL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (_SL == "")
            {
                MessageBox.Show("Kiểm tra lại :  \n Số TT Phiếu : " + STT + "\n MÃ hàng : " + MH + " \n LOT :" + LOT + "", "Thông Báo !   Không tồn tại !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            else
            {
                SLCONLAI = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            }
            if (SL > SLCONLAI)
            {
                KQ = false;
                MessageBox.Show("Kiểm tra lại : \n Số TT Phiếu : " + STT + " \n MÃ hàng : " + MH + " \n LOT :" + LOT + " \n Số lượng yêu cầu xuất :" + SL + " \n Số lượng còn lại : " + SLCONLAI + "", "Thông Báo ! - không đủ xuất !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LOT = "'" + LOT + "'";
                TONKHOTP TK = new TONKHOTP(LOT);
                TK.ShowDialog();
            }
            else
            {
                KQ = true;

            }
            return KQ;
        }
        private void cmd_CAPNHAPKHO_Click(object sender, EventArgs e)
        {
            string sql = "select count(*) from tmpphieugiaohang";
            string KQ = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (ChoPhepCNK() == true)
            {
                if (KQ == "0")
                {
                    LoadPhieu_GridView();
                }
                CAPNHAPK();
            }
            else
            {
                MessageBox.Show("Không có dữ liệu cho CNK !!!!!", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //setstatusbootomDOCQ9RCODE();
            //string NGAYGIAO = dateNX.DateTime.ToString("MM/dd/yyyy");
            //string GIOGIAO = GIOXUATH;
            //string NHAMAY;
            //if (addHVN == 1)
            //{
            //    NHAMAY = "HON DA - VIET NAM(NHA MAY VP)";
            //}
            //else { NHAMAY = "HON DA - VIET NAM(NHA MAY HA NAM)"; }

            //string sql = "INSERT INTO LUUPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,TTPHIEU,NHAMAY,GIOGIAOFCC)" +
            //      " SELECT STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,'OK' as STATUS,TTPHIEU,'" + NHAMAY + "','" + GIOGIAO + "'" +
            //        " FROM TMPPHIEUGIAOHANG where LOT <> '' and STATUS = 'OK'";
            //    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            gridCtrDOCQrCODE.RefreshDataSource();
            gridVDOCQRCODE.RefreshData();

        }
        #endregion

        private ListViewColumnSorter lvwColumnSorter;
        // this.listVGHEPLOT.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listVGHEPLOT_ColumnClick);


        private void listVGHEPLOT_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListView myListView = (ListView)sender;

            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == lvwColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            myListView.Sort();
        }

        private void CMD_XOA_Click(object sender, EventArgs e)
        {
            string sql1 = "delete docqrcode";
            string sql = "delete tmpphieugiaohang";
            DialogResult ras = MessageBox.Show("Toàn bộ Dữ Liệu đã bắn sẽ bị xóa . Bạn có chắc chắn muốn xóa ? ", "Thông Báo", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (ras == DialogResult.Yes)
            {
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql1);
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                gridCtrDOCQrCODE.RefreshDataSource();
            }

        }

        private void gridVDONHANG_RowCellStyle(object sender, RowCellStyleEventArgs e)

        {
            GridView View = sender as GridView;
            string LOT = View.GetRowCellDisplayText(e.RowHandle, View.Columns["LOT"]);

            if ((e.Column.FieldName == "STATUS" || e.Column.FieldName == "STATUSDOC" || e.Column.FieldName == "LOT"))

            {
                if (LOT.Trim() == "")
                {
                    e.Appearance.BackColor = Color.Red;
                    e.Appearance.ForeColor = Color.Yellow;
                }
                else
                {
                    e.Appearance.BackColor = Color.Green;
                    //e.Appearance.ForeColor = Color.Magenta;
                    e.Appearance.ForeColor = Color.Yellow;
                    e.Appearance.Font = new Font("Arial", 9, FontStyle.Bold); ;
                }

            }

        }

        private void txt_DOCQRCODE_TextChanged(object sender, EventArgs e)
        {

        }

        private void cmdThemDB_Click(object sender, EventArgs e)
        {
            gridVDONHANG.AddNewRow();
        }

        private void gridVDOCQRCODE_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {

        }

        private void gridCtrDONHANG_MouseClick(object sender, MouseEventArgs e)
        {

        }

        private void gridVDONHANG_PopupMenuShowing(object sender, PopupMenuShowingEventArgs e)
        {
            GridView view = sender as GridView;
            if (e.MenuType == DevExpress.XtraGrid.Views.Grid.GridMenuType.Row)
            {
                int rowHandle = e.HitInfo.RowHandle;
                // Delete existing menu items, if any.
                e.Menu.Items.Clear();
                // Add the Rows submenu with the 'Delete Row' command
                e.Menu.Items.Add(CreateSubMenuRows(view, rowHandle));
                // Add the 'Cell Merging' check menu item.
                // DXMenuItem item = CreateMenuItemCellMerging(view, rowHandle);
                //e.Menu.Items.BeginGroup = true;
                //e.Menu.Items.Add(item);
            }
        }
        DXMenuItem CreateSubMenuRows(GridView view, int rowHandle)
        {
            DXSubMenuItem subMenu = new DXSubMenuItem("Rows");
            string deleteRowsCommandCaption;

            deleteRowsCommandCaption = "&Refresh Data";
            DXMenuItem menuItemDeleteRow = new DXMenuItem(deleteRowsCommandCaption, new EventHandler(RefreshClick), imageCollection1.Images[0]);
            menuItemDeleteRow.Tag = new RowInfo(view, rowHandle);
            menuItemDeleteRow.Enabled = view.IsDataRow(rowHandle) || view.IsGroupRow(rowHandle);
            subMenu.Items.Add(menuItemDeleteRow);
            return subMenu;
        }



        void RefreshClick(object sender, EventArgs e)
        {
            DXMenuItem menuItem = sender as DXMenuItem;
            RowInfo ri = menuItem.Tag as RowInfo;
            if (ri != null)
            {
                loadPHIEU();
            }
        }



        private void cmd_KTNG_Click(object sender, EventArgs e)
        {
            string MH = gridVDONHANG.GetRowCellValue(gridVDONHANG.FocusedRowHandle, "MAHANG").ToString();
            FRM_SUALOTHVN fRM = new FRM_SUALOTHVN(MH);
            fRM.ShowDialog();

        }
        #endregion
        #region Xử lý mới theo procedue
        // Xử lý load phiếu
        //string NGAYGIAO = dateNX.DateTime.ToString("yyyy-MM-dd");
        /// <IFSDATA>
        public DataTable DONHANG = new DataTable();
        private void LoadDL()
        {


            DataSet DH = new DataSet();
            string MH,NHAMAY;
            int HOP, QCDG,SLGIAO;
            if (addHVN == 1)
                NHAMAY = "HON DA -VIET NAM- (NHA MAY VINH PHUC)";
            else
                NHAMAY = "HON DA -VIET NAM- (NHA MAY HA NAM)";
            N_XH = dateNX.DateTime.ToString("ddMMyyyy");
            string sql = "select '' as STT,CONCAT(CUSTOMER_PART_NO , cast(BUY_QTY_DUE as VARCHAR(10))) as FIND,CUSTOMER_NO,to_char(WANTED_DELIVERY_DATE,HH24) as GIOGIAO,BUY_QTY_DUE as SOLUONG,customer_part_unit_meas as DV,SHIP_ADDR_NO as ADDNM,SUB_DOCK_CODE as CUA,CUSTOMER_PART_NO as MAHANG,CATALOG_DESC as TENHANG,CUSTOMER_PO_REL_NO,DOCK_CODE as TRUYEN,ORDER_NO, " +
                   "CATALOG_NO,PLANNED_SHIP_DATE as NGAYGIAO,CUSTOMER_PO_NO,'' as HOP, '' as LOT ,'NG' as STATUS,'' as STATUSDOC,'" + GIOXUATH + "' as GIOGIAOFCC ,'" + NHAMAY + "' as NHAMAY " +
                   
                    " from CUSTOMER_ORDER_JOIN " +
                   " where " +
                   " CUSTOMER_NO = '100001' and " +
                   " SHIP_ADDR_NO = " + addHVN + " and " +
                   " (OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )and " +
                   " to_char( WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "' and " +
                   " CUSTOMER_PO_REL_NO is not null" +
                   " and  to_char(WANTED_DELIVERY_DATE, 'HH24') in (" + GIOXUAT + ")" +
                   " Order by WANTED_DELIVERY_DATE,SUB_DOCK_CODE,CUSTOMER_PART_NO ";
           
            DONHANG = iFSPROVIDER.ExecuteQuery(sql);
            for(int i=0; i < DONHANG.Rows.Count;i++)
            {
                MH = DONHANG.Rows[i]["MAHANG"].ToString();
                SLGIAO = int.Parse(DONHANG.Rows[i]["SOLUONG"].ToString());
                sql = "IF EXISTS (SELECT cast(MinCloseQty as int)  from B20Item where Code ='" + MH + "')";
                sql += " Begin ";
                sql += " SELECT cast(MinCloseQty as int)  from B20Item where Code ='" + MH + "'";
                sql += " end ";
                sql += " else ";
                sql += " select 0 ";
                QCDG = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
              
                DONHANG.Rows[i]["STT"] = (i+1).ToString();
                if (QCDG != 0)
                {
                    HOP = SLGIAO/QCDG;
                    int DU = SLGIAO % QCDG;
                    if (DU > 0)
                        HOP = HOP + 1;
                    DONHANG.Rows[i]["HOP"] = HOP.ToString();
                }
            }
            gridCtrDONHANG.DataSource = DONHANG;
            sql = "IF EXISTS(SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[IFSPHIEUGIAOHANG]') AND type in (N'U')) ";
            sql += " begin ";
            sql += " Drop table IFSPHIEUGIAOHANG ";
            sql += " end ";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            /// Tao Bang SQL Va copy dl
            string createtablehangthieu = SqlTableCreator.GetCreateFromDataTableSQL("IFSPHIEUGIAOHANG", DONHANG);
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, createtablehangthieu);
            string sqldelete = "delete from IFSPHIEUGIAOHANG";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldelete);
            SqlTableCreator.BulkInsertDataTable(sqlBRV.B7R2_FCCdb, "IFSPHIEUGIAOHANG", DONHANG);
            DH = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_LOAD_PHIEU_DOCQR");
            gridCtrDONHANG.DataSource = DH.Tables[0];
            //////////////////////////
            TTGIAOTHEOMA();

            DH = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_LOAD_HANGTHIEU");
            //gridCtrDONHANG.DataSource = DH.Tables[0];
            GCT_HT.DataSource = DH.Tables[0];
        }
        private void cmd_CheckGhep_Click_1(object sender, EventArgs e)
        {
            DataSet DH = new DataSet();
            
            DH = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_gheplot");
            //gridCtrDONHANG.DataSource = DH.Tables[0];
            gridCTTGL.DataSource = DH.Tables[0];
        }
        private void TTGIAOTHEOMA()
        {

            int GIOXUATH = int.Parse(GIOXUAT.Substring(1, 2));
            String sql = "select PART_NO as MAHANG,sum(TTSLG) as TTSLG ,GIOGIAO,NGAYGIAO" +
                " from" +
                " (select * from " +
                        " (select CUSTOMER_PART_NO as PART_NO,to_char(WANTED_DELIVERY_DATE, 'HH24') as GIOGIAO,PLANNED_SHIP_DATE as NGAYGIAO ,sum(BUY_QTY_DUE) as TTSLG " +
                        "from CUSTOMER_ORDER_JOIN " +
                        "where " +
                        " CUSTOMER_NO = '100001' and " +
                        " SHIP_ADDR_NO = " + addHVN + " and " +
                        "(OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) or " +
                        " OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )and " +
                       " CUSTOMER_PO_REL_NO is not null and " +
                       " to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy') = '" + N_XH + "'" +
                     " group by CUSTOMER_PART_NO,CATALOG_DESC,WANTED_DELIVERY_DATE,PLANNED_SHIP_DATE ) TTDH" +
                      " where GIOGIAO >  " + GIOXUATH + ") B1" +
                      " group by PART_NO,GIOGIAO,NGAYGIAO ";
            TT_HANG_MA = iFSPROVIDER.ExecuteQuery(sql);
            sql = "IF EXISTS(SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TBL_TT_MA]') AND type in (N'U')) ";
            sql += " begin ";
            sql += " Drop table TBL_TT_MA ";
            sql += " end ";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            /// Tao Bang SQL Va copy dl
            string createtablehangthieu = SqlTableCreator.GetCreateFromDataTableSQL("TBL_TT_MA", TT_HANG_MA);
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, createtablehangthieu);
            string sqldelete = "delete from TBL_TT_MA";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldelete);
            SqlTableCreator.BulkInsertDataTable(sqlBRV.B7R2_FCCdb, "TBL_TT_MA", TT_HANG_MA);
        }
        private void updateTMPPHIEUGIAOHANG()
        {
            string NGAYGIAO = dateNX.DateTime.ToString("yyyy-MM-dd");
            string delete = "delete tmpphieugiaohang";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, delete);
            if (KTTTGH() == false)
            {

                for (int i = 0; i < gridVDONHANG.RowCount; i++)
                {
                    string MaHang = gridVDONHANG.GetRowCellValue(i, "MAHANG").ToString();

                    DateTime GIO_XUAT_LIST = DateTime.Parse(gridVDONHANG.GetRowCellValue(i, "NGAYGIAO").ToString());
                    int GXH = int.Parse(GIO_XUAT_LIST.ToString("HH"));
                    int SLXUAT = int.Parse(gridVDONHANG.GetRowCellValue(i, "SOLUONG").ToString());
                    string CUA = gridVDONHANG.GetRowCellValue(i, "TRUYEN").ToString();
                    string TRUYEN = gridVDONHANG.GetRowCellValue(i, "CUA").ToString();
                    string TENHANG = gridVDONHANG.GetRowCellValue(i, "TENHANG").ToString();
                    string LOT = gridVDONHANG.GetRowCellValue(i, "LOT").ToString();
                    string DV = gridVDONHANG.GetRowCellValue(i, "DV").ToString();
                    string STT = gridVDONHANG.GetRowCellValue(i, "STT").ToString();
                    string Status = gridVDONHANG.GetRowCellValue(i, "STATUS").ToString();

                    if (Status != "OK")
                    {
                        string insert_tmpphieugiaohang = "insert into tmpphieugiaohang (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,ADDNM,STATUS) " +
                      "VALUES " +
                      "('" + STT + "','" + CUA + "','" + TRUYEN + "','" + MaHang + "','" + TENHANG + "','" + LOT + "','" + DV + "'," + SLXUAT + ",'" + NGAYGIAO + "','" + GXH + "'," + addHVN + ",'" + Status + "')";
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, insert_tmpphieugiaohang);
                    }
                }
            }
        }
        private void gridVDONHANG_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            if (!(gridVDONHANG.UpdateCurrentRow()))
            {
                return;
            }

            updateTMPPHIEUGIAOHANG();
        }

        private void simpleButton1_Click_1(object sender, EventArgs e)
        {

        }

        private void CMD_HOANTHANH_Click_1(object sender, EventArgs e)
        {

        }



        /// </IFSDATA>
        private void LoadIFS()
        {
            Boolean DANGBAN = false;
            string CUA, TRUYEN, DV, TENHANG, LOT;
            if (GIOXUAT != "#")
            {
                LoadDL();
            }
            WaitForm2.SO = 1;
            splashScreenManager2.ShowWaitForm();
            string NGAYGIAO = dateNX.DateTime.ToString("yyyy-MM-dd");
        }
           //////////////////////////////////////////////////////
 
        #endregion

    }
    public class ListViewItemComparer : IComparer
    {

        private int col;
        private SortOrder order;
        public ListViewItemComparer()
        {
            col = 1;
            order = SortOrder.Descending;
        }
        public ListViewItemComparer(int column, SortOrder order)
        {
            col = column;
            this.order = order;
        }
        public int Compare(object x, object y)
        {
            int returnVal = -1;
            returnVal = String.Compare(((ListViewItem)x).SubItems[col].Text,
                            ((ListViewItem)y).SubItems[col].Text);
            // Determine whether the sort order is descending.
            if (order == SortOrder.Descending)
                // Invert the value returned by String.Compare.
                returnVal *= -1;
            return returnVal;
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