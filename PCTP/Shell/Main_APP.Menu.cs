using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Common;
using PCTP.Modules.BaoCao.UI;
using PCTP.Modules.GiaoHangKhach.SubForm;
using System;
using System.Data;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoCore.UI;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.UI;
using PCTP.Modules.MesPlanning.UI;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.Security;

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
            _menuKho.Elements.Add(CreateMenuItem("menuNhanLinhKien", "Nhận linh kiện về kho (QR)", OpenNhanLinhKien));
            _menuKho.Elements.Add(CreateMenuItem("menuUploadKeHoachSanXuat", "Upload kế hoạch sản xuất theo ca", OpenUploadKeHoachSanXuat));
            _menuKho.Elements.Add(CreateMenuItem("menuDieuHanhSanXuat", "Điều hành sản xuất MES", OpenDieuHanhSanXuat));
            _menuKho.Elements.Add(CreateMenuItem("menuYeuCauXuatLinhKien", "Yêu cầu / Xuất linh kiện PART → WIP", OpenYeuCauXuatLinhKien));
            _menuKho.Elements.Add(_menuKhoTonTP);

            _menuKho.Elements.Add(CreateMenuItem(
                "menuDieuChinhTonKhoLot",
                "Điều chỉnh tồn kho LOT",
                OpenDieuChinhTonKhoLot));

            // Danh sách hàng đã pick chờ giao (+ huỷ để hàng quay về kho khi giao lỗi / huỷ giao).
            _menuKho.Elements.Add(CreateMenuItem(
                "menuHangChoGiao",
                "Hàng chờ giao",
                OpenHangChoGiao));

            // Đồng bộ dữ liệu IFS là công cụ quản trị, đặt trong nhóm Công Cụ.
            accordionControlElement20.Elements.Add(CreateMenuItem(
                "menuIfsMasterDataSync",
                "Đồng bộ dữ liệu IFS → FVN_PCTP",
                OpenIfsMasterDataSync));
            accordionControlElement20.Elements.Add(CreateMenuItem(
                "menuSecurityAdmin",
                "Quản trị tài khoản & phân quyền",
                OpenSecurityAdministration));
            accordionControlElement20.Elements.Add(CreateMenuItem(
                "menuChangePassword",
                "Đổi mật khẩu đăng nhập",
                OpenChangePassword));
            accordionControlElement20.Elements.Add(CreateMenuItem(
                "menuSignOut",
                "Đăng xuất",
                OpenSignOut));

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
            _menuXuLyLot.Elements.Add(CreateMenuItem(
                "menuGhepLotThuCong",
                "Ghép LOT thủ công",
                OpenGhepLotThuCong));
            
            // ── 4. Tra Cứu & Báo Cáo ────────────────────────────────────
            // Designer still contains the legacy E_Tracuulotno / E_TKTK entries.
            // Remove them before adding the current screens, otherwise both navigation
            // generations appear together and open different report entry points.
            accordionControlElement28.Elements.Clear();
            accordionControlElement28.Elements.Add(CreateMenuItem(
                "menuBcTruyXuat",
                "Truy xuất giao hàng (QR / LOT)",
                (s, e) => OpenFromMenu(() => BaoCaoNavigator.OpenTraceability(this, null), "Truy xuất giao hàng")));
            accordionControlElement28.Elements.Add(CreateMenuItem(
                "menuBcTruyXuatLinhKien",
                "Truy xuất linh kiện / LOT sản xuất",
                OpenBaoCaoTruyXuatLinhKien));
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

        private static readonly Dictionary<string, string> MenuFunctionMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["menuChuyenHangNoiBo"] = "WAREHOUSE.MOVE",
                ["menuThuHoiHangNoiBo"] = "WAREHOUSE.MOVE",
                ["menuKhoTonTP"] = "WAREHOUSE.STOCK_VIEW",
                ["menuNhanLinhKien"] = "WAREHOUSE.RECEIPT",
                ["menuUploadKeHoachSanXuat"] = "MES.PLAN_UPLOAD",
                ["menuDieuHanhSanXuat"] = "MES.PRODUCTION_EXECUTION",
                ["menuYeuCauXuatLinhKien"] = "MES.COMPONENT_ISSUE",
                ["menuDieuChinhTonKhoLot"] = "WAREHOUSE.STOCK_ADJUST",
                ["menuHangChoGiao"] = "SHIPPING.DISPATCH",
                ["menuIfsMasterDataSync"] = "MASTERDATA.IFS_SYNC",
                ["menuTachLot"] = "LOT.SPLIT",
                ["menuGhepLotThuCong"] = "LOT.MERGE",
                ["menuBcTruyXuat"] = "REPORT.TRACEABILITY",
                ["menuBcTruyXuatLinhKien"] = "REPORT.TRACEABILITY",
                ["menuBcLichSuKho"] = "REPORT.STOCK_HISTORY",
                ["menuBcLichSuQC"] = "REPORT.QUALITY_HISTORY",
                ["E_NhapTP"] = "WAREHOUSE.RECEIPT",
                ["accordionControlElement_QCDinhHuong"] = "QUALITY.RETURN",
                ["accordionControlElement_QCXacNhanCuoi"] = "QUALITY.RETURN",
                ["trahangngPD"] = "QUALITY.RETURN",
                ["trahangndHVN"] = "QUALITY.RETURN",
                ["accordionControlElement21"] = "SYSTEM.TIME_CONFIG",
                ["accordionControlElement19"] = "LOT.MERGE",
                ["accordionControlElement24"] = "SYSTEM.QR_READER_ADMIN",
                ["accordionControlElement29"] = "SYSTEM.COMPARE_PART",
                ["acrImageControl"] = "SYSTEM.IMAGE_CONTROL",
                ["cmdRackControl"] = "WAREHOUSE.STOCK_VIEW",
                ["accordionControlElement38"] = "SYSTEM.FIFO_CONFIG",
                ["E_GHHVN_MP"] = "SHIPPING.DISPATCH",
                ["E_GHHVN_SP"] = "SHIPPING.DISPATCH",
                ["HTDelever"] = "SHIPPING.DISPATCH",
                ["accordionControlElement36"] = "WAREHOUSE.STOCK_VIEW",
                ["accordionControlElement37"] = "QUALITY.RETURN",
                ["E_Tracuulotno"] = "REPORT.TRACEABILITY",
                ["E_TKTK"] = "REPORT.STOCK_HISTORY"
            };

        private AccordionControlElement CreateMenuItem(string name, string text, EventHandler onClick)
        {
            var item = new AccordionControlElement(ElementStyle.Item) { Name = name, Text = text };
            string functionCode;
            if (string.Equals(name, "menuSecurityAdmin", StringComparison.OrdinalIgnoreCase))
            {
                item.Visible = CanOpenSecurityAdministration();
                item.Click += (s, e) =>
                {
                    if (!CanOpenSecurityAdministration())
                    {
                        XtraMessageBox.Show(this, "Bạn không có quyền quản trị tài khoản, vai trò hoặc nhật ký.",
                            "PCTP - Phân quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    onClick?.Invoke(s, e);
                };
            }
            else if (MenuFunctionMap.TryGetValue(name, out functionCode))
            {
                item.Visible = UserSession.Current != null && UserSession.Current.HasPermission(functionCode, "VIEW");
                item.Click += (s, e) =>
                {
                    var session = UserSession.Current;
                    if (session == null || !session.HasPermission(functionCode, "VIEW"))
                    {
                        XtraMessageBox.Show(this, "Bạn không có quyền truy cập chức năng này.",
                            "PCTP - Phân quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    onClick?.Invoke(s, e);
                };
            }
            else item.Click += onClick;
            return item;
        }

        private void ApplyAuthorizationToNavigation()
        {
            ApplyAuthorizationRecursive(mainAccordionGroup);
        }

        private void ApplyAuthorizationRecursive(AccordionControlElement parent)
        {
            foreach (AccordionControlElement item in parent.Elements)
            {
                string functionCode;
                if (string.Equals(item.Name, "menuSecurityAdmin", StringComparison.OrdinalIgnoreCase))
                    item.Visible = CanOpenSecurityAdministration();
                else if (MenuFunctionMap.TryGetValue(item.Name ?? string.Empty, out functionCode))
                    item.Visible = UserSession.Current != null && UserSession.Current.HasPermission(functionCode, "VIEW");
                if (item.Elements.Count > 0) ApplyAuthorizationRecursive(item);
            }
        }

        private bool CanOpenSecurityAdministration()
        {
            var session = UserSession.Current;
            return session != null &&
                (session.HasPermission("ADMIN.USER_MANAGEMENT", "VIEW") ||
                 session.HasPermission("ADMIN.ROLE_MANAGEMENT", "VIEW") ||
                 session.HasPermission("ADMIN.AUDIT", "VIEW"));
        }

        private void OpenSecurityAdministration(object sender, EventArgs e)
        {
            if (!CanOpenSecurityAdministration())
            {
                XtraMessageBox.Show(this, "Bạn không có quyền mở màn hình quản trị.",
                    "PCTP - Phân quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            OpenFromMenu(() => FormWindowManager.ShowSingle(this,
                () => new FormSecurityAdministration()), "Quản trị tài khoản & phân quyền");
        }

        private void OpenChangePassword(object sender, EventArgs e)
        {
            using (var dialog = new ChangePasswordDialog(false))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var auth = new AuthenticationService(new AuthenticationRepository());
                    var current = UserSession.Current;
                    auth.ChangePassword(current, dialog.CurrentPassword, dialog.NewPassword);
                    UserSession.SignIn(auth.Authenticate(current.UserName, dialog.NewPassword));
                    XtraMessageBox.Show(this, "Đã cập nhật mật khẩu.", "PCTP",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show(this, ex.Message, "Đổi mật khẩu",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenSignOut(object sender, EventArgs e)
        {
            if (XtraMessageBox.Show(this, "Bạn muốn đăng xuất khỏi PCTP?", "Đăng xuất",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            UserSession.SignOut();
            Application.Restart();
            Close();
        }

        /// <summary>Mở 1 màn hình từ menu; lỗi hiển thị thông báo thay vì làm sập Shell.</summary>
        private void OpenFromMenu(Action open, string title)
        {
            try
            {
                string functionCode = ResolveFunctionCodeFromTitle(title);
                if (!string.IsNullOrWhiteSpace(functionCode))
                    (UserSession.Current ?? throw new UnauthorizedAccessException("Chưa đăng nhập."))
                        .DemandPermission(functionCode, "VIEW");
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

        private static string ResolveFunctionCodeFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            if (title.IndexOf("Điều hành sản xuất", StringComparison.OrdinalIgnoreCase) >= 0) return "MES.PRODUCTION_EXECUTION";
            if (title.IndexOf("Upload kế hoạch", StringComparison.OrdinalIgnoreCase) >= 0) return "MES.PLAN_UPLOAD";
            if (title.IndexOf("Nhận linh kiện", StringComparison.OrdinalIgnoreCase) >= 0) return "WAREHOUSE.RECEIPT";
            if (title.IndexOf("linh kiện", StringComparison.OrdinalIgnoreCase) >= 0 && title.IndexOf("Truy xuất", StringComparison.OrdinalIgnoreCase) < 0) return "MES.COMPONENT_ISSUE";
            if (title.IndexOf("Tách LOT", StringComparison.OrdinalIgnoreCase) >= 0) return "LOT.SPLIT";
            if (title.IndexOf("Ghép LOT", StringComparison.OrdinalIgnoreCase) >= 0) return "LOT.MERGE";
            if (title.IndexOf("Truy xuất", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("Tra cứu LOT", StringComparison.OrdinalIgnoreCase) >= 0) return "REPORT.TRACEABILITY";
            if (title.IndexOf("Lịch sử kho", StringComparison.OrdinalIgnoreCase) >= 0) return "REPORT.STOCK_HISTORY";
            if (title.IndexOf("Lịch sử QC", StringComparison.OrdinalIgnoreCase) >= 0) return "REPORT.QUALITY_HISTORY";
            if (title.IndexOf("điều chuyển", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("thu hồi hàng", StringComparison.OrdinalIgnoreCase) >= 0) return "WAREHOUSE.MOVE";
            if (title.IndexOf("Tồn kho", StringComparison.OrdinalIgnoreCase) >= 0) return "WAREHOUSE.STOCK_VIEW";
            if (title.IndexOf("Điều chỉnh tồn", StringComparison.OrdinalIgnoreCase) >= 0) return "WAREHOUSE.STOCK_ADJUST";
            if (title.IndexOf("IFS", StringComparison.OrdinalIgnoreCase) >= 0) return "MASTERDATA.IFS_SYNC";
            if (title.IndexOf("Hàng chờ giao", StringComparison.OrdinalIgnoreCase) >= 0) return "SHIPPING.DISPATCH";
            if (title.IndexOf("Nhập kho", StringComparison.OrdinalIgnoreCase) >= 0) return "WAREHOUSE.RECEIPT";
            return null;
        }

        // ── Handlers ────────────────────────────────────────────────────

        private void OpenNhapThanhPhamQr()
        {
            OpenFromMenu(OpenNhapKhoFromDashboard, "Nhập kho");
        }

        private void OpenTachLot(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new UF_TACHLOT()), "Tách LOT / In tem Ghép LOT");
        }

        private void OpenGhepLotThuCong(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new FormGhepLotThuCong()), "Ghép LOT thủ công");
        }

        private void OpenTraCuuLot()
        {
            OpenFromMenu(() => BaoCaoNavigator.OpenTraceability(this, null), "Tra cứu LOT");
        }

        private void OpenBaoCaoTongHop()
        {
            OpenFromMenu(() => BaoCaoNavigator.OpenMain(this), "Thống kê / báo cáo");
        }

        private void OpenDieuHanhSanXuat(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this,
                () => new FormDieuHanhSanXuat()), "Điều hành sản xuất MES");
        }

        private void OpenBaoCaoTruyXuatLinhKien(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this,
                () => new FormTruyXuatLinhKien()), "Truy xuất linh kiện / LOT sản xuất");
        }

        private void OpenBaoCaoLichSuKho(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new FormBaoCaoStockHistory()), "Lịch sử kho");
        }

        private void OpenBaoCaoLichSuQC(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new FormBaoCaoQualityHistory()), "Lịch sử QC");
        }

        private void OpenChuyenHangNoiBo(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new PCTP.Modules.XuatKho.FormChuyenHangNoiBo()), "Chuyển hàng sang vị trí nội bộ");
        }

        private void OpenThuHoiHangNoiBo(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new PCTP.Modules.XuatKho.FormThuHoiHangNoiBo()), "Thu hồi hàng từ vị trí nội bộ về kho");
        }

        private void OpenTonKhoThanhPham(object sender, EventArgs e)
        {
            // lotFilter rỗng → chế độ xem "Tồn kho hiện tại" (không phải điều chỉnh 1 LOT).
            OpenFromMenu(() => TONKHOTP.ShowForLot(this, string.Empty), "Tồn kho thành phẩm");
        }

        private void OpenHangChoGiao(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(
                this,
                () => new PCTP.Modules.KhoVatLy.FormHangChoGiao(
                    PCTP.Modules.KhoVatLy.MainStockModuleFactory.Build().ExportService)),
                "Hàng chờ giao");
        }

        private void OpenDieuChinhTonKhoLot(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new FormDieuChinhTonKhoLot()), "Điều chỉnh tồn kho LOT");
        }

        private void OpenIfsMasterDataSync(object sender, EventArgs e)
        {
            OpenFromMenu(
                () => FormWindowManager.ShowSingle(
                    this,
                    () => new PCTP.Modules.IfsSync.FormIfsSync()),
                "Đồng bộ dữ liệu IFS");
        }
    }

}
