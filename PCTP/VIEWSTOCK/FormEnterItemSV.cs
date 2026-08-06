using DevExpress.CodeParser;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using DevExpress.XtraSplashScreen;
using PCTP.ClassSQL;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.ViewForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace PCTP.VIEWSTOCK
{
    
        /// <summary>
        /// Form nhập kho hàng hóa: quét QR tem tổng -> đối chiếu vNhapTP ->
        /// (tuỳ ItemCode) kiểm tra qua FormInspection -> chọn Slot đích (hoặc nhập
        /// hàng loạt vào Slot ảo) -> lưu (STOCKTP + SlotLot + Slot, transactional).
        ///
        /// GHI CHÚ TÍCH HỢP:
        ///   - Toàn bộ ghi DB đi qua NhapTpReceivingService.NhapTpVaoSlot — nơi DUY NHẤT
        ///     cập nhật STOCKTP + SlotLot + Slot trong 1 transaction. KHÔNG dùng
        ///     StockService.ImportLotToSlot nữa vì method đó không đụng STOCKTP.
        ///   - Trước khi cho nhập, LOT quét được PHẢI khớp với 1 dòng trong vNhapTP
        ///     (qua StockTpRepository.TimPhieuTheoLotQR) — tái dùng thuật toán
        ///     LotNoHelper.BuildFindList (logic cũ từ NHAP_TP.cs).
        ///   - Chế độ "Nhập hàng loạt": bỏ qua bước chọn Slot, luôn nhập vào 1
        ///     Warehouse/Rack/Slot ẢO cố định (StockService.GetOrCreateBulkImportSlotText),
        ///     dùng để gom hàng tạm — cần có quy trình dồn hàng ra Slot thật sau đó.
        /// </summary>
        public partial class FormEnterItemSV : DevExpress.XtraEditors.XtraForm
        {
            // ==== Controls (giữ theo đúng những gì bạn đã có; phần còn lại do Designer sinh) ====

            private Panel panelSlotList;
            private ListBoxControl listBoxSlots;
            private SimpleButton btnOK;
            private SimpleButton btnCancel;
            private LabelControl lblTemCode, lblItemCode, lblLotNo, lblQty, lblrackName, lblSlotNumber, lblwhName;
            private GroupControl groupInfo, groupSlotList;
            private TableLayoutPanel contentPanel, mainLayout;

            // ==== THÊM: toggle nhập hàng loạt ====
            private CheckEdit chkNhapHangLoat;

            // ==== State ====
            private QRCodeInfo codeInfo;

            // ==== THÊM: phiếu sản xuất (vNhapTP) đã đối chiếu khớp với LOT quét được ====
            private PhieuNhapInfo _matchedPhieu;

            // ==== Dependencies ====
            private readonly StockService _stockService = new StockService();
            private readonly MainStockSV _mainStockForm;

            // ==== THÊM: dependencies cho luồng nhập STOCKTP (giống NHAP_TP.cs cũ) ====
            private readonly SQLPROVIDER _sql = new SQLPROVIDER();
            private readonly IStockTpRepository _stockTpRepo;
            private readonly NhapTpReceivingService _nhapTpService;

            public FormEnterItemSV(MainStockSV mainStockForm)
            {
                InitializeComponent();
                InitializeForm();
                _mainStockForm = mainStockForm;

                // ── Khởi tạo repository/service cho luồng nhập STOCKTP ──────────────
                _stockTpRepo = new StockTpRepository(_sql);
                _nhapTpService = new NhapTpReceivingService(
                    _sql,
                    _stockTpRepo,
                    new PhieuTrackingRepository(_sql));
            }

            private void InitializeForm()
            {
                this.Text = "NHẬP KHO HÀNG HÓA - CANVAS UI";
                this.Size = new Size(600, 480);
                this.StartPosition = FormStartPosition.CenterParent;

                // ── THÊM: checkbox chọn chế độ nhập hàng loạt ────────────────────────
                chkNhapHangLoat = new CheckEdit
                {
                    Text = "Nhập hàng loạt (không cần chọn Slot — vào kho tạm)",
                    Dock = DockStyle.Top,
                    Height = 30,
                    Font = new Font("Tahoma", 10, FontStyle.Bold)
                };
                chkNhapHangLoat.Properties.Appearance.ForeColor = Color.DarkOrange;
                chkNhapHangLoat.Properties.Appearance.Options.UseForeColor = true;
                this.Controls.Add(chkNhapHangLoat);

                // Textbox đọc QRCode
                txtQRCode = new TextBox();
                txtQRCode.Dock = DockStyle.Top;
                txtQRCode.Font = new Font("Tahoma", 12);
                txtQRCode.KeyDown += TxtQRCode_KeyDown;
                this.Controls.Add(txtQRCode);

                // Đảm bảo thứ tự Dock đúng: checkbox nằm trên textbox
                chkNhapHangLoat.BringToFront();
            }

            private void TxtQRCode_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;

                string qrCode = txtQRCode.Text.Trim();
                txtQRCode.Clear();
                if (string.IsNullOrEmpty(qrCode)) return;

                QRCodeInfo parsed;
                try
                {
                    parsed = _stockService.ParseQr(qrCode.ToUpper());
                    parsed.RawQr = qrCode.ToUpper();
                }
                catch (FormatException fex)
                {
                    XtraMessageBox.Show($"QR Code không hợp lệ!\n{fex.Message}",
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Cảnh báo nếu bắn nhầm tem thùng
                if (!parsed.IsTongPhieu)
                {
                    XtraMessageBox.Show("Vui lòng bắn tem TỔNG để nhập kho!",
                        "Sai loại tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // ── BƯỚC 1: Chặn sớm nếu tem này đã nhập kho / dữ liệu QR không hợp lệ ──
                var precheck = _nhapTpService.KiemTraTruocKhiNhap(parsed);
                if (!precheck.IsOK)
                {
                    XtraMessageBox.Show(precheck.Message, "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // ── BƯỚC 2: Đối chiếu LOT quét được với vNhapTP (bắt buộc) ───────────
                var phieu = _stockTpRepo.TimPhieuTheoLotQR(parsed.RawLotNo, parsed.ItemCode);
                if (phieu == null)
                {
                    XtraMessageBox.Show(
                        "Không tìm thấy phiếu sản xuất khớp với LOT này trong vNhapTP!\n" +
                        "Kiểm tra lại tem hoặc liên hệ QLSX.",
                        "Không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (phieu.KetThucLot)
                {
                    XtraMessageBox.Show(
                        "LOT này đã được đánh dấu KẾT THÚC — không thể nhập thêm!",
                        "Từ chối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!string.Equals(phieu.MaSP, parsed.ItemCode, StringComparison.OrdinalIgnoreCase))
                {
                    XtraMessageBox.Show(
                        $"Mã hàng không khớp!\nTem quét: {parsed.ItemCode}\nPhiếu SX: {phieu.MaSP}",
                        "Sai mã hàng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (phieu.SlDaNhap + parsed.Quantity > phieu.SlSanXuat)
                {
                    var confirmVuot = XtraMessageBox.Show(
                        $"Tổng SL nhập ({phieu.SlDaNhap + parsed.Quantity}) sẽ vượt quá " +
                        $"SL sản xuất ({phieu.SlSanXuat}) của LOT [{phieu.LotNo}].\n" +
                        "Bạn có chắc chắn muốn tiếp tục?",
                        "Cảnh báo vượt sản lượng",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (confirmVuot != DialogResult.Yes) return;
                }

                // Lưu lại phiếu đã match — dùng để build STOCKTP đúng (tên SP, SLSX thật)
                // và để hiển thị cho user xác nhận trước khi lưu
                _matchedPhieu = phieu;

                // ── BƯỚC 3: Kiểm tra có phải mã hàng cần kiểm tra không ──────────────
                var config = _stockService.GetInspectionConfig(parsed.ItemCode);

                if (config != null)
                {
                    // Mở form kiểm tra TRƯỚC khi cho chọn slot
                    using (var formInspect = new FormInspection(parsed, config))
                    {
                        var result = formInspect.ShowDialog(this);
                        if (result != DialogResult.OK)
                        {
                            XtraMessageBox.Show(
                                "Kiểm tra không đạt — Hàng không được nhập kho.",
                                "Từ chối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            _matchedPhieu = null;
                            return; // dừng — không cho nhập
                        }
                        // PASS -> tiếp tục bình thường
                    }
                }

                // Nhập kho bình thường
                ShowQRCodeInfo(parsed);
            }

            private bool ValidateQRCode(string qrCode)
            {
                return !string.IsNullOrEmpty(qrCode);
            }

            private LabelControl CreateLabel()
            {
                return new LabelControl
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Tahoma", 10),
                    Padding = new Padding(3),
                    AutoSizeMode = LabelAutoSizeMode.Vertical, // BẮT BUỘC để hiển thị nhiều dòng
                    Appearance =
                {
                    TextOptions = { WordWrap = DevExpress.Utils.WordWrap.Wrap }
                }
                };
            }

            private void ShowQRCodeInfo(QRCodeInfo qrInfo)
            {
                // Xóa layout cũ nếu có
                if (mainLayout != null)
                {
                    Controls.Remove(mainLayout);
                    mainLayout.Dispose();
                    mainLayout = null;
                }

                if (qrInfo == null)
                {
                    XtraMessageBox.Show("Không có dữ liệu QR.", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Lưu lại để dùng khi Import
                codeInfo = qrInfo;

                int top = 10;

                // =========================
                // Layout chính
                // =========================
                contentPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1
                };

                contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
                contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

                // =========================
                // Group thông tin
                // =========================
                groupInfo = new GroupControl
                {
                    Text = "Thông tin vị trí nhập",
                    Dock = DockStyle.Fill,
                    Padding = new Padding(10)
                };

                var layout = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(5)
                };

                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

                lblwhName = CreateLabel();
                lblrackName = CreateLabel();
                lblSlotNumber = CreateLabel();
                lblTemCode = CreateLabel();
                lblItemCode = CreateLabel();
                lblLotNo = CreateLabel();
                lblQty = CreateLabel();

                layout.Controls.Add(new LabelControl { Text = "WHName:" }, 0, 0);
                layout.Controls.Add(lblwhName, 1, 0);

                layout.Controls.Add(new LabelControl { Text = "Rack:" }, 0, 1);
                layout.Controls.Add(lblrackName, 1, 1);

                layout.Controls.Add(new LabelControl { Text = "Slot Number:" }, 0, 2);
                layout.Controls.Add(lblSlotNumber, 1, 2);

                layout.Controls.Add(new LabelControl { Text = "TemCode:" }, 0, 3);
                layout.Controls.Add(lblTemCode, 1, 3);

                layout.Controls.Add(new LabelControl { Text = "ItemCode:" }, 0, 4);
                layout.Controls.Add(lblItemCode, 1, 4);

                layout.Controls.Add(new LabelControl { Text = "LotNo:" }, 0, 5);
                layout.Controls.Add(lblLotNo, 1, 5);

                layout.Controls.Add(new LabelControl { Text = "Tồn kho:" }, 0, 6);
                layout.Controls.Add(lblQty, 1, 6);

                // ── THÊM: hiển thị thông tin phiếu SX đã đối chiếu (nếu có) ──────────
                if (_matchedPhieu != null)
                {
                    var lblPhieu = CreateLabel();
                    lblPhieu.Text =
                        $"LOT: {_matchedPhieu.LotNo}  |  SP: {_matchedPhieu.TenSP}  |  " +
                        $"Đã nhập: {_matchedPhieu.SlDaNhap}/{_matchedPhieu.SlSanXuat}";
                    lblPhieu.Appearance.Font = new Font("Tahoma", 9, FontStyle.Italic);
                    lblPhieu.Appearance.ForeColor = Color.DarkSlateGray;

                    layout.Controls.Add(new LabelControl { Text = "Phiếu SX:" }, 0, 7);
                    layout.Controls.Add(lblPhieu, 1, 7);
                }

                LoadSlotData(codeInfo);

                var scrollPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };

                scrollPanel.Controls.Add(layout);
                groupInfo.Controls.Add(scrollPanel);

                // =========================
                // Danh sách Slot
                // =========================
                groupSlotList = new GroupControl
                {
                    Text = "Danh sách vị trí trống phù hợp",
                    Dock = DockStyle.Fill,
                    Padding = new Padding(5)
                };

                listBoxSlots = new ListBoxControl
                {
                    Dock = DockStyle.Fill,
                    HorizontalScrollbar = true,
                    SelectionMode = SelectionMode.One
                };

                listBoxSlots.SelectedIndexChanged += (s, e) =>
                {
                    if (listBoxSlots.SelectedItem == null)
                        return;

                    try
                    {
                        StockService.ParseSlotString(
                            listBoxSlots.SelectedItem.ToString(),
                            out string targetWh,
                            out string targetRack,
                            out int targetSlot,
                            out int capacity);

                        lblwhName.Text = targetWh;
                        lblrackName.Text = targetRack;
                        lblSlotNumber.Text = targetSlot.ToString();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                };

                // ── THAY ĐỔI: nếu đang ở chế độ "Nhập hàng loạt" thì bỏ qua hoàn toàn
                // bước build danh sách Slot trống — luôn nhập vào Slot ảo cố định ────
                if (chkNhapHangLoat.Checked)
                {
                    groupSlotList.Visible = false;

                    lblwhName.Text = BulkImportConfig.WarehouseName;
                    lblrackName.Text = BulkImportConfig.RackName;
                    lblSlotNumber.Text = "(Slot ảo — gom hàng tạm)";
                }
                else
                {
                    // Danh sách slot trống/phù hợp cho ItemCode + số lượng cần nhập
                    var emptySlots = _stockService.GetAvailableSlotsForImport(
                        codeInfo.ItemCode,
                        codeInfo.Quantity);

                    if (emptySlots.Count == 0)
                    {
                        XtraMessageBox.Show(
                            "Không còn Slot trống nào phù hợp trong kho này!",
                            "Thông báo",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }

                    foreach (var slot in emptySlots)
                    {
                        listBoxSlots.Items.Add(slot);
                    }

                    groupSlotList.Controls.Add(listBoxSlots);
                }

                // =========================
                // Button
                // =========================
                btnOK = new SimpleButton
                {
                    Text = "OK",
                    Size = new Size(80, 30),
                    Top = top,
                    Left = 50
                };
                btnOK.Click += BtnOK_Click;

                btnCancel = new SimpleButton
                {
                    Text = "Cancel",
                    Size = new Size(80, 30),
                    Top = top,
                    Left = 150
                };
                btnCancel.Click += BtnCancel_Click;

                var bottomPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Height = 60,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(10)
                };

                bottomPanel.Controls.Add(btnCancel);
                bottomPanel.Controls.Add(btnOK);

                // =========================
                // Main Layout
                // =========================
                mainLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1
                };

                mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

                // ── THAY ĐỔI: nếu đang bulk mode, groupSlotList ẩn -> nới rộng groupInfo ─
                if (chkNhapHangLoat.Checked)
                {
                    contentPanel.RowStyles[0] = new RowStyle(SizeType.Percent, 100);
                    contentPanel.RowStyles[1] = new RowStyle(SizeType.Percent, 0);
                }

                contentPanel.Controls.Add(groupInfo, 0, 0);
                contentPanel.Controls.Add(groupSlotList, 0, 1);

                mainLayout.Controls.Add(contentPanel, 0, 0);
                mainLayout.Controls.Add(bottomPanel, 0, 1);

                Controls.Add(mainLayout);
            }

            private void LoadSlotData(QRCodeInfo qrCodeInfo)
            {
                if (qrCodeInfo != null)
                {
                    lblTemCode.Text = qrCodeInfo.WarehouseCode + qrCodeInfo.Unit;
                    lblItemCode.Text = qrCodeInfo.ItemCode;
                    lblLotNo.Text = qrCodeInfo.LotNo;
                    lblQty.Text = qrCodeInfo.Quantity.ToString();
                }
            }

            private void BtnOK_Click(object sender, EventArgs e)
            {
                string targetSlotText;

                // ── THAY ĐỔI: chọn Slot đích tuỳ theo chế độ ─────────────────────────
                if (chkNhapHangLoat.Checked)
                {
                    // Nhập hàng loạt -> luôn dùng Slot ảo cố định, tự tạo nếu chưa có
                    targetSlotText = _stockService.GetOrCreateBulkImportSlotText();
                }
                else
                {
                    if (listBoxSlots.SelectedItem == null)
                    {
                        XtraMessageBox.Show("Vui lòng chọn một Slot!", "Chú ý",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    targetSlotText = listBoxSlots.SelectedItem.ToString();
                }

                // ── THAY ĐỔI: dùng NhapTpReceivingService thay vì StockService.ImportLotToSlot
                // -> ghi đồng thời STOCKTP + SlotLot + Slot trong 1 transaction, có check
                // trùng case (NHAP_TP_HIS), check sức chứa Slot, và dùng đúng LOT/tên SP/
                // SLSX từ phiếu vNhapTP đã đối chiếu (_matchedPhieu) nếu có. ─────────────
                var result = _nhapTpService.NhapTpVaoSlot(codeInfo, targetSlotText, _matchedPhieu);

                if (!result.IsOK)
                {
                    XtraMessageBox.Show(result.Message, "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                XtraMessageBox.Show(result.Message, "Thành công",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Báo cho form cha vẽ lại Canvas trung tâm từ CSDL mới cập nhật
                _mainStockForm?.OnSlotUpdated();
                this.Close();
            }

            private void BtnCancel_Click(object sender, EventArgs e)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }
    
}