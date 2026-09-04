using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.ClassSQL;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
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
    public partial class FormInspectionConfig : DevExpress.XtraEditors.XtraForm
    {
        // ✅ Inject Service thay vì SQLPROVIDER trực tiếp
        private readonly IInspectionConfigService _svc;
        private readonly IWarehouseService _warehouseSvc; // lấy ItemName
        private GridControl _grid;
        private GridView _gridView;
        private List<InspectionConfig> _configs;
        private DataTable _dtItems; // ✅ danh sách mã hàng từ B20Item

        public FormInspectionConfig(IInspectionConfigService svc,
        IWarehouseService warehouseSvc)
        {
            _svc = svc
           ?? throw new ArgumentNullException(nameof(svc));
            _warehouseSvc = warehouseSvc
                ?? throw new ArgumentNullException(nameof(warehouseSvc));
            InitializeComponent();
            LoadItems();  // ✅ load trước
            BuildUI();
        
            LoadData();
        }

        // ✅ Load danh sách mã hàng active từ B20Item
        private void LoadItems()
        {
            // ✅ Dùng WarehouseService — không gọi SQL trực tiếp
            // Tạm dùng SQLPROVIDER chỉ cho lookup B20Item vì chưa có ItemRepository
            // TODO: tách ra IItemRepository khi có domain Item
            var sql = new SQLPROVIDER();
            _dtItems = sql.LoadData1(sql.B7R2_FCCdb,
                "SELECT Code, Name FROM vB20Item " +
                "WHERE IsActive=1 AND IsGroup=0 ORDER BY Code");
        }

        private void BuildUI()
        {
            this.Text = "Quản lý mã hàng cần kiểm tra";
            this.Size = new Size(850, 520);
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

            // ── Grid ──────────────────────────────────────────
            _grid = new GridControl { Dock = DockStyle.Fill };
            _gridView = new GridView(_grid);
            _gridView.OptionsBehavior.Editable = true;
            _grid.MainView = _gridView;

            // ✅ RepositoryItemGridLookUpEdit cho cột ItemCode
            var lookupItem = new DevExpress.XtraEditors.Repository.RepositoryItemGridLookUpEdit();
            lookupItem.DataSource = _dtItems;
            lookupItem.ValueMember = "Code";
            lookupItem.DisplayMember = "Code";
            lookupItem.NullText = "-- Chọn mã hàng --";
            lookupItem.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.Standard;
            lookupItem.SearchMode = DevExpress.XtraEditors.Repository.GridLookUpSearchMode.AutoSuggest;

            // Cấu hình cột hiển thị trong popup
            var popupView = lookupItem.View;
            popupView.Columns.Clear();
            popupView.Columns.AddVisible("Code", "Mã hàng");
            popupView.Columns.AddVisible("Name", "Tên hàng");
            popupView.Columns["Code"].Width = 150;
            popupView.Columns["Name"].Width = 300;
            popupView.OptionsView.ShowAutoFilterRow = true; // ✅ cho phép gõ tìm

            // Hiển thị cả Code + Name ở ô đã chọn
            lookupItem.Closed += (s, e) =>
            {
                if (s is DevExpress.XtraEditors.GridLookUpEdit editor)
                {
                    var row = lookupItem.View.GetFocusedRow() as DataRowView;
                    if (row != null)
                        editor.Text = row["Code"].ToString();
                }
            };

            _grid.RepositoryItems.Add(lookupItem);

            // ── Các cột ───────────────────────────────────────
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "ItemCode",
                Caption = "Mã hàng (*)",
                Width = 200,
                VisibleIndex = 0,
                ColumnEdit = lookupItem // ✅ gán lookup
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "ItemName",
                Caption = "Tên hàng",
                Width = 250,
                VisibleIndex = 1,
                OptionsColumn = { AllowEdit = false }, // readonly — tự điền
                AppearanceCell = { BackColor = Color.WhiteSmoke }
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "DefaultQty",
                Caption = "Số thùng KT",
                Width = 100,
                VisibleIndex = 2
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "CheckItemCode",
                Caption = "KT Mã hàng",
                Width = 90,
                VisibleIndex = 3,
                ColumnEdit = new DevExpress.XtraEditors.Repository.RepositoryItemCheckEdit()
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "CheckLotNo",
                Caption = "KT LotNo",
                Width = 90,
                VisibleIndex = 4,
                ColumnEdit = new DevExpress.XtraEditors.Repository.RepositoryItemCheckEdit()
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "CheckNSX",
                Caption = "KT Ngày SX",
                Width = 90,
                VisibleIndex = 5,
                ColumnEdit = new DevExpress.XtraEditors.Repository.RepositoryItemCheckEdit()
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "IsActive",
                Caption = "Áp dụng",
                Width = 80,
                VisibleIndex = 6,
                ColumnEdit = new DevExpress.XtraEditors.Repository.RepositoryItemCheckEdit()
            });
            _gridView.Columns.Add(new GridColumn
            {
                FieldName = "Note",
                Caption = "Ghi chú",
                Width = 180,
                VisibleIndex = 7
            });

            // ✅ Khi chọn ItemCode → tự điền ItemName
            _gridView.CellValueChanged += (s, e) =>
            {
                if (e.Column.FieldName != "ItemCode") return;

                string code = e.Value?.ToString();
                if (string.IsNullOrEmpty(code)) return;

                // Tìm tên hàng từ _dtItems
                var rows = _dtItems.Select($"Code = '{code.Replace("'", "''")}'");
                if (rows.Length > 0)
                {
                    _gridView.SetRowCellValue(e.RowHandle, "ItemName",
                        rows[0]["Name"].ToString());
                }
            };

            mainLayout.Controls.Add(_grid, 0, 0);

            // ── Buttons ───────────────────────────────────────
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5, 5, 5, 0)
            };

            var btnAdd = new SimpleButton { Text = "➕ Thêm", Width = 90, Height = 32 };
            btnAdd.Click += (s, e) =>
            {
                _gridView.AddNewRow();
                int h = _gridView.FocusedRowHandle;
                _gridView.SetRowCellValue(h, "DefaultQty", 1);
                _gridView.SetRowCellValue(h, "CheckItemCode", true);
                _gridView.SetRowCellValue(h, "CheckLotNo", true);
                _gridView.SetRowCellValue(h, "CheckNSX", true);
                _gridView.SetRowCellValue(h, "IsActive", true);
            };

            var btnDelete = new SimpleButton
            {
                Text = "🗑 Xóa",
                Width = 80,
                Height = 32,
                Appearance = { ForeColor = Color.Red }
            };
            btnDelete.Click += BtnDelete_Click;

            var btnSave = new SimpleButton
            {
                Text = "💾 Lưu",
                Width = 90,
                Height = 32,
                Appearance = { BackColor = Color.SeaGreen, ForeColor = Color.White }
            };
            btnSave.Click += BtnSave_Click;

            var btnClose = new SimpleButton { Text = "Đóng", Width = 80, Height = 32 };
            btnClose.Click += (s, e) => this.Close();

            btnPanel.Controls.Add(btnAdd);
            btnPanel.Controls.Add(btnDelete);
            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnClose);
            mainLayout.Controls.Add(btnPanel, 0, 1);

            this.Controls.Add(mainLayout);
        }

        private void LoadData()
        {
            // ✅ Gọi Service thay vì SQL trực tiếp
            _configs = _svc.GetAll();

            // Convert sang DataTable để bind grid giữ nguyên
            DataTable dt = ToDataTable(_configs);
            _grid.DataSource = dt;
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_gridView.FocusedRowHandle < 0) return;

            string itemCode = _gridView
                .GetFocusedRowCellValue("ItemCode")?.ToString();
            if (XtraMessageBox.Show(
                    $"Xóa mã [{itemCode}] khỏi danh sách kiểm tra?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes) return;

            int configId = DbValueHelper.ToInt(
                _gridView.GetFocusedRowCellValue("ConfigId"));

            if (configId > 0)
                // ✅ Gọi Service
                _svc.Delete(configId);

            _gridView.DeleteRow(_gridView.FocusedRowHandle);
        }


        private void BtnSave_Click(object sender, EventArgs e)
        {
            _gridView.PostEditor();
            _gridView.UpdateCurrentRow();

            int saved = 0, error = 0;
            DataTable dt = _grid.DataSource as DataTable;
            if (dt == null) return;

            foreach (DataRow row in dt.Rows)
            {
                if (row.RowState == DataRowState.Unchanged) continue;

                string itemCode = row["ItemCode"]?.ToString();
                if (string.IsNullOrWhiteSpace(itemCode)) continue;

                try
                {
                    var cfg = new InspectionConfig
                    {
                        ConfigId = DbValueHelper.ToInt(row["ConfigId"]),
                        ItemCode = itemCode,
                        DefaultQty = DbValueHelper.ToInt(row["DefaultQty"]),
                        CheckItemCode = Convert.ToBoolean(row["CheckItemCode"]),
                        CheckLotNo = Convert.ToBoolean(row["CheckLotNo"]),
                        CheckNSX = Convert.ToBoolean(row["CheckNSX"]),
                        IsActive = Convert.ToBoolean(row["IsActive"]),
                        Note = row["Note"]?.ToString()
                    };

                    // ✅ Service tự quyết Insert hay Update
                    _svc.Save(cfg);
                    saved++;
                }
                catch (Exception ex)
                {
                    error++;
                    System.Diagnostics.Debug.WriteLine(
                        $"Lỗi lưu {itemCode}: {ex.Message}");
                }
            }

            dt.AcceptChanges();
            XtraMessageBox.Show(
                $"Đã lưu {saved} dòng." +
                (error > 0 ? $" Lỗi: {error} dòng." : ""),
                "Kết quả", MessageBoxButtons.OK,
                error > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        // ── Helper: List → DataTable ─────────────────────────────────────
        private static DataTable ToDataTable(List<InspectionConfig> list)
        {
            var dt = new DataTable();
            dt.Columns.Add("ConfigId", typeof(int));
            dt.Columns.Add("ItemCode", typeof(string));
            dt.Columns.Add("ItemName", typeof(string));
            dt.Columns.Add("DefaultQty", typeof(int));
            dt.Columns.Add("CheckItemCode", typeof(bool));
            dt.Columns.Add("CheckLotNo", typeof(bool));
            dt.Columns.Add("CheckNSX", typeof(bool));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("Note", typeof(string));

            foreach (var c in list)
            {
                dt.Rows.Add(
                    c.ConfigId, c.ItemCode, c.ItemName,
                    c.DefaultQty,
                    c.CheckItemCode, c.CheckLotNo, c.CheckNSX,
                    c.IsActive, c.Note ?? "");
            }
            dt.AcceptChanges();
            return dt;
        }
        private SqlParameter[] BuildParams(DataRow row) => new[]
        {
        new SqlParameter("@ItemCode",      SqlDbType.NVarChar) { Value = row["ItemCode"] },
        new SqlParameter("@DefaultQty",    SqlDbType.Int)      { Value = row["DefaultQty"]    == DBNull.Value ? 1    : row["DefaultQty"]    },
        new SqlParameter("@CheckItemCode", SqlDbType.Bit)      { Value = row["CheckItemCode"] == DBNull.Value ? true : row["CheckItemCode"] },
        new SqlParameter("@CheckLotNo",    SqlDbType.Bit)      { Value = row["CheckLotNo"]    == DBNull.Value ? true : row["CheckLotNo"]    },
        new SqlParameter("@CheckNSX",      SqlDbType.Bit)      { Value = row["CheckNSX"]      == DBNull.Value ? true : row["CheckNSX"]      },
        new SqlParameter("@IsActive",      SqlDbType.Bit)      { Value = row["IsActive"]      == DBNull.Value ? true : row["IsActive"]      },
        new SqlParameter("@Note",          SqlDbType.NVarChar) { Value = row["Note"]          == DBNull.Value ? (object)DBNull.Value : row["Note"] },
    };
    }
}