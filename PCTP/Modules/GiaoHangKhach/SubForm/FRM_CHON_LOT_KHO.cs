using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN.SubForm
{
    public partial class FRM_CHON_LOT_KHO : XtraForm
    {
        public string LotGhep { get; private set; }

        private readonly string _maHang;
        private readonly int _soLuongCan;
        private DataTable _danhSachLot;

        public FRM_CHON_LOT_KHO(string maHang, int soLuong, DataTable danhSachLot)
        {
            InitializeComponent();
            _maHang = maHang;
            _soLuongCan = soLuong;
            _danhSachLot = danhSachLot ?? new DataTable();
        }

        private void FRM_CHON_LOT_KHO_Load(object sender, EventArgs e)
        {
            lblMaHang.Text = $"Mã hàng: {_maHang}";
            lblCanXuat.Text = $"Cần xuất: {_soLuongCan}";

            if (!_danhSachLot.Columns.Contains("SLCHON"))
                _danhSachLot.Columns.Add("SLCHON", typeof(int));

            foreach (DataRow row in _danhSachLot.Rows)
                row["SLCHON"] = 0;

            gridControl1.DataSource = _danhSachLot;

            var view = gridControl1.MainView as GridView;
            if (view != null)
            {
                foreach (GridColumn col in view.Columns)
                    col.OptionsColumn.AllowEdit = col.FieldName == "SLCHON";

                if (view.Columns["SLCHON"] != null)
                    view.Columns["SLCHON"].AppearanceCell.BackColor = Color.LightYellow;
            }
        }

        private bool IsFifoEnabled()
            => _danhSachLot.Columns.Contains("FIFO_RANK")
               && _danhSachLot.Columns.Contains("FIFO_REQUIRED");

        private static string LotKey(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return string.Empty;
            lot = lot.Trim();
            return lot.Length <= 13 ? lot : lot.Substring(0, 13);
        }

        private bool ValidateFifoSelection(List<Tuple<string, int>> selected, out string message)
        {
            message = null;
            if (!IsFifoEnabled() || selected == null || selected.Count == 0)
                return true;

            var selectedMap = selected
                .GroupBy(x => LotKey(x.Item1), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Item2), StringComparer.OrdinalIgnoreCase);

            var fifoRows = _danhSachLot.AsEnumerable()
                .Where(r => r["FIFO_RANK"] != DBNull.Value && Convert.ToInt32(r["FIFO_RANK"]) > 0)
                .OrderBy(r => Convert.ToInt32(r["FIFO_RANK"]))
                .ToList();

            int remainingSelected = selectedMap.Values.Sum();
            var consumedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow fifoRow in fifoRows)
            {
                string fifoKey = LotKey(fifoRow["LOT"]?.ToString());
                if (string.IsNullOrEmpty(fifoKey)) continue;

                int stock = fifoRow["SLCONLAI"] == DBNull.Value ? 0 : Math.Max(0, Convert.ToInt32(fifoRow["SLCONLAI"]));
                int expected = Math.Min(remainingSelected, stock);
                selectedMap.TryGetValue(fifoKey, out int selectedQty);

                if (expected == 0)
                    break;

                if (selectedQty != expected)
                {
                    string wrongLot = selected.FirstOrDefault(x => !string.Equals(LotKey(x.Item1), fifoKey, StringComparison.OrdinalIgnoreCase))?.Item1;
                    message = $"Vi phạm FIFO.\n\nMã hàng: {_maHang}\n" +
                              $"LOT {wrongLot ?? selected.First().Item1} chưa được phép xuất.\n" +
                              $"Phải xuất LOT {fifoRow["LOT"]} trước.";
                    return false;
                }

                consumedKeys.Add(fifoKey);
                remainingSelected -= expected;
                if (remainingSelected <= 0) break;
            }

            if (remainingSelected > 0)
            {
                message = $"Không đủ tồn kho theo FIFO cho mã hàng {_maHang}.";
                return false;
            }

            foreach (string selectedKey in selectedMap.Keys)
            {
                if (consumedKeys.Contains(selectedKey)) continue;

                DataRow next = fifoRows.FirstOrDefault(r => !consumedKeys.Contains(LotKey(r["LOT"]?.ToString())));
                string required = next == null ? "LOT FIFO kế tiếp" : next["LOT"].ToString();
                message = $"Vi phạm FIFO.\n\nMã hàng: {_maHang}\n" +
                          $"LOT {selectedKey} chưa được phép xuất.\n" +
                          $"Phải xuất LOT {required} trước.";
                return false;
            }

            return true;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            var parts = new List<string>();
            var selected = new List<Tuple<string, int>>();
            int tongChon = 0;

            foreach (DataRow row in _danhSachLot.Rows)
            {
                int slChon = row["SLCHON"] == DBNull.Value ? 0 : Convert.ToInt32(row["SLCHON"]);
                int slConLai = row["SLCONLAI"] == DBNull.Value ? 0 : Convert.ToInt32(row["SLCONLAI"]);
                string lot = row["LOT"].ToString().Trim();

                if (slChon <= 0) continue;

                if (slChon > slConLai)
                {
                    XtraMessageBox.Show($"LOT {lot}: số lượng chọn ({slChon}) vượt quá tồn kho ({slConLai})!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                selected.Add(Tuple.Create(lot, slChon));
                parts.Add($"{lot}-{slChon}");
                tongChon += slChon;
            }

            if (tongChon == 0)
            {
                XtraMessageBox.Show("Chưa chọn số lượng xuất!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (tongChon != _soLuongCan)
            {
                var rs = XtraMessageBox.Show(
                    $"Tổng số lượng chọn ({tongChon}) khác số lượng cần xuất ({_soLuongCan}).\nBạn có muốn tiếp tục?",
                    "Xác Nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (rs != DialogResult.Yes) return;
            }

            if (!ValidateFifoSelection(selected, out string fifoMessage))
            {
                XtraMessageBox.Show(fifoMessage, "Cảnh báo FIFO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LotGhep = string.Join(",", parts);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}