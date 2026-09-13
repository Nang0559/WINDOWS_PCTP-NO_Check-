using System;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.QRCODE_HVN.PGH.Controls
{
    /// <summary>
    /// UI boundary for the DOCQRCODE / QR area of HVN_PGH.
    ///
    /// Phase 9D:
    /// - Owns the existing DOCQRCODE GridControl instance.
    /// - Keeps the existing GridView and columns intact.
    /// - Does not move QR business logic or event handlers here yet.
    /// </summary>
    public class DocQrControl : XtraUserControl
    {
        public DocQrControl()
        {
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// The original DOCQRCODE GridControl after attachment.
        /// </summary>
        public GridControl QrGrid { get; private set; }

        /// <summary>
        /// The original DOCQRCODE GridView after attachment.
        /// </summary>
        public GridView QrView
        {
            get { return QrGrid != null ? QrGrid.MainView as GridView : null; }
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
            if (QrView == null)
                return;

            QrView.DeleteSelectedRows();
        }

        public void ClearRows()
        {
            if (QrGrid == null)
                return;

            QrGrid.DataSource = null;
        }

        /// <summary>
        /// Moves the existing DOCQRCODE GridControl into this UserControl
        /// without recreating the GridView or its columns.
        /// </summary>
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
                return;
            }

            Controls.Clear();
            existingLayout.Dock = DockStyle.Fill;
            Controls.Add(existingLayout);
            QrGrid = grid;
        }
    }
}
