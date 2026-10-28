using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using PCTP.Shared.Validation;
using System;

using System.Data;
using System.Data.SqlClient;

using System.Windows.Forms;

namespace PCTP.QRCODE_HVN
{
    public partial class FRM_SUALOTHVN : ValidatableXtraForm
    {
        private readonly SQLPROVIDER sqlBRV = new SQLPROVIDER();
        public string _MH;
     
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

            // Reset and reassign LOT in one transaction. Scope the DOCQRCODE
            // reset to the selected item so another user's item is not affected.
            double lot = double.Parse(LOTBD.Text.Trim());
            using (SqlConnection connection = new SqlConnection(sqlBRV.B7R2_FCCdb))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (SqlCommand appLock = new SqlCommand(@"
DECLARE @Result int;
EXEC @Result = sys.sp_getapplock
    @Resource = @Resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 10000;
SELECT @Result;", connection, transaction))
                        {
                            appLock.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value =
                                "PCTP_HVN_LOT_EDIT:" + (_MH ?? string.Empty).Trim();
                            int lockResult = Convert.ToInt32(appLock.ExecuteScalar());
                            if (lockResult < 0)
                                throw new InvalidOperationException("Không lấy được khóa chỉnh LOT HVN (sp_getapplock).");
                        }

                        using (SqlCommand resetTmp = new SqlCommand(
                            "UPDATE TMPPHIEUGIAOHANG SET LOT = '' WHERE MAHANG = @MaHang",
                            connection, transaction))
                        {
                            resetTmp.Parameters.Add("@MaHang", SqlDbType.NVarChar, 100).Value = _MH ?? string.Empty;
                            resetTmp.ExecuteNonQuery();
                        }

                        using (SqlCommand resetQr = new SqlCommand(
                            "UPDATE DOCQRCODE SET KETQUA = 'OK' WHERE KETQUA = 'DG' AND MAHANGFCC = @MaHang",
                            connection, transaction))
                        {
                            resetQr.Parameters.Add("@MaHang", SqlDbType.NVarChar, 100).Value = _MH ?? string.Empty;
                            resetQr.ExecuteNonQuery();
                        }

                        for (int i = 0; i < gridVSUALOTHVN.RowCount; i++)
                        {
                            if (!gridVSUALOTHVN.IsRowSelected(i)) continue;
                            int stt = Convert.ToInt32(gridVSUALOTHVN.GetRowCellValue(i, "STT"));
                            lot += 1;
                            using (SqlCommand updateQr = new SqlCommand(
                                "UPDATE DOCQRCODE SET SUALOTHVN = @LotMoi, KETQUA = 'OK' WHERE STT = @Stt AND MAHANGFCC = @MaHang",
                                connection, transaction))
                            {
                                updateQr.Parameters.Add("@LotMoi", SqlDbType.NVarChar, 50).Value = lot.ToString();
                                updateQr.Parameters.Add("@Stt", SqlDbType.Int).Value = stt;
                                updateQr.Parameters.Add("@MaHang", SqlDbType.NVarChar, 100).Value = _MH ?? string.Empty;
                                if (updateQr.ExecuteNonQuery() != 1)
                                    throw new InvalidOperationException("Không cập nhật được đúng một dòng DOCQRCODE cho STT " + stt + ".");
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        try { transaction.Rollback(); }
                        catch { /* Preserve the original exception. */ }
                        throw;
                    }
                }
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