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
using System.Data.SqlClient;

namespace PCTP
{
    public partial class TTTracuu : DevExpress.XtraEditors.XtraForm
    {
        public TTTracuu()
        {
            InitializeComponent();
        }
        SQLPROVIDER SQLPROVIDER = new SQLPROVIDER();
        
    private void TTTracuu_Load(object sender, EventArgs e)
        {
            string Lot = "2007012151";
            string Bc = "A01";
            SqlParameter [] _params = { new SqlParameter("@_ItemLotCode", Lot),new SqlParameter("@_BranchCode", Bc) };
              
            
            DataSet TT = new DataSet();
            TT = SQLPROVIDER.ExecuteProcedureReturnDataSet(SQLPROVIDER.B7R2_FCCdb, "usp_Trculotnoxuat_linhkien_na", _params);
            
           
            gridCtrTTXuatHang.DataSource = TT.Tables[0];
            gridCtrPhieuLapRap.DataSource = TT.Tables[2];
            gridCtrTTNhapKho.DataSource = TT.Tables[1];
            gridCtrTTLinhKien.DataSource = TT.Tables[3];
            
        }

        private void gridCtrTTNhapKho_Click(object sender, EventArgs e)
        {

        }
    }
}