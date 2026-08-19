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
using DevExpress.XtraGrid.Views.Grid;
using System.IO;
using DevExpress.XtraPrintingLinks;
using DevExpress.XtraPrinting;
using System.Diagnostics;
using PCTP.Acess_Image;
using DevExpress.XtraReports.Design;
using DevExpress.XtraRichEdit.Internal;

// Usage:


namespace PCTP
{
    public partial class NHAP_TP : DevExpress.XtraEditors.XtraForm
    {
        public NHAP_TP()
        {
            InitializeComponent();
        }
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public static string CASE_NO;
        public static string LOTNHAP = "",SPNhap="";
        public DateTime Ti = DateTime.Now; 
        void SetGridFont(GridView view, Font font)
        {
            foreach (AppearanceObject ap in view.Appearance)
                ap.Font = font;
        }

        private void NHAP_TP_Load(object sender, EventArgs e)
        {
            SetGridFont(gridVNHAPKHO, new Font("Courier New", 10));
            loadDLNHAPKHO();
        }
        private void loadDLNHAPKHO()
        {
            DataTable DLNHAPKHO = new DataTable();
            string sql = "select * from vNhapTP order by NGAY_SAN_XUAT DESC";
            DLNHAPKHO = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql);
            gridCTRNHAPKHO.DataSource = DLNHAPKHO;
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
                    link1.Component = gridCTRNHAPKHO;
                    complink.Links.Add(link1);
                    link.Component = gridCTRNHAPKHO;
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
        public string IDSP = "";
        private void TXT_DOCQRCODE_KeyPress(object sender, KeyPressEventArgs e)
        {
            string LOTNOSL,MH,sp;
            string LOT,LOTDD2="",LOTDD3="",LOTDD4="",LOTCH="",LOTCH2="";
            int SLSX;
            int SLSENHAP;
            int SLDANHAP;
            int TTSL;
            string KQ = "";
            TTSL = 0;
            Boolean TONTAI = true;

            if (e.KeyChar == 13)
            {
                string QRFCC = TXT_DOCQRCODE.Text.Trim();
                string[] arrQRFCC = QRFCC.Split(':');
                LOTNOSL = arrQRFCC[0];
                sp = arrQRFCC[5];
                if (arrQRFCC.Length > 5)
                {
                    SPNhap = arrQRFCC[3];
                }
                else
                {
                    SPNhap = "";
                }    
                MH = arrQRFCC[1];
                string sql = "select CustomerCode  from B20ItemQuyCach where ItemCode = '" + MH + "' ";
                KQ = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                SLSENHAP = int.Parse(arrQRFCC[3]);
                #region Xet TEM
                if (arrQRFCC.Length < 5)
                {
                    if (RDOLOAIHINHNHAP.Properties.Items[RDOLOAIHINHNHAP.SelectedIndex].AccessibleName == "NG")
                    {
                        CASE_NO = (arrQRFCC[0] + "4");
                    }
                    else
                    {
                        MessageBox.Show("Bạn đang nhâp tem thùng (tem thùng chỉ cho phép nhập lại NG)", "Thông Báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    CASE_NO = (arrQRFCC[0] + arrQRFCC[4]);
                }
                #endregion
                // Kiểm tra tồn tại case đã bắn
                string sql0 = "select count(*) from NHAP_TP_HIS where LOTCASE ='" + CASE_NO + "'";
                int tontai = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql0));
                //

               
                CMD_NHAPKHO.Enabled = true;


                string ID = "select STUFF('00000', 5-LEN(id)+1, LEN(id), id) from B20Item where code = '" + arrQRFCC[1] + "'";
                ID = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, ID);
                IDSP = ID;
                int IntID = int.Parse(IDSP);
                
                    string LOT1 = "";
                  
                        LOT1 = LOTNOSL.Substring(0, 6) + ID;

                    
                    
                    string Ca = LOTNOSL.Substring(LOT1.Length, 1);
                    string sqllinecode = "select STUFF('000', 3-LEN(B.Id)+1, LEN(B.Id), B.Id) as Id from B30AccDoc A,B20Lines B where B.Code = A.LinesCode and  stt = '"+sp+"'";
                    string Linescode = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqllinecode);
                   ///
                    string sqlc = "select  STUFF('0000', 4-LEN(A.MachinesCode)+1, LEN(A.MachinesCode), A.MachinesCode) as Dept, GearCode,ShiftCode from B30AccDoc A where  stt = '" + sp + "'";
                    DataTable tbsql = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sqlc);
                    object dept = tbsql.Rows[0]["Dept"];
                    object GearCode = tbsql.Rows[0]["GearCode"];
                    object Casx = tbsql.Rows[0]["ShiftCode"];
                    string Gear = LOTNOSL.Substring(LOT1.Length + 1, 1);

