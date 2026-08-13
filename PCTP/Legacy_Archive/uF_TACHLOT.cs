using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data.Common;
using System.Data.SqlTypes;
using Tutorial.SqlConn;
using PCTP.ClassSQL;
using PCTP.QRCODE_HVN;
using DevExpress.XtraReports.UI;
using DevExpress.Xpf;

using System.IO;
using System.Xml.Linq;
using MyValidation;
namespace PCTP
{
    public partial class UF_TACHLOT : ValidatedForm
    {
        public UF_TACHLOT()
        {

            InitializeComponent();
         
 //           validator.AddRule("PCTP.Rules.RuleSet.xml");
        }
        public int Maxvalue;
        public int STT;
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public  int DKT(string CHUOI, string KT)
        {
            int strt = 0;
            int cnt = -1;
            int idx = -1;
            while (strt != -1)
            {
                strt = CHUOI.IndexOf(KT, idx + 1);
                cnt += 1;
                idx = strt;
            }
            return cnt;
        }
       

        
        private void UF_TACHLOT_Load()
        {
            
        }

        

        private void TXT_QRCODE_KeyDown(object sender, KeyEventArgs e)
        {
            
            if (e.KeyCode == Keys.Enter )
            {

                if (DKT(TXT_QRCODE.Text, ":") == 3)
                {

                    string QRFCC = TXT_QRCODE.Text;
                    string[] ARRSTR = QRFCC.Split(':');
                    string LOT = "";
                    if (ARRSTR[0].Length == 27)
                    {
                        LOT = ARRSTR[0].Substring(0, ARRSTR[0].Length - 4);
                    }
                    //else if (ARRSTR[0].Length == 28)
                    //{
                    //    LOT = ARRSTR[0].Substring(0, ARRSTR[0].Length - 4);
                    //}
                    else
                    {
                        LOT = ARRSTR[0].Substring(0, ARRSTR[0].Length - ARRSTR[3].Length);
                    }
                    string FCCPart_NO1 = ARRSTR[1];
                    string FCCPart_NO = ARRSTR[1].Replace("-", "");
                    string NSX = ARRSTR[2];
                    string SLTEMFCC = ARRSTR[3];
                    string SLLOT1 = "0";
                    string SLLOT2 = "0";
                    string SLLOT3 = "0";
                    
                    ADDTOLIST(LOT, FCCPart_NO1, NSX, SLTEMFCC,  SLLOT1,  SLLOT2,  SLLOT3);
                    TXT_QRCODE.Text = "";
                }
                else
                {
                    MessageBox.Show( "KHÔNG ĐÚNG ĐỊNH DẠNG !","THÔNG BÁO",MessageBoxButtons.OK,MessageBoxIcon.Error);
                }

            }
        }
        private void ADDTOLIST(string LOT, string FCCPart_NO1, string NSX, string SLTEM, string SLLOT1, string SLLOT2, string SLLOT3)
        {
            
            int STT = lw_qrcode.Items.Count;
            ListViewItem lw_docqrcode = new ListViewItem((STT+1).ToString());
            ListViewItem.ListViewSubItem dl1 = new ListViewItem.ListViewSubItem(lw_docqrcode,LOT.ToString());
            ListViewItem.ListViewSubItem dl2 = new ListViewItem.ListViewSubItem(lw_docqrcode, FCCPart_NO1.ToString());
            ListViewItem.ListViewSubItem dl3 = new ListViewItem.ListViewSubItem(lw_docqrcode, NSX.ToString());
            ListViewItem.ListViewSubItem dl4 = new ListViewItem.ListViewSubItem(lw_docqrcode, SLTEM.ToString());
            ListViewItem.ListViewSubItem dl5 = new ListViewItem.ListViewSubItem(lw_docqrcode, SLLOT1.ToString());
            ListViewItem.ListViewSubItem dl6 = new ListViewItem.ListViewSubItem(lw_docqrcode, SLLOT2.ToString());
            ListViewItem.ListViewSubItem dl7 = new ListViewItem.ListViewSubItem(lw_docqrcode, SLLOT3.ToString());
            lw_docqrcode.SubItems.Add(dl1);
            lw_docqrcode.SubItems.Add(dl2);
            lw_docqrcode.SubItems.Add(dl3);
            lw_docqrcode.SubItems.Add(dl4);
            lw_docqrcode.SubItems.Add(dl5);
            lw_docqrcode.SubItems.Add(dl6);
            lw_docqrcode.SubItems.Add(dl7);
            lw_qrcode.Items.Add(lw_docqrcode);

            
            //var item2 = new ListViewItem(new[] { STT,LOT, FCCPart_NO1, NSX, SLTEM, SLLOT1, SLLOT2, SLLOT3 });

            //w_qrcode.Items.Add(item2);
            
            
             lw_qrcode.Refresh();
        }

