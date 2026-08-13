using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Localization;
using System.Collections;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.QRCODE_HVN.PGH
{
   
    public partial class LOT_EDIT : DevExpress.XtraGrid.Views.Grid.EditFormUserControl
    {
       
        public LOT_EDIT()
        {
            
            InitializeComponent();
          
          
            

        }
        ClassSQL.SQLPROVIDER sqlBRV = new ClassSQL.SQLPROVIDER();
        private GridView _MyView = null/* TODO Change to default(_) if this is not a reference type */;
        public GridView MyView
        {
            get
            {
                return _MyView;
            }
            set
            {
                _MyView = value;
            }
        }
        public LOT_EDIT(GridView view)
        {
            _MyView = view;
            InitializeComponent();

           
        }
        private void load()
        {
            
        }

       

        private void LOT_EDIT_Load(object sender, EventArgs e)
        {
            load();
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
           
            
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
           
        }

        private void label2_TextChanged(object sender, EventArgs e)
        {
            load();
        }
        void OnDisposing()
        {
            (this.Controls[0] as GridControl).Dispose();
        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            _MyView.PostEditor();
            _MyView.CloseEditForm();



        }

        private void simpleButton4_Click(object sender, EventArgs e)
        {
            MyView.CloseEditForm();
        }
    }
}
