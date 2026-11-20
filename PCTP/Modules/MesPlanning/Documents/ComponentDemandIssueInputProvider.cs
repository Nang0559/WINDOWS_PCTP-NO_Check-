using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using PCTP.Framework.Documents;
using PCTP.Framework.Documents.UI;

namespace PCTP.Modules.MesPlanning.Documents
{
    /// <summary>
    /// Module-supplied special editor for ISSUE_COMPONENT: picks the source LOT/slot, the WIP slot and the quantity
    /// for the focused demand line. It only returns the declared input parameters; the dispatcher still applies
    /// RBAC/Site scope, rejects undeclared keys, and MES.usp_FVN_ComponentIssue_Post stays the enforcement point.
    /// </summary>
    public sealed class ComponentDemandIssueInputProvider : IDocumentCommandInputProvider
    {
        private readonly IComponentDemandService _service;

        public ComponentDemandIssueInputProvider()
            : this(new ComponentDemandService(new ComponentDemandRepository()))
        {
        }

        public ComponentDemandIssueInputProvider(IComponentDemandService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
        }

        public bool TryGetInput(ResolvedDocument document, ResolvedCommand command, DataRow headerRow,
            DataRow selectedDetailRow, IWin32Window owner, out IDictionary<string, object> input)
        {
            input = null;
            if (!string.Equals(command.CommandCode, ComponentDemandDefinition.IssueCommand, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Không có màn hình nhập liệu cho thao tác " + command.CommandCode + ".");
            if (selectedDetailRow == null)
                throw new InvalidOperationException("Chọn một dòng linh kiện cần xuất.");

            string site = Text(selectedDetailRow, "SiteCode");
            string part = Text(selectedDetailRow, "ComponentPartNo");
            long lineId = Convert.ToInt64(selectedDetailRow["ComponentDemandLineId"]);
            decimal outstanding = Math.Max(0M,
                Number(selectedDetailRow, "NetRequiredQty") - Number(selectedDetailRow, "IssuedQty"));
            if (outstanding <= 0M)
                throw new InvalidOperationException("Dòng này đã xuất đủ số lượng yêu cầu.");

            // The detail view does not carry SupplyMode/SourceAreaCode: source from PART. A MAKE component is rejected by the
            // procedure's own checks, which stay authoritative.
            DataTable lots = _service.GetPartLots(site, part, "PART");
            DataTable slots = _service.GetWipSlots(site);
            if (lots == null || lots.Rows.Count == 0)
                throw new InvalidOperationException("Không có LOT khả dụng cho linh kiện " + part + " tại khu vực PART.");
            if (slots == null || slots.Rows.Count == 0)
                throw new InvalidOperationException("Không có Slot WIP khả dụng tại Site " + site + ".");

            using (var dialog = new IssueDialog(part, lots, slots, outstanding))
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
                input = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "IssueNo", dialog.IssueNo },
                    { "ComponentDemandLineId", lineId },
                    { "MaterialLotId", dialog.MaterialLotId },
                    { "FromSlotId", dialog.FromSlotId },
                    { "ToSlotId", dialog.ToSlotId },
                    { "Quantity", dialog.Quantity }
                };
                return true;
            }
        }

        private static string Text(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToString(row[column]).Trim() : string.Empty;
        }

        private static decimal Number(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToDecimal(row[column]) : 0M;
        }

        private sealed class IssueDialog : XtraForm
        {
            private readonly TextEdit _issueNo = new TextEdit();
            private readonly LookUpEdit _lots = new LookUpEdit();
            private readonly LookUpEdit _slots = new LookUpEdit();
            private readonly SpinEdit _qty = new SpinEdit();
            private readonly LabelControl _hint = new LabelControl();
            private readonly decimal _outstanding;

            public string IssueNo { get; private set; }
            public long MaterialLotId { get; private set; }
            public int FromSlotId { get; private set; }
            public int ToSlotId { get; private set; }
            public decimal Quantity { get; private set; }

