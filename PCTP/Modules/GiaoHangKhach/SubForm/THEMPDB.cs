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

namespace PCTP.QRCODE_HVN
{
    public partial class THEMPDB : DevExpress.XtraEditors.XtraForm
    {
        private readonly int _addNM;
        public THEMPDB(int addNM)
        {
            InitializeComponent();
            _addNM = addNM;
        }
        SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public string TENP;
        public string GG;
        public static Boolean ER = false;
        public Boolean Xemlai = false;
        private void load()
        {
            string sql = "select B.IDP,B.Name,B.NGAYLAP from TMPPHIEUNHANDB B, TMPPHIEUGIAOHANGDBCT A " +
                        " where B.IDP = A.IDP and A.status <> 'OK' and B.NHAMAY = " + _addNM + " and A.TTNHAN = 1 group by B.IDP,B.Name,B.NGAYLAP  ";
                // "select NAME,NGAYGIAO,GGFCC from TMPPHIEUGIAOHANGDB  where STATUS <> 'OK' group by NAME,NGAYGIAO,GGFCC ";
            DataTable PGHDB = new DataTable();
            PGHDB = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            lookUpEdit1.Properties.DataSource = PGHDB;
            for (int i = 0; i <= 24; i++)
            {

                GGFCC.Items.Add(i.ToString("00"));
            }
        }
      

        private void THEMPDB_Load(object sender, EventArgs e)
        {
            load();
            txtTP.Enabled = false;
            simpleButton1.Visible = false;
            simpleButton5.Visible = true;
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            
        }

        private void lookUpEdit1_EditValueChanged(object sender, EventArgs e)
        {
            txtTP.Text = lookUpEdit1.Text;
                //txtGG.Text = lookUpEdit1.EditValue.ToString();
        }

        private void THEMPDB_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(TENP == null)
            {
                XtraMessageBox.Show("ERR !", "ERR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ER = true;
                //e.Cancel = true;
                
            }    
        }

        private void CMDOK_Click_1(object sender, EventArgs e)
        {
            if (GGFCC.Text != "")
            {
                TENP = txtTP.Text;
                GG = GGFCC.Text ;
                ER = false;
                this.Close();
            }
            else
            {
                XtraMessageBox.Show("Lỗi ! hãy chọn giờ giao !");
            }    
        }
        
        

        private void simpleButton5_Click_1(object sender, EventArgs e)
        {
            Xemlai = true;
            ER = false;
            this.Close();
        }
    }
}