using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests
{
    /// <summary>Guard bố cục 4 màn hình MES theo thiết kế: tiêu đề runtime, Active Context, thanh trạng thái, bộ lọc.</summary>
    public sealed class MesFourScreensLayoutContractTests
    {
        private static string Source(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, name);
            Assert.True(File.Exists(path), "Missing UI source fixture: " + path);
            return File.ReadAllText(path);
        }

        [Fact]
        public void ProductionTowerHasFilterChipStatusBarFooterTotalsAndSingleMainAction()
        {
            string form = Source("FormMesProductionControlTower.source.cs");
            Assert.Contains("FvnConsoleStatusBarControl", form);
            Assert.Contains("_filterChip", form);
            Assert.Contains("ShowFooter = true", form);
            Assert.Contains("SummaryItemType.Sum", form);
            Assert.Contains("BuildNavigationMenu", form);
            Assert.Contains("CHỌN DÒNG FIRM", form);
        }

        [Fact]
        public void ExecutionConsoleHasRuntimeHeaderActiveContextAndRecordOutput()
        {
            string form = Source("FormDieuHanhSanXuat.source.cs");
            Assert.Contains("FvnConsoleHeaderControl", form);
            Assert.Contains("FvnConsoleStatusBarControl", form);
            Assert.Contains("THÔNG TIN ĐIỀU HÀNH & GHI NHẬN SẢN LƯỢNG (ACTIVE CONTEXT)", form);
            Assert.Contains("GHI SẢN LƯỢNG", form);
            Assert.Contains("RUNTIME ACTIVE", form);
            // Ghi sản lượng đi qua đúng lệnh TẠO LOT SẢN XUẤT, không có đường ghi riêng.
            Assert.Contains("ClickCmd(_cmdCreateLot)", form);
        }

        [Fact]
        public void ComponentConsoleHasRuntimeHeaderActiveContextAndStatusBar()
        {
            string form = Source("FormKhoLinhKien.source.cs");
            Assert.Contains("FvnConsoleHeaderControl", form);
            Assert.Contains("FvnConsoleStatusBarControl", form);
            // "&&" = ký tự & hiển thị thật (một "&" đơn bị hiểu là phím tắt và biến mất khỏi tiêu đề).
        Assert.Contains("THÔNG TIN LINH KIỆN && THAO TÁC NHANH (ACTIVE CONTEXT)", form);
            Assert.Contains("XUẤT LINH KIỆN", form);
            Assert.Contains("IN TEM LOT", form);
        }

        [Fact]
        public void ComponentFlowMapKeepsSixLaneTitlesAndStockControlPill()
        {
            string map = Source("MesComponentFlowMapControl.source.cs");
            Assert.Contains("UPLOAD PO LINH KIỆN", map);
            Assert.Contains("PHÁT HÀNH TEM THÙNG / TEM PALLET", map);
            Assert.Contains("NHẬP KHO LINH KIỆN", map);
            Assert.Contains("TIẾP NHẬN YÊU CẦU LINH KIỆN", map);
            Assert.Contains("XUẤT LINH KIỆN", map);
            Assert.Contains("QUẢN LÝ TỒN THEO LOTNO & TỒN TOÀN NHÀ MÁY", map);
            Assert.Contains("KIỂM SOÁT TỒN KHO THEO LOT", map);
        }
    }
}
