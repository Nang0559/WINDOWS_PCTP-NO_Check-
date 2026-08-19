using DevExpress.CodeParser;
using DevExpress.Xpf;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using MyValidation;
using PCTP;
using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.Schema;

namespace PCTP
{
    public partial class FRM_LOTNO_UPDATE_INFOR : ValidatedForm
    {
        private readonly SQLPROVIDER sqlBrv = new SQLPROVIDER();

        public static string LOTNO;

        public FRM_LOTNO_UPDATE_INFOR()
        {
            InitializeComponent();

            cmdUpdate.Enabled = false;

            // Giá trị mặc định
            txtUDSLNHAP.Text = "0";
            txt_UDSLXuat.Text = "0";

            Load_();
        }

        #region LOAD DATA

        private void Load_()
        {
            try
            {
                string lotNo = txt_LOTNO.Text.Trim();

                DataTable dt;

                if (string.IsNullOrWhiteSpace(lotNo))
                {
                    // Không nhập LOT -> load toàn bộ
                    const string sql = @"
                    SELECT *
                    FROM stocktp
                    ORDER BY LOT";

                    dt = sqlBrv.LoadData1(
                        sqlBrv.B7R2_FCCdb,
                        sql
                    );
                }
                else
                {
                    // Có LOT -> tìm chính xác hoặc 13 ký tự đầu
                    const string sql = @"
                    SELECT *
                    FROM stocktp
                    WHERE LOT = @LOT
                       OR SUBSTRING(LOT, 1, 13) = @LOT
                    ORDER BY LOT";

                    var parameters = new[]
                    {
                    new SqlParameter("@LOT", SqlDbType.NVarChar, 50)
                    {
                        Value = lotNo
                    }
                };

                    dt = sqlBrv.LoadData1(
                        sqlBrv.B7R2_FCCdb,
                        sql,
                        parameters
                    );
                }

                if (dt == null)
                {
                    gridCLOT_IF.DataSource = null;
                    ClearLotInformation();
                    return;
                }

                gridCLOT_IF.DataSource = dt;

                if (dt.Rows.Count == 0)
                {
                    ClearLotInformation();

                    if (!string.IsNullOrWhiteSpace(lotNo))
                    {
                        XtraMessageBox.Show(
                            $"Không tìm thấy LOT: {lotNo}",
                            "Thông báo",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }

                    return;
                }

                // Lấy dòng đầu tiên
                DataRow row = dt.Rows[0];

                LOTNO = Convert.ToString(row["LOT"]);

                txt_LOTNO.Text = Convert.ToString(row["LOT"]);
                txtSLSX.Text = GetIntValue(row, "SLSX").ToString();
                txtSLNHAP.Text = GetIntValue(row, "SLNHAP").ToString();
                txtSLDX.Text = GetIntValue(row, "SLXUAT").ToString();
                txtSLCL.Text = GetIntValue(row, "SLCONLAI").ToString();

                // Giá trị sửa
                txtUDSLNHAP.Text = GetIntValue(row, "SLNHAP").ToString();
                txt_UDSLXuat.Text = GetIntValue(row, "SLXUAT").ToString();

                cmdUpdate.Enabled = false;
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể tải dữ liệu LOT.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void ClearLotInformation()
        {
            LOTNO = null;

            txtSLSX.Text = "0";
            txtSLNHAP.Text = "0";
            txtSLDX.Text = "0";
            txtSLCL.Text = "0";

            txtUDSLNHAP.Text = "0";
            txt_UDSLXuat.Text = "0";

            cmdUpdate.Enabled = false;
        }

        private int GetIntValue(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
                return 0;

            if (row[columnName] == DBNull.Value)
                return 0;

            int.TryParse(row[columnName].ToString(), out int value);

            return value;
        }

        #endregion

        #region UPDATE

        private void cmdUpdate_Click(object sender, EventArgs e)
        {
            if (!ValidateInput())
                return;

            string lotNo = txt_LOTNO.Text.Trim();

            if (string.IsNullOrWhiteSpace(lotNo))
            {
                XtraMessageBox.Show(
                    "Vui lòng nhập LOT.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                txt_LOTNO.Focus();
                return;
            }

            if (!int.TryParse(txtUDSLNHAP.Text.Trim(), out int slNhap))
            {
                XtraMessageBox.Show(
                    "Số lượng nhập không hợp lệ.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                txtUDSLNHAP.Focus();
                return;
            }

            if (!int.TryParse(txt_UDSLXuat.Text.Trim(), out int slXuat))
            {
                XtraMessageBox.Show(
                    "Số lượng xuất không hợp lệ.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                txt_UDSLXuat.Focus();
                return;
            }

            // SLCONLAI luôn được tính từ SLNHAP - SLXUAT
            int slConLai = slNhap - slXuat;

            const string sql = @"
            UPDATE stocktp
            SET
                SLNHAP   = @SLNHAP,
                SLXUAT   = @SLXUAT,
                SLCONLAI = @SLCONLAI
            WHERE LOT = @LOT";

            var parameters = new[]
            {
            new SqlParameter("@SLNHAP", SqlDbType.Int)
            {
                Value = slNhap
            },

            new SqlParameter("@SLXUAT", SqlDbType.Int)
            {
                Value = slXuat
            },

            new SqlParameter("@SLCONLAI", SqlDbType.Int)
            {
                Value = slConLai
            },

            new SqlParameter("@LOT", SqlDbType.NVarChar, 50)
            {
                Value = lotNo
            }
        };

            try
            {
                int affectedRows = sqlBrv.ExecuteNonQuery(
                    sqlBrv.B7R2_FCCdb,
                    sql,
                    parameters
                );

                if (affectedRows <= 0)
                {
                    XtraMessageBox.Show(
                        $"Không tìm thấy LOT để cập nhật: {lotNo}",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return;
                }

                XtraMessageBox.Show(
                    "Cập nhật thông tin LOT thành công.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                Load_();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Cập nhật dữ liệu thất bại.\r\n\r\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        #endregion

        #region VALIDATION

        private bool ValidateInput()
        {
            eProvider.Clear();

            if (!int.TryParse(txtUDSLNHAP.Text.Trim(), out int slNhap))
            {
                eProvider.SetError(
                    txtUDSLNHAP,
                    "Số lượng nhập phải là số nguyên."
                );

                txtUDSLNHAP.Focus();
                return false;
            }

            if (!int.TryParse(txt_UDSLXuat.Text.Trim(), out int slXuat))
            {
                eProvider.SetError(
                    txt_UDSLXuat,
                    "Số lượng xuất phải là số nguyên."
                );

                txt_UDSLXuat.Focus();
                return false;
            }

            if (slNhap < 0)
            {
                eProvider.SetError(
                    txtUDSLNHAP,
                    "Số lượng nhập không được âm."
                );

                txtUDSLNHAP.Focus();
                return false;
            }

            if (slXuat < 0)
            {
                eProvider.SetError(
                    txt_UDSLXuat,
                    "Số lượng xuất không được âm."
                );

                txt_UDSLXuat.Focus();
                return false;
            }

            if (!int.TryParse(txtSLSX.Text.Trim(), out int slSX))
            {
                slSX = 0;
            }

            if (!int.TryParse(txtSLDX.Text.Trim(), out int slDaXuat))
            {
                slDaXuat = 0;
            }

            // SL nhập không được lớn hơn SL sản xuất
            if (slNhap > slSX)
            {
                eProvider.SetError(
                    txtUDSLNHAP,
                    $"SL nhập ({slNhap}) không được lớn hơn SL sản xuất ({slSX})."
                );

                txtUDSLNHAP.Focus();
                return false;
            }

            // SL nhập phải >= SL đã xuất
            if (slNhap < slDaXuat)
            {
                eProvider.SetError(
                    txtUDSLNHAP,
                    $"SL nhập ({slNhap}) không được nhỏ hơn SL đã xuất ({slDaXuat})."
                );

                txtUDSLNHAP.Focus();
                return false;
            }

            // SL xuất không được lớn hơn SL nhập
            if (slXuat > slNhap)
            {
                eProvider.SetError(
                    txt_UDSLXuat,
                    $"SL xuất ({slXuat}) không được lớn hơn SL nhập ({slNhap})."
                );

                txt_UDSLXuat.Focus();
                return false;
            }

            return true;
        }

        private void UpdateButtonState()
        {
            cmdUpdate.Enabled = ValidateInputSilently();
        }

        private bool ValidateInputSilently()
        {
            if (!int.TryParse(txtUDSLNHAP.Text.Trim(), out int slNhap))
                return false;

            if (!int.TryParse(txt_UDSLXuat.Text.Trim(), out int slXuat))
                return false;

            if (!int.TryParse(txtSLSX.Text.Trim(), out int slSX))
                slSX = 0;

            if (!int.TryParse(txtSLDX.Text.Trim(), out int slDaXuat))
                slDaXuat = 0;

            if (slNhap < 0 || slXuat < 0)
                return false;

            if (slNhap > slSX)
                return false;

            if (slNhap < slDaXuat)
                return false;

            if (slXuat > slNhap)
                return false;

            return true;
        }

        #endregion

        #region EVENTS

        private void txtUDSLNHAP_EditValueChanged(object sender, EventArgs e)
        {
            UpdateButtonState();
        }

        private void txt_UDSLXuat_EditValueChanged(object sender, EventArgs e)
        {
            UpdateButtonState();
        }

        private void txtUDSLNHAP_Properties_Validating(
            object sender,
            CancelEventArgs e)
        {
            e.Cancel = !ValidateInput();
        }

        private void txt_UDSLXuat_Validating(
            object sender,
            CancelEventArgs e)
        {
            e.Cancel = !ValidateInput();
        }

        private void cmd_TK_Click(object sender, EventArgs e)
        {
            Load_();
        }

        private void txtUDSLNHAP_KeyPress(
            object sender,
            KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                ValidateInput();
            }
        }

        #endregion
    }
}