        private void UF_TACHLOT_Load(object sender, EventArgs e)
        {
            RDO_2.Checked=true;
            lw_qrcode.Items.Clear();
            lw_qrcode.View = View.Details;
            lw_qrcode.Columns.Add("STT", 50);
            lw_qrcode.Columns.Add("LOT NO", 200);
            lw_qrcode.Columns.Add("MA SP", 170);
            lw_qrcode.Columns.Add("NGAY SAN XUAT", 100);
            lw_qrcode.Columns.Add("SL LOT", 100);
            lw_qrcode.Columns.Add("SL LOT 1", 100);
            lw_qrcode.Columns.Add("SL LOT 2", 100);
            lw_qrcode.Columns.Add("SL LOT 3", 100);
            lw_qrcode.GridLines = true;
            lw_qrcode.FullRowSelect = true;
            lw_qrcode.View = View.Details;
        }

        private void lw_qrcode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (RDO_2.Checked == true)
            {
                if (lw_qrcode.SelectedItems.Count > 0)
                {
                    ListViewItem item = lw_qrcode.SelectedItems[0];
                    txt_sllot1.Text = item.SubItems[5].Text;
                    txt_sllot2.Text = item.SubItems[6].Text;
                    STT = Convert.ToInt32(item.SubItems[0].Text);
                    MaxVL.Text = item.SubItems[4].Text;
                }
                else
                {
                    txt_sllot1.Text = string.Empty;
                    txt_sllot2.Text = string.Empty;
                }
            }
            else
            {
                if (lw_qrcode.SelectedItems.Count > 0)
                {
                    ListViewItem item = lw_qrcode.SelectedItems[0];
                    txt_sllot1.Text = item.SubItems[5].Text;
                    txt_sllot2.Text = item.SubItems[6].Text;
                    txt_sllot3.Text = item.SubItems[7].Text;
                    STT = Convert.ToInt32(item.SubItems[0].Text);
                    MaxVL.Text = item.SubItems[4].Text;
                }
                else
                {
                    txt_sllot1.Text = string.Empty;
                    txt_sllot2.Text = string.Empty;
                    txt_sllot3.Text = string.Empty;
                }
            }
        }

