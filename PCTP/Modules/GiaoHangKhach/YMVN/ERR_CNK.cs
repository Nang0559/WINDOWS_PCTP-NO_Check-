using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using PCTP.QRCODE_HVN.YMN;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.YMN
{
    public partial class ERR_CNK : DevExpress.XtraEditors.XtraForm
    {
        
        public ERR_CNK()
        {
            GIAOHANGYMN gIAOHANGYMN = new GIAOHANGYMN();
            GT_ERR_CNK.DataSource = gIAOHANGYMN.eRR_CNKs;
            InitializeComponent();
        }
        
    }
}