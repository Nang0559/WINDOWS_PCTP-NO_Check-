
    using DevExpress.DataProcessing.InMemoryDataProcessor;
    using DevExpress.XtraEditors;
    using DevExpress.XtraGrid.Views.Grid;
    using DevExpress.XtraRichEdit.Export.Doc;
using PCTP.ClassSQL;
using System;
    using System.ComponentModel;
    using System.Data;
    using System.Data.SqlClient;
    using System.Windows.Forms;

    namespace PCTP
    {
        public partial class NHAPKHOKHONGQRCODE : XtraForm
        {
            private readonly SQLPROVIDER _sql;

            public NHAPKHOKHONGQRCODE()
            {
                InitializeComponent();

                _sql = new SQLPROVIDER();

                dateNN.DateTime = DateTime.Today;
                dateENgayNhap.DateTime = DateTime.Now;

                sidePThemMoi.Enabled = false;
            }

            #region FORM LOAD

            private void NHAPKHOKHONGQRCODE_Load(object sender, EventArgs e)
            {
                LoadData();
            }

            /// <summary>
            /// Load danh sách mã hàng, ca sản xuất và tồn kho.
            /// Dùng LoadData1 vì đây là SQL Text.
            /// </summary>
            private void LoadData()
            {
                try
                {
                    LoadItemNoQrCode();
                    LoadShift();
                    LoadStock();

                    ResetInput();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(
                        $"Không thể tải dữ liệu.\r\n\r\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            private void LoadItemNoQrCode()
            {
                const string sql = @"
                SELECT 
                    Code,
                    Name
                FROM ITEM_NO_QRCODE
                GROUP BY Code, Name
                ORDER BY Code;";

                DataTable dt = _sql.LoadData1(
                    _sql.B7R2_FCCdb,
                    sql);

                lookUpMHNOQR.Properties.DataSource = dt;
                lookUpMHNOQR.Properties.DisplayMember = "Code";
                lookUpMHNOQR.Properties.ValueMember = "Code";
                lookUpMHNOQR.Properties.NullText = "Chọn mã hàng";
            }

            private void LoadShift()
            {
                const string sql = @"
                SELECT 
                    Code,
                    Name
                FROM B20Shift
                WHERE IsActive = 1
                ORDER BY Code;";

                DataTable dt = _sql.LoadData1(
                    _sql.B7R2_FCCdb,
                    sql);

                lookUpCA.Properties.DataSource = dt;
                lookUpCA.Properties.DisplayMember = "Code";
                lookUpCA.Properties.ValueMember = "Code";
                lookUpCA.Properties.NullText = "Chọn ca";
            }

            private void LoadStock()
            {
                const string sql = @"
                SELECT
                    LOT,
                    Part,
                    Name,
                    NGAYSX,
                    CASX,
                    SLSX,
                    NGAYNHAP,
                    SLNHAP,
                    NGAYXUAT,
                    SLXUAT,
                    SLCONLAI,
                    SLCONLAITMP AS SOLUONGDANGGIAO
                FROM STOCKTP
                WHERE SLCONLAI > 0
                  AND Part IN
                  (
                      SELECT Code
                      FROM ITEM_NO_QRCODE
                  )
                ORDER BY NGAYSX DESC, LOT;";

                DataTable dt = _sql.LoadData1(
                    _sql.B7R2_FCCdb,
                    sql);

                gridCTTCT.DataSource = dt;
            }

            #endregion

            #region RESET / UI

            private void ResetInput()
            {
                sidePThemMoi.Enabled = false;

                textCode.Text = string.Empty;
                textName.Text = string.Empty;
                textSLNHAP.Text = string.Empty;

                lookUpMHNOQR.EditValue = null;
                lookUpCA.EditValue = null;

                dateNN.DateTime = DateTime.Today;
                dateENgayNhap.DateTime = DateTime.Now;

                ClearErrors();
            }

            private void ClearErrors()
            {
                textCode.ErrorText = string.Empty;
                textName.ErrorText = string.Empty;
                textSLNHAP.ErrorText = string.Empty;
                lookUpMHNOQR.ErrorText = string.Empty;
                lookUpCA.ErrorText = string.Empty;
                dateNN.ErrorText = string.Empty;
            }

            #endregion

            #region THÊM MÃ HÀNG KHÔNG QR

            private void CMDTHEMMOI_Click(object sender, EventArgs e)
            {
                sidePThemMoi.Enabled = true;

                textCode.Focus();
            }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            if (!ValidateNewItem())
                return;

            string code = textCode.Text.Trim();
            string name = textName.Text.Trim();

            SqlTransaction tran = null;

            try
            {
                // ============================================================
                // 1. Kiểm tra mã đã tồn tại trong ITEM_NO_QRCODE
                // ============================================================

                const string sqlCheck = @"
            SELECT COUNT(1)
            FROM ITEM_NO_QRCODE
            WHERE Code = @Code;";

                int count = Convert.ToInt32(
                    _sql.ExecuteScalar(
                        _sql.B7R2_FCCdb,
                        sqlCheck,
                        new[]
                        {
                    new SqlParameter("@Code", SqlDbType.NVarChar, 50)
                    {
                        Value = code
                    }
                        }));

                if (count > 0)
                {
                    XtraMessageBox.Show(
                        $"Mã hàng [{code}] đã tồn tại.",
                        "Trùng mã",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    textCode.Focus();
                    textCode.SelectAll();
                    return;
                }

                // ============================================================
                // 2. Mở transaction
                // ============================================================

                using (SqlConnection conn = _sql.BeginTransaction(
                    _sql.B7R2_FCCdb,
                    out tran))
                {
                    try
                    {
                        // ====================================================
                        // 3. Lấy ID mới cho ITEM_NO_QRCODE
                        // ====================================================

                        const string sqlMaxId = @"
                    SELECT ISNULL(MAX(ID), 0) + 1
                    FROM ITEM_NO_QRCODE;";

                        int newId = Convert.ToInt32(
                            _sql.ExecuteScalar(
                                conn,
                                tran,
                                sqlMaxId));

                        // ====================================================
                        // 4. Kiểm tra B20Item
                        // ====================================================

                        const string sqlCheckB20 = @"
                    SELECT COUNT(1)
                    FROM B20Item
                    WHERE Code = @Code;";

                        int b20Count = Convert.ToInt32(
                            _sql.ExecuteScalar(
                                conn,
                                tran,
                                sqlCheckB20,
                                new[]
                                {
                            new SqlParameter("@Code", SqlDbType.NVarChar, 50)
                            {
                                Value = code
                            }
                                }));

                        // ====================================================
                        // 5. Nếu chưa có B20Item thì thêm mới
                        // ====================================================

                        if (b20Count == 0)
                        {
                            const string sqlInsertB20 = @"
                        INSERT INTO B20Item
                        (
                            ParentId,
                            Code,
                            Name,
                            Unit
                        )
                        VALUES
                        (
                            @ParentId,
                            @Code,
                            @Name,
                            @Unit
                        );";

                            _sql.ExecuteNonQuery(
                                conn,
                                tran,
                                sqlInsertB20,
                                new SqlParameter("@ParentId", SqlDbType.Int)
                                {
                                    Value = 1
                                },
                                new SqlParameter("@Code", SqlDbType.NVarChar, 50)
                                {
                                    Value = code
                                },
                                new SqlParameter("@Name", SqlDbType.NVarChar, 255)
                                {
                                    Value = name
                                },
                                new SqlParameter("@Unit", SqlDbType.NVarChar, 50)
                                {
                                    Value = "Cái"
                                });
                        }

                        // ====================================================
                        // 6. Thêm ITEM_NO_QRCODE
                        // ====================================================

                        const string sqlInsertNoQr = @"
                    INSERT INTO ITEM_NO_QRCODE
                    (
                        ID,
                        Code,
                        Name
                    )
                    VALUES
                    (
                        @ID,
                        @Code,
                        @Name
                    );";

                        _sql.ExecuteNonQuery(
                            conn,
                            tran,
                            sqlInsertNoQr,
                            new SqlParameter("@ID", SqlDbType.Int)
                            {
                                Value = newId
                            },
                            new SqlParameter("@Code", SqlDbType.NVarChar, 50)
                            {
                                Value = code
                            },
                            new SqlParameter("@Name", SqlDbType.NVarChar, 255)
                            {
                                Value = name
                            });

                        // ====================================================
                        // 7. Commit
                        // ====================================================

                        tran.Commit();
                        tran = null;
                    }
                    catch
                    {
                        try
                        {
                            tran?.Rollback();
                        }
                        catch
                        {
                            // Không che mất exception SQL gốc
                        }

                        throw;
                    }
                }

                // ============================================================
                // 8. Thành công
                // ============================================================

                XtraMessageBox.Show(
                    $"Đã thêm mã hàng thành công.\r\n\r\n" +
                    $"Mã hàng: {code}\r\n" +
                    $"Tên hàng: {name}",
                    "Hoàn tất",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadData();

                // Reset form
                textCode.Text = string.Empty;
                textName.Text = string.Empty;
                textCode.Focus();
            }
            catch (SqlException ex)
            {
                XtraMessageBox.Show(
                    "Không thể lưu mã hàng vào cơ sở dữ liệu.\r\n\r\n" +
                    ex.Message,
                    "Lỗi SQL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(
                    "Không thể thêm mã hàng.\r\n\r\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private bool ValidateNewItem()
            {
                ClearErrors();

                bool valid = true;

                if (string.IsNullOrWhiteSpace(textCode.Text))
                {
                    textCode.ErrorText = "Chưa nhập mã hàng.";
                    valid = false;
                }

                if (string.IsNullOrWhiteSpace(textName.Text))
                {
                    textName.ErrorText = "Chưa nhập tên hàng.";
                    valid = false;
                }

                if (!valid)
                {
                    XtraMessageBox.Show(
                        "Vui lòng nhập đầy đủ thông tin.",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return valid;
            }

            #endregion

            #region LOT

            /// <summary>
            /// Lấy ID của mã hàng không QR.
            /// </summary>
            private int GetItemNoQrId(string code)
            {
                const string sql = @"
                SELECT TOP 1 ID
                FROM ITEM_NO_QRCODE
                WHERE Code = @Code;";

                object result = _sql.ExecuteScalar(
                    _sql.B7R2_FCCdb,
                    sql,
                    new[]
                    {
                    new SqlParameter("@Code", SqlDbType.NVarChar, 50)
                    {
                        Value = code
                    }
                    });

                if (result == null || result == DBNull.Value)
                    return 0;

                return Convert.ToInt32(result);
            }

            /// <summary>
            /// Sinh LOT theo cấu trúc cũ:
            /// SP + yyMMdd + ID mã hàng + Ca
            /// </summary>
            private string GenerateLotNo(
                string itemCode,
                DateTime productionDate,
                string shift)
            {
                int itemId = GetItemNoQrId(itemCode);

                if (itemId <= 0)
                    throw new InvalidOperationException(
                        $"Không tìm thấy ID của mã hàng [{itemCode}].");

                return $"SP{productionDate:yyMMdd}{itemId}{shift}";
            }

            #endregion

            #region VALIDATE NHẬP KHO

            private bool ValidateImport()
            {
                ClearErrors();

                bool valid = true;

                if (lookUpMHNOQR.EditValue == null ||
                    string.IsNullOrWhiteSpace(lookUpMHNOQR.EditValue.ToString()))
                {
                    lookUpMHNOQR.ErrorText = "Hãy chọn mã hàng.";
                    valid = false;
                }

                if (lookUpCA.EditValue == null ||
                    string.IsNullOrWhiteSpace(lookUpCA.EditValue.ToString()))
                {
                    lookUpCA.ErrorText = "Hãy chọn ca sản xuất.";
                    valid = false;
                }

                if (dateNN.EditValue == null)
                {
                    dateNN.ErrorText = "Hãy chọn ngày sản xuất.";
                    valid = false;
                }

                if (!int.TryParse(
                        textSLNHAP.Text.Trim(),
                        out int quantity) ||
                    quantity <= 0)
                {
                    textSLNHAP.ErrorText =
                        "Số lượng nhập phải là số nguyên lớn hơn 0.";
                    valid = false;
                }

                if (!valid)
                {
                    XtraMessageBox.Show(
                        "Vui lòng kiểm tra lại thông tin nhập kho.",
                        "Dữ liệu chưa hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return valid;
            }

            #endregion

            #region SAVE STOCKTP

            private void SaveData()
            {
                string itemCode = lookUpMHNOQR.EditValue?.ToString()?.Trim();
                string shift = lookUpCA.EditValue?.ToString()?.Trim();

                int quantity = Convert.ToInt32(textSLNHAP.Text.Trim());

                DateTime productionDate = dateNN.DateTime.Date;
                DateTime importDate = dateENgayNhap.DateTime;

                string lotNo = GenerateLotNo(
                    itemCode,
                    productionDate,
                    shift);

                using (SqlConnection conn =
                    _sql.BeginTransaction(
                        _sql.B7R2_FCCdb,
                        out SqlTransaction tran))
                {
                    try
                    {
                        // Lấy tên hàng từ bảng ITEM_NO_QRCODE.
                        const string sqlGetName = @"
                        SELECT TOP 1 Name
                        FROM ITEM_NO_QRCODE
                        WHERE Code = @Code;";

                        object nameResult = _sql.ExecuteScalar(
                            conn,
                            tran,
                            sqlGetName,
                            new[]
                            {
                            new SqlParameter("@Code", SqlDbType.NVarChar, 50)
                            {
                                Value = itemCode
                            }
                            });

                        string itemName =
                            nameResult == null || nameResult == DBNull.Value
                                ? string.Empty
                                : nameResult.ToString().Trim();

                        if (string.IsNullOrWhiteSpace(itemName))
                        {
                            throw new InvalidOperationException(
                                $"Không tìm thấy tên hàng của mã [{itemCode}].");
                        }

                        // Kiểm tra LOT.
                        const string sqlCheckLot = @"
                        SELECT COUNT(1)
                        FROM STOCKTP
                        WHERE LOT = @LOT;";

                        int lotCount = Convert.ToInt32(
                            _sql.ExecuteScalar(
                                conn,
                                tran,
                                sqlCheckLot,
                                new[]
                                {
                                new SqlParameter("@LOT", SqlDbType.NVarChar, 100)
                                {
                                    Value = lotNo
                                }
                                }));

                        if (lotCount == 0)
                        {
                            // LOT mới.
                            const string sqlInsert = @"
                            INSERT INTO STOCKTP
                            (
                                LOT,
                                Part,
                                NAME,
                                CASX,
                                NGAYSX,
                                NGAYNHAP,
                                SLNHAP,
                                NGAYXUAT,
                                SLXUAT,
                                SLCONLAI,
                                Satus
                            )
                            VALUES
                            (
                                @LOT,
                                @Part,
                                @Name,
                                @CaSX,
                                @NgaySX,
                                @NgayNhap,
                                @SLNhap,
                                @NgayXuat,
                                @SLXuat,
                                @SLConLai,
                                @Status
                            );";

                            _sql.ExecuteNonQuery(
                                conn,
                                tran,
                                sqlInsert,
                                new SqlParameter("@LOT", SqlDbType.NVarChar, 100)
                                {
                                    Value = lotNo
                                },
                                new SqlParameter("@Part", SqlDbType.NVarChar, 50)
                                {
                                    Value = itemCode
                                },
                                new SqlParameter("@Name", SqlDbType.NVarChar, 255)
                                {
                                    Value = itemName
                                },
                                new SqlParameter("@CaSX", SqlDbType.Int)
                                {
                                    Value = Convert.ToInt32(shift)
                                },
                                new SqlParameter("@NgaySX", SqlDbType.DateTime)
                                {
                                    Value = productionDate
                                },
                                new SqlParameter("@NgayNhap", SqlDbType.DateTime)
                                {
                                    Value = importDate
                                },
                                new SqlParameter("@SLNhap", SqlDbType.Int)
                                {
                                    Value = quantity
                                },
                                new SqlParameter("@NgayXuat", SqlDbType.DateTime)
                                {
                                    Value = productionDate
                                },
                                new SqlParameter("@SLXuat", SqlDbType.Int)
                                {
                                    Value = 0
                                },
                                new SqlParameter("@SLConLai", SqlDbType.Int)
                                {
                                    Value = quantity
                                },
                                new SqlParameter("@Status", SqlDbType.Int)
                                {
                                    Value = 1
                                });
                        }
                        else
                        {
                            // LOT đã tồn tại -> cộng thêm số lượng.
                            const string sqlUpdate = @"
                            UPDATE STOCKTP
                            SET
                                SLNHAP = ISNULL(SLNHAP, 0) + @SLNhap,
                                SLCONLAI = ISNULL(SLCONLAI, 0) + @SLNhap,
                                NGAYNHAP = @NgayNhap
                            WHERE LOT = @LOT;";

                            _sql.ExecuteNonQuery(
                                conn,
                                tran,
                                sqlUpdate,
                                new SqlParameter("@SLNhap", SqlDbType.Int)
                                {
                                    Value = quantity
                                },
                                new SqlParameter("@NgayNhap", SqlDbType.DateTime)
                                {
                                    Value = importDate
                                },
                                new SqlParameter("@LOT", SqlDbType.NVarChar, 100)
                                {
                                    Value = lotNo
                                });
                        }

                        tran.Commit();
                    }
                    catch
                    {
                        try
                        {
                            tran.Rollback();
                        }
                        catch
                        {
                            // Giữ exception gốc.
                        }

                        throw;
                    }
                }
            }

            #endregion

            #region BUTTON NHẬP KHO

            private void simpleButton1_Click(object sender, EventArgs e)
            {
                if (!ValidateImport())
                    return;

                try
                {
                    string itemCode = lookUpMHNOQR.EditValue.ToString();
                    string shift = lookUpCA.EditValue.ToString();

                    string lotNo = GenerateLotNo(
                        itemCode,
                        dateNN.DateTime.Date,
                        shift);

                    SaveData();

                    XtraMessageBox.Show(
                        $"Nhập kho thành công.\r\n\r\nLOT: {lotNo}\r\nSố lượng: {textSLNHAP.Text}",
                        "Hoàn tất",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    LoadData();

                    lookUpMHNOQR.Focus();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(
                        $"Nhập kho thất bại.\r\n\r\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            #endregion

            #region SỬA LOT

            private void cmdSua_ButtonClick(
                object sender,
                DevExpress.XtraEditors.Controls.ButtonPressedEventArgs e)
            {
                GridView view = gridVCTK;

                if (view.FocusedRowHandle < 0)
                    return;

                string lot = Convert.ToString(
                    view.GetRowCellValue(
                        view.FocusedRowHandle,
                        "lot"));

                if (string.IsNullOrWhiteSpace(lot))
                    return;

                int slNhap = GetGridInt(view, "slnhap");
                int slConLai = GetGridInt(view, "SLCONLAI");
                int slXuat = GetGridInt(view, "slxuat");
                int slSx = GetGridInt(view, "slsx");

                if (slNhap < slXuat)
                {
                    XtraMessageBox.Show(
                        "SL nhập không được nhỏ hơn SL xuất.",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                if (slConLai != slNhap - slXuat)
                {
                    XtraMessageBox.Show(
                        $"SL còn lại phải bằng SL nhập - SL xuất.\r\n\r\n" +
                        $"SL nhập: {slNhap}\r\n" +
                        $"SL xuất: {slXuat}\r\n" +
                        $"SL còn lại đúng: {slNhap - slXuat}",
                        "Dữ liệu không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                const string sql = @"
                UPDATE STOCKTP
                SET
                    SLNHAP = @SLNhap,
                    SLCONLAI = @SLConLai,
                    SLSX = @SLSX,
                    SLXUAT = @SLXuat
                WHERE LOT = @LOT;";

                try
                {
                    int affected = _sql.ExecuteNonQuery(
                        _sql.B7R2_FCCdb,
                        sql,
                        new SqlParameter("@SLNhap", SqlDbType.Int)
                        {
                            Value = slNhap
                        },
                        new SqlParameter("@SLConLai", SqlDbType.Int)
                        {
                            Value = slConLai
                        },
                        new SqlParameter("@SLSX", SqlDbType.Int)
                        {
                            Value = slSx
                        },
                        new SqlParameter("@SLXuat", SqlDbType.Int)
                        {
                            Value = slXuat
                        },
                        new SqlParameter("@LOT", SqlDbType.NVarChar, 100)
                        {
                            Value = lot
                        });

                    if (affected > 0)
                    {
                        XtraMessageBox.Show(
                            $"Đã cập nhật LOT [{lot}].",
                            "Hoàn tất",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        LoadStock();
                    }
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(
                        $"Không thể cập nhật LOT.\r\n\r\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            private int GetGridInt(
                GridView view,
                string fieldName)
            {
                object value = view.GetRowCellValue(
                    view.FocusedRowHandle,
                    fieldName);

                if (value == null || value == DBNull.Value)
                    return 0;

                if (int.TryParse(value.ToString(), out int result))
                    return result;

                return 0;
            }

            #endregion

            #region XÓA LOT

            private void cmdxoa_Click(object sender, EventArgs e)
            {
                if (gridVCTK.FocusedRowHandle < 0)
                    return;

                string lot = Convert.ToString(
                    gridVCTK.GetRowCellValue(
                        gridVCTK.FocusedRowHandle,
                        "lot"));

                if (string.IsNullOrWhiteSpace(lot))
                    return;

                DialogResult result = XtraMessageBox.Show(
                    $"Bạn có chắc muốn xóa LOT:\r\n\r\n{lot}?",
                    "Xác nhận xóa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                try
                {
                    const string sql = @"
                    DELETE FROM STOCKTP
                    WHERE LOT = @LOT;";

                    int affected = _sql.ExecuteNonQuery(
                        _sql.B7R2_FCCdb,
                        sql,
                        new SqlParameter("@LOT", SqlDbType.NVarChar, 100)
                        {
                            Value = lot
                        });

                    if (affected > 0)
                    {
                        XtraMessageBox.Show(
                            $"Đã xóa LOT [{lot}].",
                            "Hoàn tất",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        XtraMessageBox.Show(
                            $"Không tìm thấy LOT [{lot}].",
                            "Thông báo",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }

                    LoadStock();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(
                        $"Không thể xóa LOT.\r\n\r\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            #endregion

            #region COPY GRID

            private void gridVCTK_KeyDown(
                object sender,
                KeyEventArgs e)
            {
                GridView view = sender as GridView;

                if (view == null)
                    return;

                if (e.Control && e.KeyCode == Keys.C)
                {
                    object value = view.GetRowCellValue(
                        view.FocusedRowHandle,
                        view.FocusedColumn);

                    if (value != null && value != DBNull.Value)
                    {
                        Clipboard.SetText(value.ToString());
                    }

                    e.Handled = true;
                }
            }

            #endregion

            #region VALIDATING

            private void textCode_Properties_Validating(
                object sender,
                CancelEventArgs e)
            {
                TextEdit edit = sender as TextEdit;

                if (string.IsNullOrWhiteSpace(edit.Text))
                {
                    edit.ErrorText = "Không được để trống mã hàng.";
                    e.Cancel = true;
                }
                else
                {
                    edit.ErrorText = string.Empty;
                }
            }

            private void textName_Properties_Validating(
                object sender,
                CancelEventArgs e)
            {
                TextEdit edit = sender as TextEdit;

                if (string.IsNullOrWhiteSpace(edit.Text))
                {
                    edit.ErrorText = "Không được để trống tên hàng.";
                    e.Cancel = true;
                }
                else
                {
                    edit.ErrorText = string.Empty;
                }
            }

            private void lookUpMHNOQR_Properties_Validating(
                object sender,
                CancelEventArgs e)
            {
                LookUpEdit edit = sender as LookUpEdit;

                if (edit.EditValue == null ||
                    string.IsNullOrWhiteSpace(edit.EditValue.ToString()))
                {
                    edit.ErrorText = "Hãy chọn mã hàng.";
                    e.Cancel = true;
                }
                else
                {
                    edit.ErrorText = string.Empty;
                }
            }

            private void dateNN_Properties_Validating(
                object sender,
                CancelEventArgs e)
            {
                DateEdit edit = sender as DateEdit;

                if (edit.EditValue == null)
                {
                    edit.ErrorText = "Hãy chọn ngày sản xuất.";
                    e.Cancel = true;
                }
                else
                {
                    edit.ErrorText = string.Empty;
                }
            }

            private void textSLNHAP_Properties_Validating(
                object sender,
                CancelEventArgs e)
            {
                TextEdit edit = sender as TextEdit;

                if (!int.TryParse(
                        edit.Text.Trim(),
                        out int quantity) ||
                    quantity <= 0)
                {
                    edit.ErrorText =
                        "Số lượng phải là số nguyên lớn hơn 0.";

                    e.Cancel = true;
                }
                else
                {
                    edit.ErrorText = string.Empty;
                }
            }

            private void lookUpCA_Properties_Validating(
                object sender,
                CancelEventArgs e)
            {
                LookUpEdit edit = sender as LookUpEdit;

                if (edit.EditValue == null ||
                    string.IsNullOrWhiteSpace(edit.EditValue.ToString()))
                {
                    edit.ErrorText = "Hãy chọn ca sản xuất.";
                    e.Cancel = true;
                }
                else
                {
                    edit.ErrorText = string.Empty;
                }
            }

            #endregion
        }
    }
