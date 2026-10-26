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
using DevExpress.Spreadsheet;
using DevExpress.XtraSpreadsheet;
using DevExpress.XtraEditors.Repository;

namespace PCTP.QRCODE_HVN.NhanLaiNG
{
    public partial class ImPortPGHDB : DevExpress.XtraEditors.XtraForm
    {
        public ImPortPGHDB()
        {
            InitializeComponent();
        }
      
       

       // Create a repository item corresponding to a SpinEdit control
       RepositoryItemSpinEdit repository = new RepositoryItemSpinEdit();
        private void spreadsheetControl1_CustomCellEdit(object sender, DevExpress.XtraSpreadsheet.SpreadsheetCustomCellEditEventArgs e)
        {
            // Specify the type of the custom editor assigned to cells of the "Quantity" table column.
            // To identify the custom editor, use a value of ValueObject associated with it. 

            if (e.ValueObject.IsText && e.ValueObject.TextValue == "MySpinEdit")
            {
                //Specify the repository item settings.
                repository.AutoHeight = false;
                repository.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;

                repository.MinValue = 1;
                repository.MaxValue = 1000;
                repository.IsFloatValue = false;
                // Assign the SpinEdit editor to a cell.
                e.RepositoryItem = repository;
            }
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XLS files (*.xls, *.xlt)|*.xls;*.xlt|XLSX files (*.xlsx, *.xlsm, *.xltx, *.xltm)|*.xlsx;*.xlsm;*.xltx;*.xltm|ODS files (*.ods, *.ots)|*.ods;*.ots|CSV files (*.csv, *.tsv)|*.csv;*.tsv|HTML files (*.html, *.htm)|*.html;*.htm";
            openFileDialog.FilterIndex = 2;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                spreadsheetControl1.LoadDocument(openFileDialog.FileName);
            }
            DevExpress.Spreadsheet.IWorkbook workbook = spreadsheetControl1.Document;
           
            foreach (Worksheet sheet in workbook.Worksheets)
            {
               comboBox1.Items.Add( sheet.Name);
                comboBox2.Items.Add(sheet.Name);
            }
            spreadsheetControl1.CustomCellEdit += spreadsheetControl1_CustomCellEdit;
        }
    }
}