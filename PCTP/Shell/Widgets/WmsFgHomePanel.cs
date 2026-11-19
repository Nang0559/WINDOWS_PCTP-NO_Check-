using DevExpress.XtraEditors;
using PCTP.Common;
using PCTP.Modules.NhapKho;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Security;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Shell.Widgets
{
    /// <summary>
    /// Màn hình công việc Main_APP — KHO THÀNH PHẨM (FG), hiển thị theo quyền.
    /// Mỗi ô nghiệp vụ chỉ xuất hiện (và chỉ truy vấn số liệu) khi tài khoản có quyền VIEW tương ứng:
    ///   Xuất hàng   -> SHIPPING.DISPATCH      (hàng đã pick chờ giao - FVN_HangChoGiao, chờ giao bù)
    ///   Nhập kho    -> WAREHOUSE.RECEIPT      (phiếu chờ nhập, chờ nhập lại)
    ///   Tồn kho     -> WAREHOUSE.STOCK_VIEW   (lệch đối chiếu A0)
    ///   Công việc khác -> QUALITY.RETURN      (QC định hướng / QC cuối / rework)
    /// MainApp chỉ điều hướng; kiểm tra nghiệp vụ vẫn nằm ở module service.
    /// </summary>
    public sealed class WmsFgHomePanel : XtraUserControl
    {
        private const string LoadingText = "…";
        private const string ErrorText = "!";

        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;
        private readonly INhapKhoDashboardRepository _nhapKhoRepo;
        private readonly IHangChoGiaoRepository _hangChoGiaoRepo;
        private readonly Action _openNhapKho;
        private readonly Action _openHangChoGiao;
        private readonly Action _openTonKho;

        private readonly Label _lblHeader = new Label();
        private readonly Label _lblEmpty = new Label();
        private readonly TableLayoutPanel _grid = new TableLayoutPanel();
        private readonly List<Card> _cards = new List<Card>();
        private readonly ToolTip _tip = new ToolTip();

        private Card _cardXuat, _cardNhap, _cardTon, _cardKhac;
        private string _permissionSignature = string.Empty;
        private bool _loading;

        public WmsFgHomePanel(
            IPhieuXuLyBatThuongRepository phieuXuLyRepo,
            INhapKhoDashboardRepository nhapKhoRepo,
            IHangChoGiaoRepository hangChoGiaoRepo,
            Action openNhapKho,
            Action openHangChoGiao,
            Action openTonKho)
        {
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException("phieuXuLyRepo");
            _nhapKhoRepo = nhapKhoRepo ?? throw new ArgumentNullException("nhapKhoRepo");
            _hangChoGiaoRepo = hangChoGiaoRepo ?? throw new ArgumentNullException("hangChoGiaoRepo");
            _openNhapKho = openNhapKho ?? throw new ArgumentNullException("openNhapKho");
            _openHangChoGiao = openHangChoGiao ?? throw new ArgumentNullException("openHangChoGiao");
            _openTonKho = openTonKho ?? throw new ArgumentNullException("openTonKho");

            Dock = DockStyle.Top;
            Height = 150;
            BuildUi();
            ApplyPermissions();
        }

        // ───────────────────────────── UI ─────────────────────────────

        private sealed class Card
        {
            public Panel Root;
            public Label Title;
            public Label Count;
            public Label Line1;
            public Label Line2;
            public Action Open;
            public Color Accent;
            public bool Allowed;
        }

        private void BuildUi()
        {
            BackColor = Color.FromArgb(246, 248, 250);

            _lblHeader.Dock = DockStyle.Top;
            _lblHeader.Height = 28;
            _lblHeader.Font = new Font("Tahoma", 10F, FontStyle.Bold);
            _lblHeader.ForeColor = Color.FromArgb(40, 60, 90);
            _lblHeader.Padding = new Padding(12, 6, 0, 0);

            _lblEmpty.Dock = DockStyle.Fill;
            _lblEmpty.Font = new Font("Tahoma", 10F, FontStyle.Italic);
            _lblEmpty.ForeColor = Color.DimGray;
            _lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _lblEmpty.Text = "Tài khoản chưa được cấp quyền thao tác kho thành phẩm. Vui lòng liên hệ quản trị để được phân quyền.";
            _lblEmpty.Visible = false;

            _grid.Dock = DockStyle.Fill;
            _grid.RowCount = 1;
            _grid.Padding = new Padding(8, 0, 8, 6);

            _cardXuat = NewCard("XUẤT HÀNG", Color.FromArgb(0, 122, 204), _openHangChoGiao);
            _cardNhap = NewCard("NHẬP KHO", Color.FromArgb(46, 139, 87), _openNhapKho);
            _cardTon = NewCard("TỒN KHO", Color.FromArgb(230, 126, 34), _openTonKho);
            _cardKhac = NewCard("CÔNG VIỆC KHÁC", Color.FromArgb(142, 68, 173),
                delegate { WarehouseProcessNavigator.OpenQuanLyTienTrinhHangLoi(this); });

            Controls.Add(_grid);
            Controls.Add(_lblEmpty);
            Controls.Add(_lblHeader);
            _lblEmpty.BringToFront();
            _grid.BringToFront();
            _lblHeader.SendToBack();
        }

        private Card NewCard(string title, Color accent, Action open)
        {
            var card = new Card { Accent = accent, Open = open };

            card.Root = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Padding = new Padding(14, 8, 8, 6)
            };
            var stripe = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent };
            card.Title = new Label
            {
                Dock = DockStyle.Top, Height = 22, Text = title, ForeColor = accent,
                Font = new Font("Tahoma", 9.5F, FontStyle.Bold)
            };
            card.Count = new Label
            {
                Dock = DockStyle.Top, Height = 40, Text = LoadingText, ForeColor = Color.FromArgb(40, 40, 40),
                Font = new Font("Tahoma", 22F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft
            };
            card.Line1 = new Label
            {
                Dock = DockStyle.Top, Height = 18, ForeColor = Color.FromArgb(70, 70, 70),
                Font = new Font("Tahoma", 8.5F)
            };
            card.Line2 = new Label
            {
                Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray,
                Font = new Font("Tahoma", 8.5F)
            };

            // Thứ tự Add ngược để Dock=Top xếp đúng: Title -> Count -> Line1 -> Line2
            card.Root.Controls.Add(card.Line2);
            card.Root.Controls.Add(card.Line1);
            card.Root.Controls.Add(card.Count);
            card.Root.Controls.Add(card.Title);
            card.Root.Controls.Add(stripe);

            Color normal = card.Root.BackColor;
            Color hover = Color.FromArgb(235, 244, 252);
            foreach (Control c in new Control[] { card.Root, card.Title, card.Count, card.Line1, card.Line2, stripe })
            {
                c.Cursor = Cursors.Hand;
                c.Click += delegate { OpenCard(card); };
                c.MouseEnter += delegate { card.Root.BackColor = hover; };
                c.MouseLeave += delegate { card.Root.BackColor = normal; };
            }
            return card;
        }

        private void OpenCard(Card card)
        {
            // Kiểm tra lại quyền ngay lúc bấm (session có thể đổi sau khi dựng panel).
            if (!IsAllowed(card))
            {
                XtraMessageBox.Show(this, "Bạn không có quyền truy cập chức năng này.",
                    "PCTP - Phân quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ApplyPermissions();
                return;
            }
            card.Open();
        }

        // ───────────────────────────── Quyền ─────────────────────────────

        private static bool Has(string function)
        {
            var s = UserSession.Current;
            return s != null && s.HasPermission(function, "VIEW");
        }

        private bool IsAllowed(Card card)
        {
            if (card == _cardXuat) return Has("SHIPPING.DISPATCH");
            if (card == _cardNhap) return Has("WAREHOUSE.RECEIPT");
            if (card == _cardTon) return Has("WAREHOUSE.STOCK_VIEW");
            if (card == _cardKhac) return Has("QUALITY.RETURN");
            return false;
        }

        /// <summary>Dựng lại các ô theo quyền hiện tại; trả về true nếu bố cục thay đổi.</summary>
        public bool ApplyPermissions()
        {
            var s = UserSession.Current;
            string signature = string.Join("|", new[]
            {
                Has("SHIPPING.DISPATCH") ? "X" : "-", Has("WAREHOUSE.RECEIPT") ? "N" : "-",
                Has("WAREHOUSE.STOCK_VIEW") ? "T" : "-", Has("QUALITY.RETURN") ? "K" : "-",
                s == null ? "" : s.UserName
            });
            if (signature == _permissionSignature) return false;
            _permissionSignature = signature;

            _lblHeader.Text = "MAIN APP — KHO THÀNH PHẨM (FG)" +
                (s == null ? string.Empty : "    |    " +
                    (string.IsNullOrWhiteSpace(s.DisplayName) ? s.UserName : s.DisplayName));

            foreach (Card c in new[] { _cardXuat, _cardNhap, _cardTon, _cardKhac })
                c.Allowed = IsAllowed(c);

            _cards.Clear();
            _grid.SuspendLayout();
            _grid.Controls.Clear();
            _grid.ColumnStyles.Clear();
            foreach (Card c in new[] { _cardXuat, _cardNhap, _cardTon, _cardKhac })
                if (c.Allowed) _cards.Add(c);

            _grid.ColumnCount = Math.Max(1, _cards.Count);
            for (int i = 0; i < _cards.Count; i++)
            {
                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / _cards.Count));
                _grid.Controls.Add(_cards[i].Root, i, 0);
            }
            _grid.ResumeLayout();

            _lblEmpty.Visible = _cards.Count == 0;
            _grid.Visible = _cards.Count > 0;
            return true;
        }

        // ───────────────────────────── Số liệu ─────────────────────────────

        /// <summary>
        /// Làm mới số liệu. Hiện "…" ngay, rồi truy vấn ở vòng message kế tiếp để UI không bị đứng hình.
        /// Chỉ truy vấn những ô mà tài khoản có quyền.
        /// </summary>
        public void RefreshData()
        {
            if (IsDisposed || _loading) return;
            ApplyPermissions();
            foreach (Card c in _cards)
            {
                c.Count.Text = LoadingText;
                c.Line1.Text = string.Empty;
                c.Line2.Text = string.Empty;
            }
            _loading = true;
            if (IsHandleCreated) BeginInvoke(new Action(LoadCounts)); else LoadCounts();
        }

        private void LoadCounts()
        {
            try
            {
                if (IsDisposed || UserSession.Current == null) return;
                bool quality = Has("QUALITY.RETURN");

                if (_cardXuat.Allowed)
                    Fill(_cardXuat, delegate
                    {
                        int choGiao = _hangChoGiaoRepo.CountPending();           // FVN_HangChoGiao
                        int giaoBu = _phieuXuLyRepo.CountByStatus(QTChungStatus.ChoGiaoBu);
                        return new CardData(choGiao.ToString(), "Yêu cầu xuất / hàng chờ giao",
                            "Chờ giao bù: " + giaoBu, choGiao + giaoBu);
                    });

                if (_cardNhap.Allowed)
                    Fill(_cardNhap, delegate
                    {
                        int choNhap = _nhapKhoRepo.DemPhieuChoNhap();
                        int nhapLai = quality ? _phieuXuLyRepo.CountByStatus(QTChungStatus.DaQCXacNhanCuoi) : 0;
                        return new CardData(choNhap.ToString(), "Phiếu chờ nhập / nhập lại",
                            quality ? "Chờ nhập lại: " + nhapLai : string.Empty, choNhap + nhapLai);
                    });

                if (_cardTon.Allowed)
                    Fill(_cardTon, delegate
                    {
                        int lech = _nhapKhoRepo.DemLechDoiChieu();
                        return new CardData(lech.ToString(), "Đối chiếu và cảnh báo tồn",
                            lech > 0 ? "⚠ Lệch đối chiếu A0" : "✔ Không lệch đối chiếu A0", lech);
                    });

                if (_cardKhac.Allowed)
                    Fill(_cardKhac, delegate
                    {
                        int dinhHuong = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaTaoPhieuBatThuong);
                        int qcCuoi = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaGiaoSanXuat);
                        int rework = _phieuXuLyRepo.CountByStatus(QTChungStatus.DaDinhHuong);
                        return new CardData((dinhHuong + qcCuoi + rework).ToString(), "Các chức năng được phân quyền",
                            "QC định hướng " + dinhHuong + " · QC cuối " + qcCuoi + " · Rework " + rework,
                            dinhHuong + qcCuoi + rework);
                    });
            }
            finally
            {
                _loading = false;
            }
        }

        private sealed class CardData
        {
            public readonly string Count, Line1, Line2;
            public readonly int Pending;
            public CardData(string count, string line1, string line2, int pending)
            { Count = count; Line1 = line1; Line2 = line2; Pending = pending; }
        }

        private void Fill(Card card, Func<CardData> read)
        {
            try
            {
                CardData d = read();
                card.Count.Text = d.Count;
                card.Line1.Text = d.Line1;
                card.Line2.Text = d.Line2;
                card.Count.ForeColor = d.Pending > 0 ? Color.DarkOrange : Color.SeaGreen;
                _tip.SetToolTip(card.Count, string.Empty);
            }
            catch (Exception ex)
            {
                card.Count.Text = ErrorText;
                card.Count.ForeColor = Color.Crimson;
                card.Line1.Text = "Không tải được số liệu";
                _tip.SetToolTip(card.Count, ex.Message);
                System.Diagnostics.Debug.WriteLine("[WmsFgHomePanel] " + card.Title.Text + ": " + ex.Message);
            }
        }
    }
}
