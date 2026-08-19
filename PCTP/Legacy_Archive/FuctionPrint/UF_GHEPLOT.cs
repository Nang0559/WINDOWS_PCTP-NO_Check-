using DevExpress.Data;
using DevExpress.Utils.Menu;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using PCTP.ClassSQL;
using PCTP.FuctionMain;
using PCTP.QRCODE_HVN.Report;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.FuctionPrint
{
    public partial class UF_GHEPLOT : XtraForm
    {
        #region Fields

        private readonly SQLPROVIDER _sql = new SQLPROVIDER();
        private readonly ClassFunction _qrFunction = new ClassFunction();

        private readonly BindingList<Record> _records =
            new BindingList<Record>();

        private readonly BindingList<DetailGL> _recordsGL =
            new BindingList<DetailGL>();

        private readonly BindingList<DetailGL> _recordsIN =
            new BindingList<DetailGL>();

        /// <summary>
        /// STT các record đã được ghép.
        /// </summary>
        private readonly HashSet<int> _mergedRecordIds =
            new HashSet<int>();

        #endregion

        #region Constructor

        public UF_GHEPLOT()
        {
            InitializeComponent();

            InitializeForm();
            InitializeGrid();
        }

        #endregion

        #region Initialize

        private void InitializeForm()
        {
            GCT_DOCQR.DataSource = _records;
            GCT_GEPLOT.DataSource = _recordsGL;

            btGL.Visible = false;

            txtDocQR.Focus();
        }

        private void InitializeGrid()
        {
            GV_ReadQR.ClearSorting();

            if (GV_ReadQR.Columns["ItemCode"] != null)
            {
                GV_ReadQR.Columns["ItemCode"].SortOrder =
                    ColumnSortOrder.Ascending;
            }

            foreach (GridColumn column in GV_ReadQR.VisibleColumns)
            {
                if (column.FieldName == "SLG")
                {
                    column.OptionsColumn.ReadOnly = false;
                    column.AppearanceHeader.BackColor =
                        Color.CornflowerBlue;
                }
                else
                {
                    column.OptionsColumn.ReadOnly = true;
                    column.AppearanceHeader.BackColor =
                        Color.AliceBlue;
                }
            }
        }

        #endregion

        #region SQL - Product

        /// <summary>
        /// Lấy thông tin sản phẩm theo mã.
        /// </summary>
        private ProductInfo GetProductInfo(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                return null;

            const string sql = @"
SELECT TOP 1
       Name,
       CAST(MinCloseQty AS INT) AS Qty,
       Model
FROM B20Item
WHERE Code = @ItemCode;";

            DataTable table = _sql.LoadData1(
                _sql.B7R2_FCCdb,
                sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 50)
                {
                    Value = itemCode.Trim()
                });

            if (table == null || table.Rows.Count == 0)
                return null;

            DataRow row = table.Rows[0];

            return new ProductInfo
            {
                Name = row["Name"] == DBNull.Value
                    ? string.Empty
                    : row["Name"].ToString(),

                Qty = row["Qty"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(row["Qty"]),

                Model = row["Model"] == DBNull.Value
                    ? string.Empty
                    : row["Model"].ToString()
            };
        }

        /// <summary>
        /// Lấy tên Gear.
        /// </summary>
        private string GetGearName(int code)
        {
            const string sql = @"
SELECT TOP 1 Name
FROM B20Gear
WHERE Code = @Code;";

            object result = _sql.ExecuteScalar(
                _sql.B7R2_FCCdb,
                sql,
                new[]
                {
                    new SqlParameter("@Code", SqlDbType.Int)
                    {
                        Value = code
                    }
                });

            return result == null || result == DBNull.Value
                ? string.Empty
                : result.ToString();
        }

        /// <summary>
        /// Kiểm tra Item có phải YAMH hay không.
        /// </summary>
        private bool IsYamh(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                return false;

            const string sql = @"
SELECT TOP 1 CustomerCode
FROM B20ItemQuyCach
WHERE ItemCode = @ItemCode;";

            object result = _sql.ExecuteScalar(
                _sql.B7R2_FCCdb,
                sql,
                new[]
                {
                    new SqlParameter("@ItemCode", SqlDbType.NVarChar, 50)
                    {
                        Value = itemCode.Trim()
                    }
                });

            return result != null &&
                   result != DBNull.Value &&
                   string.Equals(
                       result.ToString(),
                       "0100002",
                       StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region QR

        private void txtDocQR_KeyPress(
            object sender,
            KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)Keys.Enter)
                return;

            e.Handled = true;

            ProcessQRCode(txtDocQR.Text);

            txtDocQR.SelectAll();
        }

        private void ProcessQRCode(string qrCode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
                return;

            try
            {
                string[] lotInfo = _qrFunction.LOT(qrCode);

                if (lotInfo == null || lotInfo.Length < 4)
                {
                    ShowWarning("QR Code không đúng định dạng.");
                    return;
                }

                string lotNo = lotInfo[0];
                string itemCode = lotInfo[1];

                if (string.IsNullOrWhiteSpace(lotNo) ||
                    string.IsNullOrWhiteSpace(itemCode))
                {
                    ShowWarning("Không đọc được Lot hoặc mã sản phẩm.");
                    return;
                }

                ProductInfo product = GetProductInfo(itemCode);

                if (product == null)
                {
                    ShowWarning(
                        $"Không tìm thấy thông tin sản phẩm [{itemCode}].");
                    return;
                }

                if (!TryParseProductionDate(lotNo, out DateTime productionDate))
                {
                    ShowWarning(
                        $"Không xác định được ngày sản xuất từ Lot [{lotNo}].");
                    return;
                }

                bool isYamh = IsYamh(itemCode);

                List<string> lotData =
                    _qrFunction.DLLOT(lotNo, isYamh);

                if (lotData == null || lotData.Count < 2)
                {
                    ShowWarning(
                        $"Không lấy được thông tin Lot [{lotNo}].");
                    return;
                }

                if (!int.TryParse(lotData[1], out int shiftCode))
                {
                    ShowWarning("ShiftCode không hợp lệ.");
                    return;
                }

                if (!int.TryParse(lotInfo[3], out int quantity))
                {
                    ShowWarning("Số lượng trên QR không hợp lệ.");
                    return;
                }

                string gear = string.Empty;

                if (isYamh)
                {
                    if (!int.TryParse(
                            lotNo.Substring(12, 1),
                            out int gearCode))
                    {
                        ShowWarning(
                            $"Không xác định được Gear từ Lot [{lotNo}].");
                        return;
                    }

                    gear = GetGearName(gearCode);
                }

                // Không cho add Lot trùng.
                if (_records.Any(x =>
                        string.Equals(
                            x.ItemLotCode,
                            lotNo,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    ShowWarning($"Lot [{lotNo}] đã được quét.");
                    return;
                }

                var record = CreateRecord(
                    lotNo,
                    itemCode,
                    product,
                    gear,
                    productionDate,
                    shiftCode,
                    quantity,
                    qrCode);

                _records.Add(record);

                RefreshGrid();

                txtDocQR.Clear();
            }
            catch (Exception ex)
            {
                ShowError(
                    "Không thể xử lý QR Code.",
                    ex);
            }
        }

        private static bool TryParseProductionDate(
            string lotNo,
            out DateTime productionDate)
        {
            productionDate = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(lotNo) ||
                lotNo.Length < 6)
            {
                return false;
            }

            string dateText = lotNo.Substring(0, 6);

            return DateTime.TryParseExact(
                dateText,
                "yyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out productionDate);
        }

        private Record CreateRecord(
            string lotNo,
            string itemCode,
            ProductInfo product,
            string gear,
            DateTime productionDate,
            int shiftCode,
            int quantity,
            string qrCode)
        {
            return new Record
            {
                STT = GetNextSTT(),
                ItemCode = itemCode,
                ItemLotCode = lotNo,
                ItemName = product.Name,
                DocDate = productionDate,
                ShiftCode = shiftCode,
                Model = gear,
                QCDG = product.Qty,
                Quantity9 = quantity,
                SLG = 0,
                State = true,
                QRCODE = qrCode
            };
        }

        private int GetNextSTT()
        {
            return _records.Count == 0
                ? 1
                : _records.Max(x => x.STT) + 1;
        }

        #endregion

        #region Merge Lot

        private void btGL_Click(object sender, EventArgs e)
        {
            int[] selectedRows = GV_ReadQR.GetSelectedRows();

            if (selectedRows.Length < 2)
            {
                ShowWarning(
                    "Bạn chưa chọn đủ Lot để ghép.\r\n" +
                    "Vui lòng chọn từ 2 Lot trở lên.");
                return;
            }

            if (!CheckCanMerge(selectedRows))
                return;

            MergeSelectedLots(selectedRows);

            RefreshGrid();

            GV_ReadQR.ClearSelection();
            UpdateMergeButtonState();
        }

        private bool CheckCanMerge(int[] selectedRows)
        {
            if (selectedRows == null || selectedRows.Length < 2)
                return false;

            string itemCode = null;
            int totalMergeQuantity = 0;
            int qualityQuantity = 0;

            foreach (int rowHandle in selectedRows)
            {
                if (!GV_ReadQR.IsDataRow(rowHandle))
                    continue;

                Record record =
                    GV_ReadQR.GetRow(rowHandle) as Record;

                if (record == null)
                    continue;

                if (_mergedRecordIds.Contains(record.STT))
                {
                    ShowWarning(
                        $"Lot [{record.ItemLotCode}] đã được ghép.");
                    return false;
                }

                if (record.SLG <= 0)
                {
                    ShowWarning(
                        $"Lot [{record.ItemLotCode}] chưa nhập số lượng ghép.");
                    return false;
                }

                if (record.SLG > record.Quantity9)
                {
                    ShowWarning(
                        $"Số lượng ghép Lot [{record.ItemLotCode}] " +
                        $"không được lớn hơn số lượng tem.");
                    return false;
                }

                if (string.IsNullOrEmpty(itemCode))
                {
                    itemCode = record.ItemCode;
                    qualityQuantity = record.QCDG;
                }
                else if (!string.Equals(
                             itemCode,
                             record.ItemCode,
                             StringComparison.OrdinalIgnoreCase))
                {
                    ShowWarning(
                        "Không thể ghép các Lot khác mã sản phẩm.");
                    return false;
                }

                totalMergeQuantity += record.SLG;
            }

            if (totalMergeQuantity != qualityQuantity)
            {
                ShowWarning(
                    $"Tổng SL ghép = {totalMergeQuantity:N0}, " +
                    $"nhưng QCDG = {qualityQuantity:N0}.\r\n\r\n" +
                    "Vui lòng kiểm tra lại số lượng.");
                return false;
            }

            return true;
        }

        private void MergeSelectedLots(int[] selectedRows)
        {
            string itemCode = string.Empty;
            string itemName = string.Empty;
            string model = string.Empty;

            int shiftCode = 0;
            int totalQuantity = 0;
            int qualityQuantity = 0;

            DateTime productionDate = DateTime.MinValue;

            var lotParts = new List<string>();

            foreach (int rowHandle in selectedRows)
            {
                Record record =
                    GV_ReadQR.GetRow(rowHandle) as Record;

                if (record == null)
                    continue;

                itemCode = record.ItemCode;
                itemName = record.ItemName;
                model = record.Model;
                shiftCode = record.ShiftCode;
                productionDate = record.DocDate;
                qualityQuantity = record.QCDG;

                totalQuantity += record.SLG;

                lotParts.Add(
                    $"{GetLotDisplayCode(record.ItemLotCode, record.ItemCode)}-{record.SLG}");

                // Nếu ghép một phần Lot thì tạo Lot tách.
                if (record.Quantity9 > record.SLG)
                {
                    CreateRemainingLot(record);
                }

                record.State = false;

                _mergedRecordIds.Add(record.STT);
            }

            string mergedLot =
                string.Join(",", lotParts);

            var detail = new DetailGL
            {
                STT = _recordsGL.Count + 1,
                ItemCode = itemCode,
                ItemName = itemName,
                Model = model,
                ItemLotCode = mergedLot,
                DocDate = productionDate,
                ShiftCode = shiftCode,
                Quantity9 = totalQuantity,
                QRCODE =
                    $"{mergedLot}:{itemCode}:" +
                    $"{productionDate:dd/MM/yyyy}:{totalQuantity}"
            };

            _recordsGL.Add(detail);

            btGL.Visible = false;
        }

        private string GetLotDisplayCode(
            string lotNo,
            string itemCode)
        {
            try
            {
                bool isYamh = IsYamh(itemCode);

                List<string> data =
                    _qrFunction.DLLOT(lotNo, isYamh);

                if (data != null &&
                    data.Count > 0 &&
                    !string.IsNullOrWhiteSpace(data[0]))
                {
                    return data[0];
                }
            }
            catch
            {
                // Fallback về Lot gốc.
            }

            return lotNo;
        }

        private void CreateRemainingLot(Record record)
        {
            int remainingQuantity =
                record.Quantity9 - record.SLG;

            if (remainingQuantity <= 0)
                return;

            string lotTach =
                record.ItemLotCode.Substring(0, 23) +
                remainingQuantity.ToString().PadLeft(4, '0');

            string qrCode =
                $"{lotTach}:{record.ItemCode}:" +
                $"{record.DocDate:dd/MM/yyyy}:" +
                $"{remainingQuantity}";

            ProductInfo product =
                GetProductInfo(record.ItemCode);

            var remainingRecord = new Record
            {
                STT = GetNextSTT(),
                ItemCode = record.ItemCode,
                ItemLotCode = lotTach,
                ItemName = record.ItemName,
                Model = record.Model,
                DocDate = record.DocDate,
                ShiftCode = record.ShiftCode,
                QCDG = product?.Qty ?? record.QCDG,
                Quantity9 = remainingQuantity,
                SLG = 0,
                State = true,
                QRCODE = qrCode
            };

            _records.Add(remainingRecord);
        }

        #endregion

        #region Grid Validation

        private bool ShouldBeReadOnly(int rowHandle)
        {
            if (!GV_ReadQR.IsDataRow(rowHandle))
                return true;

            Record record =
                GV_ReadQR.GetRow(rowHandle) as Record;

            return record == null ||
                   _mergedRecordIds.Contains(record.STT);
        }

        private void GV_ReadQR_ShowingEditor(
            object sender,
            CancelEventArgs e)
        {
            e.Cancel =
                ShouldBeReadOnly(GV_ReadQR.FocusedRowHandle);

            UpdateMergeButtonState();
        }

        private void GV_ReadQR_ValidatingEditor(
            object sender,
            DevExpress.XtraEditors.Controls.BaseContainerValidateEditorEventArgs e)
        {
            GridView view = sender as GridView;

            if (view == null)
                return;

            GridColumn column = view.FocusedColumn;

            if (column == null ||
                column.FieldName != nameof(Record.SLG))
            {
                return;
            }

            Record record =
                view.GetRow(view.FocusedRowHandle) as Record;

            if (record == null)
            {
                e.Valid = false;
                e.ErrorText = "Không xác định được Lot.";
                return;
            }

            if (!int.TryParse(
                    Convert.ToString(e.Value),
                    out int quantity))
            {
                e.Valid = false;
                e.ErrorText =
                    "Số lượng ghép phải là số nguyên.";
                return;
            }

            if (quantity <= 0)
            {
                e.Valid = false;
                e.ErrorText =
                    "Số lượng ghép phải lớn hơn 0.";
                return;
            }

            if (quantity > record.Quantity9)
            {
                e.Valid = false;
                e.ErrorText =
                    $"Số lượng ghép không được lớn hơn " +
                    $"{record.Quantity9:N0}.";
                return;
            }

            e.Valid = true;
        }

        private void GV_ReadQR_RowStyle(
            object sender,
            RowStyleEventArgs e)
        {
            if (e.RowHandle < 0)
                return;

            Record record =
                GV_ReadQR.GetRow(e.RowHandle) as Record;

            if (record == null)
                return;

            if (_mergedRecordIds.Contains(record.STT))
            {
                e.Appearance.BackColor = Color.MistyRose;
                e.Appearance.ForeColor = Color.Gray;
            }
        }

        #endregion

        #region Grid Mouse

        private void GV_ReadQR_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            GridView view = sender as GridView;

            if (view == null)
                return;

            GridHitInfo hitInfo =
                view.CalcHitInfo(e.Location);

            if (!hitInfo.InDataRow)
                return;

            object value =
                view.GetRowCellValue(
                    hitInfo.RowHandle,
                    nameof(Record.SLG));

            if (value == null ||
                !int.TryParse(value.ToString(), out int slg) ||
                slg <= 0)
            {
                view.UnselectRow(hitInfo.RowHandle);
            }
        }

        private void GV_ReadQR_MouseUp(
            object sender,
            MouseEventArgs e)
        {
            GridView view = sender as GridView;

            if (view == null)
                return;

            GridHitInfo hitInfo =
                view.CalcHitInfo(e.Location);

            if (!hitInfo.InDataRow)
                return;

            UpdateSelectionRules(hitInfo.RowHandle);

            UpdateMergeButtonState();
        }

        private void UpdateSelectionRules(int clickedRowHandle)
        {
            int[] selectedRows =
                GV_ReadQR.GetSelectedRows();

            if (!GV_ReadQR.IsDataRow(clickedRowHandle))
                return;

            Record clickedRecord =
                GV_ReadQR.GetRow(clickedRowHandle) as Record;

            if (clickedRecord == null)
                return;

            if (clickedRecord.SLG <= 0)
            {
                GV_ReadQR.UnselectRow(clickedRowHandle);
                return;
            }

            foreach (int rowHandle in selectedRows)
            {
                if (rowHandle == clickedRowHandle)
                    continue;

                Record record =
                    GV_ReadQR.GetRow(rowHandle) as Record;

                if (record == null)
                    continue;

                if (!string.Equals(
                        record.ItemCode,
                        clickedRecord.ItemCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    GV_ReadQR.UnselectRow(clickedRowHandle);

                    ShowWarning(
                        "Chỉ được chọn các Lot cùng mã sản phẩm.");
                    break;
                }
            }
        }

        private void UpdateMergeButtonState()
        {
            int[] rows =
                GV_ReadQR.GetSelectedRows();

            btGL.Visible = rows.Length >= 2;
        }

        #endregion

        #region Popup Menu

        private void GV_ReadQR_PopupMenuShowing(
            object sender,
            PopupMenuShowingEventArgs e)
        {
            if (e.MenuType != GridMenuType.Row)
                return;

            GridView view = sender as GridView;

            if (view == null)
                return;

            e.Menu.Items.Clear();

            int rowHandle = e.HitInfo.RowHandle;

            if (!view.IsDataRow(rowHandle))
                return;

            DXMenuItem item =
                new DXMenuItem(
                    "Lấy lại tem",
                    OnBackClick);

            item.Tag =
                new RowInfo(view, rowHandle);

            if (imageCollection1 != null &&
                imageCollection1.Images.Count > 0)
            {
                item.ImageOptions.Image =
                    imageCollection1.Images[0];
            }

            e.Menu.Items.Add(item);
        }

        private void OnBackClick(
            object sender,
            EventArgs e)
        {
            if (GV_ReadQR.FocusedRowHandle < 0)
                return;

            object value =
                GV_ReadQR.GetRowCellValue(
                    GV_ReadQR.FocusedRowHandle,
                    nameof(Record.QRCODE));

            if (value != null)
                txtDocQR.Text = value.ToString();

            txtDocQR.Focus();
        }

        #endregion

        #region Print

        private void bt_Print_Click(
            object sender,
            EventArgs e)
        {
            if (_records.Count == 0)
            {
                ShowWarning("Không có dữ liệu để in.");
                return;
            }

            try
            {
                _recordsIN.Clear();

                // Các Lot đã ghép.
                foreach (DetailGL detail in _recordsGL)
                {
                    detail.MO =
                        IsYamh(detail.ItemCode)
                            ? "GEAR"
                            : string.Empty;

                    _recordsIN.Add(detail);
                }

                // Các Lot chưa ghép.
                foreach (Record record in _records)
                {
                    if (_mergedRecordIds.Contains(record.STT))
                        continue;

                    if (!record.State)
                        continue;

                    _recordsIN.Add(
                        new DetailGL
                        {
                            STT = _recordsIN.Count + 1,
                            ItemCode = record.ItemCode,
                            ItemName = record.ItemName,
                            Model = record.Model,
                            MO = IsYamh(record.ItemCode)
                                ? "GEAR"
                                : string.Empty,
                            ItemLotCode = record.ItemLotCode,
                            DocDate = record.DocDate,
                            ShiftCode = record.ShiftCode,
                            Quantity9 = record.Quantity9,
                            QRCODE = record.QRCODE
                        });
                }

                if (_recordsIN.Count == 0)
                {
                    ShowWarning("Không có dữ liệu phù hợp để in.");
                    return;
                }

                GHEPLOT report = new GHEPLOT
                {
                    DataSource = _recordsIN
                };

                using (ReportPrintTool printTool =
                       new ReportPrintTool(report))
                {
                    printTool.ShowPreviewDialog();
                }
            }
            catch (Exception ex)
            {
                ShowError(
                    "Không thể tạo báo cáo in.",
                    ex);
            }
        }

        #endregion

        #region Clear

        private void simpleButton1_Click(
            object sender,
            EventArgs e)
        {
            if (_records.Count == 0 &&
                _recordsGL.Count == 0)
            {
                return;
            }

            DialogResult result =
                XtraMessageBox.Show(
                    "Bạn có chắc muốn xóa toàn bộ dữ liệu hiện tại?",
                    "Xác nhận",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            _records.Clear();
            _recordsGL.Clear();
            _recordsIN.Clear();
            _mergedRecordIds.Clear();

            RefreshGrid();

            txtDocQR.Clear();
            txtDocQR.Focus();

            btGL.Visible = false;
        }

        #endregion

        #region Cell Merge

        private void GV_ReadQR_CellMerge(
            object sender,
            DevExpress.XtraGrid.Views.Grid.CellMergeEventArgs e)
        {
            if (e.Column.FieldName != nameof(Record.QCDG))
                return;

            string item1 =
                Convert.ToString(
                    GV_ReadQR.GetRowCellValue(
                        e.RowHandle1,
                        nameof(Record.ItemCode)));

            string item2 =
                Convert.ToString(
                    GV_ReadQR.GetRowCellValue(
                        e.RowHandle2,
                        nameof(Record.ItemCode)));

            e.Merge =
                string.Equals(
                    item1,
                    item2,
                    StringComparison.OrdinalIgnoreCase);

            e.Handled = true;
        }

        #endregion

        #region Helpers

        private void RefreshGrid()
        {
            GCT_DOCQR.RefreshDataSource();
            GCT_GEPLOT.RefreshDataSource();

            GV_ReadQR.RefreshData();
        }

        private static void ShowWarning(string message)
        {
            XtraMessageBox.Show(
                message,
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void ShowError(
            string message,
            Exception ex)
        {
            XtraMessageBox.Show(
                $"{message}\r\n\r\nChi tiết: {ex.Message}",
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        #endregion
    }

    #region Supporting Models

    internal sealed class ProductInfo
    {
        public string Name { get; set; }
        public int Qty { get; set; }
        public string Model { get; set; }
    }

    internal sealed class RowInfo
    {
        public RowInfo(
            GridView view,
            int rowHandle)
        {
            View = view;
            RowHandle = rowHandle;
        }

        public GridView View { get; }
        public int RowHandle { get; }
    }

    #endregion
}