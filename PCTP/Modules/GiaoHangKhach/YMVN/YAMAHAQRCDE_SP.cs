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
using DevExpress.XtraGrid;
using PCTP.ClassSQL;
using DevExpress.XtraReports.UI;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.QRCODE_HVN.YMN;

namespace PCTP.YMN
{
    public partial class YAMAHAQRCDE_SP : DevExpress.XtraEditors.XtraForm
    {
        public YAMAHAQRCDE_SP()
        {
            InitializeComponent();
            //this.Load += YAMAHAQRCDE_SP_Load;
            //end-users cannot add rows
            //GridVTTDH.OptionsBehavior.AllowAddRows = DevExpress.Utils.DefaultBoolean.False;
        }
        IFSPROVIDER iFSPROVIDER = new IFSPROVIDER();
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public static string PARTYMVN = "";
        public static int SLYMVN = 0;
        public static string ODERYMVN = "";
        public static DataTable TB_PGH = new DataTable();
        #region TEM FCC
        private Boolean KIEMTRATTRUNGTEM(string LOTFCC)
        {
            Boolean KQ = false;

            string sqlKT = "select count(*) from SP_DOCQRCODE where LOTNO = '" + LOTFCC + "'";
            if (int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlKT)) == 0)
            {
                KQ = true;
            }
            else
            { KQ = false; }
            return KQ;
        }
        private Boolean KTRAMA(string MaHang)
        {
            Boolean KQ = false;
            string sql = "select count(*) from SP_TMPPHIEUGIAOHANG where MAHANG = '" + MaHang + "'";
            string KQSQL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            int KQTIMKIEM = int.Parse(KQSQL);
            if (KQTIMKIEM == 0)
            {
                KQ = false;
            }
            else
            {
                KQ = true;
            }
            return KQ;
        }
        private DataTable PO_NO_S(string MH)
        {
            string  sql;
            DataTable tbl = new DataTable();
            sql= "select PO_NO,TENHANG,SLGIAO from SP_TMPPHIEUGIAOHANG where MAHANG = '"+MH+"'";
            tbl = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            return tbl;
        }
        private Boolean KTHANGDABAN_DANGBANQR(string MH, int SLBAN,string Cty)
        {
            Boolean KQ = true;
            int SLDABAN;
            string SL_DB, sql;
            int SLCANGIAO;

            DataTable TB_CONLAI = new DataTable();
            if(Cty=="YMVN")
            {
                sql = "select MAHANG,sum(SLGIAO) as SOLUONG ,STATUS  from  SP_TMPPHIEUGIAOHANG where   REPLACE(MAHANG,'-','')  = '" + MH + "'  group by MAHANG ,STATUS ";
            }
                else
            {
                sql = "select MAHANG,sum(SLGIAO) as SOLUONG ,STATUSFCC  from  SP_TMPPHIEUGIAOHANG where  MAHANG = '" + MH + "'  group by MAHANG ,STATUSFCC ";
            }       
            //sql = "select MAHANG,sum(SLG) as SOLUONG ,STATUS  from  SP_TMPPHIEUGIAOHANG where ( STATUS <> '1' or STATUS is null ) and MAHANG = '" + MH + "'  group by MAHANG ,STATUS ";


            TB_CONLAI = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            string sql1;
            if (TB_CONLAI.Rows.Count > 0)
            {
                SLCANGIAO = int.Parse(TB_CONLAI.Rows[0]["SOLUONG"].ToString());
                if (Cty == "YMVN")
                {
                    sql1 = "select sum(SLTEM) from SP_docqrcode where  MAHANG = '" + MH + "'";
                }
                else
                {
                    sql1 = "select sum(SLTEM) from SP_docqrcode where  MAHANG = '" + MH + "'";
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
        private void luuDQCQRCODE( string Cty,int STT, String LOT, string MAHANG, int SLTEM,string PONO)
        {
            string sql;
            
                sql = "insert into SP_DOCQRCODE (Cty,STT,LOTNO,MAHANG,SLTEM,PONO) " +
                "VALUES " +
                "('" + Cty + "','" + STT + "' , '" + LOT + "','" + MAHANG + "'," + SLTEM +  ",'"+ PONO + "')";
           
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
        }
        private void LoadDOCQR()
        {
            string sql;
            DataTable Tbl_QR;
            sql = "select * from SP_DocQRCode order by STT asc";
            Tbl_QR = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
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
        private void TachQRYMVN(string QRYMVN)
        {
             PARTYMVN = "";
             SLYMVN = 0;
             ODERYMVN = "";
            int VTP, VTOR, VTSL;
            VTP = 0;
            VTOR = 0;
            VTSL = 0;
            for (int i = 0; i < QRYMVN.Length; i++)
            {
                if (QRYMVN[i].ToString() == "P")
                {
                    PARTYMVN = QRYMVN.Substring(i + 1, 14);
                    VTP = i + 1 + 14;
                    break;
                }

            }
            for (int j = VTP; j < QRYMVN.Length; j++)
            {
                if (QRYMVN[j].ToString() == "K")
                {
                    ODERYMVN = QRYMVN.Substring(j + 1, 5);
                    VTOR = j + 1 + 5;
                    break;
                }
            }
            for (int j = VTOR; j < QRYMVN.Length; j++)
            {
                if (QRYMVN[j].ToString() == "Q")
                {
                    SLYMVN = int.Parse(QRYMVN.Substring(j + 1, 6));
                    VTSL = j + 1 + 6;
                    break;
                }
            }
        }
#endregion
        // Xử lý Yamaha
        #region Yamaha
        private Boolean KTODER(string ODER,string MAHANG)
        {
            Boolean KQKT = false;
            string sql = "select count(*) from SP_tmpphieugiaohang where PO_NO = '" + ODER + "' and REPLACE(MAHANG,'-','') = '" + MAHANG + "'  ";
            int TT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            if (TT == 0)
            {
                KQKT = false;
            }
            else
            {
                KQKT = true;
            }
            return KQKT;
        }
        #endregion
        private void txt_DOCQRCODE_KeyPress(object sender, KeyPressEventArgs e)
        {



            string LOTSLFCC, LOTFCC, MHSL;
            string Cty = "";
            string _MAFCC, MAHANGFCC = "";
            string PO = "";
            int SLTEMFCC,thoat=0, Gear = 0;

            int STTBAN = 0;
            string sqlTIMSTTBAN, TIMSTTBAN;

            string QRcode = txt_DOCQRCODE.Text.Trim();
            if (e.KeyChar == 13)
            {
                Cty = "FCC";
                sqlTIMSTTBAN = "select max(STT) from SP_DOCQRCODE";
                TIMSTTBAN = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlTIMSTTBAN);
                if (TIMSTTBAN != "")
                {
                    STTBAN = int.Parse(TIMSTTBAN);
                }
                //LISTV_BANQRCODE.Items[1].SubItems["LOTHVN"].Text == "")



                string[] arrQRFCC = QRcode.Split(':');

                if (arrQRFCC.Length == 4)
                {

                    LOTSLFCC = arrQRFCC[0];
                    if (KIEMTRATTRUNGTEM(LOTSLFCC) == true)
                    {
                        MAHANGFCC = arrQRFCC[1];
                        _MAFCC = MAHANGFCC.Replace("-", "");
                        string[] arrQRFCC_GHEP = LOTSLFCC.Split(',');
                        SLTEMFCC = int.Parse(arrQRFCC[3]);
                        MHSL = MAHANGFCC + SLTEMFCC;

                        if (arrQRFCC_GHEP.Length == 1)
                        {
                            if (LOTSLFCC.Length == 27)
                            {

                                LOTFCC = LOTSLFCC.Substring(0, 12);
                                int PartCode = int.Parse(LOTFCC.Substring(6, 5));
                                LOTFCC = LOTSLFCC.Substring(0, 6) + PartCode + LOTFCC.Substring(LOTFCC.Length - 1, 1);
                                Gear = int.Parse(LOTSLFCC.Substring(12, 1));
                                LOTFCC = LOTFCC + Gear.ToString();
                                string sql = "select Name from B20Gear where Code = " + Gear + "";
                               // S_Gear = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);


                            }
                            else
                            {
                                LOTFCC = LOTSLFCC.Substring(0, LOTSLFCC.Length - arrQRFCC[3].Length);
                            }
                        }
                        else
                        {
                            if (LOTSLFCC.Length == 22)
                            {
                                string LOT = LOTSLFCC.Substring(1, LOTSLFCC.Length - 10);
                                string PartCode = LOT.Substring(7, 5);
                                LOTFCC = LOTSLFCC.Substring(1, 6) + PartCode + LOT.Substring(LOT.Length - 1, 1);

                            }
                            else
                            {
                                LOTFCC = LOTSLFCC;
                            }
                        }

                        if (KTRAMA(MAHANGFCC) == true)
                        {
                            if (KTHANGDABAN_DANGBANQR(MAHANGFCC, SLTEMFCC, Cty) == true)
                            {
                                //FRM_LISTRUNGMSL.MAHANG = "";

                                //ADDGRIDV(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, SLTEMFCC, "", "");
                                PO = PO_NO_S(MAHANGFCC).Rows[0]["PO_NO"].ToString();
                                string TH = PO_NO_S(MAHANGFCC).Rows[0]["TENHANG"].ToString();
                                string TTSLG = PO_NO_S(MAHANGFCC).Rows[0]["SLGIAO"].ToString();
                                int TTSL = int.Parse(PO_NO_S(MAHANGFCC).Rows[0]["SLGIAO"].ToString());
                                luuDQCQRCODE(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, SLTEMFCC, PO);
                                //LoadDOCQR();
                                LOAD();
                                txt_DOCQRCODE.Text = "";

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
                            MessageBox.Show("không tồn tại ! mã trong phiếu giao ! ", "Thông Báo FCC",
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
                // Tem YMVN check
                else
                {
                    Cty = "YMVN";
                    TachQRYMVN(QRcode);
                    if (KTODER(ODERYMVN, PARTYMVN) == true)
                    {
                        if (KTHANGDABAN_DANGBANQR(PARTYMVN, SLYMVN, Cty) == true)
                        {
                            //ADDGRIDV(Cty, STTBAN, QRcode, PARTYMVN, SLYMVN, "", "OK");
                            luuDQCQRCODE(Cty, STTBAN, "", PARTYMVN, SLYMVN, ODERYMVN);
                            LOAD();
                            txt_DOCQRCODE.Text = "";
                        }
                        else
                        {
                            MessageBox.Show("Số lượng bắn đang vượt quá số lượng giao ! Hãy kiểm tra lại ", "Thông Báo FCC", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        }


                    }
                    else
                    {
                        MessageBox.Show("Không tồn tại trên phiếu đã chọn ! \n Oder No : " + ODERYMVN + "\n Hoặc Mã Hàng : " + PARTYMVN + " \n  Kiểm tra lại !", "Thông Báo FCC",
                                             MessageBoxButtons.OK,
                                             MessageBoxIcon.Error);
                    }

                    

                }

            }
        }

        private void txt_DOCQRCODE_TextChanged(object sender, EventArgs e)
        {

        }

        private void YAMAHAQRCDE_SP_Load(object sender, EventArgs e)
        {
            LOAD();
        }
        private void LOAD()
        {
            LoadDOCQR();
            string MH, PO, sql = "select STT, PO_NO,MAHANG,TENHANG,SLGIAO,'' as SLBANFCC,'' as SLBANYMVN from SP_TMPPHIEUGIAOHANG";
            int SLG, SLBAN;
            TB_PGH = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCTTDH.DataSource = TB_PGH;
            for (int i = 0; i < GridVTTDH.RowCount; i++)
            {
                PO = GridVTTDH.GetRowCellValue(i, "PO_NO").ToString();
                MH = GridVTTDH.GetRowCellValue(i, "MAHANG").ToString();
                SLG = int.Parse(GridVTTDH.GetRowCellValue(i, "SLGIAO").ToString());
                sql = "select case when sum(SLTEM) IS NULL then 0 else sum(SLTEM) end as SLBAN from SP_DOCQRCODE  where MAHANG = '" + MH + "' and PONO = '" + PO + "'";
                SLBAN = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                GridVTTDH.SetRowCellValue(i, "SLBANFCC", SLBAN);
                sql = "select case when sum(SLTEM) IS NULL then 0 else sum(SLTEM) end as SLBAN from SP_DOCQRCODE  where MAHANG = '" + MH.Replace("-","") + "' and PONO = '" + PO + "'";
                SLBAN = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                GridVTTDH.SetRowCellValue(i, "SLBANYMVN", SLBAN);
            }
            
        }
        public BindingList<Entry> SampleDS()
        {
            BindingList<Entry> ds = new BindingList<Entry>();
            
            ds.AllowNew = true;
            return ds;
        }

      

        private void CMD_HOANTHANH_Click(object sender, EventArgs e)
        {
            this.Close();
            GIAOHANGYMN FDOC = new GIAOHANGYMN();
            FDOC.Show();
        }

        private void GridVTTDH_RowCellStyle_2(object sender, RowCellStyleEventArgs e)
        {
            string sql, LOTSLXUAT = "";
            DataTable tb = new DataTable();
            GridView View = sender as GridView;
            if (e.RowHandle >= 0)
            {
                string STT = View.GetRowCellDisplayText(e.RowHandle, View.Columns["STT"]);
                string PO = View.GetRowCellDisplayText(e.RowHandle, View.Columns["PO_NO"]);
                string MH = View.GetRowCellDisplayText(e.RowHandle, View.Columns["MAHANG"]);
                string SLBFCC = View.GetRowCellDisplayText(e.RowHandle, View.Columns["SLBANFCC"]);
                string SLBYMVN = View.GetRowCellDisplayText(e.RowHandle, View.Columns["SLBANYMVN"]);
                string SLX = View.GetRowCellDisplayText(e.RowHandle, View.Columns["SLGIAO"]);
                if ((int.Parse(SLBFCC) != int.Parse(SLX)) || (int.Parse(SLBYMVN) != int.Parse(SLX)) || (int.Parse(SLBYMVN) != int.Parse(SLBFCC)))
                {
                    e.Appearance.BackColor = Color.Red;
                    e.Appearance.BackColor2 = Color.SeaShell;
                    sql = "update SP_TMPPHIEUGIAOHANG set LOTNO = '',STATUSFCC = 'NG',STATUSYMVN= 'NG',STATUS = 'NG' where STT = " + int.Parse(STT) + "";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);

                }
                else
                {

                    e.Appearance.BackColor = Color.Green;
                    e.Appearance.BackColor2 = Color.SeaShell;
                    sql = "select LOTNO,sum(SLTEM) as SLLOT from SP_DOCQRCODE where MAHANG = '" + MH + "' and PONO = '" + PO + "'  group by LOTNO";
                    tb = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
                    if (tb.Rows.Count > 0)
                    {
                        for (int j = 0; j < tb.Rows.Count; j++)
                        {
                            string IDM, LTX = tb.Rows[j]["LOTNO"].ToString();
                            if(LTX.Length > 15 )
                            {
                                LTX = LTX.Substring(0, 13);
                                IDM = int.Parse(LTX.Substring(6, 5)).ToString();
                                LTX = LTX.Substring(0, 6) + IDM + LTX.Substring(11, 2);
                            }
                            if(LTX.Substring(LTX.Length-1,1)== "0")
                            {
                                LTX = LTX.Substring(0, LTX.Length - 1);
                            }
                            if (LOTSLXUAT == "")
                            {
                                LOTSLXUAT = LTX + "-" + tb.Rows[j]["SLLOT"].ToString();
                            }
                            else
                            {
                                LOTSLXUAT = LOTSLXUAT + "," + LTX + "-" + tb.Rows[j]["SLLOT"].ToString();
                            }

                        }
                        if ((int.Parse(SLBFCC) == int.Parse(SLX)))
                        {
                            sql = "update SP_TMPPHIEUGIAOHANG set LOTNO = '" + LOTSLXUAT + "',STATUSFCC = 'OK' where STT = " + int.Parse(STT) + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }
                        else
                        {
                            sql = "update SP_TMPPHIEUGIAOHANG set LOTNO = '',STATUSFCC = 'NG' where STT = " + int.Parse(STT) + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }
                        if((int.Parse(SLBYMVN) == int.Parse(SLX)))
                        {
                            sql = "update SP_TMPPHIEUGIAOHANG set STATUSYMVN = 'OK' where STT = " + int.Parse(STT) + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }
                        else
                        {
                            sql = "update SP_TMPPHIEUGIAOHANG set STATUSYMVN = 'NG' where STT = " + int.Parse(STT) + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }
                        if ((int.Parse(SLBFCC) == int.Parse(SLX)) || (int.Parse(SLBYMVN) == int.Parse(SLX)) || (int.Parse(SLBYMVN) == int.Parse(SLBFCC)))
                        {
                            sql = "update SP_TMPPHIEUGIAOHANG set STATUS = 'NG' where STT = " + int.Parse(STT) + "";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }
                        
                    }
                }
            }
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            string STT = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "STT").ToString().Trim();
            gridVDOCQRCODE.DeleteSelectedRows();
            string sql = "delete SP_DOCQRCODE where STT =  " + int.Parse(STT) + "";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            LOAD();

        }

        private void CMD_XOA_Click(object sender, EventArgs e)
        {
            string sql = "delete SP_DOCQRCODE ";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            LOAD();
        }
    }
    public class Entry
    {
        public Entry() { }
        public Entry(string LOTNO, string MAHANG, string TENHANG,Int32 SLGIAO,Int32 SLBAN)
        {
            LOTNO = lot; MAHANG = mh; TENHANG = th; SLGIAO = slg; SLBAN = slb;
        }
        public string lot { get; set; }
        public string mh { get; set; }
        public string th { get; set; }

        public Int32 slg { get; set; }
        public Int32 slb { get; set; }
    }
}