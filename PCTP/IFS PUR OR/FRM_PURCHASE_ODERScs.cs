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
using DevExpress.XtraBars.Docking;
using PCTP.ClassSQL;
using System.Text.RegularExpressions;

namespace PCTP.IFS_PUR_OR
{
    public partial class FRM_PURCHASE_ODERScs : DevExpress.XtraEditors.XtraForm
    {
        public FRM_PURCHASE_ODERScs()
        {
            InitializeComponent();
            LOAD_DL();
        }
        IFSPROVIDER IFSdata = new IFSPROVIDER();
        DataTable Pur_Oder = new DataTable();
        public static Regex rx = new Regex(@"\b(?<word>\w+)\s+(\k<word>)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //PURCHASE_ORDER_LINE_HIST
        private DataTable DKOracal()
        {
            DataTable DK = new DataTable();
            DataRow row;
            DK.Columns.Add("Code", typeof(string));
            DK.Columns.Add("Name", typeof(string));
            row = DK.NewRow();
            row["Code"] = "%";
            row["Name"] = "Anny value";
            DK.Rows.Add(row);
            row = DK.NewRow();
            row["Code"] = ">";
            row["Name"] = "Greater than";
            DK.Rows.Add(row);
            row = DK.NewRow();
            row["Code"] = "<";
            row["Name"] = "less than";
            DK.Rows.Add(row);
            row = DK.NewRow();
            row["Code"] = ">=";
            row["Name"] = "equal or greater";
            DK.Rows.Add(row);
            row = DK.NewRow();
            row["Code"] = "<=";
            row["Name"] = "equal or less";
            DK.Rows.Add(row);
            row = DK.NewRow();
            row["Code"] = "<>";
            row["Name"] = "not equal";
            DK.Rows.Add(row);
            row = DK.NewRow();
            row["Code"] = "!%";
            row["Name"] = "no value";
            DK.Rows.Add(row);
            row = DK.NewRow();
            row["Code"] = "..";
            row["Name"] = "between";
            DK.Rows.Add(row);
            return DK;
        }
        private void LOAD_DL()
        {

            lookUpDate1.Properties.DataSource = DKOracal();
            lookUpDate1.Properties.DisplayMember = "Name";
            lookUpDate1.Properties.ValueMember = "Code";
            #region Oder No
            // SQL
            SQLOrderNo.Properties.DataSource = DKOracal();
            SQLOrderNo.Properties.DisplayMember = "Name";
            SQLOrderNo.Properties.ValueMember = "Code";
            /// OderNo
            #endregion
            #region  Part No
            SQLPartNo.Properties.DataSource = DKOracal();
            SQLPartNo.Properties.DisplayMember = "Name";
            SQLPartNo.Properties.ValueMember = "Code";
            /// part Info
            lookUpEditPartNo.Properties.DataSource = PartNo();
            lookUpEditPartNo.Properties.DisplayMember = "VENDOR_PART_DESCRIPTION";
            lookUpEditPartNo.Properties.ValueMember = "PART_NO";
            #endregion
            #region Supplier
            SQLSupplier.Properties.DataSource = DKOracal();
            SQLSupplier.Properties.DisplayMember = "Name";
            SQLSupplier.Properties.ValueMember = "Code";
            /// Info Supplier
            lookUpEditSuplier.Properties.DataSource = Supplier();
            lookUpEditSuplier.Properties.DisplayMember = "NAME";
            lookUpEditSuplier.Properties.ValueMember = "SUPPLIER_ID";
            #endregion
            string sql = "select ORDER_NO,ORDER_CODE,OBJSTATE,VENDOR_NO , CONTRACT,CURRENCY_CODE,DATE_ENTERED  , WANTED_RECEIPT_DATE , SHIP_VIA_CODE,BUYER_CODE, REVISION ,PRINTED_FLAG_DB , DELIVERY_ADDRESS " +
                       " from PURCHASE_ORDER "+
                       " where " +
                       " (OBJSTATE = (select IFSAPP.PURCHASE_ORDER_API.FINITE_STATE_ENCODE__('Planned') from dual) or " +
                       " OBJSTATE = (select IFSAPP.PURCHASE_ORDER_API.FINITE_STATE_ENCODE__('Planned') from dual) or " +
                       " OBJSTATE = (select IFSAPP.PURCHASE_ORDER_API.FINITE_STATE_ENCODE__('Received') from dual)) " +
                       " and(VENDOR_NO = '100032' or VENDOR_NO = '100020') and " +
                       " CONTRACT = 'VN2W' " +
                       " and WANTED_RECEIPT_DATE between to_date( '20201107', 'YYYYMMDD' ) and " +
                       " to_date( '20201120', 'YYYYMMDD' ) +(1 - 1 / (60 * 60 * 24))";
            Pur_Oder=  IFSdata.ExecuteQuery(sql);
            gridCDDH.DataSource = Pur_Oder;
        }
        private DataTable Supplier()
        {
            DataTable sPli = new DataTable();
            string sql = "select SUPPLIER_ID,NAME from SUPPLIER_INFO_GENERAL";
            sPli = IFSdata.ExecuteQuery(sql);
            return sPli;
        }
        private DataTable PartNo()
        {
            DataTable pArtNo = new DataTable();
            string sql = "select PART_NO,VENDOR_PART_DESCRIPTION from PURCHASE_PART_SUPPLIER";
            pArtNo= IFSdata.ExecuteQuery(sql);
            return pArtNo;
        }

        private void lookUpDate1_Properties_EditValueChanged(object sender, EventArgs e)
        {
            string[] TextDelivery;
            TextDelivery = textWanteddeliverydate.Text.Split(';');
            if (textWanteddeliverydate.Text == "")
            {
                textWanteddeliverydate.Text = lookUpDate1.EditValue.ToString();
            }
            else
            {
                if (lookUpDate1.EditValue.ToString() == "..")
                {
                    if (IsDateTime(TextDelivery[TextDelivery.Length-1]) == true)
                    {
                        textWanteddeliverydate.Text = ";" + textWanteddeliverydate.Text + lookUpDate1.EditValue.ToString();
                    }
                }
                else
                {


                }
            }
        }
        public static bool IsDateTime(string txtDate)
        {
            DateTime tempDate;
            return DateTime.TryParse(txtDate, out tempDate);
        }
        private void dateEdit1_Properties_EditValueChanged(object sender, EventArgs e)
        {
            textWanteddeliverydate.Text = textWanteddeliverydate.Text + dateEdit1.EditValue.ToString();
        }

        private void dockPanel1_Click(object sender, EventArgs e)
        {

        }
    }
}