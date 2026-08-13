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
using DevExpress.XtraGrid;
using DevExpress.Pdf.Native;
using PCTP.QRCODE_HVN.YMN;
using DevExpress.CodeParser;

namespace PCTP.YMN
{
    public partial class YMVNDOCQRCODE : DevExpress.XtraEditors.XtraForm
    {
        public YMVNDOCQRCODE()
        {
            InitializeComponent();
            LoadDOCQR();
        }
        
        IFSPROVIDER iFSPROVIDER = new IFSPROVIDER();
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public string PARTYMVN, ODERYMVN;
        public int SLYMVN;
        public static Boolean HT;
        private string _MAFCC;
        #region Load and DOCQRCODE
        private void TachQRYMVN(string QRYMVN)
        {
            PARTYMVN = "";
            SLYMVN = 0;
            ODERYMVN = "";
            int VTP, VTOR, VTSL;
            VTP = 0;
            VTOR = 0;
            VTSL = 0;
            for (int i = 0; i< QRYMVN.Length;i++)
            {
                if(QRYMVN[i].ToString() == "P")
                {
                    PARTYMVN = QRYMVN.Substring(i + 1, 14);
                    VTP = i + 1 + 14;
                    break;
                }

            }    
            for(int j = VTP;j< QRYMVN.Length; j++)
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
                MAHANGFCC = gridVDOCQRCODE.GetRowCellValue(i-1, "MAHANGFCC").ToString().Trim();
               

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
        private Boolean KTRAMA(string MaHang)
        {
            Boolean KQ = false;
            string sql = "select count(*) from YMVN_TMPPHIEUGIAOHANG where MAHANG = '" + MaHang + "'";
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
        private Boolean KTRAMA_Gear(string MaHang,string Gear,int SLTEM)
        {
            
            DataTable tbl = new DataTable();
            Boolean KQ = false;
            if (Gear != "")
            {
                int TTSLBAN=0, SLGear = 0;
                string sql = "select case when sum(sltemfcc) is null then 0 else sum(sltemfcc) end as SLBAN from YMVN_DOCQRCODE where MAHANGFCC = '" + MaHang + "' and Gear = '" + Gear + "'";

                int TTSLBANQR = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                sql = "select TTPHIEU from YMVN_TMPPHIEUGIAOHANG where MAHANG = '" + MaHang + "' and TTPHIEU like '%" + Gear + "%' Group by TTPHIEU ";
                string KQSQL;
                string[] SQLKQ;
                tbl = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

                if (tbl.Rows.Count > 0)
                {
                    for (int i = 0; i < tbl.Rows.Count; i++)
                    {
                        KQSQL = tbl.Rows[i]["TTPHIEU"].ToString();
                        SQLKQ = KQSQL.Split(',');
                        if (SQLKQ.Length > 1)
                        {
                            for (int j = 0; j < SQLKQ.Length; j++)
                            {
                                if (SQLKQ[j].Contains(Gear) == true)
                                {
                                    string[] SL = SQLKQ[j].Split(':');
                                    string SLTIMD = SL[1].Trim();
                                    SLGear = SLGear + int.Parse(SLTIMD.Substring(0, SLTIMD.Length - 3));
                                }
                            }
                        }
                        else
                        {
                            if (SQLKQ[0].Contains(Gear) == true)
                            {
                                string[] SL = SQLKQ[0].Split(':');
                                string SLTIMD = SL[1].Trim();
                                SLGear = SLGear + int.Parse(SLTIMD.Substring(0, SLTIMD.Length - 3));
                            }
                        }
                    }
                    if (TTSLBANQR + SLTEM > SLGear)
                    {
                        KQ = false;
                    }
                    else
                    {
                        KQ = true;
                    }

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
                string LOTHVN;
                if (gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "LOTHVN") != null)
                {
                    LOTHVN = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "LOTHVN").ToString();
                }
                else { LOTHVN = ""; }
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
        private Boolean KIEMTRATHUTUBANYMVN()
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
        private Boolean KTHANGDABAN_DANGBANQR(string MH, int SLBAN,string Gear)
        {
            Boolean KQ = true;
            int SLDABAN;
            string SL_DB, sql;
            int SLCANGIAO;
     
            DataTable TB_CONLAI = new DataTable();
            if (Gear == "")
            {
                sql = "select MAHANG,sum(SOLUONG) as SOLUONG ,STATUS  from  YMVN_TMPPHIEUGIAOHANG where ( STATUS <> '1' or STATUS is null ) and MAHANG = '" + MH + "' and TTPHIEU like '" + Gear + "'  group by MAHANG ,STATUS ";
            }
            else
            {
                sql = "select MAHANG,sum(SOLUONG) as SOLUONG ,STATUS  from  YMVN_TMPPHIEUGIAOHANG where ( STATUS <> '1' or STATUS is null ) and MAHANG = '" + MH + "' and TTPHIEU like '%" + Gear + "%'  group by MAHANG ,STATUS ";
            }
           
            TB_CONLAI = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            string sql1;
            if (TB_CONLAI.Rows.Count > 0)
            {
                SLCANGIAO = int.Parse(TB_CONLAI.Rows[0]["SOLUONG"].ToString());
               
                    sql1 = "select sum(SLTEMFCC) from YMVN_DOCQRCODE where MAHANGFCC = '" + MH + "'";
                
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
                string sql = "select SLTEMFCC from YMVN_DOCQRCODE where LOTHVN is null";

                SLTEMFCC = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb,sql));


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
        private Boolean KIEMTRATTRUNGTEM(string LOTFCC)
        {
            Boolean KQ = false;
           
            string sqlKT = "select count(*) from YMVN_DOCQRCODE where MAFCC = '" + LOTFCC + "'";
                if(int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlKT)) == 0 )
            {
                KQ = true;
            }    
                else
            { KQ = false; }    
            return KQ;
        }
        public static bool IsNumber(string pValue)
        {
            foreach (Char c in pValue)
            {
                if (!Char.IsDigit(c))
                    return false;
            }
            return true;
        }
        private Boolean KTODER(string ODER)
        {
            string sql;
            Boolean KQKT = false;
            if (IsNumber(ODER) == true)
            {
                sql = "select count(*) from YMVN_TMPPHIEUGIAOHANG where CUA = '" + int.Parse(ODER) + "'";
            }
            else
            {
                sql = "select count(*) from YMVN_TMPPHIEUGIAOHANG where CUA = '" + ODER + "'";
            }
            int TT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            if(TT==0)
            {
                KQKT = false;
            }
            else
            {
                KQKT = true;
            }
            return KQKT;
        }
       
        private void ADDGRIDV(string CTY, int STT, String LOT, String MAHANG, int SLTEM, string STTP, string KQ)
        {
            int i = gridVDOCQRCODE.RowCount;
            int rowHandle = gridVDOCQRCODE.GetDataRowHandleByGroupRowHandle(gridVDOCQRCODE.FocusedRowHandle);
            if (CTY == "FCC")
            {
                gridVDOCQRCODE.AddNewRow();
                
                // int i = gridVDOCQRCODE.RowCount;
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["STT"], STT);
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["LOTFCC"], LOT);
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["MAHANGFCC"], MAHANG);
                gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["SLTEMFCC"], SLTEM);
                //gridVDOCQRCODE.SetRowCellValue(GridControl.NewItemRowHandle, gridVDOCQRCODE.Columns["GIO"], STTP);
                gridVDOCQRCODE.UpdateCurrentRow();
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
                gridVDOCQRCODE.RefreshData();
            }
            //gridVDOCQRCODE.RefreshData();
        }

        private void txt_DOCQRCODE_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            try
            {
                string Cty = "";

                string LOTSLFCC, LOTFCC, LOTYMVN, MAHANGHVN, MHSL;

                string MAHANGFCC = "";

                int SLTEMFCC, SLTEMYMVN, Gear = 0;

                int STTBAN = 0;
                string sqlTIMSTTBAN, TIMSTTBAN, S_Gear = "";

                string QRcode = txt_DOCQRCODE.Text.Trim().ToUpper();
                if (e.KeyChar == 13)
                {
                    sqlTIMSTTBAN = "select max(STT) from YMVN_DOCQRCODE";
                    TIMSTTBAN = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlTIMSTTBAN);
                    if (TIMSTTBAN != "")
                    {
                        STTBAN = int.Parse(TIMSTTBAN);
                    }
                    //LISTV_BANQRCODE.Items[1].SubItems["LOTHVN"].Text == "")



                    string[] arrQRFCC = QRcode.Split(':');

                    if (arrQRFCC.Length == 4)
                    {
                        if (KIEMTRATHUTUBANFCC() == true)
                        {

                            Cty = "FCC";
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
                                    int kt = LOTSLFCC.Length;
                                    if (LOTSLFCC.Length == 26)
                                    {
                                        string LOT = LOTSLFCC.Substring(0, LOTSLFCC.Length - 14);
                                        int PartCode = int.Parse(LOT.Substring(6, 5));
                                        LOTFCC = LOTSLFCC;

                                    }
                                    else
                                    {
                                        if (LOTSLFCC.Length == 27)
                                        {

                                            LOTFCC = LOTSLFCC.Substring(0, 13);
                                            //int PartCode = int.Parse(LOTFCC.Substring(6, 5));
                                            //LOTFCC = LOTSLFCC.Substring(0, 6) + PartCode + LOTFCC.Substring(LOTFCC.Length - 1, 1);
                                            //Gear = int.Parse(LOTSLFCC.Substring(12, 1));
                                            //if (Gear == 0)
                                            //{
                                            //    LOTFCC = LOTFCC;
                                            //}
                                            //else
                                            //{
                                            //    LOTFCC = LOTFCC + Gear.ToString();
                                            //}

                                            string sql = "select Name from B20Gear where Code = " + Gear + "";
                                            S_Gear = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                                        }
                                        else
                                        {
                                            if (LOTSLFCC.Length == 28)
                                            {
                                                LOTFCC = LOTSLFCC.Substring(0, 13);
                                                int PartCode = int.Parse(LOTFCC.Substring(6, 5));
                                                //LOTFCC = LOTSLFCC.Substring(0, 6) + PartCode + LOTFCC.Substring(LOTFCC.Length - 1, 1);
                                                Gear = int.Parse(LOTSLFCC.Substring(12, 1));
                                                //if (Gear == 0)
                                                //{
                                                //    LOTFCC = LOTFCC;
                                                //}
                                                //else
                                                //{
                                                //    LOTFCC = LOTFCC + Gear.ToString();
                                                //}

                                                string sql = "select Name from B20Gear where Code = " + Gear + "";
                                                S_Gear = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                                            }
                                            else
                                                if (kt > 29)
                                            {
                                                LOTFCC = LOTSLFCC.Substring(0, 13);
                                            }
                                            else
                                                LOTFCC = LOTSLFCC.Substring(0, 13);

                                        }
                                    }

                                }
                                else
                                {
                                    if (LOTSLFCC.Length == 22)
                                    {
                                        string LOT = LOTSLFCC.Substring(1, LOTSLFCC.Length - 10);
                                        string PartCode = LOT.Substring(7, 5);
                                        LOTFCC = LOTSLFCC;

                                    }
                                    else 
                                    {

                                        //string[] arrQRFCC_GHEP1 = arrQRFCC_GHEP[0].Split('-');
                                        //if (arrQRFCC_GHEP1[0].Length == 10)
                                        //    Gear = 0;
                                        //if (arrQRFCC_GHEP1[0].Length == 11)
                                        //        Gear = int.Parse(arrQRFCC_GHEP1[0].Substring(10, 1));
                                        //if (arrQRFCC_GHEP1[0].Length > 11)
                                        //    Gear = int.Parse(arrQRFCC_GHEP1[0].Substring(12, 1));
                                        //string sql = "select Name from B20Gear where Code = " + Gear + "";
                                        //S_Gear = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                                        LOTFCC = LOTSLFCC;
                                    }
                                }

                                if (KTRAMA(MAHANGFCC) == true)
                                {
                                    if (KTRAMA_Gear(MAHANGFCC, S_Gear, SLTEMFCC) == true)
                                    {
                                        if (KTHANGDABAN_DANGBANQR(MAHANGFCC, SLTEMFCC, S_Gear) == true)
                                        {
                                            //FRM_LISTRUNGMSL.MAHANG = "";

                                            //ADDGRIDV(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, SLTEMFCC, "", "");

                                            luuDQCQRCODE(Cty, STTBAN + 1, LOTFCC, MAHANGFCC, LOTSLFCC, SLTEMFCC, "", "", S_Gear);
                                            LoadDOCQR();
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

                                        MessageBox.Show("không tồn tại hoặc số lượng vượt quá  : \n Mã : " + MAHANGFCC + " \n Sử dụng Gear : " + S_Gear + " \n trong phiếu giao ! ", "Thông Báo FCC",
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
                    // Tem YMVN check
                    else
                    {
                        if (KIEMTRATHUTUBANYMVN() == true)
                        {

                            Cty = "YMVN";
                            TachQRYMVN(QRcode);
                            if (KTODER(ODERYMVN) == true)
                            {
                                if (KIEMTRATT2TEMMA(PARTYMVN) == true)
                                {
                                    _MAFCC = PARTYMVN;
                                    //if (KIEMTRATT2TEMSL(SLYMVN) == true)
                                    //{
                                    //ADDGRIDV(Cty, STTBAN, QRcode, PARTYMVN, SLYMVN, "", "OK");
                                    luuDQCQRCODE(Cty, STTBAN, ODERYMVN, _MAFCC, PARTYMVN, SLYMVN, "", "OK", "");
                                    LoadDOCQR();
                                    txt_DOCQRCODE.Text = "";
                                    //}
                                    //else
                                    //{
                                    //    DialogResult re = MessageBox.Show("Số lượng TEM không khớp bạn có muốn nhập ? ! ", "Thông Báo FCC",
                                    //          MessageBoxButtons.YesNo,
                                    //          MessageBoxIcon.Warning);
                                    //    if (re == DialogResult.Yes)
                                    //    {
                                    //        //ADDGRIDV(Cty, STTBAN + 1, QRcode, PARTYMVN, SLYMVN, "", "KHAC SLTEM");
                                    //        luuDQCQRCODE(Cty, STTBAN + 1, ODERYMVN, _MAFCC, PARTYMVN, SLYMVN, "", "KHAC SLTEM","");
                                    //        LoadDOCQR();
                                    //        txt_DOCQRCODE.Text = "";
                                    //    }

                                    //}

                                }
                                else
                                {
                                    MessageBox.Show("Mã Hàng Tem YMVN không khớp với FCC ! ", "Thông Báo FCC",
                                                     MessageBoxButtons.OK,
                                                     MessageBoxIcon.Error);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Không tồn tại Oder No : " + ODERYMVN + " trên phiếu đã chọn ! Kiểm tra lại !", "Thông Báo FCC",
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
            catch
            {
                MessageBox.Show("Có lỗi sảy ra !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                YMVNDOCQRCODE YM = new YMVNDOCQRCODE();
                YM.Show();
            }
        }
 private void LoadDOCQR()
        {
            string sql;
            DataTable Tbl_QR;
            sql = "select * from YMVN_DOCQRCODE order by STT asc";
            Tbl_QR= sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
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
        private void luuDQCQRCODE(string CTY, int STT, String LOT, string MAHANG, String MAFCC, int SLTEM, string STTP, string KQ,string Gear)
        {
            string sql;
            if (CTY == "FCC")
            {
                sql = "insert into YMVN_DOCQRCODE (STT,LOTFCC,MAHANGFCC,MAFCC,SLTEMFCC,GIO,Gear) " +
                "VALUES " +
                "('" + STT + "' , '" + LOT + "','" + MAHANG + "','" + MAFCC + "'," + SLTEM + ",'" + STTP + "','" + Gear + "')";
            }
            else
            {
                if (MAHANG == "22810KTL7402" || MAHANG == "22660KWB6014M1")
                {
                    sql = "update YMVN_DOCQRCODE set LOTHVN = '" + LOT + "',MAHANGHVN = '" + MAHANG + "',SLTEMHVN = " + SLTEM + ", STATUS = 1 , KETQUA = '" + KQ + "'" +
                " WHERE " +
                " REPLACE('" + MAFCC + "','-','') like '" + MAHANG + "%' and MAHANGHVN is null ";
                }
                sql = "update YMVN_DOCQRCODE set LOTHVN = '" + LOT + "',MAHANGHVN = '" + MAHANG + "',SLTEMHVN = " + SLTEM + ", STATUS = 1 , KETQUA = '" + KQ + "'" +
                  "WHERE " +
                 "REPLACE('" + MAFCC + "','-','') = '" + MAHANG + "' and MAHANGHVN is null ";
            }
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
        }

        #endregion

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            string STT = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "STT").ToString().Trim();
            string STTPHIEU = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "GIO").ToString().Trim();
            string M = gridVDOCQRCODE.GetRowCellValue(gridVDOCQRCODE.FocusedRowHandle, "MAHANGFCC").ToString().Trim();
            int TTSL;
            gridVDOCQRCODE.DeleteSelectedRows();
            string sql = "delete YMVN_DOCQRCODE where STT =  " + int.Parse(STT) + "";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            if (STTPHIEU != "")
            {
                sql = "select SOLUONG from YMVN_TMPPHIEUGIAOHANG where stt = " + int.Parse(STTPHIEU) + "";
                TTSL = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                if (STTPHIEU != "")
                {

                    //KTSLBANCUAMATRUNG(M, int.Parse(STTPHIEU), TTSL);
                }
            }
        }

        private void CMD_HOANTHANH_Click(object sender, EventArgs e)
        {

            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_Take_LotYMVN");
            #region /////////////////////////////////
            //string G = "''", LOTXUAT = "",sql1,sqlcheck,sql = "select *  from   YMVN_TMPPHIEUGIAOHANG where STATUS = 'NG'";
            //string[] LG,TAch_G,T_G;
            //DataTable DH = new DataTable();
            //DataTable QRCODE = new DataTable();
            //DataTable QRCODESUM = new DataTable();
            //DH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            //for (int i =0; i< DH.Rows.Count;i++)
            //{
            //    object  STTP = DH.Rows[i].Field<Int16>("STT");
            //    object MH = DH.Rows[i].Field<string>("MAHANG");
            //    string IDMH = "select ID from B20Item where code = '" + MH.ToString() + "'";
            //    if (MH.ToString()== "BBN-E7601-00-00-80")
            //    { int checkii = 1; }    
            //    object SLG = DH.Rows[i].Field<Int16>("SOLUONG");
            //    object Gear = DH.Rows[i].Field<string>("TTPHIEU");
            //    object PO = DH.Rows[i].Field<string>("CUA");
            //    if(PO.ToString().Length < 5 )
            //    {
            //        if (PO.ToString().Length == 4)
            //            PO = "0" + PO;
            //        if(PO.ToString().Length==3)
            //            PO = "00" + PO;
            //        if (PO.ToString().Length == 2)
            //            PO = "000" + PO;
            //        if (PO.ToString().Length == 1)
            //            PO = "0000" + PO;
            //    }    
            //    sqlcheck = "select count(*) from YMVN_DOCQRCODE where mahangfcc = '" + MH.ToString() + "' and LOTHVN is null ";
            //    sqlcheck = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlcheck);
            //    if (sqlcheck == "0")
            //    {
            //        if (Gear == "")
            //        {
            //            sql = "select sum(SLTEMFCC) from YMVN_DOCQRCODE where MAHANGFCC = '" + MH.ToString() + "' and Gear = '" + Gear + "' and LOTHVN  = '" + PO.ToString().Trim() + "'";
            //        }
            //        else
            //        {
            //            TAch_G = Gear.ToString().Split(',');
            //            if (TAch_G.Length == 1)
            //            {
            //                TAch_G = TAch_G[0].Split(':');
            //                G = "'" + TAch_G[0].Substring(TAch_G[0].Trim().Length - 1, 1) + "'";
            //            }
            //            else
            //            {
            //                for (int j = 0; j < TAch_G.Length; j++)
            //                {

            //                    T_G = TAch_G[j].Split(':');
            //                    if (G == "''")
            //                    {
            //                        G = "'" + T_G[0].Substring(T_G[0].Trim().Length - 1, 1) + "'";
            //                    }
            //                    else
            //                    {
            //                        G = G + "," + "'" + T_G[0].Substring(T_G[0].Trim().Length - 1, 1) + "'";
            //                    }
            //                }
            //            }
            //            //select case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13) end as LOT,sum(SLTEMFCC) from YMVN_DOCQRCODE group by case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13)
            //            sql = "select case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13) end as LOT,sum(SLTEMFCC) as SLB from YMVN_DOCQRCODE where MAHANGFCC = '" +
            //                MH.ToString() + "' and Gear in (" + G + ") and LOTHVN  = '" + PO.ToString().Trim() + "' group by case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13) end";
            //        }
            //        if (sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql) == "")
            //        {

            //        }
            //        else
            //        {
            //            int TTSL = 0;
            //            QRCODESUM = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            //            foreach (DataRow dt in QRCODESUM.Rows)
            //            {
            //                TTSL = TTSL + int.Parse(dt["SLB"].ToString());
            //            }


            //            //if (Gear == "")
            //            //{
            //            ////    sql = "select case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13) end as LOT,sum(SLTEMFCC) from YMVN_DOCQRCODE where MAHANGFCC = '" +
            //            ////MH.ToString() + "' and Gear in (" + G + ") and LOTHVN  = '" + PO.ToString().Trim() + "' group by case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13) end";
            //            //    sql1 = "update YMVN_DOCQRCODE set KETQUA = 'DG' where  MAHANGFCC = '" + MH.ToString() + "' and Gear = '" + Gear + "' and LOTHVN  = '" + PO.ToString().Trim() + "' ";
            //            //}
            //            //else
            //            //{
            //            //    //sql = "select case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13) end as LOT,sum(SLTEMFCC) from YMVN_DOCQRCODE where MAHANGFCC = '" +
            //            //    //MH.ToString() + "' and Gear in (" + G + ") and LOTHVN  = '" + PO.ToString().Trim() + "' group by case when(CHARINDEX(',', LOTFCC, 1) > 1) then LOTFCC else SUBSTRING(LOTFCC, 1, 13) end";
            //            //    sql1 = "update YMVN_DOCQRCODE set KETQUA = 'DG' where  MAHANGFCC = '" + MH.ToString() + "' and Gear in (" + G + ") and LOTHVN  = '" + PO.ToString().Trim() + "'";
            //            //}

            //            //QRCODE = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            //            //string LT1,SL1,  LOTXUAT = "";
            //            //string[] LG,LG1;
            //            //object LOTFCC;
            //            //for (int j = 0; j < QRCODE.Rows.Count; j++)
            //            //{
            //            //    int dd = QRCODE.Rows[j].Field<string>("LOTFCC").Trim().Length;
            //            //    if (dd < 13)
            //            //    {
            //            //        string sqlidmh = "select STUFF('00000', 5-LEN(Id)+1, LEN(Id), Id) from B20Item where Code = '" + MH.ToString() + "'";
            //            //        sqlidmh = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlidmh);
            //            //        LOTFCC = QRCODE.Rows[j].Field<string>("LOTFCC").Trim().Substring(0, 6) + sqlidmh + QRCODE.Rows[j].Field<string>("LOTFCC").Trim().Substring(dd - 2, 2);
            //            //    }
            //            //    else
            //            //    {
            //            if (int.Parse(SLG.ToString()) == TTSL)
            //            {

            //                foreach (DataRow dt in QRCODESUM.Rows)
            //                {
            //                    LG = dt["LOT"].ToString().Trim().Split(',');

            //                    if (LG.Length > 1)
            //                    {

            //                        int SL,vt,vtt; string LOT_LOT = "";
            //                        foreach (var LOTINLOT in LG)
            //                        {
            //                            string[] LOTSL = LOTINLOT.Split('-');
            //                            if (LOTXUAT == "")
            //                            {
            //                                LOTXUAT = dt["LOT"].ToString();

            //                            }
            //                            else
            //                            {
            //                                vt = LOTXUAT.IndexOf(",");
            //                                vtt = LOTXUAT.IndexOf(LOTSL[0].ToString());
            //                                if (vt > 0)
            //                                {
            //                                    SL = int.Parse(LOTXUAT.Substring(vtt+LOTSL[0].Length, LOTXUAT.Length - vtt+ LOTSL[0].Length + 1));
            //                                }
            //                                else
            //                                    SL = int.Parse(LOTXUAT.Substring(LOTSL[0].Length, LOTXUAT.Length - LOTSL[0].Length + 1));
            //                               // if(vtt>0)
            //                                // LOTXUAT = LOTXUAT.Trim().Replace();
            //                            }
            //                        }
            //                    }
            //                    else
            //                    {
            //                        //object SLOFLOT = QRCODE.Rows[j].Field<int>("SL");
            //                        //if (LOTXUAT == "")
            //                        //    LOTXUAT = LOTFCC.ToString().Substring(0, 13).Trim() + "-" + SLOFLOT.ToString().Trim();
            //                        //else
            //                        //    LOTXUAT = LOTXUAT.Trim() + "," + LOTFCC.ToString().Substring(0, 13).Trim() + "-" + SLOFLOT.ToString().Trim();
            //                    }
            //                }
            //            }
            //                sql = "update YMVN_TMPPHIEUGIAOHANG set LOT = '" + LOTXUAT + "' where STT = " + int.Parse(STTP.ToString()) + " ";
            //                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            //                //sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql1);
            //        }

            //    }
            //    else
            //    {

            //    }    
            //}
            #endregion  //////////////////////
            this.Close();
            GIAOHANGYMN GH = new GIAOHANGYMN();
            
            GH.Show();
            HT = true;
        }
       
        private void CMD_XOA_Click(object sender, EventArgs e)
        {
            string sql = "delete SP_DOCQRCODE ";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
            gridCtrDOCQrCODE.RefreshDataSource();
        }
    }
}