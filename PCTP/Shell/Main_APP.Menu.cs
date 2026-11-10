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
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;

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

            // Danh sách hàng đã pick chờ giao (+ huỷ để hàng quay về kho khi giao lỗi / huỷ giao).
            _menuKho.Elements.Add(CreateMenuItem(
                "menuHangChoGiao",
                "Hàng chờ giao",
                OpenHangChoGiao));

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
    }

}
