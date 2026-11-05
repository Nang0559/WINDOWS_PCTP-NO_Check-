using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using PCTP.Common;
using PCTP.Modules.BaoCao.UI;
using PCTP.Modules.GiaoHangKhach.SubForm;
using System;
using System.Windows.Forms;
using System.Linq;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Shared.Common;

namespace PCTP
{
    /// <summary>
    /// Menu chuẩn của Shell (accordion bên trái).
    ///
    /// Thứ tự theo luồng nghiệp vụ kho:
    ///   Nhập Kho → Kho → Giao Hàng → Xử lý hàng lỗi → Tra Cứu &amp; Báo Cáo → Công Cụ
    ///
    /// Main_APP chỉ là Shell: mỗi mục menu chỉ mở đúng 1 màn hình có sẵn qua navigator /
    /// form đã có, không chứa nghiệp vụ.
    /// </summary>
    public partial class Main_APP
    {
        private AccordionControlElement _menuKho;
        private AccordionControlElement _menuKhoTonTP;
        private AccordionControlElement _menuBcLichSuKho;
        private AccordionControlElement _menuBcLichSuQC;
        private AccordionControlElement _menuXuLyLot;
        private AccordionControlElement _menuDieuChuyenNoiBo;

        private void BuildStandardMenus()
        {
            // ── 1. Nhập Kho ─────────────────────────────────────────────
            // QC đã chuyển sang "Xử lý hàng lỗi" (NormalizeHangLoiNavigation) nên nhóm chỉ còn nhập kho.
            NHAccordionControlElement.Text = "Nhập Kho";

            // "Nhập thành phẩm - Không QR": chưa có màn hình nhập tương ứng
            // (FormEnterItemSV / FormNhapKhoTienTrinh đều dựa trên quét QR) → ẩn, tránh mục bấm không có phản hồi.
            E_NhapTP_0QR.Visible = false;

            // ── 2. Kho (vật lý) ─────────────────────────────────────────
            _menuKho = new AccordionControlElement(ElementStyle.Group)
            {
                Name = "menuKho",
                Text = "Kho",
                Expanded = true
            };

            // "Bản đồ kho" từng nằm ở Công Cụ và Giao Hàng (trùng) → gom về nhóm Kho.
            accordionControlElement20.Elements.Remove(cmdRackControl);
            accordionControlElement22.Elements.Remove(accordionControlElement36);
            _menuDieuChuyenNoiBo = new AccordionControlElement(ElementStyle.Group)
            {
                Name = "menuDieuChuyenNoiBo",
                Text = "Điều chuyển nội bộ",
                Expanded = true
            };
            _menuDieuChuyenNoiBo.Elements.Add(CreateMenuItem("menuChuyenHangNoiBo", "Chuyển hàng sang vị trí nội bộ", OpenChuyenHangNoiBo));
            _menuDieuChuyenNoiBo.Elements.Add(CreateMenuItem("menuThuHoiHangNoiBo", "Thu hồi hàng từ vị trí nội bộ về kho", OpenThuHoiHangNoiBo));
            accordionControlElement36.Visible = false;
            _menuKho.Elements.Add(cmdRackControl);

            _menuKhoTonTP = CreateMenuItem("menuKhoTonTP", "Tồn kho thành phẩm", OpenTonKhoThanhPham);
            _menuKho.Elements.Add(_menuKhoTonTP);

            _menuKho.Elements.Add(CreateMenuItem(
                "menuDieuChinhTonKhoLot",
                "Điều chỉnh tồn kho LOT",
                OpenDieuChinhTonKhoLot));

            // ── 3. Xử lý LOT ───────────────────────────────────────────
            // Tách LOT là luồng nghiệp vụ; form tự chọn mẫu in Ghép LOT theo CustomerCode.
            _menuXuLyLot = new AccordionControlElement(ElementStyle.Group)
            {
                Name = "menuXuLyLot",
                Text = "Xử lý LOT",
                Expanded = true
            };
            _menuXuLyLot.Elements.Add(CreateMenuItem(
                "menuTachLot",
                "Tách LOT / In tem Ghép LOT",
                OpenTachLot));
            
            // ── 4. Tra Cứu & Báo Cáo ────────────────────────────────────
            // "Tra cứu LOT" / "Thống kê / báo cáo" (E_Tracuulotno / E_TKTK) nối ở handler.
            _menuBcLichSuKho = CreateMenuItem("menuBcLichSuKho", "Lịch sử kho / Tồn hiện tại", OpenBaoCaoLichSuKho);
            _menuBcLichSuQC = CreateMenuItem("menuBcLichSuQC", "Lịch sử QC / Inspection", OpenBaoCaoLichSuQC);
            accordionControlElement28.Elements.Add(_menuBcLichSuKho);
            accordionControlElement28.Elements.Add(_menuBcLichSuQC);

            // ── 4. Sắp xếp lại các nhóm theo luồng nghiệp vụ ─────────────
            mainAccordionGroup.Elements.Clear();
            mainAccordionGroup.Elements.Add(NHAccordionControlElement);   // Nhập Kho
            mainAccordionGroup.Elements.Add(_menuKho);                    // Kho
            mainAccordionGroup.Elements.Add(_menuDieuChuyenNoiBo);         // Điều chuyển nội bộ
            mainAccordionGroup.Elements.Add(accordionControlElement22);   // Giao Hàng
            mainAccordionGroup.Elements.Add(_menuXuLyLot);                // Tách / Ghép LOT
            mainAccordionGroup.Elements.Add(E_Trahang);                   // Xử lý hàng lỗi
            mainAccordionGroup.Elements.Add(accordionControlElement28);   // Tra Cứu & Báo Cáo
            mainAccordionGroup.Elements.Add(accordionControlElement20);   // Công Cụ
        }

