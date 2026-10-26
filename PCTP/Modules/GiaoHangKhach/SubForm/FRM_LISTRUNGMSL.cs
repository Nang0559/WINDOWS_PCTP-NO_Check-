using System;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.QRCODE_HVN
{
    /// <summary>
    /// Existing duplicate MAHANG/SOLUONG selector. Selection belongs to this
    /// dialog instance; no static fields can leak a previous user's choice.
    /// </summary>
    public partial class FRM_LISTRUNGMSL : DevExpress.XtraEditors.XtraForm
    {
        public ListView SourceListView { get; set; }
        public int? SelectedStt { get; private set; }

        public FRM_LISTRUNGMSL()
        {
            InitializeComponent();
        }

        public FRM_LISTRUNGMSL(ListView listView)
        {
            SourceListView = listView ?? throw new ArgumentNullException(nameof(listView));
            InitializeComponent();
        }

        private void BAN_QRCODE_Load(object sender, EventArgs e)
        {
            if (SourceListView == null) return;
            listVTrungMaSL.Items.AddRange(
                SourceListView.Items.Cast<ListViewItem>()
                    .Select(item => (ListViewItem)item.Clone())
                    .ToArray());
        }

        private void txt_DOCQRCODE_KeyPress(object sender, KeyPressEventArgs e) { }

        private void listVTrungMaSL_SelectedIndexChanged(object sender, EventArgs e) { }

        private void listVTrungMaSL_DoubleClick(object sender, EventArgs e)
        {
            if (listVTrungMaSL.SelectedItems.Count == 0)
                return;

            ListViewItem item = listVTrungMaSL.SelectedItems[0];
            if (item.SubItems.Count == 0 || !int.TryParse(item.SubItems[0].Text, out int stt) || stt <= 0)
                return;

            SelectedStt = stt;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}