        private void CMD_SUA_Click(object sender, EventArgs e)
        {
            //bool result = validator.Validate();
           // if (result == true)
            //{
                if (RDO_2.Checked == true)
                {

                    if (KTTEXT() == true)
                    {
                        if (lw_qrcode.SelectedItems.Count > 0)
                        {
                            ListViewItem item = lw_qrcode.SelectedItems[0];
                            item.SubItems[5].Text = txt_sllot1.Text;
                            item.SubItems[6].Text = txt_sllot2.Text;
                            STT = Convert.ToInt32(item.SubItems[0].Text);
                        }
                        else
                        {
                            txt_sllot1.Text = string.Empty;
                            txt_sllot2.Text = string.Empty;
                        }
                    }
                    else
                        MessageBox.Show("Không thể nhập số lượng lớn hơn số lượng hiện tại của TEM", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    if (KTTEXT() == true)
                    {
                        if (lw_qrcode.SelectedItems.Count > 0)
                        {
                            ListViewItem item = lw_qrcode.SelectedItems[0];
                            item.SubItems[5].Text = txt_sllot1.Text;
                            item.SubItems[6].Text = txt_sllot2.Text;
                            item.SubItems[7].Text = txt_sllot3.Text;
                            STT = Convert.ToInt32(item.SubItems[0].Text);
                        }
                        else
                        {
                            txt_sllot1.Text = string.Empty;
                            txt_sllot2.Text = string.Empty;
                            txt_sllot3.Text = string.Empty;
                        }
                    }
                    else
                        MessageBox.Show("Không thể nhập số lượng lớn hơn số lượng hiện tại của TEM", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
           // }
            //lw_qrcode. .SubItems[5].Text = txt_sllot1.Text;
            //lw_qrcode.Items[STT].SubItems[6].Text = txt_sllot2.Text;
        }
        private DataTable loadDATArt()
        {

            DataSet DTS = new DataSet();
            DTS = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_BarCodeView_ThongKe_Tmp5");
            return DTS.Tables[0];



        }
        private DataTable loadDATArtYMVN()
        {

            DataSet DTS = new DataSet();
            DTS = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_BarCodeView_ThongKe_Tmp5_YMVN");
            return DTS.Tables[0];



        }
        private string Dien0tolot(int sllot)
        {
            string KQ = "";
            int spt = sllot.ToString().Length;
            if(spt == 1)
            {
                KQ = "000" + sllot.ToString();
            }
            if (spt == 2)
            {
                KQ = "00" + sllot.ToString();
            }
            if (spt == 3)
            {
                KQ = "0" + sllot.ToString();
            }

            return KQ;
        }
        private void CMD_XUATDS_Click(object sender, EventArgs e)
        {
            string sql1;
            string sql2;
            string sql3;
            string MAHANG="";
            sql1 = "delete from TMPLOTTACH";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql1);
            
            foreach (ListViewItem it in lw_qrcode.Items)
            {
                int i = 1;
                string lotno = it.SubItems[1].Text;
                MAHANG = it.SubItems[2].Text;
                int sllot1 = Convert.ToInt32(it.SubItems[5].Text);
                string sl1 = Dien0tolot(sllot1);
                int sllot2 = Convert.ToInt32(it.SubItems[6].Text);
                string sl2 = Dien0tolot(sllot2);
               
                int sllot3 = Convert.ToInt32(it.SubItems[7].Text);
                string sl3 = Dien0tolot(sllot3);

             
                if (RDO_2.Checked == true)
                {
                    sql1 = "insert into TMPLOTTACH (STT,LOT,MAHANG,SL,flag) values (" + i + ",'" + lotno + sl1 + "','" + MAHANG + "'," + sllot1 + " , 0)";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql1);
                    sql2 = "insert into TMPLOTTACH(STT,LOT,MAHANG,SL,flag) values ( " + (i + 1) + ",'" + lotno + sl2 + "','" + MAHANG + "'," + sllot2 + " , 0)";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql2);
                }

                else
                {
                    sql1 = "insert into TMPLOTTACH (STT,LOT,MAHANG,SL,flag) values (" + i + ",'" + lotno + sl1 + "','" + MAHANG + "'," + sllot1 + " , 0)";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql1);
                    sql2 = "insert into TMPLOTTACH(STT,LOT,MAHANG,SL,flag) values ( " + (i + 1) + ",'" + lotno + sl2 + "','" + MAHANG + "'," + sllot2 + " , 0)";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql2);
                    sql3 = "insert into TMPLOTTACH(STT,LOT,MAHANG,SL,flag) values ( " + (i + 2) + ",'" + lotno  +sl3+ "','" + MAHANG + "'," + sllot3 + " , 0)";
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql3);

                }
                
                i ++;
            }

            string SQLCHECK = "select CustomerCode from B20ItemQuyCach where itemcode = '" + MAHANG + "'";
            SQLCHECK = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, SQLCHECK);
            if (SQLCHECK == "0100002")
            {
                PCTP.QRCODE_HVN.Report.GHEPLOT_YMVN report = new PCTP.QRCODE_HVN.Report.GHEPLOT_YMVN();
                report.DataSource = loadDATArtYMVN();
                ReportPrintTool printTool = new ReportPrintTool(report);
                printTool.ShowPreviewDialog();
            }
            else
            {
                PCTP.QRCODE_HVN.Report.GHEPLOT report = new PCTP.QRCODE_HVN.Report.GHEPLOT();
                report.DataSource = loadDATArt();
                ReportPrintTool printTool = new ReportPrintTool(report);
                printTool.ShowPreviewDialog();
            }
            
            
           // MessageBox.Show("XONG !", "THÔNG BÁO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CMD_XOA_Click(object sender, EventArgs e)
        {
            if (lw_qrcode.SelectedItems.Count > 0)
            {
                ListViewItem item = lw_qrcode.SelectedItems[0];
                STT = Convert.ToInt32(item.SubItems[0].Text);
                var result = MessageBox.Show("BẠN MUỐN XÓA ITEM THỨ : " + STT + "!", "THÔNG BÁO",
                             MessageBoxButtons.OK,
                             MessageBoxIcon.Error);
                if(result == DialogResult.OK  )
                {
                    
                        for (int i = 0; i < lw_qrcode.SelectedItems.Count; i++)
                        lw_qrcode.Items.Remove(lw_qrcode.SelectedItems[i]);

                   
                }
            }
            else
            {
                string MSG = "CHƯA CÓ ITEM NÀO ĐƯỢC CHỌN";
                MessageBox.Show(MSG ,"THÔNG BÁO");
            }
            
        }

        private void txt_sllot1_TextChanged(object sender, EventArgs e)
        {
            //if (txt_sllot1.Text == "")
            //{ 
            //    txt_sllot1.Text = "";
            //}
            //int SL = Convert.ToInt32(txt_sllot1.Text);
            //if (KTTEXT(SL) == true)
            //{

            //}
            //else
            //{
            //    MessageBox.Show("Không thể nhập số lượng lớn hơn số lượng hiện tại của TEM", "Thông báo!",MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    txt_sllot1.Text = "";
            //}
        }
        private Boolean KTTEXT()
        {
            int SL1, SL2, SL3;
            if (lw_qrcode.SelectedItems.Count > 0)
            {
                if (txt_sllot1.Text == "")
                {
                    SL1 = 0;
                }
                else
                {
                    SL1 = int.Parse(txt_sllot1.Text);
                }
                if (txt_sllot2.Text == "")
                {
                    SL2 = 0;
                }
                else
                {
                    SL2 = int.Parse(txt_sllot2.Text);
                }
                if (txt_sllot3.Text == "")
                {
                    SL3 = 0;
                }
                else
                {
                    SL3 = int.Parse(txt_sllot3.Text);
                }
                ListViewItem item = lw_qrcode.SelectedItems[0];
                int SLL = Convert.ToInt32(item.SubItems[4].Text);
                //Maxvalue= Convert.ToInt32(item.SubItems[4].Text);
                if (SL1+SL2+SL3 <= SLL)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void txt_sllot2_TextChanged(object sender, EventArgs e)
        {
            //if (txt_sllot2.Text == "")
            //{
            //    txt_sllot2.Text = "0";
            //}
            //int SL = Convert.ToInt32(txt_sllot2.Text);
            //if (KTTEXT(SL) == true)
            //{

            //}
            //else
            //{
            //    MessageBox.Show("Không thể nhập số lượng lớn hơn số lượng hiện tại của TEM", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    txt_sllot2.Text = "0";
            //}
        }

        private void txt_sllot3_TextChanged(object sender, EventArgs e)
        {
            //if (txt_sllot3.Text == "")
            //{
            //    txt_sllot3.Text = "0";
            //}
            //int SL = Convert.ToInt32(txt_sllot3.Text);
            //if (KTTEXT(SL) == true)
            //{

            //}
            //else
            //{
            //    MessageBox.Show("Không thể nhập số lượng lớn hơn số lượng hiện tại của TEM", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    txt_sllot3.Text = "0";
            //}
        }
    }
}
