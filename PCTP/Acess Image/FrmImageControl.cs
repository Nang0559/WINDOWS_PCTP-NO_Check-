using PCTP.Infrastructure.Security;
using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Acess_Image
{
    public partial class FrmImageControl : DevExpress.XtraEditors.XtraForm
    {
        public FrmImageControl()
        {
            AuthorizationGuard.Demand("SYSTEM.IMAGE_CONTROL", "ADMIN");
            InitializeComponent();
        }

        private void imagesControl1_Load(object sender, EventArgs e)
        {

        }
    }
}