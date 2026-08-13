using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace PCTP
{
    public partial class FrmRenameCaption : DevExpress.XtraEditors.XtraForm
    {
        public string Caption;
        public FrmRenameCaption(string caption)
        {
            InitializeComponent();
            Caption = caption;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtnewName.Text = Caption;
            txtnewName.SelectAll();
            txtnewName.Focus();
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            Caption = txtnewName.Text;
            this.Close();
        }

        private void FrmRenameCaption_Load(object sender, EventArgs e)
        {

        }

        private void FrmRenameCaption_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
    }
}