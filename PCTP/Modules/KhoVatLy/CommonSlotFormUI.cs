using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.FunctionForm
{
    public class CommonSlotFormUI
    {
        public TableLayoutPanel ContentPanel { get; private set; }
        public GroupControl GroupInfo { get; private set; }
        public GroupControl GroupSlotList { get; private set; }
        public ListBoxControl ListBoxSlots { get; private set; }
        public SimpleButton BtnAction1 { get; private set; }
        public SimpleButton BtnAction2 { get; private set; }
        public SimpleButton BtnCancel { get; private set; }
        public SpinEdit SpinExportQty { get; private set; } // optional

        public LabelControl LblWhName, LblRackName, LblSlotNumber, LblTemCode, LblItemCode, LblLotNo, LblQty;

        public Control BuildLayout(bool includeExportQty, EventHandler btn1Handler, EventHandler btn2Handler, EventHandler cancelHandler)
        {
            ContentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            ContentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            ContentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            // ==== groupInfo ====
            GroupInfo = new GroupControl
            {
                Text = "Thông tin Slot",
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

            LblWhName = CreateLabel(); LblRackName = CreateLabel(); LblSlotNumber = CreateLabel();
            LblTemCode = CreateLabel(); LblItemCode = CreateLabel(); LblLotNo = CreateLabel(); LblQty = CreateLabel();

            layout.Controls.Add(new LabelControl { Text = "WHName:" }, 0, 0); layout.Controls.Add(LblWhName, 1, 0);
            layout.Controls.Add(new LabelControl { Text = "Rack:" }, 0, 1); layout.Controls.Add(LblRackName, 1, 1);
            layout.Controls.Add(new LabelControl { Text = "Slot Number:" }, 0, 2); layout.Controls.Add(LblSlotNumber, 1, 2);
            layout.Controls.Add(new LabelControl { Text = "TemCode:" }, 0, 3); layout.Controls.Add(LblTemCode, 1, 3);
            layout.Controls.Add(new LabelControl { Text = "ItemCode:" }, 0, 4); layout.Controls.Add(LblItemCode, 1, 4);
            layout.Controls.Add(new LabelControl { Text = "LotNo:" }, 0, 5); layout.Controls.Add(LblLotNo, 1, 5);
            layout.Controls.Add(new LabelControl { Text = "Tồn kho:" }, 0, 6); layout.Controls.Add(LblQty, 1, 6);

            if (includeExportQty)
            {
                SpinExportQty = new SpinEdit
                {
                    Dock = DockStyle.Fill,
                    Properties = { MinValue = 1, MaxValue = 99999, IsFloatValue = false },
                    Value = 1
                };
                layout.Controls.Add(new LabelControl { Text = "SL xuất:" }, 0, 7); layout.Controls.Add(SpinExportQty, 1, 7);
            }

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            scrollPanel.Controls.Add(layout);
            GroupInfo.Controls.Add(scrollPanel);

            // ==== groupSlotList ====
            GroupSlotList = new GroupControl
            {
                Text = "Danh sách vị trí trống",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            ListBoxSlots = new ListBoxControl
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true,
                SelectionMode = SelectionMode.One
            };
            GroupSlotList.Controls.Add(ListBoxSlots);

            ContentPanel.Controls.Add(GroupInfo, 0, 0);
            ContentPanel.Controls.Add(GroupSlotList, 0, 1);

            // ==== Bottom Buttons ====
            BtnAction1 = new SimpleButton { Text = includeExportQty ? "Xuất kho" : "Nhập kho", Width = 100, Margin = new Padding(5) };
            BtnAction1.Click += btn1Handler;

            BtnAction2 = new SimpleButton { Text = includeExportQty ? "In phiếu" : "Kiểm tra", Width = 100, Margin = new Padding(5) };
            BtnAction2.Click += btn2Handler;

            BtnCancel = new SimpleButton { Text = "Hủy", Width = 100, Margin = new Padding(5) };
            BtnCancel.Click += cancelHandler;

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10)
            };
            bottomPanel.Controls.Add(BtnCancel);
            bottomPanel.Controls.Add(BtnAction1);
            bottomPanel.Controls.Add(BtnAction2);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            mainLayout.Controls.Add(ContentPanel, 0, 0);
            mainLayout.Controls.Add(bottomPanel, 0, 1);

            return mainLayout;
        }

        private LabelControl CreateLabel()
        {
            return new LabelControl
            {
                Font = new Font("Tahoma", 10),
                Padding = new Padding(3),
                AutoSizeMode = LabelAutoSizeMode.Vertical,
                Appearance =
            {
                TextOptions = { WordWrap = DevExpress.Utils.WordWrap.Wrap }
            }
            };
        }
    }
}
