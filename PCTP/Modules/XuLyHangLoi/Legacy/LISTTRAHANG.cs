using DevExpress.Export.Xl;
using DevExpress.Spreadsheet;
using DevExpress.XtraSpreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PCTP.ClassSQL;
using DevExpress.XtraPrinting;
using System.Web.Mvc;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraPrintingLinks;
using System.Data.Odbc;
using Office_12 = Microsoft.Office.Core;
using Excel_12 = Microsoft.Office.Interop.Excel;
using System.Collections;

namespace PCTP
{
    public partial class LISTTRAHANG : DevExpress.XtraEditors.XtraForm
    {
        public LISTTRAHANG()
        {
            InitializeComponent();
        }
        SQLPROVIDER Datasql = new SQLPROVIDER();
        private void button1_Click(object sender, EventArgs e)
        {
            gridView1.OptionsSelection.MultiSelect = true;
            ArrayList rows = new ArrayList();
            gridView1.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CheckBoxRowSelect;
            //CellRange oRange ;
            SpreadsheetControl spreadsheetControl1 = new SpreadsheetControl();
            if (checkfile(Application.StartupPath + @"\ExcelTemplate\FormTra.xlsx") == true)
            {
                //string partfile = Path.GetDirectoryName(Path.GetDirectoryName(System.IO.Directory.GetCurrentDirectory()));
                //partfile = partfile + "ExcelTemplate\FormTra.xlsx";
                IWorkbook workbook = spreadsheetControl1.Document;
                workbook.LoadDocument(Application.StartupPath + @"\ExcelTemplate\FormTra.xlsx", DocumentFormat.Xlsx);
                Worksheet sheet = workbook.Worksheets[0];
                //sheet.ClearContents .Range("B17:J27") = "";
                sheet.ClearContents(sheet["B17:J27"]);
                workbook.BeginUpdate();
                try
                {
                    ExternalDataSourceOptions options = new ExternalDataSourceOptions() { ImportHeaders = true };
                    // Bắt đầu ghi từ dong thứ 17
                    Int32[] selectedRowHandles = gridView1.GetSelectedRows();
                    
                    for (int i = 0; i < selectedRowHandles.Length; i++)
                    {
                        for (int j = 0; j < gridView1.Columns.Count; j++)
                        {
                            //sheet.Cells[i + 16, j].Value = i + 1;
                            sheet.Cells[i + 16, j + 1].Value = gridView1.GetRowCellValue(selectedRowHandles[i], gridView1.Columns[j].FieldName).ToString();
                        }
                        
                    }
                      
                }
                finally
                {
                    workbook.EndUpdate();
                }
               
                spreadsheetControl1.SaveDocument("FormTra.xlsx", DocumentFormat.Xlsx);
                Process.Start("FormTra.xlsx");
            }
            else
            {
                MessageBox.Show("File is in use!! Close it and try again");
            }
        }
        private bool checkfile(string part_file)
        {
            string path = part_file;
            bool available = true;
            try
            {
                using (FileStream fs = File.Open(path, FileMode.Open))
                {
                    available = true;
                }
            }
            catch (Exception ex)
            {
                available = false;
            }
            return available;
        }

        private void gridControl1_Click(object sender, EventArgs e)
        {

        }

        private void LISTTRAHANG_Load(object sender, EventArgs e)
        {
            NgayTra.EditValue = DateTime.Now;
            //NgayTra.Properties.Mask.MaskType = MaskType.DateTime
            //NgayTra.Properties.Mask.EditMask = "dd-MM-yyyy"


        }

        private void NgayTra_EditValueChanged(object sender, EventArgs e)
        {
            DateTime dateTimeNT = NgayTra.DateTime;
            if (NgayTra.Text != "")
            {
                string sql00 = "select k.PART,k.NAME,'' as HS, t.SLTRA,'' as KT,t.SLTRA ,t.LOT,t.LY_DO_NG from STOCKTPTRAHANG T, STOCKTP K " +
                              " where T.LOT = K.LOT and ngaytra = Convert(smalldatetime , '" + NgayTra.Text + "',104)";
                DataTable dt_list = Datasql.ExecuteQuery(Datasql.B7R2_FCCdb, sql00);
                gridControl1.DataSource = dt_list;
            }
        }
    }
}