                    //if (int.Parse(Gear) == 0)
                     //   Gear = "";
                    string BP = "", BP2 = "", BP3 = "", BP4 = "";
                    // xua ly bộ phận
                    BP = LOTNOSL.Substring((LOTNOSL.Length - 8), 4);
                    LOTDD2 = LOTNOSL.Substring(0, 6) + IntID.ToString() + Ca + BP + Gear;
                    // dang 2
                    BP2 = LOTNOSL.Substring((LOT1.Length + 2), 4);
                    LOTDD3 = LOTNOSL.Substring(0, 6) + IntID.ToString() + Ca + BP2 + Gear;
                    // dang 3
                    BP3 = LOTNOSL.Substring((LOT1.Length + 5), 4);
                    LOTDD4 = LOTNOSL.Substring(0, 6) + IntID.ToString() + Ca + BP3 + Gear;
                    //dang khác
                    LOT = LOTNOSL.Substring(0, 6) + IntID.ToString() + Ca;
                    // dang chuan hoá :
                    LOTCH = LOTNOSL.Substring(0, 20);
                    LOTCH2 = LOT1 + Casx.ToString() + GearCode.ToString()+ Linescode + dept;
                    //string MCCode = LOTNOSL.Substring(LOT.Length, 2);
                    //LOT =  LOT + Convert.ToInt32(MCCode).ToString();
                //}

                int rowHandle = gridVNHAPKHO.LocateByValue("FIND", LOT);
                int rowHandle1 = gridVNHAPKHO.LocateByValue("FIND", LOTDD2);
                int rowHandle2 = gridVNHAPKHO.LocateByValue("FIND", LOTDD3);
                int rowHandle3 = gridVNHAPKHO.LocateByValue("FIND", LOTDD4);
                int rowHandle4 = gridVNHAPKHO.LocateByValue("FIND", LOTCH);
                int rowHandle5 = gridVNHAPKHO.LocateByValue("FIND", LOTCH2);
                int rh = -1;
                if (RDOLOAIHINHNHAP.Properties.Items[RDOLOAIHINHNHAP.SelectedIndex].AccessibleName == "N")

