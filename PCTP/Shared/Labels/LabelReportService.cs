using System;
using System.Data;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.UserDesigner;
using PCTP.Security;

namespace PCTP.Shared.Labels
{
    /// <summary>
    /// Cửa duy nhất để dựng / xem trước / in / thiết kế mọi loại tem. Màn hình nghiệp vụ chỉ gọi lớp này
    /// (không tự new XtraReport) nên layout chỉnh trong Report Designer có hiệu lực ở mọi nơi in cùng loại tem.
    /// </summary>
    public static class LabelReportService
    {
        public const string DesignerFunction = "ADMIN.DOCUMENT_DESIGNER";

        public static XtraReport CreateReport(string code, DataTable rawData)
        {
            return CreateReport(LabelCatalog.Get(code), rawData);
        }

        /// <summary>Dựng báo cáo: layout tùy chỉnh (.repx) nếu có, không thì bố cục gốc; rồi gắn dữ liệu. Layout tùy chỉnh hỏng thì báo lỗi, không in lẫn mẫu khác.</summary>
        public static XtraReport CreateReport(LabelDefinition definition, DataTable rawData)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (rawData == null) throw new ArgumentNullException(nameof(rawData));
            XtraReport report = definition.CreateReport();
            string custom = LabelLayoutStore.FindCustom(definition.Code);
            if (custom != null)
            {
                try { report.LoadLayoutFromXml(custom); }
                catch (Exception ex)
                {
                    report.Dispose();
                    throw new InvalidOperationException("Layout tem '" + definition.Title + "' đã chỉnh bị lỗi (" + custom + "): " + ex.Message +
                        "\r\nVào Thiết kế layout tem → Khôi phục layout gốc, hoặc sửa file.", ex);
                }
            }
            report.DataSource = definition.PrepareData != null ? definition.PrepareData(rawData) : rawData;
            report.DisplayName = definition.Title;
            return report;
        }

        /// <summary>
        /// Hiện bản xem trước (như Ghép LOT) để người dùng bấm In. Trả true nếu đã in (sự kiện EndPrint của PrintingSystem),
        /// false nếu chỉ xem rồi đóng — luồng gọi chỉ ghi lịch sử in khi true.
        /// </summary>
        public static bool PreviewAndPrint(IWin32Window owner, LabelDefinition definition, DataTable rawData, string title)
        {
            using (XtraReport report = CreateReport(definition, rawData))
            {
                if (!string.IsNullOrWhiteSpace(title)) report.DisplayName = title;
                bool printed = false;
                var tool = new ReportPrintTool(report);
                tool.PrintingSystem.EndPrint += (s, e) => printed = true;
                tool.ShowPreviewDialog();
                return printed;
            }
        }

        /// <summary>Xem trước / in thử bằng dữ liệu mẫu của loại tem.</summary>
        public static void PreviewSample(IWin32Window owner, LabelDefinition definition)
        {
            using (XtraReport report = CreateReport(definition, definition.CreateSampleData()))
                new ReportPrintTool(report).ShowPreviewDialog();
        }

        /// <summary>Mở Report Designer với dữ liệu mẫu. Nút Save của Designer ghi layout vào <see cref="LabelLayoutStore"/> (cần EDIT trên ADMIN.DOCUMENT_DESIGNER).</summary>
        public static void Design(IWin32Window owner, LabelDefinition definition)
        {
            DemandEdit();
            XtraReport report = CreateReport(definition, definition.CreateSampleData());
            var tool = new ReportDesignTool(report);
            tool.DesignRibbonForm.DesignMdiController.DesignPanelLoaded += (s, e) =>
            {
                var panel = s as XRDesignPanel;
                if (panel != null) panel.AddCommandHandler(new SaveLayoutHandler(panel, definition));
            };
            tool.ShowRibbonDesignerDialog();
        }

        public static void ResetLayout(LabelDefinition definition)
        {
            DemandEdit();
            LabelLayoutStore.Reset(definition.Code);
        }

        public static bool CanEdit()
        {
            var session = UserSession.Current;
            return session != null && session.HasPermission(DesignerFunction, "EDIT");
        }

        private static void DemandEdit()
        {
            (UserSession.Current ?? throw new UnauthorizedAccessException("Chưa đăng nhập.")).DemandPermission(DesignerFunction, "EDIT");
        }

        /// <summary>Chặn Save / Save As của Designer: ghi vào kho layout chung thay vì hỏi đường dẫn file.</summary>
        private sealed class SaveLayoutHandler : ICommandHandler
        {
            private readonly XRDesignPanel _panel;
            private readonly LabelDefinition _definition;

            public SaveLayoutHandler(XRDesignPanel panel, LabelDefinition definition)
            {
                _panel = panel;
                _definition = definition;
            }

            public bool CanHandleCommand(ReportCommand command, ref bool useNextHandler)
            {
                bool mine = command == ReportCommand.SaveFile || command == ReportCommand.SaveFileAs;
                useNextHandler = !mine;
                return mine;
            }

            public void HandleCommand(ReportCommand command, object[] args)
            {
                if (command != ReportCommand.SaveFile && command != ReportCommand.SaveFileAs) return;
                try
                {
                    DemandEdit();
                    XtraReport report = _panel.Report;
                    object data = report.DataSource;
                    report.DataSource = null;   // không nhúng dữ liệu mẫu vào file layout
                    try { LabelLayoutStore.Save(_definition.Code, report); }
                    finally { report.DataSource = data; }
                    _panel.ReportState = ReportState.Saved;
                    XtraMessageBox.Show("Đã lưu layout '" + _definition.Title + "'. Mọi máy in tem này sẽ dùng layout mới từ lần in sau.",
                        "Layout tem", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show("Không lưu được layout: " + ex.Message, "Layout tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
