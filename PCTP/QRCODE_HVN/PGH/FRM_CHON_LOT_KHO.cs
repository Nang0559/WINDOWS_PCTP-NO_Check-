using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN.PGH
{
    public partial class FRM_CHON_LOT_KHO : XtraForm
    {
        public string LotGhep { get; private set; }

        private readonly string _maHang;
        private readonly int _soLuongCan;
        private DataTable _danhSachLot;

        public FRM_CHON_LOT_KHO(string maHang, int soLuong,
            DataTable danhSachLot)
        {
            InitializeComponent();
            _maHang = maHang;
            _soLuongCan = soLuong;
            _danhSachLot = danhSachLot;
        }

        private void FRM_CHON_LOT_KHO_Load(object sender, EventArgs e)
        {
            lblMaHang.Text = $"Mã hàng: {_maHang}";
            lblCanXuat.Text = $"Cần xuất: {_soLuongCan}";

            // Thêm cột SlChon để user nhập
            if (!_danhSachLot.Columns.Contains("SLCHON"))
                _danhSachLot.Columns.Add("SLCHON", typeof(int));

            // Mặc định SlChon = 0
            foreach (DataRow row in _danhSachLot.Rows)
                row["SLCHON"] = 0;

            gridControl1.DataSource = _danhSachLot;

            // Cho phép edit cột SLCHON
            var view = gridControl1.MainView as GridView;
            if (view != null)
            {
                foreach (GridColumn col in view.Columns)
                    col.OptionsColumn.AllowEdit = col.FieldName == "SLCHON";

                // Highlight cột SLCHON
                view.Columns["SLCHON"].AppearanceCell.BackColor
                    = Color.LightYellow;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Validate tổng SlChon không vượt quá SlConLai
            var parts = new List<string>();
            int tongChon = 0;

            foreach (DataRow row in _danhSachLot.Rows)
            {
                int slChon = row["SLCHON"] == DBNull.Value
                    ? 0 : Convert.ToInt32(row["SLCHON"]);
                int slConLai = row["SLCONLAI"] == DBNull.Value
                    ? 0 : Convert.ToInt32(row["SLCONLAI"]);
                string lot = row["LOT"].ToString().Trim();

                if (slChon <= 0) continue;

                if (slChon > slConLai)
                {
                    XtraMessageBox.Show(
                        $"LOT {lot}: số lượng chọn ({slChon}) " +
                        $"vượt quá tồn kho ({slConLai})!",
                        "Lỗi", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                parts.Add($"{lot}-{slChon}");
                tongChon += slChon;
            }

            if (tongChon == 0)
            {
                XtraMessageBox.Show("Chưa chọn số lượng xuất!",
                    "Thông Báo", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (tongChon != _soLuongCan)
            {
                var rs = XtraMessageBox.Show(
                    $"Tổng số lượng chọn ({tongChon}) " +
                    $"khác số lượng cần xuất ({_soLuongCan}).\n" +
                    "Bạn có muốn tiếp tục?",
                    "Xác Nhận", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (rs != DialogResult.Yes) return;
            }

            // Ghép LOT: "LOT1-100,LOT2-50"
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