using DevExpress.XtraBars.Navigation;
using DevExpress.XtraEditors;
using PCTP.Modules.BaoCao.UI;
using PCTP.Modules.GiaoHangKhach.SubForm;
using System;
using System.Data;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;

using PCTP.Modules.KhoCore.UI;

using PCTP.Framework.Documents;
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
        private AccordionControlElement _menuUiScreens;
        private AccordionControlElement _menuLinhKien;
        private AccordionControlElement _menuSanXuat;
        private AccordionControlElement _menuQuanTri;

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

            // Kho chỉ giữ hàng tồn vật lý: bản đồ kho, tồn thành phẩm, điều chỉnh tồn LOT.
            _menuKhoTonTP = CreateMenuItem("menuKhoTonTP", "Tồn kho thành phẩm", OpenTonKhoThanhPham);
            _menuKho.Elements.Add(_menuKhoTonTP);
            _menuKho.Elements.Add(CreateMenuItem(
                "menuDieuChinhTonKhoLot",
                "Điều chỉnh tồn kho LOT",
                OpenDieuChinhTonKhoLot));

            // ── 2b. Linh kiện PART: đi theo luồng Đặt hàng → Nhận hàng → Kho → Yêu cầu → Xuất ──
            _menuLinhKien = new AccordionControlElement(ElementStyle.Group)
            {
                Name = "menuLinhKien",
                Text = "Linh kiện PART",
                Expanded = true
            };
            // Màn hình dựng từ layout đã duyệt (Bravo style); có nút Upload Excel PO. Chuột phải để khai báo dữ liệu / layout.
            _menuLinhKien.Elements.Add(CreateMenuItem("menuPhieuDatHangLinhKienUi", "Đặt hàng linh kiện (Upload PO)", OpenPhieuDatHangLinhKienUi));
            // Khai báo SL / thùng (Quantity) và SL / palet (MaxCloseQty): phát hành PO chia tem thùng / tem palet theo quy cách này.
            _menuLinhKien.Elements.Add(CreateMenuItem("menuQuyCachDongGoi", "Quy cách đóng gói (thùng / palet)", OpenQuyCachDongGoi));
            _menuLinhKien.Elements.Add(CreateMenuItem("menuNhanLinhKien", "Kiểm kê hàng về (QR)", OpenNhanLinhKien));
            _menuLinhKien.Elements.Add(CreateMenuItem("menuKhoLinhKien", "Kho linh kiện PART (PO / QR / Tồn / Cấp WIP)", OpenKhoLinhKien));
            _menuLinhKien.Elements.Add(CreateMenuItem("menuChungTuYeuCauLinhKien", "Chứng từ yêu cầu linh kiện", OpenChungTuYeuCauLinhKien));
            _menuLinhKien.Elements.Add(CreateMenuItem("menuYeuCauXuatLinhKien", "Yêu cầu / Xuất linh kiện PART → WIP", OpenYeuCauXuatLinhKien));

            // ── 2c. Sản xuất MES: kế hoạch → điều hành → theo dõi → cấu trúc sản phẩm ──
            _menuSanXuat = new AccordionControlElement(ElementStyle.Group)
            {
                Name = "menuSanXuat",
                Text = "Sản xuất MES",
                Expanded = true
            };
            _menuSanXuat.Elements.Add(CreateMenuItem("menuUploadKeHoachSanXuat", "Upload kế hoạch sản xuất theo ca", OpenUploadKeHoachSanXuat));
            _menuSanXuat.Elements.Add(CreateMenuItem("menuKhaiBaoSite", "Khai báo Site PCTP (cho phép / tạm ngưng)", OpenKhaiBaoSite));
            _menuSanXuat.Elements.Add(CreateMenuItem("menuKhaiBaoChuyenSanXuat", "Khai báo chuyền sản xuất theo Site", OpenKhaiBaoChuyenSanXuat));
            _menuSanXuat.Elements.Add(CreateMenuItem("menuLienKetWorkCenter", "Liên kết Work Center IFS ↔ Bộ phận", OpenLienKetWorkCenter));
            _menuSanXuat.Elements.Add(CreateMenuItem("menuMaChuyen5Va3", "Mã chuyền 5 ký tự ↔ 3 ký tự (LOT)", OpenMaChuyen5Va3));
            _menuSanXuat.Elements.Add(CreateMenuItem("menuDieuHanhSanXuat", "Điều hành sản xuất MES", OpenDieuHanhSanXuat));
            // Control Tower và BOM được thêm ở AddMesBomNavigation (cần kiểm quyền riêng từng mục).

            // Hàng đã pick chờ giao (+ huỷ để hàng quay về kho khi giao lỗi / huỷ giao) thuộc luồng Giao Hàng.
            accordionControlElement22.Elements.Add(CreateMenuItem(
                "menuHangChoGiao",
                "Hàng chờ giao",
                OpenHangChoGiao));

            // ── 2d. Công cụ người dùng + Quản trị hệ thống tách riêng ─────
            _menuQuanTri = new AccordionControlElement(ElementStyle.Group)
            {
                Name = "menuQuanTri",
                Text = "Quản trị hệ thống",
                Expanded = true
            };
            _menuQuanTri.Elements.Add(CreateMenuItem(
                "menuSecurityAdmin",
                "Quản trị tài khoản & phân quyền",
                OpenSecurityAdministration));
            _menuQuanTri.Elements.Add(CreateMenuItem(
                "menuIfsMasterDataSync",
                "Đồng bộ dữ liệu IFS → FVN_PCTP",
                OpenIfsMasterDataSync));
            _menuQuanTri.Elements.Add(CreateMenuItem(
                "menuDocumentDesigner",
                "Thiết kế giao diện (PCTP UI Designer)",
                OpenDocumentDesigner));
            _menuQuanTri.Elements.Add(CreateMenuItem(
                "menuUiDataSourceDesigner",
                "Khai báo cơ sở dữ liệu UI",
                OpenUiDataSourceDesigner));
            _menuQuanTri.Elements.Add(CreateMenuItem(
                "menuUiMenuDesigner",
                "Thiết kế menu (Tùy chọn trình đơn)",
                OpenUiMenuDesigner));
            _menuQuanTri.Elements.Add(CreateMenuItem(
                "menuLabelCenter",
                "Thiết kế layout tem (tem thùng / palet / ghép LOT)",
                OpenLabelCenter));
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
            mainAccordionGroup.Elements.Add(_menuLinhKien);               // Linh kiện PART
            mainAccordionGroup.Elements.Add(_menuSanXuat);                // Sản xuất MES
            mainAccordionGroup.Elements.Add(accordionControlElement22);   // Giao Hàng
            mainAccordionGroup.Elements.Add(_menuXuLyLot);                // Tách / Ghép LOT
            mainAccordionGroup.Elements.Add(E_Trahang);                   // Xử lý hàng lỗi
            mainAccordionGroup.Elements.Add(accordionControlElement28);   // Tra Cứu & Báo Cáo
            // "Màn hình UI" (RefreshUiScreenMenu) được chèn ở đây, trước Công Cụ.
            mainAccordionGroup.Elements.Add(accordionControlElement20);   // Công Cụ
            mainAccordionGroup.Elements.Add(_menuQuanTri);                // Quản trị hệ thống
        }

        private static readonly HashSet<string> AutoHiddenGroups =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "menuLinhKien", "menuSanXuat", "menuQuanTri" };

        private static readonly Dictionary<string, string> MenuFunctionMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["menuChuyenHangNoiBo"] = "WAREHOUSE.MOVE",
                ["menuThuHoiHangNoiBo"] = "WAREHOUSE.MOVE",
                ["menuKhoTonTP"] = "WAREHOUSE.STOCK_VIEW",
                ["menuKhoLinhKien"] = "WAREHOUSE.COMPONENT_STOCK",
                ["menuNhanLinhKien"] = "WAREHOUSE.COMPONENT_RECEIPT",
                ["menuUploadKeHoachSanXuat"] = "MES.PLAN_UPLOAD",
                ["menuKhaiBaoChuyenSanXuat"] = "MES.PRODUCTION_LINE_SETUP",
                ["menuKhaiBaoSite"] = "MES.SITE_SETUP",
                ["menuLienKetWorkCenter"] = "MES.SITE_DEPARTMENT_LINE",
                ["menuDieuHanhSanXuat"] = "MES.PRODUCTION_EXECUTION",
                ["menuYeuCauXuatLinhKien"] = "MES.COMPONENT_DEMAND",
                ["menuChungTuYeuCauLinhKien"] = "MES.COMPONENT_DEMAND",
                ["menuDocumentDesigner"] = "ADMIN.DOCUMENT_DESIGNER",
                ["menuUiDataSourceDesigner"] = "ADMIN.DOCUMENT_DESIGNER",
                ["menuUiMenuDesigner"] = "ADMIN.DOCUMENT_DESIGNER",
                ["menuLabelCenter"] = "ADMIN.DOCUMENT_DESIGNER",
                ["menuPhieuDatHangLinhKienUi"] = PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoFunctionCode,
                ["menuQuyCachDongGoi"] = PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoFunctionCode,
                ["menuDieuChinhTonKhoLot"] = "WAREHOUSE.STOCK_ADJUST",
                ["menuHangChoGiao"] = "SHIPPING.DISPATCH",
                ["menuIfsMasterDataSync"] = "MASTERDATA.IFS_SYNC",
                ["menuTachLot"] = "LOT.SPLIT",
                ["menuGhepLotThuCong"] = "LOT.MERGE",
                ["menuBcTruyXuat"] = "REPORT.TRACEABILITY",
                ["menuBcTruyXuatLinhKien"] = "REPORT.COMPONENT_TRACEABILITY",
                ["menuBcLichSuKho"] = "REPORT.STOCK_HISTORY",
                ["menuBcLichSuQC"] = "REPORT.QUALITY_HISTORY",
                ["menuMesBomGraphic"] = "MES.BOM_VIEW",
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
            else if (string.Equals(name, "menuYeuCauXuatLinhKien", StringComparison.OrdinalIgnoreCase))
            {
                item.Visible = HasComponentDemandAndIssueAccess();
                item.Click += (s, e) =>
                {
                    if (!HasComponentDemandAndIssueAccess())
                    {
                        XtraMessageBox.Show(this, "Cần đồng thời quyền xem nhu cầu linh kiện và quyền xem chức năng xuất PART → WIP.",
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
            foreach (AccordionControlElement root in accordionControl.Elements)
                ApplyAuthorizationElement(root);
        }

        private void ApplyAuthorizationElement(AccordionControlElement item)
        {
            string functionCode;
            if (string.Equals(item.Name, "menuSecurityAdmin", StringComparison.OrdinalIgnoreCase))
                item.Visible = CanOpenSecurityAdministration();
            else if (string.Equals(item.Name, "menuYeuCauXuatLinhKien", StringComparison.OrdinalIgnoreCase))
                item.Visible = HasComponentDemandAndIssueAccess();
            else if (MenuFunctionMap.TryGetValue(item.Name ?? string.Empty, out functionCode))
                item.Visible = UserSession.Current != null && UserSession.Current.HasPermission(functionCode, "VIEW");

            if (item.Elements.Count > 0)
            {
                foreach (AccordionControlElement child in item.Elements)
                    ApplyAuthorizationElement(child);
                // Groups we own disappear when the user may open nothing inside them.
                if (AutoHiddenGroups.Contains(item.Name ?? string.Empty))
                    item.Visible = item.Elements.Cast<AccordionControlElement>().Any(child => child.Visible);
            }
        }

        private bool HasComponentDemandAndIssueAccess()
        {
            var session = UserSession.Current;
            return session != null &&
                session.HasPermission("MES.COMPONENT_DEMAND", "VIEW") &&
                session.HasPermission("MES.COMPONENT_ISSUE", "VIEW");
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

        private void OpenDocumentDesigner(object sender, EventArgs e)
        {
            // VIEW is checked by the menu map; EDIT / APPROVE are demanded again by DocumentDesignerService and by SQL.
            OpenFromMenu(() => FormWindowManager.ShowSingle(this,
                () => new PCTP.Framework.Documents.UI.DocumentDesignerForm(
                    PCTP.Modules.MesPlanning.Documents.MesDocumentModule.CreateDesigner(),
                    PCTP.Modules.MesPlanning.Documents.MesDocumentModule.CreateMenuDesigner())),
                "Thiết kế giao diện");
        }

        /// <summary>Trung tâm layout tem dùng chung (Report Designer + xem trước / in thử). VIEW kiểm theo menu; EDIT kiểm lại khi thiết kế / khôi phục.</summary>
        private void OpenLabelCenter(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(this, () => new PCTP.Shared.Labels.FormLabelCenter()),
                "Thiết kế layout tem");
        }

        /// <summary>
        /// Quy cách đóng gói linh kiện (SL / thùng, SL / palet, đồng bộ Bravo B20ItemQuyCach). VIEW kiểm theo menu; Site chọn theo phạm vi
        /// của tài khoản (như màn hình PO); thêm / sửa / upload / đồng bộ cần CREATE và được service + SQL kiểm lại.
        /// </summary>
        private void OpenQuyCachDongGoi(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                string site = ResolveDocumentSite(PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoFunctionCode);
                if (site == null) return; // user cancelled the site chooser
                FormWindowManager.ShowSingle(this,
                    () => new PCTP.Modules.KhoLinhKien.UI.FormComponentPackSpec(site),
                    "PackSpec:" + site);
            }, "Quy cách đóng gói");
        }

        /// <summary>Phiếu đặt hàng linh kiện vẽ từ layout đã duyệt. VIEW + SITE được kiểm lại trong UiScreenRuntime và SQL.</summary>
        private void OpenPhieuDatHangLinhKienUi(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                string site = ResolveDocumentSite(PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoFunctionCode);
                if (site == null) return; // user cancelled the site chooser
                string screen = PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoScreenCode;
                FormWindowManager.ShowSingle(this,
                    () => PCTP.Framework.UiLayout.UiLayoutModule.CreateScreenForm(screen, site,
                        ComponentPoActions()),
                    "UiScreen:" + screen);
            }, "Phiếu đặt hàng linh kiện");
        }

        /// <summary>Upload Excel PO + các nút workflow (Kiểm tra / Phát hành / Đóng PO / Hủy PO) của màn hình PO.</summary>
        private static System.Collections.Generic.IEnumerable<PCTP.Framework.UiLayout.UiScreenAction> ComponentPoActions()
        {
            var actions = new System.Collections.Generic.List<PCTP.Framework.UiLayout.UiScreenAction>
            {
                PCTP.Modules.KhoLinhKien.ComponentPoUploadAction.Create(),
                PCTP.Modules.KhoLinhKien.ComponentPoTemplateAction.Create()
            };
            actions.AddRange(PCTP.Modules.KhoLinhKien.ComponentPoWorkflowActions.CreateSteps());
            actions.AddRange(PCTP.Modules.KhoLinhKien.ComponentPoLabelActions.Create());
            actions.Add(PCTP.Modules.KhoLinhKien.ComponentPackSpecAction.Create());
            return actions;
        }

        /// <summary>Khai báo cơ sở dữ liệu (view đã duyệt, tạo màn hình). APPROVE được SQL kiểm lại ở từng thao tác.</summary>
        private void OpenUiDataSourceDesigner(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                using (var form = new PCTP.Framework.UiLayout.UiDataSourceForm(
                    PCTP.Framework.UiLayout.UiLayoutModule.CreateDesigner()))
                    form.ShowDialog(this);
                RefreshUiScreenMenu();   // a screen created in the dialog appears without restarting the app
            }, "Khai báo cơ sở dữ liệu UI");
        }

        /// <summary>
        /// Rebuilds the "Màn hình UI" group from the approved-screen catalogue (migration 057). Every item calls the same
        /// C# handler with a validated code, so a SQL row cannot supply code or a navigation target. If the catalogue
        /// cannot be read (e.g. migration 057 not applied) the standard menu is left untouched.
        /// </summary>
        private void RefreshUiScreenMenu()
        {
            try
            {
                if (UserSession.Current == null) return;
                var screens = PCTP.Framework.UiLayout.UiLayoutModule.CreateCatalog().List()
                    .Where(x => !string.Equals(x.ScreenCode,
                        PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoScreenCode, StringComparison.Ordinal))   // has its own menu item
                    .ToList();
                if (_menuUiScreens == null)
                    _menuUiScreens = new AccordionControlElement(ElementStyle.Group)
                    {
                        Name = "menuUiScreens",
                        Text = "Màn hình UI",
                        Expanded = true
                    };
                _menuUiScreens.Elements.Clear();
                foreach (var screen in screens)
                {
                    string code = screen.ScreenCode;
                    // A menu profile may already have moved this item elsewhere: do not create a second copy.
                    if (FindMenuItem(accordionControl.Elements.Cast<AccordionControlElement>(), "menuUiScreen_" + code) != null) continue;
                    _menuUiScreens.Elements.Add(CreateMenuItem("menuUiScreen_" + code, screen.Title,
                        (s, e) => OpenUiScreenByCode(code)));
                }
                bool attached = _menuUiScreens.OwnerElement == mainAccordionGroup;
                if (_menuUiScreens.Elements.Count > 0 && !attached)
                {
                    // Keep "Công Cụ" and "Quản trị hệ thống" last: re-append them after "Màn hình UI".
                    mainAccordionGroup.Elements.Remove(accordionControlElement20);
                    mainAccordionGroup.Elements.Remove(_menuQuanTri);
                    mainAccordionGroup.Elements.Add(_menuUiScreens);
                    mainAccordionGroup.Elements.Add(accordionControlElement20);
                    mainAccordionGroup.Elements.Add(_menuQuanTri);
                }
                else if (_menuUiScreens.Elements.Count == 0 && attached) mainAccordionGroup.Elements.Remove(_menuUiScreens);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("UI screen menu skipped: " + ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Generic "open UI screen by code". The code must be in the catalogue SQL lists for this user; Site scope for the
        /// screen's own function code is chosen/checked like every other screen, and SQL re-checks VIEW on load.
        /// Deliberately does not go through OpenFromMenu: its title-to-permission guess must never see a screen title.
        /// </summary>
        private void OpenUiScreenByCode(string screenCode)
        {
            try
            {
                var info = PCTP.Framework.UiLayout.UiLayoutModule.CreateCatalog().Find(screenCode);
                string site = ResolveDocumentSite(info.ViewFunctionCode);
                if (site == null) return; // user cancelled the site chooser
                // Kế hoạch / Lệnh sản xuất (migration 066) có nút riêng: upload Excel, workflow, in tem thùng / palet.
                System.Collections.Generic.IEnumerable<PCTP.Framework.UiLayout.UiScreenAction> actions =
                    PCTP.Modules.MesPlanning.ProductionScreenActions.Handles(info.ScreenCode)
                        ? PCTP.Modules.MesPlanning.ProductionScreenActions.For(info.ScreenCode)
                        : null;
                FormWindowManager.ShowSingle(this,
                    () => PCTP.Framework.UiLayout.UiLayoutModule.CreateScreenForm(info.ScreenCode, site, actions),
                    "UiScreen:" + info.ScreenCode);
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(this, "Không thể mở màn hình.\r\n\r\n" + ex.Message,
                    "Màn hình UI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
            if (title.IndexOf("Kho linh kiện", StringComparison.OrdinalIgnoreCase) >= 0) return "WAREHOUSE.COMPONENT_STOCK";
            if (title.IndexOf("Điều hành sản xuất", StringComparison.OrdinalIgnoreCase) >= 0) return "MES.PRODUCTION_EXECUTION";
            if (title.IndexOf("Upload kế hoạch", StringComparison.OrdinalIgnoreCase) >= 0) return "MES.PLAN_UPLOAD";
            if (title.IndexOf("Khai báo chuyền", StringComparison.OrdinalIgnoreCase) >= 0) return "MES.PLAN_UPLOAD";
            if (title.IndexOf("Nhận linh kiện", StringComparison.OrdinalIgnoreCase) >= 0) return "WAREHOUSE.RECEIPT";
            if (title.IndexOf("Phiếu đặt hàng", StringComparison.OrdinalIgnoreCase) >= 0) return PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoFunctionCode;
            if (title.IndexOf("Quy cách đóng gói", StringComparison.OrdinalIgnoreCase) >= 0) return PCTP.Framework.UiLayout.UiLayoutModule.ComponentPoFunctionCode;
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

        private void OpenKhoLinhKien(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(
                this,
                () => new PCTP.Modules.KhoLinhKien.UI.FormKhoLinhKien()),
                "Kho linh kiện PART");
        }

        private void OpenNhanLinhKien(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(
                this,
                () => new PCTP.Modules.KhoLinhKien.UI.FormKiemKeHangVeList()),
                "Kiểm kê hàng về");
        }

        private void OpenUploadKeHoachSanXuat(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(
                this,
                () => new PCTP.Modules.MesPlanning.UI.FormUploadKeHoachSanXuat()),
                "Upload kế hoạch sản xuất");
        }

        /// <summary>Gán IFS.FVN_ProductionLine (SiteCode + ProductionLineCode) sang mã LOT 3 ký tự / bộ phận 4 ký tự. Quyền riêng MES.PRODUCTION_LINE_SETUP (VIEW/CREATE + SITE).</summary>
        private void OpenKhaiBaoChuyenSanXuat(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                string site = ResolveDocumentSite("MES.PRODUCTION_LINE_SETUP");
                if (site == null) return; // user cancelled the site chooser
                FormWindowManager.ShowSingle(this,
                    () => new PCTP.Modules.MesPlanning.UI.FormProductionLineSetup(site),
                    "ProductionLineSetup:" + site);
            }, "Khai báo chuyền sản xuất");
        }

        /// <summary>Danh sách Site PCTP được phép (dbo.FVN_PCTPSite): cho phép / bật / tạm ngưng / đổi tên. Quyền MES.SITE_SETUP; Site chỉ cho phép khi đã đồng bộ từ IFS.</summary>
        private void OpenKhaiBaoSite(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(
                this,
                () => new PCTP.Modules.IfsSync.FormSiteSetup(),
                "SiteSetup"),
                "Khai báo Site PCTP");
        }

        /// <summary>Liên kết Work Center IFS với bộ phận PCTP của Site; SQL chặn khi lệch chuyền IFS với mapping. Quyền MES.SITE_DEPARTMENT_LINE (VIEW/EDIT + SITE).</summary>
        private void OpenLienKetWorkCenter(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                string site = ResolveDocumentSite("MES.SITE_DEPARTMENT_LINE");
                if (site == null) return; // user cancelled the site chooser
                FormWindowManager.ShowSingle(this,
                    () => new PCTP.Modules.IfsSync.FormWorkCenterDepartmentLink(site),
                    "WorkCenterLink:" + site);
            }, "Liên kết Work Center");
        }

        /// <summary>Đối chiếu và sinh mã chuyền 5 ký tự (tham chiếu) ↔ 3 ký tự (LOT). Quyền MES.SITE_DEPARTMENT_LINE (VIEW/EDIT + SITE).</summary>
        private void OpenMaChuyen5Va3(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                string site = ResolveDocumentSite("MES.SITE_DEPARTMENT_LINE");
                if (site == null) return; // user cancelled the site chooser
                FormWindowManager.ShowSingle(this,
                    () => new PCTP.Modules.IfsSync.FormLineCodeCrossRef(site),
                    "LineCodeCrossRef:" + site);
            }, "Mã chuyền 5 ký tự ↔ 3 ký tự");
        }

        private void OpenYeuCauXuatLinhKien(object sender, EventArgs e)
        {
            OpenFromMenu(() => FormWindowManager.ShowSingle(
                this,
                () => new PCTP.Modules.MesPlanning.UI.FormYeuCauVaXuatLinhKien()),
                "Yêu cầu / Xuất linh kiện");
        }

        private void OpenChungTuYeuCauLinhKien(object sender, EventArgs e)
        {
            OpenFromMenu(() =>
            {
                const string module = PCTP.Modules.MesPlanning.Documents.ComponentDemandDefinition.ModuleCode;
                // Throws with the validation reason when startup validation failed (the screen stays closed, fail-closed).
                var runtime = PCTP.Modules.MesPlanning.Documents.MesDocumentModule.GetRuntime(module);
                string site = ResolveDocumentSite("MES.COMPONENT_DEMAND");
                if (site == null) return; // user cancelled the site chooser
                FormWindowManager.ShowSingle(this, () => new PCTP.Framework.Documents.UI.DocumentListForm(
                    runtime, module, site,
                    new PCTP.Modules.MesPlanning.Documents.ComponentDemandIssueInputProvider()));
            }, "Màn hình yêu cầu linh kiện");
        }

        // SQL menu metadata can only reposition this explicit C# allow-list of existing shell items.
        // A database row cannot create a new navigation target or supply an event handler.
        private static readonly Dictionary<string, string> DocumentMenuItemMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PCTP.Modules.MesPlanning.Documents.ComponentDemandDefinition.ModuleCode] = "menuChungTuYeuCauLinhKien"
            };

        private AccordionControlElement ResolveMenuGroup(string menuGroup)
        {
            switch (menuGroup)
            {
                case "NHAPKHO": return NHAccordionControlElement;
                case "KHO": return _menuKho;
                case "GIAOHANG": return accordionControlElement22;
                case "BAOCAO": return accordionControlElement28;
                case "CONGCU": return accordionControlElement20;
                default: return null;
            }
        }

        private static AccordionControlElement FindMenuItem(IEnumerable<AccordionControlElement> elements, string name)
        {
            foreach (AccordionControlElement element in elements)
            {
                if (string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase)) return element;
                var child = FindMenuItem(element.Elements.Cast<AccordionControlElement>(), name);
                if (child != null) return child;
            }
            return null;
        }

        /// <summary>
        /// Applies presentation configuration only to known C# menu items and preserves the prior authorization result.
        /// </summary>
        private void ApplyDocumentMenuConfiguration()
        {
            var session = UserSession.Current;
            if (session == null) return;
            var entries = PCTP.Modules.MesPlanning.Documents.MesDocumentModule.ResolveShellMenu(
                functionCode => session.HasPermission(functionCode, "VIEW"));
            var touched = new Dictionary<AccordionControlElement, List<KeyValuePair<AccordionControlElement, int>>>();
            foreach (var entry in entries)
            {
                string itemName;
                if (!DocumentMenuItemMap.TryGetValue(entry.ModuleCode, out itemName)) continue;
                var item = FindMenuItem(accordionControl.Elements.Cast<AccordionControlElement>(), itemName);
                var target = ResolveMenuGroup(entry.MenuGroup);
                if (item == null || target == null) continue;

                // Never turn a hidden (unauthorized) item visible again.
                item.Visible = item.Visible && entry.IsActive;
                if (item.OwnerElement != target)
                {
                    if (item.OwnerElement != null) item.OwnerElement.Elements.Remove(item);
                    target.Elements.Add(item);
                }
                List<KeyValuePair<AccordionControlElement, int>> groupItems;
                if (!touched.TryGetValue(target, out groupItems))
                    touched[target] = groupItems = new List<KeyValuePair<AccordionControlElement, int>>();
                groupItems.Add(new KeyValuePair<AccordionControlElement, int>(item, entry.SequenceNo));
            }

            foreach (var group in touched)
                foreach (var item in group.Value.OrderBy(x => x.Value))
                {
                    group.Key.Elements.Remove(item.Key);
                    group.Key.Elements.Add(item.Key);
                }
        }

        private readonly Dictionary<string, string> _documentSiteChoice =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Site to open a document for: the only site the user may read, or the one chosen from a short list. Returns null when
        /// the user cancels the chooser. Reads site from SiteCode, from ScopeValue of SITE scopes, and expands ALL scopes
        /// to the active sites (see <see cref="DocumentSiteResolver"/>).
        /// </summary>
        private string ResolveDocumentSite(string functionCode)
        {
            var session = UserSession.Current ?? throw new UnauthorizedAccessException("Chưa đăng nhập.");
            var sites = DocumentSiteResolver.Candidates(session, functionCode, LoadActiveSites);
            if (sites.Count == 0)
                throw new UnauthorizedAccessException("Tài khoản chưa được gán Site cho chức năng " + functionCode +
                    ". Cần cấp phạm vi dữ liệu SITE (hoặc ALL) cho chức năng này ở màn hình phân quyền.");
            if (sites.Count == 1) return sites[0];

            string remembered;
            if (_documentSiteChoice.TryGetValue(functionCode, out remembered) &&
                sites.Contains(remembered, StringComparer.OrdinalIgnoreCase))
                return remembered;
            string chosen = PickSite(sites, functionCode);
            if (chosen != null) _documentSiteChoice[functionCode] = chosen;
            return chosen;
        }

        private static IEnumerable<string> LoadActiveSites()
        {
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
            var sites = new List<string>();
            using (var cn = new System.Data.SqlClient.SqlConnection(connectionString))
            using (var cmd = new System.Data.SqlClient.SqlCommand(
                "SELECT SiteCode FROM dbo.FVN_PCTPSite WHERE IsActive = 1 ORDER BY SiteCode", cn))
            {
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) sites.Add(rd.GetString(0));
            }
            return sites;
        }

        private string PickSite(IReadOnlyList<string> sites, string functionCode)
        {
            using (var dialog = new XtraForm())
            {
                dialog.Text = "Chọn Site";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ClientSize = new System.Drawing.Size(360, 130);
                var label = new LabelControl { Text = "Chọn Site cho " + functionCode + ":", Left = 14, Top = 14, AutoSizeMode = LabelAutoSizeMode.Default };
                var combo = new ComboBoxEdit { Left = 14, Top = 40, Width = 330 };
                combo.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
                foreach (var site in sites) combo.Properties.Items.Add(site);
                combo.SelectedIndex = 0;
                var ok = new SimpleButton { Text = "MỞ", Left = 154, Top = 84, Width = 90, Height = 30, DialogResult = DialogResult.OK };
                var cancel = new SimpleButton { Text = "HỦY", Left = 254, Top = 84, Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] { label, combo, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? combo.SelectedItem as string : null;
            }
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
