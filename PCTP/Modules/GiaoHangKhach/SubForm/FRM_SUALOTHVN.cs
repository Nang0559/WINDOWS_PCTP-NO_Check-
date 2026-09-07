using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using PCTP.Shared.Validation;
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

namespace PCTP.QRCODE_HVN
{
    public partial class FRM_SUALOTHVN : ValidatableXtraForm
    {
        private readonly SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public string _MH;
        private string STTDAU;
        public FRM_SUALOTHVN()
        {
            InitializeComponent();
        }


        public FRM_SUALOTHVN(string maHang) : this()
        {
            _MH = maHang;
        }
        private void FRM_SUALOTHVN_Load(object sender, EventArgs e)
        {
            SetupValidation();
            LoadData();   // gộp 2 hàm load()/Load trùng lặp thành 1

        }
        private void SetupValidation()
        {
            AddRule(LOTBD, new RequiredNumericRule(),
                "Bạn hãy nhập LOT Gốc hợp lệ (dạng số) để bắt đầu sửa !");
        }

        private void LoadData()
        {
            const string sql =
                "SELECT * FROM DOCQRCODE WHERE MAHANGFCC = @MaHang ORDER BY LOTHVN";

            DataTable table = sqlBRV.LoadData1(sqlBRV.B7R2_FCCdb, sql,
                new SqlParameter("@MaHang", SqlDbType.NVarChar, 100) { Value = _MH ?? "" });

            gridCtrSUALOTHVN.DataSource = table;
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            // 1. Validate LOTBD (required + numeric) qua DXValidationProvider —
            //    thay cho check tay "LOTBD.Text == """
            if (!ValidateAll())
                return;

            // 2. Guard: phải có ít nhất 1 dòng được chọn trước khi đụng vào DB.
            //    Bug ở bản gốc: 2 lệnh UPDATE reset bên dưới chạy vô điều kiện
            //    kể cả khi chưa chọn dòng nào — dữ liệu bị "dọn" oan.
            bool coDongChon = false;
            for (int i = 0; i < gridVSUALOTHVN.RowCount; i++)
            {
                if (gridVSUALOTHVN.IsRowSelected(i)) { coDongChon = true; break; }
            }
            if (!coDongChon)
            {
                MessageBox.Show("Bạn chưa chọn dòng nào để sửa LOT !", "Thông Báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 3. Reset trạng thái — parameterized, không còn nối chuỗi.
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                "UPDATE TMPPHIEUGIAOHANG SET LOT = '' WHERE MAHANG = @MaHang",
                new SqlParameter("@MaHang", SqlDbType.NVarChar, 100) { Value = _MH });

            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                "UPDATE DOCQRCODE SET KETQUA = 'OK' WHERE KETQUA = 'DG'");

            // 4. Gán LOT tăng dần cho từng dòng được chọn — giữ đúng thứ tự
            //    duyệt (i tăng dần) và logic tăng LOT như bản gốc.
            double lot = double.Parse(LOTBD.Text.Trim());

            for (int i = 0; i < gridVSUALOTHVN.RowCount; i++)
            {
                if (!gridVSUALOTHVN.IsRowSelected(i)) continue;

                int stt = Convert.ToInt32(gridVSUALOTHVN.GetRowCellValue(i, "STT"));
                lot += 1;

                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                    "UPDATE DOCQRCODE SET SUALOTHVN = @LotMoi, KETQUA = 'OK' WHERE STT = @Stt",
                    new SqlParameter("@LotMoi", SqlDbType.NVarChar, 50) { Value = lot.ToString() },
                    new SqlParameter("@Stt", SqlDbType.Int) { Value = stt });
            }

            LOTBD.Text = lot.ToString();
            this.Close();
        }

        private void gridCtrSUALOTHVN_DoubleClick(object sender, EventArgs e)
        {
            if (gridVSUALOTHVN.FocusedRowHandle < 0) return;

            LOTBD.Text = gridVSUALOTHVN
                .GetRowCellValue(gridVSUALOTHVN.FocusedRowHandle, "LOTHVN")
                ?.ToString() ?? "";
        }
    }

}