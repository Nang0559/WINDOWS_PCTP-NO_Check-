using DevExpress.Export.Xl;
using DevExpress.Spreadsheet;
using DevExpress.XtraSpreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraReports.UI;
using DevExpress.XtraPrinting;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraPrintingLinks;
using System.Data.Odbc;
using DevExpress.Spreadsheet;
using PCTP.ClassSQL;
using PCTP.QRCODE_HVN.Report;
using Excel_12 = Microsoft.Office.Interop.Excel;
using System.Collections;

namespace PCTP.REQUEST_LK
{
    public partial class RequestLK : DevExpress.XtraEditors.XtraForm
    {
        public RequestLK()
        {
            InitializeComponent();
        }
        IFSPROVIDER IFS = new IFSPROVIDER();
        DataTable PGH = new DataTable();
        DataTable KH = new DataTable();
        private void buttonEdit1_EditValueChanged(object sender, EventArgs e)
        {
            //ReportPrintTool printTool = new ReportPrintTool(report);
            //printTool.ShowPreviewDialog();
        }

        private void button1_Click(object sender, EventArgs e)
        {


        }

        private void button1_Click_1(object sender, EventArgs e)
        {


            
        }

        private void dateNG_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter )
            {
                
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void RequestLK_Load(object sender, EventArgs e)
        {
            string sql = " select CUSTOMER_ID,Name as Cus_Name from CUSTOMER_INFO";
            KH = IFS.ExecuteQuery(sql);
            lookUpKH.Properties.DataSource = KH;
            lookUpKH.Properties.ValueMember = "CUSTOMER_ID";
            lookUpKH.Properties.DisplayMember = "CUSTOMER_ID";
            lookUpKH.EditValue = "300002";
            lookUpKH.Text = "Chọn Mã KH"; 
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {

            string N, N_XH, TUNGAY, DENNGAY;
            string[] NDN;
            N = textENDN.Text;
            NDN = N.Split('-');
            N_XH = dateNG.Text;
            TUNGAY = NDN[0] + N_XH + "000000";
            DENNGAY = NDN[1] + N_XH + "235959";

            //dateNX.Properties.DisplayFormat.FormatString = "ddmmyyyy";
            // try
            //{

            string sql = " select ROWNUM as STT ,Cus_Name,Cus_Part,PART_NO,Part_name,SL,Ngay_Giao,ORDER_NO,customer_po_no from " +
            "(select " +
            "t2.ORDER_NO,SUPPLIER_API.GET_Vendor_Name(t3.VENDOR_NO) as Cus_Name,t2.CUSTOMER_PART_NO as Cus_Part,t1.PART_NO,t1.DESCRIPTION as Part_name " +
            ", t2.WANTED_DELIVERY_DATE as Ngay_Giao,t2.BUY_QTY_DUE as SL,t3.VENDOR_NO,t2.CUSTOMER_PO_LINE_NO,t2.customer_po_no " +
            " from INVENTORY_PART t1, CUSTOMER_ORDER_JOIN t2 , PURCHASE_PART_SUPPLIER t3 " +
            " where " +
            " t2.part_no = T1.part_no and T1.part_no = t3.part_no and " +
              " OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) and t1.CONTRACT = 'VN2W' and" +
              " t2.WANTED_DELIVERY_DATE between to_date( '" + TUNGAY + "', 'DDMMYYYYHH24:MI:SS' ) and to_date( '" + DENNGAY + "', 'DDMMYYYYHH24:MI:SS' ) " +
              " and TYPE_CODE_DB = (select INVENTORY_PART_TYPE_API.Encode('Purchased') from dual) and t2.CUSTOMER_NO = '" + lookUpKH.EditValue + "' " +
              "order by t2.WANTED_DELIVERY_DATE) ";
            PGH = IFS.ExecuteQuery(sql);
            gridControl1.DataSource = PGH;

            //}
            //catch
            //{
            //    MessageBox.Show("Có lỗi hãy kiểm tra lại điều kiện nhập vào !", "Thông Báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {

            Report report = new Report();
            report.DataSource = PGH;
            ReportPrintTool printTool = new ReportPrintTool(report);
            printTool.ShowPreviewDialog();
        }
    }
}