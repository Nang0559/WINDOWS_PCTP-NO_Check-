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
using PCTP;
using PCTP.ClassSQL;
using DevExpress.XtraReports.UI;

namespace PCTP.Giao_Hang_XK
{
    public partial class PGH_XK : DevExpress.XtraEditors.XtraForm
    {
        public PGH_XK()
        {
            InitializeComponent();
            dateENXH.DateTime = DateTime.Now;
            string sql = " select CUSTOMER_ID as ID,NAME as CusName from CUSTOMER_INFO where CUSTOMER_ID like '3%'";
            KH = IFS.ExecuteQuery(sql);
            lookUpKH.Properties.DataSource = KH;
            lookUpKH.Properties.ValueMember = "ID";
            lookUpKH.Properties.DisplayMember = "CUSNAME";
            lookUpKH.EditValue = "300002";
            lookUpKH.Text = "Chọn Mã KH";
        }
        IFSPROVIDER IFS = new IFSPROVIDER();
        DataTable KH = new DataTable();
       public static DataTable PGH = new DataTable();
        private void Load_DL()
        {
            string sql;
           
            // DL Khach Hàng
            
            // DL Ngày
            
            string NX = dateENXH.DateTime.ToString("ddMMyyyy");
            // L(
            if (RChonTK.SelectedIndex == 0)
            {
                ControlKH.Enabled = false;
                ControlKH.Appearance.BackColor = Color.LightYellow;
                sql = "select ROWNUM as STT, CUSTOMER_PO_NO,CUSTOMER_NO,CUSTOMER_NAME,CATALOG_NO,CUSTOMER_PART_NO,PART_NO,CATALOG_DESC,BUY_QTY_DUE,CUSTOMER_PART_UNIT_MEAS,WANTED_DELIVERY_DATE,DELIVERY_LEADTIME," +
                         "PICKING_LEADTIME,DELIVERY_TERMS,SHIP_VIA_CODE,PLANNED_DELIVERY_DATE,PLANNED_DUE_DATE,PLANNED_SHIP_DATE,ADDRESS " +
                            "from CUSTOMER_ORDER_JOIN  A,CUSTOMER_INFO_ADDRESS B  where " +
                            " A.CUSTOMER_NO = B.CUSTOMER_ID and A.SHIP_ADDR_NO = B.ADDRESS_ID and CUSTOMER_NO like '3%' and " +
                            " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual)) and to_char(PLANNED_SHIP_DATE, 'ddmmyyyy' ) = '" + NX + "' " +

                            " Order by STT,CUSTOMER_NO,CUSTOMER_PART_NO ";
            }
            else
            {
                ControlKH.Enabled = true;
                sql = "select ROWNUM as STT, CUSTOMER_PO_NO,CUSTOMER_NO,CUSTOMER_NAME,CATALOG_NO,CUSTOMER_PART_NO,PART_NO,CATALOG_DESC,BUY_QTY_DUE,CUSTOMER_PART_UNIT_MEAS,DELIVERY_LEADTIME," +
                         "PICKING_LEADTIME,DELIVERY_TERMS,SHIP_VIA_CODE,PLANNED_DELIVERY_DATE,PLANNED_DUE_DATE,PLANNED_SHIP_DATE " +
                            "from CUSTOMER_ORDER_JOIN  where CUSTOMER_NO = '" + lookUpKH.EditValue + "' and " +
                            " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual)) and to_char(PLANNED_SHIP_DATE, 'ddmmyyyy' ) = '" + NX + "' " +

                            " Order by STT,CUSTOMER_NO,CUSTOMER_PART_NO ";
            }
            PGH = IFS.ExecuteQuery(sql);
            gridCDH.DataSource = PGH;
        }

        private void PGH_XK_Load(object sender, EventArgs e)
        {
            Load_DL();
        }

        private void dateENXH_Properties_EditValueChanged(object sender, EventArgs e)
        {
            
        }

        private void dateENXH_Properties_DateTimeChanged(object sender, EventArgs e)
        {
            Load_DL();
        }

        private void RChonTK_Properties_SelectedIndexChanged(object sender, EventArgs e)
        {
            Load_DL();
        }

        private void lookUpKH_EditValueChanged(object sender, EventArgs e)
        {
            //
        }

        private void lookUpKH_Closed(object sender, DevExpress.XtraEditors.Controls.ClosedEventArgs e)
        {
            Load_DL();
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            RPGIAOHANGXK report = new RPGIAOHANGXK();
            report.DataSource = PGH  ;
            ReportPrintTool tool = new ReportPrintTool(report);
            //ReportPrintTool printTool = new ReportPrintTool(report);
            //tool.PreviewForm.FormClosed += new FormClosedEventHandler(PreviewForm_FormClosed);
            tool.ShowPreview();
            return;
        }
    }
}