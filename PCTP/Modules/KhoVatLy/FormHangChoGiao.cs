using DevExpress.XtraEditors;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
{
    /// <summary>
    /// Danh sách HÀNG CHỜ GIAO (đã pick, chưa giao) + huỷ để hàng quay về đúng Slot/LOT nguồn.
    /// Dùng chung cho nút "Hàng chờ giao" của ExportFormSV và menu Kho → Hàng chờ giao (Main_APP).
    /// </summary>
    public sealed class FormHangChoGiao : XtraForm
    {
        private sealed class Dong
        {
            public int Id { get; set; }
            public string Loai { get; set; }
            public string MaHang { get; set; }
            public string Lot { get; set; }
            public string LotThung { get; set; }
            public int SoLuong { get; set; }
            public string SlotNguon { get; set; }
            public string PhieuThamChieu { get; set; }
            public DateTime NgayPick { get; set; }
            public string NguoiPick { get; set; }
        }

        private readonly IStockExportService _exportService;
        private readonly DataGridView _grid;
        private readonly TextEdit _txtLoc;
        private readonly LabelControl _lblTong;
        private List<Dong> _all = new List<Dong>();

        public FormHangChoGiao(IStockExportService exportService)
        {
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));

            Text = "Hàng chờ giao";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 560);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", HeaderText = "ID chờ giao", FillWeight = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Loai", HeaderText = "Loại", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "MaHang", HeaderText = "Mã hàng", FillWeight = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Lot", HeaderText = "LOT", FillWeight = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LotThung", HeaderText = "LOT thùng", FillWeight = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SoLuong", HeaderText = "Số lượng", FillWeight = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SlotNguon", HeaderText = "Slot nguồn", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PhieuThamChieu", HeaderText = "Phiếu tham chiếu", FillWeight = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NgayPick", HeaderText = "Ngày pick", FillWeight = 100, DefaultCellStyle = { Format = "dd/MM/yyyy HH:mm" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NguoiPick", HeaderText = "Người pick", FillWeight = 90 });

            _txtLoc = new TextEdit { Left = 8, Top = 10, Width = 320 };
            _txtLoc.Properties.NullValuePrompt = "Lọc theo mã hàng / LOT / phiếu / người pick";
            _txtLoc.EditValueChanged += (s, e) => LocLai();

            _lblTong = new LabelControl { Left = 340, Top = 14, AutoSizeMode = LabelAutoSizeMode.None, Width = 360 };

            var btnReload = new SimpleButton { Text = "Làm mới", Width = 100 };
            var btnCancelPick = new SimpleButton { Text = "Huỷ dòng đã chọn", Width = 150 };
            var btnClose = new SimpleButton { Text = "Đóng", Width = 100 };
            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            footer.Controls.Add(btnClose);
            footer.Controls.Add(btnCancelPick);
            footer.Controls.Add(btnReload);

            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            top.Controls.Add(_txtLoc);
            top.Controls.Add(_lblTong);

            Controls.Add(_grid);
            Controls.Add(footer);
            Controls.Add(top);

            btnReload.Click += (s, e) => TaiLai();
            btnClose.Click += (s, e) => Close();
            btnCancelPick.Click += (s, e) => HuyDongDaChon();

            Shown += (s, e) => TaiLai();
        }

        private static string TenLoai(HangChoGiaoLoai loai)
        {
            return loai == HangChoGiaoLoai.GiaoBuNG ? "Giao bù NG" : "Giao hàng";
        }

        private void TaiLai()
        {
            try
            {
                _all = (_exportService.GetPendingChoGiao() ?? new List<PCTP.Modules.XuLyHangLoi.Models.HangChoGiao>())
                    .Where(x => x != null)
                    .Select(x => new Dong
                    {
                        Id = x.Id,
                        Loai = TenLoai(x.LoaiYeuCauGiao),
                        MaHang = x.MaHang,
                        Lot = x.LotGoc,
                        LotThung = x.LotThung,
                        SoLuong = x.SoLuong,
                        SlotNguon = x.SlotIdNguon.HasValue ? x.SlotIdNguon.Value.ToString() : string.Empty,
                        PhieuThamChieu = StockExportReferenceFormatter.Format(x.ReferenceType, x.ReferenceId) ?? string.Empty,
                        NgayPick = x.NgayXuatKho,
                        NguoiPick = x.NguoiXuatKho
                    })
                    .OrderByDescending(x => x.NgayPick)
                    .ToList();

                LocLai();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không tải được danh sách chờ giao: " + ex.Message,
                    "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LocLai()
        {
            string k = (_txtLoc.Text ?? string.Empty).Trim();

            var rows = string.IsNullOrEmpty(k)
                ? _all
                : _all.Where(x =>
                    Chua(x.MaHang, k) || Chua(x.Lot, k) || Chua(x.LotThung, k) ||
                    Chua(x.PhieuThamChieu, k) || Chua(x.NguoiPick, k) || Chua(x.Loai, k)).ToList();

            _grid.DataSource = null;
            _grid.DataSource = rows;

            _lblTong.Text = string.Format("{0:n0} dòng chờ giao, tổng {1:n0} cái", rows.Count, rows.Sum(x => x.SoLuong));
        }

        private static bool Chua(string text, string keyword)
        {
            return text != null && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void HuyDongDaChon()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Cells["Id"].Value == null)
            {
                XtraMessageBox.Show(this, "Vui lòng chọn một dòng chờ giao.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = Convert.ToInt32(_grid.CurrentRow.Cells["Id"].Value);
            if (XtraMessageBox.Show(this,
                $"Huỷ chờ giao Id={id}? Hàng sẽ được hoàn về đúng Slot/LOT nguồn; STOCKTP không thay đổi.\r\n\r\n" +
                "Dùng khi giao lỗi hoặc huỷ giao.",
                "Xác nhận huỷ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var result = _exportService.CancelChoGiao(id, Environment.UserName);
            XtraMessageBox.Show(this, result.Message,
                result.IsOK ? "Thành công" : "Không thể huỷ",
                MessageBoxButtons.OK,
                result.IsOK ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            if (result.IsOK) TaiLai();
        }
    }
}
