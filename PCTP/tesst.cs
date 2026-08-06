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
using DevExpress.XtraCharts;

namespace PCTP
{
    public partial class tesst : DevExpress.XtraEditors.XtraForm
    {
        public tesst()
        {
            InitializeComponent();
            ((XYDiagram)chartControl1.Diagram).AxisX.QualitativeScaleOptions.AutoGrid = false;
        }
        ClassSQL.IFSPROVIDER IFS = new ClassSQL.IFSPROVIDER();
        ClassSQL.SQLPROVIDER SQL = new ClassSQL.SQLPROVIDER();
        private void tesst_Load(object sender, EventArgs e)
        {
            load();
        }
        private void load()
        {
            string sql, MH;
            DataTable tbl = new DataTable();
            sql = "select sum(BUY_QTY_DUE) as TTCS,0 as SLTONKHO,CUSTOMER_PART_NO " +

                    " from CUSTOMER_ORDER_JOIN " +
                   " where " +
                   " CUSTOMER_NO = '100001' and " +

                   " (OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual) or OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Partially Delivered') from dual) )and " +
                   " to_char(WANTED_DELIVERY_DATE,'ddmm') =  to_char(SYSDATE,'ddmm') and " +
                   " CUSTOMER_PO_REL_NO is not null " +
                   " group by CUSTOMER_PART_NO" +
                   " Order by CUSTOMER_PART_NO ";
            tbl = IFS.ExecuteQuery(sql);

            for (int i = 0; i < tbl.Rows.Count; i++)
            {
                MH = tbl.Rows[i]["CUSTOMER_PART_NO"].ToString();
                sql = "select sum(slconlai) from stocktp where PART = '" + MH + "'";
                string KQ = SQL.ExecuteReader(SQL.B7R2_FCCdb, sql);
                if (KQ == "")
                {
                    KQ = "0";
                }
                tbl.Rows[i]["SLTONKHO"] = KQ;
            }
            //ChartControl chart = new ChartControl();
            // Generate a data table and bind the chart to it.
            //((DevExpress.XtraCharts.XYDiagram)chart.Diagram).AxisX.QualitativeScaleOptions.AutoGrid = false;
            chartControl1.DataSource = tbl;
            //gridControl1.DataSource = tbl;

            //// Specify data members to bind the chart's series template.
            //chart.SeriesDataMember = "Stock";
            //chart.SeriesTemplate.ArgumentDataMember = "CUSTOMER_PART_NO";
            //chart.SeriesTemplate.ValueDataMembers.AddRange(new string[] { "SLTONKHO" });
            ////chart.SeriesDataMember = "Stock";
            ////chart.SeriesTemplate.ArgumentDataMember = "CUSTOMER_PART_NO";
            ////chart.SeriesTemplate.ValueDataMembers.AddRange(new string[] { "SLTONKHO" });
            //// Specify the template's series view.
            //chart.SeriesTemplate.View = new StackedBarSeriesView();

            //// Specify the template's name prefix.
            //chart.SeriesNameTemplate.BeginText = "Stock: ";

            //// Dock the chart into its parent, and add it to the current form.
            //chart.Dock = DockStyle.Fill;
            //this.Controls.Add(chart);
       
        }

    }
}