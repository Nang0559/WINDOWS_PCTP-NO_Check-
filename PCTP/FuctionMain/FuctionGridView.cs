using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.FuctionMain
{
    class FuctionGridView
    {
        int index;
        public void AddColumn( DevExpress.XtraGrid.Views.Grid.GridView GVDH )
        {
            index++;
            int rowhandle = GVDH.GetDataRowHandleByGroupRowHandle(GVDH.FocusedRowHandle);
            //Store group column values 
            object[] groupValues = null;
            int groupColumnCount = GVDH.GroupedColumns.Count;
            if (groupColumnCount > 0)
            {
                groupValues = new object[groupColumnCount];
                for (int i = 0; i < groupColumnCount; i++)
                {
                    groupValues[i] = GVDH.GetRowCellValue(rowhandle, GVDH.GroupedColumns[i]);
                }
            }
            //Add a new row 
            GVDH.AddNewRow();
            //Get the handle of the new row 
            int newRowHandle = GVDH.FocusedRowHandle;
            object newRow = GVDH.GetRow(newRowHandle);
            //Set cell values corresponding to group columns 
            if (groupColumnCount > 0)
            {
                for (int i = 0; i < groupColumnCount; i++)
                {
                    GVDH.SetRowCellValue(newRowHandle, GVDH.GroupedColumns[i], groupValues[i]);
                }
            }
            //Accept the new row 
            //The row moves to a new position according to the current group settings 
            GVDH.UpdateCurrentRow();
            //Locate the new row 
            for (int n = 0; n < GVDH.DataRowCount; n++)
            {
                if (GVDH.GetRow(n).Equals(newRow))
                {
                    GVDH.FocusedRowHandle = n;
                    break;
                }
            }
            //DevExpress.XtraGrid.Columns.GridColumn column = new DevExpress.XtraGrid.Columns.GridColumn()
            //{
            //    Caption = String.Format("Selected{0}", index),
            //    FieldName = "Selected",
            //    ColumnEdit = new RepositoryItemCheckEdit() { },
            //    Width = 25
            //};
            //GVDH.Columns.Add(column);
            //column.VisibleIndex = 0;
        }
    }
}
