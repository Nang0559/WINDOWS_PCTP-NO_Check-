using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.ViewForm
{
    public partial class FormStockHistory : DevExpress.XtraEditors.XtraForm
    {
        public FormStockHistory()
        {
            InitializeComponent();
        }
        SQLPROVIDER sqlpr = new SQLPROVIDER();
        private bool bt = false;
        private void btnSearch_Click(object sender, EventArgs e)
        {
            bt = false;
            DateTime? fromDate = dateFrom.EditValue as DateTime?;
            DateTime? toDate = dateTo.EditValue as DateTime?;
            string itemCode = lookupItemCode.EditValue?.ToString();

            var parameters = new[]
            {
            new SqlParameter("@FromDate", fromDate ?? (object)DBNull.Value),
            new SqlParameter("@ToDate", toDate ?? (object)DBNull.Value),
            new SqlParameter("@ItemCode", string.IsNullOrEmpty(itemCode) ? (object)DBNull.Value : itemCode)
        };

            var data = sqlpr.LoadData(sqlpr.B7R2_FCCdbb, "sp_GetStockHistory", parameters);

            gridControlHistory.DataSource = data;
            gridViewHistory.PopulateColumns();
            gridViewHistory.BestFitColumns();
        }

        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Excel File (*.xlsx)|*.xlsx";
                if (bt==true)
                    sfd.FileName = "KhoHienTai_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                else
                sfd.FileName = "LichSuNhapXuat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    gridViewHistory.ExportToXlsx(sfd.FileName);
                    XtraMessageBox.Show("Xuất Excel thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        public DataTable GetAllItemCodes()
        {
            using (SqlConnection conn = new SqlConnection(sqlpr.B7R2_FCCdbb))
            {
                string query = "SELECT DISTINCT ItemCode FROM StockHistory ORDER BY ItemCode";
                SqlDataAdapter da = new SqlDataAdapter(query, conn);
                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }
        private void LoadItemCodeLookup()
        {
            var dt = GetAllItemCodes();
            lookupItemCode.Properties.DataSource = dt;
            lookupItemCode.Properties.DisplayMember = "ItemCode";
            lookupItemCode.Properties.ValueMember = "ItemCode";
            lookupItemCode.Properties.NullText = "Chọn mã hàng...";
        }
        private void btnPrint_Click(object sender, EventArgs e)
        {
            gridControlHistory.ShowPrintPreview();
            gridViewHistory.OptionsPrint.AutoWidth = false; // tránh tự dãn cột
            gridViewHistory.OptionsPrint.PrintHeader = true;
            gridViewHistory.OptionsPrint.PrintFooter = true;
        }

        private void LoadCurrentStockStatus()
        {
            DataTable dt = GetCurrentStockStatus();
            gridControlHistory.DataSource = dt;
            gridViewHistory.PopulateColumns();
        }
        public DataTable GetCurrentStockStatus()
        {
            using (SqlConnection conn = new SqlConnection(sqlpr.B7R2_FCCdbb))
            using (SqlCommand cmd = new SqlCommand("sp_GetCurrentStockStatus", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        private void FormStockHistory_Load(object sender, EventArgs e)
        {
            LoadItemCodeLookup();
        }

        private void btnCurentStock_Click(object sender, EventArgs e)
        {
            bt = true;
            LoadCurrentStockStatus();
        }
    }
}