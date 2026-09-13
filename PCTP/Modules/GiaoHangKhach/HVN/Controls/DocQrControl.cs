using System;
using System.Data;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the DOCQRCODE / QR area of HVN_PGH.
    /// Owns the existing GridControl instance and its presentation helpers.
    /// QR business logic remains in DocQRScanEngine/DocQRSessionState/DocQRService.
    /// </summary>
    public class DocQrControl : XtraUserControl
    {
        public DocQrControl()
        {
            Dock = DockStyle.Fill;
        }

        public event EventHandler<FocusedRowChangedEventArgs> FocusedRowChanged = delegate { };

        public GridControl QrGrid { get; private set; }

        public GridView QrView
        {
            get { return QrGrid != null ? QrGrid.MainView as GridView : null; }
        }

        public void Bind(DataTable data)
        {
            if (QrGrid != null)
                QrGrid.DataSource = data;
        }

        public void MoveLastVisible()
        {
            if (QrView == null)
                return;

            int rowHandle = QrView.RowCount - 1;
            if (rowHandle >= 0)
                QrView.FocusedRowHandle = rowHandle;
        }

        public void RefreshData()
        {
            if (QrView != null)
                QrView.RefreshData();
        }

        public void ShowAndBringToFront()
        {
            Visible = true;
            BringToFront();
        }

        public int GetFocusedStt()
        {
            if (QrView == null || QrView.FocusedRowHandle < 0)
                return -1;

            string value = QrView.GetFocusedRowCellDisplayText("STT");
            return int.TryParse(value, out int stt) ? stt : -1;
        }

        public (string LotFcc, int SlFcc, int SlHvn) GetFocusedTemInfo()
        {
            if (QrView == null || QrView.FocusedRowHandle < 0)
                return (string.Empty, 0, 0);

            string lot = QrView.GetFocusedRowCellDisplayText("LOTFCC");
            int.TryParse(QrView.GetFocusedRowCellDisplayText("SLTEMFCC"), out int slFcc);
            int.TryParse(QrView.GetFocusedRowCellDisplayText("SLTEMHVN"), out int slHvn);
            return (lot, slFcc, slHvn);
        }

        public void DeleteFocusedRow()
        {
            if (QrView != null)
                QrView.DeleteSelectedRows();
        }

        public void ClearRows()
        {
            if (QrGrid != null)
                QrGrid.DataSource = null;
        }

        public void AttachExistingLayout(Control existingLayout)
        {
            if (existingLayout == null)
                return;

            var grid = existingLayout as GridControl;
            if (grid == null)
                throw new ArgumentException(
                    "DocQrControl chỉ nhận DevExpress GridControl hiện có.",
                    nameof(existingLayout));

            if (existingLayout.Parent == this)
            {
                QrGrid = grid;
                WireGridEvents();
                return;
            }

            Controls.Clear();
            existingLayout.Dock = DockStyle.Fill;
            Controls.Add(existingLayout);
            QrGrid = grid;
            WireGridEvents();
        }

        private void WireGridEvents()
        {
            if (QrView == null)
                return;

            QrView.FocusedRowChanged -= QrView_FocusedRowChanged;
            QrView.FocusedRowChanged += QrView_FocusedRowChanged;
        }

        private void QrView_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            FocusedRowChanged.Invoke(this, e);
        }
    }
}