        private static AccordionControlElement CreateMenuItem(string name, string text, EventHandler onClick)
        {
            var item = new AccordionControlElement(ElementStyle.Item)
            {
                Name = name,
                Text = text
            };

            item.Click += onClick;
            return item;
        }

        /// <summary>Mở 1 màn hình từ menu; lỗi hiển thị thông báo thay vì làm sập Shell.</summary>
        private void OpenFromMenu(Action open, string title)
        {
            try
            {
                open();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this,
                    "Không thể mở màn hình.\r\n\r\n" + ex.Message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // ── Handlers ────────────────────────────────────────────────────

        private void OpenNhapThanhPhamQr()
        {
            OpenFromMenu(OpenNhapKhoFromDashboard, "Nhập kho");
        }

        private void OpenTachLot(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var form = new UF_TACHLOT())
                    form.ShowDialog(this);
            }, "Tách LOT / In tem Ghép LOT");
        }

        private void OpenTraCuuLot()
        {
            OpenFromMenu(() => BaoCaoNavigator.OpenTraceability(this, null), "Tra cứu LOT");
        }

        private void OpenBaoCaoTongHop()
        {
            OpenFromMenu(() => BaoCaoNavigator.OpenMain(this), "Thống kê / báo cáo");
        }

        private void OpenBaoCaoLichSuKho(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var f = new FormBaoCaoStockHistory())
                    f.ShowDialog(this);
            }, "Lịch sử kho");
        }

        private void OpenBaoCaoLichSuQC(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var f = new FormBaoCaoQualityHistory())
                    f.ShowDialog(this);
            }, "Lịch sử QC");
        }

        private void OpenChuyenHangNoiBo(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var f = new PCTP.Modules.XuatKho.FormChuyenHangNoiBo())
                    f.ShowDialog(this);
            }, "Chuyển hàng sang vị trí nội bộ");
        }

        private void OpenThuHoiHangNoiBo(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var f = new PCTP.Modules.XuatKho.FormThuHoiHangNoiBo())
                    f.ShowDialog(this);
            }, "Thu hồi hàng từ vị trí nội bộ về kho");
        }

        private void OpenTonKhoThanhPham(object sender, EventArgs e)
        {
            // lotFilter rỗng → chế độ xem "Tồn kho hiện tại" (không phải điều chỉnh 1 LOT).
            OpenFromMenu(() => TONKHOTP.ShowForLot(this, string.Empty), "Tồn kho thành phẩm");
        }

        private void OpenDieuChinhTonKhoLot(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var form = new FormDieuChinhTonKhoLot())
                    form.ShowDialog(this);
            }, "Điều chỉnh tồn kho LOT");
        }
    }

    /// <summary>
    /// Màn hình điều chỉnh số lượng đích của một ItemCode + LOT.
    /// STOCKTP là số chuẩn; toàn bộ SlotLot đang hoạt động được cân đối trong một transaction.
    /// </summary>
    public sealed class FormDieuChinhTonKhoLot : XtraForm
    {
        private readonly UnitOfWork _uow;
        private readonly StockTpRepository _stockTp;
        private readonly SlotRepository _slotRepo;
        private readonly BulkStockSlotRepository _adjustmentRepo;
        private readonly TextEdit _item = new TextEdit();
        private readonly TextEdit _lot = new TextEdit();
        private readonly TextEdit _destinationSlot = new TextEdit();
        private readonly SpinEdit _target = new SpinEdit();
        private readonly MemoEdit _reason = new MemoEdit();
        private readonly LabelControl _summary = new LabelControl();
        private readonly SimpleButton _preview = new SimpleButton();
        private readonly SimpleButton _apply = new SimpleButton();
        private Guid _requestId = Guid.NewGuid();
        private int _stockBefore;
        private int _slotBefore;
        private bool _hasPreview;

        public FormDieuChinhTonKhoLot()
        {
            Text = "Điều chỉnh tồn kho LOT";
            Width = 920;
            Height = 510;
            MinimumSize = new System.Drawing.Size(780, 430);
            StartPosition = FormStartPosition.CenterParent;

            var provider = new SQLPROVIDER();
            // LOT adjustment is a write operation: fail closed unless the configured
            // application connection is explicitly DB57. Never permit DB14 writes.
            var connection = new System.Data.SqlClient.SqlConnectionStringBuilder(provider.B7R2_FCCdb);
            if (!string.Equals(connection.InitialCatalog, "B7R2_FCC", StringComparison.OrdinalIgnoreCase) ||
                connection.DataSource.IndexOf("192.168.200.57", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Điều chỉnh tồn kho LOT chỉ được phép chạy trên DB57 (192.168.200.57 / B7R2_FCC). Cấu hình hiện tại không phải DB57; thao tác bị chặn.");
            var db = new PhieuSqlExecutor(provider);
            _uow = new UnitOfWork(provider);
            _stockTp = new StockTpRepository(db, _uow);
            _slotRepo = new SlotRepository(db, _uow);
            _adjustmentRepo = new BulkStockSlotRepository(db, _uow);

            BuildUi();
            FormClosed += (s, e) => _uow.Dispose();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(root);

            var group = new GroupControl { Text = "Thông tin LOT và số lượng đích", Dock = DockStyle.Fill };
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 4, RowCount = 4 };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 4; i++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 3 ? 54 : 36));
            AddField(fields, 0, 0, "Mã hàng", _item);
            AddField(fields, 2, 0, "LOT", _lot);
            AddField(fields, 0, 1, "Số lượng đích", _target);
            AddField(fields, 2, 1, "Slot nhận thêm", _destinationSlot);
            AddField(fields, 0, 2, "Lý do", _reason, 3);
            _item.Properties.NullValuePrompt = "ItemCode trong STOCKTP";
            _lot.Properties.NullValuePrompt = "Nhập chính xác LOT";
            _destinationSlot.Properties.NullValuePrompt = "Để trống = A0 mặc định";
            _target.Properties.MinValue = 0;
            _target.Properties.MaxValue = int.MaxValue;
            _target.Properties.IsFloatValue = false;
            _reason.Properties.NullValuePrompt = "Bắt buộc nhập lý do điều chỉnh";
            _reason.Properties.NullValuePromptShowForEmptyValue = true;
            group.Controls.Add(fields);
            root.Controls.Add(group, 0, 0);

            _summary.Dock = DockStyle.Fill;
            _summary.AutoSizeMode = LabelAutoSizeMode.None;
            _summary.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _summary.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            root.Controls.Add(_summary, 0, 1);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(2) };
            _preview.Text = "Đối soát LOT";
            _preview.Width = 140;
            _preview.Click += (s, e) => PreviewLot();
            _apply.Text = "Xác nhận điều chỉnh";
            _apply.Width = 170;
            _apply.Enabled = false;
            _apply.Click += (s, e) => ApplyAdjustment();
            footer.Controls.Add(_preview);
            footer.Controls.Add(_apply);
            root.Controls.Add(footer, 0, 2);
        }

        private static void AddField(TableLayoutPanel layout, int col, int row, string caption, Control editor, int span = 1)
        {
            var label = new LabelControl { Text = caption, Dock = DockStyle.Fill, AutoSizeMode = LabelAutoSizeMode.None };
            label.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            editor.Dock = DockStyle.Fill;
            layout.Controls.Add(label, col, row);
            layout.Controls.Add(editor, col + 1, row);
            if (span > 1) layout.SetColumnSpan(editor, span);
        }

        private void PreviewLot()
        {
            string item = (_item.Text ?? string.Empty).Trim();
            string lot = (_lot.Text ?? string.Empty).Trim();
            _hasPreview = false;
            _apply.Enabled = false;
            if (item.Length == 0 || lot.Length == 0)
            {
                XtraMessageBox.Show(this, "Phải nhập cả ItemCode và LOT.", Text);
                return;
            }

            try
            {
                var stock = _stockTp.GetByLot(lot);
                if (stock == null)
                    throw new InvalidOperationException("Không tìm thấy LOT trong STOCKTP.");
                if (!string.Equals((stock.Part ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("ItemCode không khớp PART trong STOCKTP.");

                var rows = _slotRepo.GetAllActiveSlotLots()
                    .Where(x => string.Equals((x.LotNo ?? string.Empty).Trim(), lot, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var otherItem = rows.FirstOrDefault(x => !string.Equals((x.ItemCode ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase));
                if (otherItem != null)
                    throw new InvalidOperationException("LOT đang có SlotLot thuộc ItemCode khác tại Slot " + otherItem.SlotId + ".");

                _stockBefore = stock.SlConLai ?? 0;
                _slotBefore = rows.Where(x => string.Equals((x.ItemCode ?? string.Empty).Trim(), item, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity);
                _target.Value = _stockBefore;
                _summary.Text = string.Format(
                    "STOCKTP hiện tại: {0:n0}\r\nTổng SlotLot khả dụng: {1:n0}\r\nChênh lệch hiện tại (STOCKTP - SlotLot): {2:+#;-#;0}\r\n\r\nKhi xác nhận: hệ thống sẽ đưa cả hai về số lượng đích; nếu cần giảm SlotLot sẽ trừ FIFO qua các Slot. Nếu cần tăng SlotLot, mặc định thêm vào A0 khi không chọn Slot nhận.",
                    _stockBefore, _slotBefore, _stockBefore - _slotBefore);
                _hasPreview = true;
                _apply.Enabled = true;
            }
            catch (Exception ex)
            {
                _summary.Text = "Không thể đối soát LOT: " + ex.Message;
                XtraMessageBox.Show(this, _summary.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyAdjustment()
        {
            if (!_hasPreview)
            {
                XtraMessageBox.Show(this, "Hãy đối soát LOT trước khi điều chỉnh.", Text);
                return;
            }
            string reason = (_reason.Text ?? string.Empty).Trim();
            if (reason.Length == 0)
            {
                XtraMessageBox.Show(this, "Phải nhập lý do điều chỉnh.", Text);
                _reason.Focus();
                return;
            }
            int? destination = null;
            string destinationText = (_destinationSlot.Text ?? string.Empty).Trim();
            if (destinationText.Length > 0)
            {
                int parsed;
                if (!int.TryParse(destinationText, out parsed) || parsed <= 0)
                {
                    XtraMessageBox.Show(this, "Slot nhận phải là SlotId nguyên dương; để trống nếu dùng A0.", Text);
                    return;
                }
                destination = parsed;
            }

            int target = Convert.ToInt32(_target.Value);
            string prompt = string.Format(
                "Xác nhận đưa LOT [{0}] / Item [{1}] về số lượng đích {2:n0}?\r\nSTOCKTP: {3:n0} → {2:n0}\r\nSlotLot: {4:n0} → {2:n0}\r\n\r\nThao tác sẽ ghi lịch sử và rollback toàn bộ nếu invariant không đạt.",
                _lot.Text.Trim(), _item.Text.Trim(), target, _stockBefore, _slotBefore);
            if (XtraMessageBox.Show(this, prompt, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _apply.Enabled = false;
            try
            {
                _uow.Begin();
                _adjustmentRepo.AdjustLotToTarget(
                    _item.Text.Trim(), _lot.Text.Trim(), target, destination,
                    reason, Environment.UserName, _requestId);
                _uow.Commit();
                XtraMessageBox.Show(this, "Đã điều chỉnh LOT và xác minh STOCKTP = tổng SlotLot = " + target.ToString("n0") + ".", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                _requestId = Guid.NewGuid();
                _reason.Text = string.Empty;
                PreviewLot();
            }
            catch (Exception ex)
            {
                try { _uow.Rollback(); } catch { }
                XtraMessageBox.Show(this,
                    "Điều chỉnh thất bại; transaction đã được rollback.\r\n\r\n" + ex.Message +
                    "\r\n\r\nNếu lỗi thiếu bảng PCTPStockLotAdjustmentHistory, hãy chạy SQL/PCTP_StockLotAdjustmentHistory.sql trên DB57 trước khi sử dụng.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                _apply.Enabled = _hasPreview;
            }
        }
    }
}