                {
                    if (rowHandle >= 0 || rowHandle1 >=0 || rowHandle2 >=0 || rowHandle3 >=0 || rowHandle4 >= 0 || rowHandle5 >=0 )
                    {

                        // sl da nhap
                        if(rowHandle>=0)
                        {
                            rh = rowHandle;
                        }    
                        else if(rowHandle1 >=0)
                        { rh = rowHandle1; }
                        else if (rowHandle2 >= 0)
                        { rh = rowHandle2; }
                        else if( rowHandle3 >= 0)
                        { rh = rowHandle3; }
                        else
                            if(rowHandle4 >= 0)
                        { rh = rowHandle4; }
                        else
                            if (rowHandle5 >= 0)
                        { rh = rowHandle5; }



                        if (gridVNHAPKHO.GetRowCellValue(rh, "SL_DA_NHAP").ToString() == "")
                            SLDANHAP = 0;
                        else
                        {
                            SLDANHAP = int.Parse(gridVNHAPKHO.GetRowCellValue(rh, "SL_DA_NHAP").ToString());
                        }
                        // sl san xuat
                        if (gridVNHAPKHO.GetRowCellValue(rh, "SL_DA_SAN_XUAT").ToString() == "")
                            SLSX = 0;
                        else
                        {
                            SLSX = int.Parse(gridVNHAPKHO.GetRowCellValue(rh, "SL_DA_SAN_XUAT").ToString());
                        }
                        // kiem tra lot va sl
                        if (KTTRUNGLIST(CASE_NO)  == false && tontai == 0)
                        {
                            if (SLDANHAP + SLSENHAP > SLSX)
                            {
                                var result = MessageBox.Show("Tổng số lượng nhập đang lớn hơn số lượng sản xuất . Bạn có muốn xác nhận lại ! ", "Thông Báo FCC",
                                          MessageBoxButtons.YesNo,
                                          MessageBoxIcon.Warning);
                                if (result == DialogResult.Yes)
                                {
                                    TXT_DOCQRCODE.Text = "";
                                    TXT_DOCQRCODE.Focus();
                                    return;
                                }

                            }



                            NHAPSL(SLSENHAP, gridVNHAPKHO, rh);
                            TXT_DOCQRCODE.Text = "";
                            TXT_DOCQRCODE.Focus();
                            SQLPROVIDER.c_Ns.Add(CASE_NO + Ti.ToString("yyMMddHHmmss"));

                        }
                        else
                        {
                           
                            //if (KTTRUNGLIST(CASE_NO) == true || tontai != 0)
                            //{
                            //    MessageBox.Show("không thể nhập do trùng case ", "Thông Báo FCC",
                            //                 MessageBoxButtons.OK,
                            //                 MessageBoxIcon.Error);

                            //    LOTDANHAP lOTDANHAP = new LOTDANHAP(CASE_NO);
                            //    lOTDANHAP.Show();
                            //    TXT_DOCQRCODE.Text = "";

                            //}
                        }
                        
                    }
                   
                    
                    else
                    {
                        MessageBox.Show("Không tồn tại phiếu nhập ", "Thông Báo FCC",
                                     MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);


                        TXT_DOCQRCODE.Text = "";

                    }// Khong ton tai phieu
                }
                else // Nhập lại NG
                {
                    
                        DataTable Tb_NG = new DataTable();
                        DataGridView NGL = new DataGridView();
                        string sqlKT = "select count(*) from STOCKTPTRAHANG where STATUS= 0 and lot= '" + LOT + "'";
                        int KT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlKT));
                    if (KT > 0)
                    {
                        string sqldlng = "select lot,NGAYTRA,SLTRA,slnhanlai,LY_DO_NG from STOCKTPTRAHANG where STATUS= 0 and lot= '" + LOT + "'";
                        Tb_NG = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sqldlng);
                        NGL.DataSource = Tb_NG;
                    
                        UF_NHAPLAI_NG UF_NGLIST = new UF_NHAPLAI_NG(LOT, NGL);
                        UF_NGLIST.ShowDialog();
                        string LD_NG = ALLVAR.LD_NG1;
                        int SLNHANLAI = ALLVAR.a;
                        string LOTNG = LOT + LD_NG;
                        int rowHandleNG = gridVNHAPKHO.LocateByValue("FIND", LOTNG);
                        if (rowHandleNG >=0 && SLNHANLAI >0)
                        {
                            //if (gridVNHAPKHO.GetRowCellValue(rowHandleNG, "SL_SE_NHAP").ToString() == "")
                            //{
                            //    SLSENHAP = 0 + SLNHANLAI;
                                
                            //}
                            //else
                            //{
                           
                            //    SLSENHAP = SLNHANLAI;
                            //}
                            NHAPSL(SLNHANLAI, gridVNHAPKHO, rowHandleNG);
                            TXT_DOCQRCODE.Text = "";
                            TXT_DOCQRCODE.Focus();
                            SQLPROVIDER.c_Ns.Add(CASE_NO + Ti.ToString("yyMMddHHmmss") );
                        }
                    }
                    else
                    {
                        MessageBox.Show("Không tồn tại phiếu nhập NG ", "Thông Báo FCC",
                                     MessageBoxButtons.OK,
                                     MessageBoxIcon.Error);


                        TXT_DOCQRCODE.Text = "";

                    }
                }
            }
            
        }
        public static Boolean KTTRUNGLIST(string CASE_NO)
        {
            Boolean KQ = false;
            foreach (string item in SQLPROVIDER.c_Ns)
            {
                string CSNO = item.Substring(0, item.Length - 12);
                if(CSNO.Contains(CASE_NO) == true)
                {

                    KQ = true;
                    break;
                }
                else
                {
                    KQ = false;
                }    
            }
            return KQ;
        }
        private void savedata()
        {
            LOTNHAP = "" ;
            int Status = 0;
            int solanupdate = 0;
            for (int i = 0; i < gridVNHAPKHO.DataRowCount; i++)
            {
                string LOT = gridVNHAPKHO.GetRowCellValue(i, "LOT_NO").ToString();
                if (gridVNHAPKHO.GetRowCellValue(i, "SL_SE_NHAP").ToString() != "")
                {
                    if (LOTNHAP.Length == 0)
                    {
                        LOTNHAP = "'" + LOT + "'";
                    }
                    else
                    {
                        LOTNHAP = LOTNHAP + "," + "'" + LOT + "'";
                    }
                    string    sql = "select count(*) from Stocktp where lot = '" + LOT + "'";
                    int KT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                    int SLSENHAP = int.Parse(gridVNHAPKHO.GetRowCellValue(i, "SL_SE_NHAP").ToString());
                    int SLDN = 0;
                    if (gridVNHAPKHO.GetRowCellValue(i, "SL_DA_NHAP").ToString() != "")
                      
                    { SLDN = int.Parse(gridVNHAPKHO.GetRowCellValue(i, "SL_DA_NHAP").ToString()); }
                    int SLSX = 0;
                    if (gridVNHAPKHO.GetRowCellValue(i, "SL_DA_SAN_XUAT").ToString() != "")
                    { SLSX = int.Parse(gridVNHAPKHO.GetRowCellValue(i, "SL_DA_SAN_XUAT").ToString()); }

                    string Model = gridVNHAPKHO.GetRowCellValue(i, "Model").ToString();
                    string part = gridVNHAPKHO.GetRowCellValue(i, "MA_SAN_PHAM").ToString();
                    string Name = gridVNHAPKHO.GetRowCellValue(i, "TEN_SAN_PHAM").ToString();
                    int CaSX = int.Parse(gridVNHAPKHO.GetRowCellValue(i, "CA_SAN_XUAT").ToString());
                    int SLNHAPOK = 0;
                    if (gridVNHAPKHO.GetRowCellValue(i, "SL_DA_NHAP").ToString() != "")
                    {
                        SLNHAPOK = int.Parse(gridVNHAPKHO.GetRowCellValue(i, "SL_DA_NHAP").ToString());
                    }
                    string LD_NG_NHAP = gridVNHAPKHO.GetRowCellValue(i, "LY_DO_TRA").ToString();
                    DateTime NGAYNHAP = DateTime.Now;
                    DateTime NGAYSX = Convert.ToDateTime(gridVNHAPKHO.GetRowCellValue(i, "NGAY_SAN_XUAT").ToString());
                    if (SLSENHAP + SLDN == SLSX || SLSENHAP== SLDN)
                    {
                        Status = 1;
                        
                    }
                    
                    if (KT > 0)
                    {
                        
                        sql = "UPDATE STOCKTP SET slnhap = (slnhap + " + SLSENHAP + "),SLCONLAI = (SLCONLAI + " + SLSENHAP + "),NGAYNHAP = cast(GETDATE() as smalldatetime),Satus = " + Status + " WHERE LOT = '" + LOT + "'";
                    }
                    else
                    {
                        sql = "INSERT INTO STOCKTP(LOT, MODEL, Part, NAME, CASX, NGAYSX, SLSX, NGAYNHAP, SLNHAP, NGAYXUAT, SLXUAT, SLCONLAI, Satus) VALUES('" + LOT + "'" + ", " + "'" + Model + "'" + ", " + "'" + part + "'" + ", " + "'" + Name + "'" + ", " + CaSX + ", " + "'" + NGAYSX.ToString("MM/dd/yyy") + "', " + SLSX + ", '" + NGAYNHAP.ToString("MM/dd/yyyy") + "', " + SLSENHAP + ", '" + NGAYNHAP.ToString("MM/dd/yyyy") + "', " + 0 + ", " + SLSENHAP + ",  " + Status + " )";
                     }
                    int TTNHAP = sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    solanupdate++;
                    if(RDOLOAIHINHNHAP.Properties.Items[RDOLOAIHINHNHAP.SelectedIndex].AccessibleName == "NG")
                    {
                        sql = "INSERT INTO STOCKTPNHANTRA (LOT,PART_NO,PART_NAME,NGAY_NHAN_TRA,SL_NHAN_TRA,LY_DO_NG) VALUES ( " + "'" + LOT + "'" + "," + "'" + part + "'" + "," + "'" + Name + "'" + "," + "'" + NGAYNHAP.ToString("MM/dd/yyyy") + "'" + "," + "'" + SLSENHAP + "'" + "," + "N'" + LD_NG_NHAP + "'" + ")";
                        int NHAPSTOCKNHANTRA = sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        string sqlcheckcolai = "select SLCONLAI  from STOCKTPTRAHANG where LOT= '" + LOT + "'";
                        int SLNGCONLAI = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqlcheckcolai));
                        int NG_STATUA = 1;
                        if (SLSENHAP == SLNGCONLAI)
                            NG_STATUA = 0;
                        sql = "UPDATE STOCKTPTRAHANG SET SLNHANLAI = SLNHANLAI + " + SLSENHAP + ", SLCONLAI = SLCONLAI - " + SLSENHAP + ", STATUS = " + NG_STATUA + " WHERE LOT = '" + LOT + "' and Ly_do_ng = N'" + LD_NG_NHAP + "'";
                        int UPDATESTOCKTRAHANG = sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    }
                    
                
                
                
                
                }
            }
            foreach (string item in SQLPROVIDER.c_Ns)
            {
                {
                    string sql = "insert into NHAP_TP_HIS (lotcase)  values ('" + item.Substring(0,item.Length-12) + "')";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                }
            }
            if (solanupdate > 0)
            {
                MessageBox.Show("Nhập thành công " + solanupdate + "LOT .", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SQLPROVIDER.c_Ns.Clear();
                loadDLNHAPKHO();
                TONKHOTP TK = new TONKHOTP(LOTNHAP);
                TK.ShowDialog();
                LOTNHAP = "";
            }
            else
            {
                MessageBox.Show("Bạn chưa chọn có thông tin để nhập !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private Boolean TONTAI(object rh)
        {
            Boolean TT = false;
            int rowHandle = (int)rh;
            if (gridVNHAPKHO.IsValidRowHandle(rowHandle))
                TT = true;
            else
                TT = false;
            return TT;
        }
        public void NHAPSL(int SLNHAP ,GridView view, int rowHandle)
        {
            if (gridVNHAPKHO.GetRowCellValue(rowHandle, "SL_SE_NHAP").ToString() != "")
            
                SLNHAP = SLNHAP + int.Parse(gridVNHAPKHO.GetRowCellValue(rowHandle, "SL_SE_NHAP").ToString());
            
                view.SetRowCellValue(rowHandle, "SL_SE_NHAP", SLNHAP);
            


        }
        private void MyGridView_RowStyle(object sender, RowStyleEventArgs e)
        {
            
        }
        
        private void gridVNHAPKHO_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            int quantity = 0;
            if (gridVNHAPKHO.GetRowCellValue(e.RowHandle, "SL_SE_NHAP").ToString() == "")
            {
                quantity = 0;
            }
            else
            {
                quantity = Convert.ToInt32(gridVNHAPKHO.GetRowCellValue(e.RowHandle, "SL_SE_NHAP"));
            }

            if (quantity > 0)
            {
                e.Appearance.BackColor = Color.Red;
                e.Appearance.ForeColor = Color.Pink;
                e.Appearance.Font = new Font("Arial", 12, FontStyle.Bold);
            }
            else
            {
                //e.Appearance.BackColor = Color.LightGreen;
            }

            //Override any other formatting  
            //e.h = true;
        }

        private void CMD_NHAPKHO_Click(object sender, EventArgs e)
        {
            FRMSHOW fRMSHOW = new FRMSHOW(IDSP);
            fRMSHOW.ShowDialog();
            savedata();
            
        }

        private void CMD_MOLOT_Click(object sender, EventArgs e)
        {
        //    MOLAILOT ML = new MOLAILOT();
        //    ML.Show();
        }

        private void CMD_REFESH_Click(object sender, EventArgs e)
        {
            PCTP.ClassSQL.SQLPROVIDER.c_Ns.Clear();
            loadDLNHAPKHO();
        }

        private void CMD_KTLOT_Click(object sender, EventArgs e)
        {
            int SOLANUPDATE = 0;
            for (int i = 0; i < gridVNHAPKHO.DataRowCount; i++)
            {
                if (gridVNHAPKHO.GetRowCellValue(i, "KET_THUC_LOT").ToString() != "")
                {
                    
                    if (int.Parse(gridVNHAPKHO.GetRowCellValue(i, "KET_THUC_LOT").ToString()) == 1)
                    {
                        string LOT = gridVNHAPKHO.GetRowCellValue(i, "LOT_NO").ToString();
                        string sql = "select count(*) from Stocktp where lot = '" + LOT + "'";
                        int KT = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                        if (KT > 0)
                        {
                            sql = "UPDATE STOCKTP SET SATUS = 1 WHERE LOT ='" + LOT + "'";
                            int kq = sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                            SOLANUPDATE++;
                        }
                    }
                }
            }
            if(SOLANUPDATE > 0)
            {
                MessageBox.Show("Kết thúc " + SOLANUPDATE + "LOT .", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                MessageBox.Show("Bạn chưa chọn LOT để kết thúc !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void cmd_Tonkho_Click(object sender, EventArgs e)
        {
            TONKHOTP TK = new TONKHOTP();
            TK.Show();
        }

        private void cmdEX_Click(object sender, EventArgs e)
        {
            exportToExcel();
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            FRMSHOW fRMSHOW = new FRMSHOW(IDSP);
            fRMSHOW.ShowDialog();
        }

        private void TXT_DOCQRCODE_TextChanged(object sender, EventArgs e)
        {

        }
    }
}