            public IssueDialog(string part, DataTable lots, DataTable slots, decimal outstanding)
            {
                _outstanding = outstanding;
                Text = "Xuất linh kiện " + part + " PART → WIP";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(560, 250);

                _issueNo.Text = "WIP-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                Configure(_lots, lots, "LotSlotDisplay", "MaterialLotId");
                Configure(_slots, slots, "SlotDisplay", "SlotId");
                _lots.EditValueChanged += (s, e) => SuggestQuantity();
                _qty.Properties.IsFloatValue = true;
                _qty.Properties.Mask.EditMask = "n3";
                _qty.Properties.Mask.UseMaskAsDisplayFormat = true;
                _qty.Properties.MinValue = Math.Min(0.001M, outstanding);
                _qty.Properties.MaxValue = outstanding;
                _qty.Properties.Increment = 1M;
                _hint.Text = "Còn cần xuất: " + outstanding.ToString("N3");

                var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                AddRow(grid, 0, "Số phiếu xuất", _issueNo);
                AddRow(grid, 1, "LOT / Slot nguồn", _lots);
                AddRow(grid, 2, "Slot đích WIP", _slots);
                AddRow(grid, 3, "Số lượng", _qty);
                AddRow(grid, 4, string.Empty, _hint);

                var ok = new SimpleButton { Text = "XÁC NHẬN", Width = 120, Height = 30, Location = new Point(300, 205) };
                var cancel = new SimpleButton { Text = "HỦY", Width = 110, Height = 30, Location = new Point(430, 205), DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => Accept();
                Controls.Add(grid);
                Controls.Add(ok);
                Controls.Add(cancel);
                CancelButton = cancel;
                SuggestQuantity();
            }

            private static void Configure(LookUpEdit editor, DataTable source, string display, string value)
            {
                editor.Properties.DataSource = source;
                editor.Properties.DisplayMember = display;
                editor.Properties.ValueMember = value;
                editor.Properties.PopulateColumns();
                if (editor.Properties.Columns[value] != null) editor.Properties.Columns[value].Visible = false;
                editor.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
                editor.Properties.NullText = string.Empty;
                if (source.Rows.Count > 0) editor.EditValue = source.Rows[0][value];
            }

            private static void AddRow(TableLayoutPanel grid, int row, string caption, Control editor)
            {
                grid.Controls.Add(new LabelControl { Text = caption, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
                editor.Dock = DockStyle.Fill;
                grid.Controls.Add(editor, 1, row);
            }

            private DataRow SelectedLot()
            {
                var table = _lots.Properties.DataSource as DataTable;
                if (table == null || _lots.EditValue == null || _lots.EditValue == DBNull.Value) return null;
                var rows = table.Select("MaterialLotId = " + Convert.ToInt64(_lots.EditValue));
                return rows.Length == 0 ? null : rows[0];
            }

            private void SuggestQuantity()
            {
                var lot = SelectedLot();
                decimal available = lot == null || !lot.Table.Columns.Contains("AvailableQty") || lot["AvailableQty"] == DBNull.Value
                    ? 0M : Convert.ToDecimal(lot["AvailableQty"]);
                decimal suggested = Math.Min(_outstanding, available);
                if (suggested >= _qty.Properties.MinValue) _qty.Value = suggested;
            }

            private void Accept()
            {
                try
                {
                    var lot = SelectedLot();
                    if (lot == null) throw new InvalidOperationException("Chọn LOT / Slot nguồn.");
                    if (_slots.EditValue == null || _slots.EditValue == DBNull.Value) throw new InvalidOperationException("Chọn Slot đích WIP.");
                    string issueNo = (_issueNo.Text ?? string.Empty).Trim();
                    if (issueNo.Length == 0) throw new InvalidOperationException("Nhập số phiếu xuất.");
                    if (_qty.Value <= 0M || _qty.Value > _outstanding)
                        throw new InvalidOperationException("Số lượng phải lớn hơn 0 và không vượt quá " + _outstanding.ToString("N3") + ".");

                    IssueNo = issueNo;
                    MaterialLotId = Convert.ToInt64(lot["MaterialLotId"]);
                    FromSlotId = Convert.ToInt32(lot["SlotId"]);
                    ToSlotId = Convert.ToInt32(_slots.EditValue);
                    Quantity = _qty.Value;
                    DialogResult = DialogResult.OK;
                }
                catch (InvalidOperationException ex)
                {
                    XtraMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
