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
using PCTP;
using DevExpress.CodeParser;
using DevExpress.XtraReports.UI;
using PCTP.YMN;
using System.Globalization;
using DevExpress.PivotGrid.PivotTable;
using DevExpress.XtraGrid.Views.Grid;

using DevExpress.XtraReports.UserDesigner;
using System.Drawing.Design;
using DevExpress.XtraGrid;
using PCTP.QRCODE_HVN.PGH;
using DevExpress.Utils.Serializing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Drawing.Printing;

namespace PCTP.QRCODE_HVN.YMN
{
   
    public partial class GIAOHANGYMN : DevExpress.XtraEditors.XtraForm
    {
        public GIAOHANGYMN()
        {
            InitializeComponent();
            khoitao = 1;
            dateNG.DateTime = DateTime.Now;
            // LOADPHIEUDANGDOC();
              LOAD();
            
          khoitao = 0;
        }
        
        string N_XH;
        //public ClsPGH _PGHs = new ClsPGH();
        public Queue<ClsPGH> QPGHs = new Queue<ClsPGH>();
        public List<DS_ERR_CNK> eRR_CNKs = new List<DS_ERR_CNK>();
        DataTable tb_PGH = new DataTable();
        ClassSQL.IFSPROVIDER IFS = new ClassSQL.IFSPROVIDER();
        ClassSQL.SQLPROVIDER sqlBRV = new ClassSQL.SQLPROVIDER();
        private int Daluu;
        private int khoitao;
        public DataTable tbl;
        public DataTable tbllechIFS;
        private string GioXuat = "";
        public Boolean KT;
        public Boolean c_L;
        public Boolean Da_DUYET;
        public Boolean DUYET;
        public string PO_PART;
        public static string ListPOIFS = "";
        #region So sanh 2 bang
        private DataTable getDifferentRecords(DataTable FirstDataTable, DataTable SecondDataTable)
        {
            //Create Empty Table     
            DataTable ResultDataTable = new DataTable("ResultDataTable");

            //use a Dataset to make use of a DataRelation object     
            using (DataSet ds = new DataSet())
            {
                //Add tables     
                ds.Tables.AddRange(new DataTable[] { FirstDataTable.Copy(), SecondDataTable.Copy() });

                //Get Columns for DataRelation     
                DataColumn[] firstColumns = new DataColumn[ds.Tables[0].Columns.Count];
                for (int i = 0; i < firstColumns.Length; i++)
                {
                    firstColumns[i] = ds.Tables[0].Columns[i];
                }

                DataColumn[] secondColumns = new DataColumn[ds.Tables[1].Columns.Count];
                for (int i = 0; i < secondColumns.Length; i++)
                {
                    secondColumns[i] = ds.Tables[1].Columns[i];
                }

                //Create DataRelation     
                DataRelation r1 = new DataRelation(string.Empty, firstColumns, secondColumns, false);
                ds.Relations.Add(r1);

                DataRelation r2 = new DataRelation(string.Empty, secondColumns, firstColumns, false);
                ds.Relations.Add(r2);

                //Create columns for return table     
                for (int i = 0; i < FirstDataTable.Columns.Count; i++)
                {
                    ResultDataTable.Columns.Add(FirstDataTable.Columns[i].ColumnName, FirstDataTable.Columns[i].DataType);
                }

                //If FirstDataTable Row not in SecondDataTable, Add to ResultDataTable.     
                ResultDataTable.BeginLoadData();
                foreach (DataRow parentrow in ds.Tables[0].Rows)
                {
                    DataRow[] childrows = parentrow.GetChildRows(r1);
                    if (childrows == null || childrows.Length == 0)
                        ResultDataTable.LoadDataRow(parentrow.ItemArray, true);
                }

                //If SecondDataTable Row not in FirstDataTable, Add to ResultDataTable.     
                foreach (DataRow parentrow in ds.Tables[1].Rows)
                {
                    DataRow[] childrows = parentrow.GetChildRows(r2);
                    if (childrows == null || childrows.Length == 0)
                        ResultDataTable.LoadDataRow(parentrow.ItemArray, true);
                }
                ResultDataTable.EndLoadData();
            }

            return ResultDataTable;
        }
        #endregion 
        private bool CheckOpened(string name)
        {
            FormCollection fc = Application.OpenForms;

            foreach (Form frm in fc)
            {
                if (frm.Text == name)
                {
                    return true;
                }
            }
            return false;
        }
        private void LOAD()
        {
           
            WaitForm2.SO = 1;
            //try
            //{
                splashScreenManager1.ShowWaitForm();
            if (YMVN_CHONGIAO.MP_SP == "MP")
            {
                if (TONTAI() == 0)
                {

                    dateNG.Enabled = true;
                    LOAD_PHIEU_YMN();

                }
                else
                {


                    LOADPHIEUDANGDOC();
                    dateNG.Enabled = false;

                }
            }
            else
            {
                if (TONTAISP() == 0)
                {

                    dateNG.Enabled = true;
                    LOAD_PHIEU_YMN();

                }
                else
                {


                    LOADPHIEUDANGDOCSP();
                    dateNG.Enabled = false;

                }
            }    
                splashScreenManager1.CloseWaitForm();
            //}
            //catch
            //{ }
        }
        
        private void LOAD_PHIEU_YMN()
        {

            if(YMVNDOCQRCODE.HT==true)
            {
                DUYET = true;
            }
            string sql;
            if (YMVN_CHONGIAO.MP_SP == "MP")
            {
                // (REGEXP_LIKE(SUB_DOCK_CODE, '^[[:digit:]]+$') or SUB_DOCK_CODE like 'A%')
                sql = "select '' as STT,WANTED_DELIVERY_DATE,SUB_DOCK_CODE as PO,DOCK_CODE,CUSTOMER_PART_NO,CATALOG_DESC,customer_part_unit_meas as DV,BUY_QTY_DUE, " +
                        " '' as HOP ,'NG' as STATUS, '' as LOT " +


                        "from CUSTOMER_ORDER_JOIN  where CUSTOMER_NO = '100002' and " +
                        " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual)) and to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "' and DOCK_CODE <> 'VSP1'  " +

                        " Order by PO,CUSTOMER_PART_NO ";
            }
            else
            {
                sql = "select '' as STT,WANTED_DELIVERY_DATE,SUB_DOCK_CODE as PO,DOCK_CODE,CUSTOMER_PART_NO,CATALOG_DESC,customer_part_unit_meas as DV,BUY_QTY_DUE, " +
                        " '' as HOP ,'NG' as STATUS, '' as LOT " +


                        "from CUSTOMER_ORDER_JOIN  where CUSTOMER_NO = '100002' and " +
                        " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual)) and to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "' and DOCK_CODE = 'VSP1' " +

                        " Order by PO,CUSTOMER_PART_NO ";
            }    
            tb_PGH = IFS.ExecuteQuery(sql);
            gridCtrDONHANG.DataSource = tb_PGH;
            
            for(int m = 0;m< tb_PGH.Rows.Count;m++)
            {
                string POIFS = tb_PGH.Rows[m]["PO"].ToString();
                if(ListPOIFS=="")
                {
                    ListPOIFS = "'" + POIFS + "'";
                }
                else
                {
                    ListPOIFS = ListPOIFS + ",'" + POIFS + "'";
                }
            }
            DuyetTTHangThieu();
            if (DUYET == false)
            {
                LOADGX(); }

            duyetlit();
            LoadPhieuGH_GIO(GioXuat);
            //DataSet GHEPLOT = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_gheplotYM1");
            //gridCTTGL.DataSource = GHEPLOT.Tables[0];
            //GHEP_LOT(tbl);

            Da_DUYET = false;
            DUYET = true;
        }
        # region Xử Lý Load Phiếu

        private void Loaddaluu()
        {
            string _l = "", sql, ODN, MH, SLG, Gi,Ger;
                object Gear;
            DateTime GG;
            Daluu = 0;
            string GGGG;
            for (int i = 0;i<gridVDONHANG.RowCount;i++)
            {
                ODN= gridVDONHANG.GetRowCellValue(i, "PO").ToString();
                MH = gridVDONHANG.GetRowCellValue(i, "CUSTOMER_PART_NO").ToString();
                GG = DateTime.Parse(gridVDONHANG.GetRowCellValue(i, "WANTED_DELIVERY_DATE").ToString());
                Gi = GG.ToString("yyyy-MM-dd HH:mm:ss");
                GGGG = GG.ToString("HH:mm:ss");
                Gear = gridVDONHANG.GetRowCellValue(i, "Gear");
                if(Gear == null )
                {
                    Ger = "";
                }
                SLG = gridVDONHANG.GetRowCellValue(i, "BUY_QTY_DUE").ToString();
                sql = "select lot from LUUPHIEUGIAOHANG where rtrim(CUA) = '" + ODN + "' and MAHANG =  '" + MH + "' and NGAYGIAO = '" + Gi + "' and GearYMVN = '"+Gear+"' ";
                _l = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                if (_l != "" )
                {
                    gridVDONHANG.SetRowCellValue(i, "LOT", _l);
                    gridVDONHANG.SetRowCellValue(i, "STATUS", "OK");
                    Daluu = Daluu + 1;
                    
                        for (int j = 0; j < CheckGX.Items.Count; j++)
                        {
                            // For every other item in the list, set as checked.
                            if (CheckGX.Items[j].ToString() == GGGG)
                            {
                            CheckGX.SetItemChecked(j, true);
                            }
                        }
                    
                }
            }

        }
        private void LoadLectIFSMI(string GX)
        {
          

            DataTable tb_PGHSS = new DataTable();
            if (PO_PART == "")
                PO_PART = "''";
            string sql = "select  TO_DATE( WANTED_DELIVERY_DATE) AS WANTED_DELIVERY_DATE,SUB_DOCK_CODE as PO,CUSTOMER_PART_NO,BUY_QTY_DUE " +

                       "from CUSTOMER_ORDER_JOIN  where CUSTOMER_NO = '100002' and " +
                       " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual)) and to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "'" +
                        " and CONCAT(SUB_DOCK_CODE,CUSTOMER_PART_NO)  not in (" + PO_PART + ") "+
                       " Order by PO,CUSTOMER_PART_NO ";
            tb_PGHSS = IFS.ExecuteQuery(sql);
            gridCLECH.DataSource = tb_PGHSS;
        }
        private void LoadPhieuGH_GIO(string GX)
        {
            PO_PART = "";
             List<int> ID_DUYET = new List<int>();
             int PON,dem=0;
            DataTable TB_SQL = new DataTable();
            DataTable tb_dc_dv= new DataTable();
            tbl = new DataTable();
            tbl.Columns.Add("STT", typeof(string));
            tbl.Columns.Add("WANTED_DELIVERY_DATE", typeof(DateTime));
            tbl.Columns.Add("PO", typeof(string));
            tbl.Columns.Add("DOCK_CODE", typeof(string));
            tbl.Columns.Add("CUSTOMER_PART_NO", typeof(string));
            tbl.Columns.Add("Gear", typeof(string));
            tbl.Columns.Add("CATALOG_DESC", typeof(string));
            tbl.Columns.Add("DV", typeof(string));
            
            tbl.Columns.Add("BUY_QTY_DUE", typeof(Decimal));
            tbl.Columns.Add("HOP", typeof(string));
            
            tbl.Columns.Add("STATUS", typeof(string));
            tbl.Columns.Add("LOT", typeof(string));
            tbl.Columns.Add("XE", typeof(string));
            DataRow row;
            string PO,DC, PNO, PN, DV, Gear, QCDG_BRV;
            object DCC;
            DateTime GIO;
            int QTY, QCDG,XE,DU=0;
            string sql, N_XHSQL = dateNG.DateTime.ToString("MM/dd/yyyy");
            if (GX == "")
            {
                if (YMVN_CHONGIAO.MP_SP == "MP")
                {
                    sql = " select ROW_NUMBER() OVER (ORDER BY Part_no DESC) as N,* from " +
                        "(" +
                            " select Oder_no, Part_no, Part_name, NgayGiao, sum(Slgiao) as Slgiao,QCDG,Gear from Purchase_Order_YMVN where  CONVERT(VARCHAR(10), NgayGiao, 101) = '" + N_XHSQL + "' and CUA <> 'VSP1'" +
                                " group by Oder_no,Part_no,Part_name,NgayGiao,QCDG,Gear " +

                         " ) as S " +
                        " order by N ";


                }
                else
                {
                    sql = " select ROW_NUMBER() OVER (ORDER BY Part_no DESC) as N,* from " +
                        "(" +
                            " select Oder_no, Part_no, Part_name, NgayGiao, sum(Slgiao) as Slgiao,QCDG,Gear from Purchase_Order_YMVN where  CONVERT(VARCHAR(10), NgayGiao, 101) = '" + N_XHSQL + "' and CUA = 'VSP1'" +
                                " group by Oder_no,Part_no,Part_name,NgayGiao,QCDG,Gear " +

                         " ) as S " +
                        " order by N ";
                }    
            }
            else
            {
                if (YMVN_CHONGIAO.MP_SP == "MP")
                {
                    sql = " select ROW_NUMBER() OVER (ORDER BY Part_no DESC) as N,* " +
                 " from Purchase_Order_YMVN where   CONVERT(VARCHAR(10), NgayGiao, 101) = '" + N_XHSQL + "' and CONVERT(VARCHAR, NgayGiao, 108) in (" + GX + ") and CUA <> 'VSP1' order by NgayGiao";
                }
                else
                {
                    sql = " select ROW_NUMBER() OVER (ORDER BY Part_no DESC) as N,* " +
                                     " from Purchase_Order_YMVN where   CONVERT(VARCHAR(10), NgayGiao, 101) = '" + N_XHSQL + "' and CONVERT(VARCHAR, NgayGiao, 108) in (" + GX + ") and CUA = 'VSP1' order by NgayGiao";
                }    
            }
            TB_SQL = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            if (TB_SQL.Rows.Count > 0)
            {
               
                dem = dem + 1;
                
                for (int t = 0; t < TB_SQL.Rows.Count; t++)
                {
                    XE = 0;
                    DU = 0;
                    #region TMP2
                    int STT = tbl.Rows.Count;
                    row = tbl.NewRow();
                    row["STT"] = STT + 1;
                    PO = TB_SQL.Rows[t].Field<string>("Oder_no");
                    row["PO"] = PO;

                    //row["DOCK_CODE"] = "";
                    GIO = TB_SQL.Rows[t].Field<DateTime>("NgayGiao");
                    row["WANTED_DELIVERY_DATE"] = GIO;
                    PNO = TB_SQL.Rows[t].Field<string>("Part_no");
                    row["CUSTOMER_PART_NO"] = PNO;
                    QCDG_BRV = "select CAST(MinCloseQty as int) from B20Item where Code = '" + PNO +"'";
                    QCDG_BRV = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, QCDG_BRV);
                    if(QCDG_BRV == "")
                    {
                        QCDG = 1;
                    }
                    else
                    {
                        QCDG = int.Parse(QCDG_BRV);
                    }
                    Gear = TB_SQL.Rows[t].Field<string>("Gear");
                    row["Gear"] = Gear;
                    PN = TB_SQL.Rows[t].Field<string>("Part_name");
                    if (YMVN_CHONGIAO.MP_SP == "MP")
                    {
                        sql = "select DOCK_CODE,customer_part_unit_meas as DV " +

                           //and to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "''

                           "from CUSTOMER_ORDER_JOIN  where CUSTOMER_NO = '100002' and " +
                           " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual)) " +

                           " and SUB_DOCK_CODE = '" + PO + "' and  CUSTOMER_PART_NO = '" + PNO + "' and   DOCK_CODE <> 'VSP1'  ";
                    }
                    else
                    {
                        sql = "select DOCK_CODE,customer_part_unit_meas as DV " +

                           //and to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "''

                           "from CUSTOMER_ORDER_JOIN  where CUSTOMER_NO = '100002' and " +
                           " (OBJSTATE <> (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Cancelled') from dual)) " +

                           " and SUB_DOCK_CODE = '" + PO + "' and  CUSTOMER_PART_NO = '" + PNO + "' and  DOCK_CODE = 'VSP1' ";
                    }    
                    tb_dc_dv = IFS.ExecuteQuery(sql);
                    if (tb_dc_dv.Rows.Count > 0)
                    {
                        
                        DCC = tb_dc_dv.Rows[0]["DOCK_CODE"];
                        if (DCC == null )
                        {
                            DC = "";
                        }
                        else
                        { DC = DCC.ToString(); }
                        row["DOCK_CODE"] = DC;
                        DV = tb_dc_dv.Rows[0].Field<string>("DV");
                        row["DV"] = DV;
                    }
                    else
                    {
                        row["DOCK_CODE"] = "";
                        row["DV"] = "";
                    }    
                    
                    row["CATALOG_DESC"] = PN;
                    
                    QTY = TB_SQL.Rows[t].Field<int>("Slgiao");
                    row["BUY_QTY_DUE"] = QTY;
                    if(QCDG > 1)
                    {
                        DU = QTY % QCDG;
                        QCDG = QTY / QCDG;
                    }
                    if (DU > 0)
                    {
                        QCDG = QCDG + 1;
                    }
                    //QCDG = TB_SQL.Rows[t].Field<int>("QCDG");


                    row["HOP"] = QCDG;
                    DU = QCDG % 10;
                    XE = QCDG / 10;
                    if(DU>0)
                    {
                        XE = XE + 1;
                    }    
                    row["XE"] = XE;
                    row["STATUS"] = "NG";
                    if (PO_PART == "")
                    {
                        PO_PART = "'" + PO + PNO + "'";
                            }
                    else
                    {
                        PO_PART = PO_PART + ",'" + PO + PNO + "'";
                    }
                    tbl.Rows.Add(row);
                    #endregion
                }
                gridCtrDONHANG.DataSource = tbl;
                //foreach (var rc in tb_PGH.Rows)
                //{
                //    var dl = new
                //    {
                //        Mahang = rc["MaHang"] .ToString(),

                //    };
                //    QPGHs.Append();
                //}
            }

            LOADGX();
            LoadLectIFSMI(GioXuat);

            //else
            //{
            //    gridCtrDONHANG.DataSource = null;
            //}
            #endregion
            Loaddaluu();
        }
        private int TONTAI()
        {
            int  KQ = -1;
            string sql = "select count(*) from YMVN_TMPPHIEUGIAOHANG where addnm = 0";
            KQ = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            if(KQ > 0)
            {
                sql = "select count(*) from YMVN_DOCQRCODE";
                KQ = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                if(KQ >0)
                {
                    KQ = -1;
                }    
                else
                {
                    KQ = 0;
                }    
            }   
            else
            {
                KQ = 0;
            }    
            return KQ;
        }
        #region YMVN CHECK SP
        private int CHOPHEPCN(string MPSP,string LOT)
        {
            int KQ = -1;
            string sql = "select count(*) from " + MPSP + " where " + LOT + "<> '' and STATUS = 'NG'";
            KQ = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            if (KQ > 0)
            {
                KQ = 1;
            }
            else
            {
                KQ = 0;
            }
            return KQ;
        }
        private int TONTAISP()
        {
            int KQ = -1;
            string sql = "select count(*) from SP_tmpphieugiaohang ";
            KQ = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            if (KQ > 0)
            {
                sql = "select count(*) from SP_docqrcode";
                KQ = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                if (KQ > 0)
                {
                    KQ = -1;
                }
                else
                {
                    KQ = 0;
                }
            }
            else
            {
                KQ = 0;
            }
            return KQ;
        }
        public void LOADPHIEUDANGDOCSP()
        {
            string sql;

            DataTable TMPGH = new DataTable();
            sql = "select CONVERT(VARCHAR(10), NgayGiao, 101) as NG from SP_TMPPHIEUGIAOHANG group by CONVERT(VARCHAR(10), NgayGiao, 101)";
            TMPGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            string ngayg = TMPGH.Rows[0]["NG"].ToString();
            dateNG.DateTime = Convert.ToDateTime(DateTime.ParseExact(ngayg, "MM/dd/yyyy", CultureInfo.InvariantCulture));




          LOADGX();
            KT = false;
            sql = "select NGAYGIAO,GIOGIAO from SP_TMPPHIEUGIAOHANG group by NGAYGIAO,GIOGIAO";
            TMPGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            for (int i = 0; i < TMPGH.Rows.Count; i++)
            {
                object ngaygiao = TMPGH.Rows[i].Field<DateTime>("NGAYGIAO");
                
                string GGG = Convert.ToDateTime(ngaygiao).ToString("HH:mm:ss");
                object GG = TMPGH.Rows[i].Field<string>("GIOGIAO");

                for (int j = 0; j < CheckGX.Items.Count; j++)
                {
                    object IT = CheckGX.Items[j];
                    // For every other item in the list, set as checked.
                    if (IT.ToString() == GGG)
                    {
                        CheckGX.SetItemChecked(j, true);
                    }
                }
            }
            //LoadPhieuGH_GIO(GioXuat);
            sql = " select STT, NGAYGIAO as WANTED_DELIVERY_DATE,SLGIAO as BUY_QTY_DUE,'pcs' as DV,'' as Gear,NHAMAY,PO_NO as PO,MAHANG as CUSTOMER_PART_NO,TENHANG as CATALOG_DESC,CUA as DOCK_CODE, " +
                   " '' AS HOP,LOTNO as LOT,STATUS  from SP_TMPPHIEUGIAOHANG ";
            DataTable TMPPGH = new DataTable();
            TMPPGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            gridCtrDONHANG.DataSource = TMPPGH;
            //gridCtrDONHANG.RefreshDataSource();
            DataSet GHEPLOT = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_gheplotYM1");
            gridCTTGL.DataSource = GHEPLOT.Tables[0];
            //if (DUYET == false)
            //{ }
            //GHEP_LOT(TMPPGH);
        }
        private Boolean KTTTGHSP()
        {
            Boolean KQ;
            string sql = "select lotno,status from SP_tmpphieugiaohang where lotno <> ''";
            DataTable SKQ = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            if (SKQ.Rows.Count > 1)
            {
                KQ = true;
            }
            else
            {
                KQ = false;
            }
            return KQ;
        }
        #endregion
        public void LOADPHIEUDANGDOC()
        {
            string sql;

            DataTable TMPGH = new DataTable();
            sql = "select CONVERT(VARCHAR(10), NgayGiao, 101) as NG from YMVN_TMPPHIEUGIAOHANG group by CONVERT(VARCHAR(10), NgayGiao, 101)";
            TMPGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            string ngayg = TMPGH.Rows[0]["NG"].ToString();
            dateNG.DateTime = Convert.ToDateTime(DateTime.ParseExact(ngayg, "MM/dd/yyyy", CultureInfo.InvariantCulture));
            LOADGX();
            KT = false;
                sql = "select NGAYGIAO,GIOGIAO from YMVN_TMPPHIEUGIAOHANG group by NGAYGIAO,GIOGIAO";
                TMPGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
                for (int i = 0; i < TMPGH.Rows.Count; i++)
                {
                    object ngaygiao = TMPGH.Rows[i].Field<DateTime>("NGAYGIAO");
                    
                string GGG = Convert.ToDateTime(ngaygiao).ToString("HH:mm:ss");
                    object GG = TMPGH.Rows[i].Field<string>("GIOGIAO");
               
                    for (int j = 0; j < CheckGX.Items.Count; j++)
                    {
                    object IT = CheckGX.Items[j];
                        // For every other item in the list, set as checked.
                        if (IT.ToString() == GGG)
                        {
                            CheckGX.SetItemChecked(j, true);
                        }
                    }
                }
            //LoadPhieuGH_GIO(GioXuat);
            sql = " select STT, NGAYGIAO as WANTED_DELIVERY_DATE,SOLUONG as BUY_QTY_DUE,DV,NHAMAY,CUA as PO,MAHANG as CUSTOMER_PART_NO,TENHANG as CATALOG_DESC,TRUYEN as DOCK_CODE, " +
                   " '' AS HOP,LOT,STATUS,TTPHIEU as Gear  from YMVN_TMPPHIEUGIAOHANG ";
                DataTable TMPPGH = new DataTable();
                TMPPGH = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
                gridCtrDONHANG.DataSource = TMPPGH;
            //gridCtrDONHANG.RefreshDataSource();
            DataSet GHEPLOT = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_gheplotYM1");
            gridCTTGL.DataSource = GHEPLOT.Tables[0];
            //if (DUYET == false)
            //{  }
            //GHEP_LOT(TMPPGH);
        }
        private void LOADGX()
        {
            CheckGX.Items.Clear();
            string GXH, sql;
            string N_XHG = dateNG.DateTime.ToString("MM/dd/yyyy");
            if (c_L != true)
            {     
              
            DataTable tblGX = new DataTable();
            sql = "select CONVERT(VARCHAR, NgayGiao, 108) AS TIMES " +
                     //  " CONVERT(VARCHAR(8), NgayGiao, 101) AS DATES " +
                     " from Purchase_Order_YMVN " +
                     " where CONVERT(VARCHAR(10), NgayGiao, 101) = '" + N_XHG + "' group by NgayGiao order by NgayGiao ";
            tblGX = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
      
            for (int i = 0; i < tblGX.Rows.Count; i++)
            {
                    object it = tblGX.Rows[i].Field<string>("TIMES");
                    CheckGX.Items.Add(it);
               

            }
            }
        }
        

        private void dateNG_EditValueChanged(object sender, EventArgs e)
        {
            N_XH = dateNG.DateTime.ToString("ddMMyyyy");
            c_L = false;
            DUYET = false;
            Da_DUYET = true;
            if (khoitao == 0)
            {
                LOAD();
            }
        }
        


        private void duyetlit()
        {
            Da_DUYET = true;
            GioXuat = "";
            //foreach (ListViewItem item in this.listVKGX.CheckedItems)
            //{
            //    if (GioXuat == "")
            //    {
            //        GioXuat = "'" + item.Text + "'";
            //    }
            //    else
            //    {
            //        GioXuat = GioXuat + ",'" + item.Text + "'";
            //    }
            //}
            foreach(object item in CheckGX.CheckedItems )
            {
                if (GioXuat == "")
                {
                    GioXuat = "'" + item + "'";
                }
                else
                {
                    GioXuat = GioXuat + ",'" + item + "'";
                }
            }
        }


        #region Ghep Lot giao hàng
        private int F_Gear(string G)
        {
            int I_G = 0;
            if(G == "A")
            {
                I_G = 1;

            }   
            else
            {
                if(G=="B")
                { I_G = 2; }
                else
                {
                    if(G=="C")
                    { I_G = 3; }
                    else
                    {
                        if(G=="D")
                        { I_G = 4; } 
                        else
                        { if (G=="E")
                            { I_G = 5; }    
                                    } 
                    }    
                }    
            }
            return I_G;
        }

        private void GHEP_LOT(DataTable T_B)
        {

            string sql, Status, sqlsltk = "";
            string PartNo, Gear,LOT,STATUS;
            string[] A_Gear, G_SL;
            DateTime GioGiao;
            int SLGIAO, In_Gear;
            int TCDG;
            object O_G;
            object O_SLGIAO_G;
            DataTable tblGL = new DataTable();
            tblGL.Columns.Add("MH", typeof(string));
            tblGL.Columns.Add("GG", typeof(string));
            tblGL.Columns.Add("LG", typeof(string));
            tblGL.Columns.Add("Gear", typeof(string));

            // Lay don hang theo gio
            string N_XHSQL = dateNG.DateTime.ToString("MM/dd/yy");
           
            //---------------------------------------------------------------------------------------------------------------------------------
            //string sqlTK = "select LOT,SLCONLAI from STOCKTP where SLCONLAI > 0 and PART = '" + MaHang + "' order by lot";
            DataTable TBTK = new DataTable();

            if (T_B.Rows.Count > 0)
            {
               
                
                    CAPNHAPTMP("%", 0);

                    for (int j = 0; j < T_B.Rows.Count; j++)
                    {

                     
                        PartNo = T_B.Rows[j].Field<string>("CUSTOMER_PART_NO");
                        if (PartNo == "2ND-E6300-00-00-80")
                        {
                            int check = 0;
                        }
                        LOT = T_B.Rows[j].Field<string>("LOT");
                        STATUS = T_B.Rows[j].Field<string>("STATUS");
                        if (STATUS == "NG")
                        {


                            // Lấy tiêu chuẩn đóng gói .của mã hàng .
                            sql = "SELECT cast(isnull(MinCloseQty,0) as int)  from B20Item where Code= '" + PartNo + "'";
                            string sTCDG = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                            if (sTCDG == "")
                            {
                                TCDG = -1;
                            }
                            else
                            {

                                TCDG = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                                if (TCDG == 0)
                                {
                                    TCDG = -1;
                                }
                            }
                            GioGiao = T_B.Rows[j].Field<DateTime>("WANTED_DELIVERY_DATE");
                            O_G = T_B.Rows[j].Field<string>("Gear");
                            if (O_G == null)
                                Gear = "";
                            else
                                Gear = O_G.ToString();
                            if (Gear == "")
                            {
                                In_Gear = 0;
                                O_SLGIAO_G = T_B.Rows[j]["BUY_QTY_DUE"];
                                if (O_SLGIAO_G != "")
                                { SLGIAO = Convert.ToInt32(O_SLGIAO_G); }
                                else
                                { SLGIAO = 0; }

                                sqlsltk = "select lot,part,isnull(slconlai,0) as slconlai ,isnull(slconlaitmp,0) as  slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0 order by lot";
                                DuyetTONKHO_GHEP(tblGL, sqlsltk, SLGIAO, TCDG, GioGiao, PartNo, "", 0);
                            }
                            else
                            {
                                A_Gear = Gear.Split(',');
                                if (A_Gear.Length > 1)
                                {
                                    for (int i = 0; i < A_Gear.Length; i++)
                                    {
                                        G_SL = A_Gear[i].Split(':');
                                        In_Gear = F_Gear(G_SL[0].Trim().Substring(G_SL[0].Trim().Length - 1, 1));
                                        SLGIAO = int.Parse(G_SL[1].Trim().Substring(0, G_SL[1].Trim().Length - 3));
                                        if (PartNo == "1FP-E6611-11-00-80")
                                            PartNo = "1FP-E6611-11-00-80";
                                        sqlsltk = "select lot,part,isnull(slconlai,0) as slconlai ,isnull(slconlaitmp,0) as  slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0 and right(substring(LOT,1,13),1) = '" + In_Gear.ToString() + "' order by lot";
                                        DuyetTONKHO_GHEP(tblGL, sqlsltk, SLGIAO, TCDG, GioGiao, PartNo, G_SL[0].Trim().Substring(G_SL[0].Trim().Length - 1, 1), In_Gear);
                                    }
                                }
                                else
                                {
                                    G_SL = A_Gear[0].Split(':');
                                    In_Gear = F_Gear(G_SL[0].Trim().Substring(G_SL[0].Trim().Length - 1, 1));
                                    SLGIAO = int.Parse(G_SL[1].Trim().Substring(0, G_SL[1].Trim().Length - 3));
                                    sqlsltk = "select lot,part,isnull(slconlai,0) as slconlai ,isnull(slconlaitmp,0) as  slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0 and right(substring(LOT,1,13),1) = '" + In_Gear.ToString() + "'order by lot";
                                    DuyetTONKHO_GHEP(tblGL, sqlsltk, SLGIAO, TCDG, GioGiao, PartNo, G_SL[0].Trim().Substring(G_SL[0].Trim().Length - 1, 1), In_Gear);
                                }
                            }



                            // Lấy tồn kho theo LOT của mã hàng va gear .

                        

                    }
                }
            }

            gridCTTGL.DataSource = tblGL;
        }
        private void DuyetTONKHO_GHEP(DataTable tblGL, string sql,int SLGIAO,int TCDG,DateTime GioGiao,string PartNo,string Gear,int In_Gear)
        {
            DataRow row;
            string LOTDUYET, LOTDUYET1;
            int SLTKCONLAI_LOTDUYET;
            object TONKHOTEM;
            object TONKHOCOLAITMP;
            int slle = 0;
            string LOTGHEP = "";
            int SLCANGHEP = 0;
           
            DataTable TONKHOTHEOMA1 = new DataTable();
            DataTable TONKHOTHEOMATMP = new DataTable();
            TONKHOTHEOMA1.Clear();
            TONKHOTHEOMA1 = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            // Lấy tồn kho tổng của mã hàng.

            //Duyệt Đơn hàng và với kho .

            if (TONKHOTHEOMA1.Rows.Count > 0)// Nếu có tồn kho
            {
                int TongDuyet = 0;
                for (int i = 0; i < TONKHOTHEOMA1.Rows.Count; i++)// duyệt tồn kho
                {
                    // Cập Nhập tạm thông tin tồn kho vào trường TMP

                    TONKHOTEM = TONKHOTHEOMA1.Rows[i][3];
                    if (TONKHOTEM == null)
                    {
                        TONKHOTEM = "0";
                    }
                    else

                    if (Convert.ToInt32(TONKHOTEM) == 0)
                    {
                        LOTDUYET = TONKHOTHEOMA1.Rows[i].Field<string>("lot");
                        object TONKHOCOLAI = TONKHOTHEOMA1.Rows[i][2];
                        
                        SLTKCONLAI_LOTDUYET = Convert.ToInt32(TONKHOCOLAI);
                        TongDuyet = TongDuyet + SLTKCONLAI_LOTDUYET;
                        CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET);
                        if (TongDuyet >= SLGIAO)
                            break;
                    }
                    // Nếu tổng tồn kho ít hơn số lượng cần giao thì cho vào danh sách hàng thiếu
                }
                // Lấy tồn kho theo LOTTMP của mã hàng .
                  
                string sqlTMP;
                if (In_Gear != 0)
                {
                    sqlTMP = "select lot,part,isnull(slconlai,0) as slconlai ,isnull(slconlaitmp,0) as slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlaitmp > 0 and slconlai >0 and right(substring(LOT,0,14),1) = '" + In_Gear.ToString() + "' order by lot";
                }
                else
                {
                    sqlTMP = "select lot,part,isnull(slconlai,0) as slconlai ,isnull(slconlaitmp,0) as slconlaitmp  from stocktp where part = '" + PartNo + "' and slconlai > 0  order by lot";
                }    
                TONKHOTHEOMATMP = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sqlTMP);
                // Duyệt Hàng theo TMP tồn kho .
                //if(PartNo = "")
                Boolean canghep = false;
                for (int i = 0; i < TONKHOTHEOMATMP.Rows.Count; i++)
                {
                    LOTDUYET = TONKHOTHEOMATMP.Rows[i].Field<string>("lot");
                    if (LOTDUYET.Length > 13)
                        LOTDUYET1 = LOTDUYET.Substring(0, 13);
                    else
                        LOTDUYET1 = LOTDUYET;
                    TONKHOCOLAITMP = TONKHOTHEOMATMP.Rows[i][3];
                    SLTKCONLAI_LOTDUYET = Convert.ToInt32(TONKHOCOLAITMP);
                    if (Convert.ToInt32(TONKHOCOLAITMP) > 0)
                    {
                        if (SLTKCONLAI_LOTDUYET < SLGIAO)
                        {
                            slle = SLTKCONLAI_LOTDUYET % TCDG;
                            if (slle == 0 && canghep==false)
                            {

                                CAPNHAPTMP(LOTDUYET, -1);
                                SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;


                            }
                            else
                            {
                                if (canghep == true)
                                {
                                    if (SLCANGHEP > SLTKCONLAI_LOTDUYET)
                                    {
                                        LOTGHEP = LOTGHEP + LOTDUYET1 + "-" + SLTKCONLAI_LOTDUYET + ",";
                                        SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
                                        SLCANGHEP = SLCANGHEP - SLTKCONLAI_LOTDUYET;
                                        canghep = true;
                                        CAPNHAPTMP(LOTDUYET, -1);


                                    }
                                    else
                                    {
                                        LOTGHEP = LOTGHEP  + LOTDUYET1 + "-" + SLCANGHEP;
                                        row = tblGL.NewRow();
                                        row["MH"] = PartNo;
                                        row["GG"] = GioGiao;
                                        row["LG"] = LOTGHEP;
                                        row["Gear"] = Gear;
                                        tblGL.Rows.Add(row);
                                        SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
                                        SLCANGHEP = 0;
                                        canghep = false;
                                        CAPNHAPTMP(LOTDUYET, -1);

                                        //break;
                                    }
                                }
                                else
                                {
                                    LOTGHEP = LOTDUYET1 + "-" + slle + ",";
                                    SLGIAO = SLGIAO - SLTKCONLAI_LOTDUYET;
                                    SLCANGHEP = TCDG - slle;
                                    canghep = true;
                                    CAPNHAPTMP(LOTDUYET, -1);
                                }

                            }
                        }
                        else
                        {

                            if (canghep == true)
                            {
                                LOTGHEP = LOTGHEP + LOTDUYET1 + "-" + SLCANGHEP;

                                //for (int i = 0; i < 10; i++)
                                //{
                                row = tblGL.NewRow();
                                row["MH"] = PartNo;
                                row["GG"] = GioGiao;
                                row["LG"] = LOTGHEP;
                                row["Gear"] = Gear;
                                tblGL.Rows.Add(row);
                                //}
                                CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET - SLGIAO);
                                canghep = false;
                                SLCANGHEP = 0;
                                break;
                            }
                            else
                            {

                                CAPNHAPTMP(LOTDUYET, SLTKCONLAI_LOTDUYET - SLGIAO);
                                canghep = false;
                                SLCANGHEP = 0;
                                break;
                            }
                        }

                    }

                }
            }
            else
            {
                //MessageBox.Show("Không có tồn kho của mã hàng : " + PartNo, "THÔNG BÁO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        
        

            
        }
        private void CAPNHAPTMP(string LOT, int SLCONLAITMP)
        {
            string SQL = "update stocktp set slconlaitmp = " + SLCONLAITMP + " where lot like '" + LOT + "' and part in (select ItemCode from B20ItemQuyCach where CustomerCode = '0100002') and slconlai >0 ";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, SQL);
        }
        #endregion
        #region Hàng Thiếu
        private void DuyetTTHangThieu()
        {
            string Mahang;
            int TT_GIAO;
            int TONGTON_THEOMA;
            int GIOGIO;
            int GIOTHIEU = 9999;
            int SLGIAO = 0;

            string sql = "select CUSTOMER_PART_NO,sum(BUY_QTY_DUE)  " +



                       "from CUSTOMER_ORDER_JOIN  where CUSTOMER_NO = '100002' and " +
                       " (OBJSTATE = (select CUSTOMER_ORDER_LINE_API.FINITE_STATE_ENCODE__('Released') from dual)) and to_char(WANTED_DELIVERY_DATE, 'ddmmyyyy' ) = '" + N_XH + "'" +
                       " Group by CUSTOMER_PART_NO " +
                       " Order by CUSTOMER_PART_NO ";
            DataTable Tb_HT = new DataTable();
            Tb_HT = IFS.ExecuteQuery(sql);
            for (int i = 0; i < Tb_HT.Rows.Count; i++)
            {
                Mahang = Tb_HT.Rows[i].Field<string>("CUSTOMER_PART_NO");
                object TLg = Tb_HT.Rows[i][1];
                TT_GIAO = Convert.ToInt32(TLg);
                // Lấy tổng tồn kho theo mã 
                sql = "select isnull(sum(slconlai),0) as TT_TK from stocktp where part = '" + Mahang + "' and slconlai > 0 ";
                TONGTON_THEOMA = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
                // Tìm giờ thiếu hàng .
  
            
                   
                
                
                    if (TT_GIAO > TONGTON_THEOMA)
                    {
                        
                        ADDLISTHT(Mahang, TT_GIAO, TONGTON_THEOMA, TONGTON_THEOMA - TT_GIAO, GIOTHIEU);
               
                      

                    }
                    
                }

            
        }
        private void ADDLISTHT(string patno, int slgiao, int sltonkho, int slthieu, int giothieu)
        {
            listHANGTHIEU.View = View.Details;
            listHANGTHIEU.GridLines = true;



            ListViewItem item1 = new ListViewItem(patno);
            item1.SubItems.Add(slgiao.ToString());
            item1.SubItems.Add(sltonkho.ToString());
            item1.SubItems.Add(slthieu.ToString());
            item1.SubItems.Add(giothieu.ToString());
            listHANGTHIEU.Items.Add(item1);
        }
        #endregion
        #region In Ghep Lot
        private void CMD_INGHEPLOT_Click(object sender, EventArgs e)
        {
            CHONYMVN = 1;
            string LOT, MA, GIO,GearName, sqlinsert;
            sqlinsert = "delete from TMPLOTGHEPTEST";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlinsert);
            foreach (int i in GridVTTGL.GetSelectedRows())
            {
                DataRow row = GridVTTGL.GetDataRow(i);
                MA = row[0].ToString();
                LOT = row[2].ToString();
                //DateTime dt = DateTime.ParseExact(row[1].ToString(),
                //                  "dd/MM/yyyy HH:mm:ss",
                //                  CultureInfo.InvariantCulture);
                GIO = row[1].ToString();
                GearName = row[3].ToString();
                sqlinsert = "insert into TMPLOTGHEPTEST (LOT,MAHANG,GIOXUAT,GearName,flag) values ( '" + LOT + "','" + MA + "','" + GIO + "','" + GearName + "',0 )";
                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlinsert);
            }
            DataSet GHEPLOT = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_gheplotPrintYM1");
           // gridCTTGL.DataSource = GHEPLOT.Tables[0];

            Report.GHEPLOT_YMVN report = new Report.GHEPLOT_YMVN();
            report.DataSource = GHEPLOT.Tables[0];

            report.PaperKind = (DevExpress.Drawing.Printing.DXPaperKind)PaperKind.A4;
            ReportPrintTool tool = new ReportPrintTool(report);

            // ReportPrintTool printTool = new ReportPrintTool(report);
            //tool.PreviewForm.FormClosed += new FormClosedEventHandler(PreviewForm_FormClosed);
            //tool.ShowPreview();
            //return;

            //ReportPrintTool printTool = new ReportPrintTool(report);
            tool.ShowPreviewDialog();
        }
        
        private DataTable loadDATArt()
        {

            DataSet DTS = new DataSet();
            DTS = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_BarCodeView_Ghep_YMVN");
            return DTS.Tables[0];



        }
        #endregion
        #region Doc QRcode
        private Boolean KTPHIEU_CHOPHEP_DOC()
        {
            Boolean KQ = false;
            int demST = 0;
            string Status,LOt= "";
            if (gridVDONHANG.RowCount > 0)
            {
                for (int i = 0; i < gridVDONHANG.RowCount; i++)
                {
                    LOt = gridVDONHANG.GetRowCellValue(i, "LOT").ToString();
                    Status = gridVDONHANG.GetRowCellValue(i, "STATUS").ToString();
                    if (LOt  == "")
                    {
                        KQ = true;
                        break;
                    }
                    else
                    {
                        if(Status== "OK")
                        {
                            demST = demST + 1;
                        }    

                        KQ = false;
                    }
                    if(demST == gridVDONHANG.RowCount)
                    {
                        string sql = "delete from YMVN_DOCQRCODE";
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    }    
                }
            }
            else
            {
                KQ = false;
            }
            return KQ;
        }
        #region Update OFF
        private void gridVDONHANG_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            if (!(gridVDONHANG.PostEditor() && gridVDONHANG.UpdateCurrentRow())) return;
            
                switch (e.Column.AbsoluteIndex)
                {
                    case 7: //STTID  
                        string TB_Name, LOT, delete;
                        if (YMVN_CHONGIAO.MP_SP == "MP")
                        {
                            TB_Name = "YMVN_TMPPHIEUGIAOHANG";
                            LOT = "LOT";
                            delete = "delete " + TB_Name;
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, delete);
                            loadG_SQL("MP");
                        }
                        else
                        {
                            TB_Name = "SP_TMPPHIEUGIAOHANG";
                            LOT = "LOTNO";
                            delete = "delete " + TB_Name;
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, delete);
                            loadG_SQL("SP");
                        }

                        int STT = e.RowHandle + 1; //starts from zero  
                        string sql = "update " + TB_Name + " set " + LOT + " = '" + e.Value.ToString() + "' where STT = " + STT;
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        break;
                }
            
        }
        public List<object> GX = new List<object>();
        private void loadG_SQL(string MP_SP)
        {
            
            string NXH;
            for (int i = 0; i < gridVDONHANG.RowCount; i++)
            {

                NXH = dateNG.DateTime.ToString("yyyy-MM-dd");
                string MaHang = gridVDONHANG.GetRowCellValue(i, "CUSTOMER_PART_NO").ToString();
                string GGV = gridVDONHANG.GetRowCellDisplayText(i, "WANTED_DELIVERY_DATE").ToString();
                for (int j = 0; j < CheckGX.Items.Count; j++)
                {
                    if (CheckGX.GetItemCheckState(j) == CheckState.Checked)
                    {
                        
                        object IT = CheckGX.Items[j];
                        GX.Add(IT);
                        // For every other item in the list, set as checked.
                        string G = IT.ToString();
                        if (G.Contains(GGV) == true)
                        {
                            DateTime GIO_XUAT_LIST = DateTime.Parse(gridVDONHANG.GetRowCellValue(i, "WANTED_DELIVERY_DATE").ToString());
                            //DateTime.ParseExact(
                            string GTXH = GIO_XUAT_LIST.ToLongDateString();// .ToShortDateString();//  .ToShortTimeString();
                            NXH = NXH + " " + GIO_XUAT_LIST.ToString("HH:mm:ss");
                            //, "MM/dd/yyyy hh:mm:ss tt", provider)
                            string GXH = GIO_XUAT_LIST.ToString("HH:mm");
                            int SLXUAT = int.Parse(gridVDONHANG.GetRowCellValue(i, "BUY_QTY_DUE").ToString());
                            string CUA = gridVDONHANG.GetRowCellValue(i, "PO").ToString();
                            string TRUYEN = gridVDONHANG.GetRowCellValue(i, "DOCK_CODE").ToString();
                            string TENHANG = gridVDONHANG.GetRowCellValue(i, "CATALOG_DESC").ToString();
                            string Gear = gridVDONHANG.GetRowCellValue(i, "Gear").ToString();
                            string LOT;
                            if (gridVDONHANG.GetRowCellValue(i, "LOT").ToString() != null)
                            {
                                LOT = gridVDONHANG.GetRowCellValue(i, "LOT").ToString();
                            }
                            else LOT = "";
                            string DV = gridVDONHANG.GetRowCellValue(i, "DV").ToString();
                            string STT = gridVDONHANG.GetRowCellValue(i, "STT").ToString();
                            string Status = gridVDONHANG.GetRowCellValue(i, "STATUS").ToString();

                            if (Status != "OK")
                            {
                                if (MP_SP == "MP")
                                {
                                    string insert_YMVN_TMPPHIEUGIAOHANG = "insert into YMVN_TMPPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,TTPHIEU,GIOGIAO,ADDNM,STATUS) " +
                                  "VALUES " +
                                  "('" + STT + "','" + CUA + "','" + TRUYEN + "','" + MaHang + "','" + TENHANG + "','" + LOT + "','" + DV + "'," + SLXUAT + ",'" + NXH + "','" + Gear + "','" + GXH + "','0','" + Status + "')";
                                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, insert_YMVN_TMPPHIEUGIAOHANG);
                                }
                                else
                                {
                                    string insert_tmpphieugiaohang = "insert into SP_tmpphieugiaohang (STT, MAHANG, TENHANG, CUA, SLGIAO, LOTNO, PO_NO, NGAYGIAO, GIOGIAO, NHAMAY, STATUSFCC, STATUSYMVN, STATUS) " +
                                     " VALUES" +
                                     "('" + STT + "','" + MaHang + "','" + TENHANG + "','" + TRUYEN + "'," + SLXUAT + ",'" + LOT + "','" + CUA + "','" + NXH + "','" + GXH + "','YAMAHA','NG','NG','NG')";
                                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, insert_tmpphieugiaohang);
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion

        private void CMD_DOCQRCODE_Click(object sender, EventArgs e)
        {
            
            if (KTPHIEU_CHOPHEP_DOC() == true)
            {
                CultureInfo provider = CultureInfo.InvariantCulture;
                int KT = 0;
                duyetlit();
                if (GioXuat != "")
                {
                    if(YMVN_CHONGIAO.MP_SP == "MP")
                    { 
                    
                        if (TONTAI() == 0)
                        {
                            string delete = "delete YMVN_TMPPHIEUGIAOHANG";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, delete);

                            if (KTTTGH() == false)
                            {
                                #region TMP
                                //for (int i = 0; i < gridVDONHANG.RowCount; i++)
                                //{

                                //    NXH = dateNG.DateTime.ToString("yyyy-MM-dd");
                                //    string MaHang = gridVDONHANG.GetRowCellValue(i, "CUSTOMER_PART_NO").ToString();
                                //    string GGV = gridVDONHANG.GetRowCellDisplayText(i, "WANTED_DELIVERY_DATE").ToString();
                                //    for (int j = 0; j < CheckGX.Items.Count; j++)
                                //    {
                                //        if (CheckGX.GetItemCheckState(j) == CheckState.Checked)
                                //        {

                                //            object IT = CheckGX.Items[j];
                                //            // For every other item in the list, set as checked.
                                //            string G = IT.ToString();
                                //            if (G.Contains(GGV) == true)
                                //            {
                                //                DateTime GIO_XUAT_LIST = DateTime.Parse(gridVDONHANG.GetRowCellValue(i, "WANTED_DELIVERY_DATE").ToString());
                                //                //DateTime.ParseExact(
                                //                string GTXH = GIO_XUAT_LIST.ToLongDateString();// .ToShortDateString();//  .ToShortTimeString();
                                //                NXH = NXH + " " + GIO_XUAT_LIST.ToString("HH:mm:ss");
                                //                //, "MM/dd/yyyy hh:mm:ss tt", provider)
                                //                string GXH = GIO_XUAT_LIST.ToString("HH:mm");
                                //                int SLXUAT = int.Parse(gridVDONHANG.GetRowCellValue(i, "BUY_QTY_DUE").ToString());
                                //                string CUA = gridVDONHANG.GetRowCellValue(i, "PO").ToString();
                                //                string TRUYEN = gridVDONHANG.GetRowCellValue(i, "DOCK_CODE").ToString();
                                //                string TENHANG = gridVDONHANG.GetRowCellValue(i, "CATALOG_DESC").ToString();
                                //                string Gear = gridVDONHANG.GetRowCellValue(i, "Gear").ToString();
                                //                string LOT;
                                //                if (gridVDONHANG.GetRowCellValue(i, "LOT").ToString() != null)
                                //                {
                                //                    LOT = gridVDONHANG.GetRowCellValue(i, "LOT").ToString();
                                //                }
                                //                else LOT = "";
                                //                string DV = gridVDONHANG.GetRowCellValue(i, "DV").ToString();
                                //                string STT = gridVDONHANG.GetRowCellValue(i, "STT").ToString();
                                //                string Status = gridVDONHANG.GetRowCellValue(i, "STATUS").ToString();

                                //                if (Status != "OK")
                                //                {
                                //                    string insert_YMVN_TMPPHIEUGIAOHANG = "insert into YMVN_TMPPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,TTPHIEU,GIOGIAO,ADDNM,STATUS) " +
                                //                  "VALUES " +
                                //                  "('" + STT + "','" + CUA + "','" + TRUYEN + "','" + MaHang + "','" + TENHANG + "','" + LOT + "','" + DV + "'," + SLXUAT + ",'" + NXH + "','" + Gear + "','" + GXH + "','0','" + Status + "')";
                                //                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, insert_YMVN_TMPPHIEUGIAOHANG);
                                //                }
                                //            }
                                //        }
                                //    }
                                //}
                                #endregion
                                //this.Close();
                                loadG_SQL("MP");
                                KT = 1;

                            }
                            else
                            {
                                MessageBox.Show("Không thể DocQRCODE , Kiểm tra lại phiếu ! ", "Thông Báo!", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                            }
                        }
                        else
                        {
                            KT = 1;
                        }
                    }
                    else
                    {

                        //this.Close();
                        string NXH;
                        if (TONTAI() == 0)
                        {
                            string delete = "delete sp_tmpphieugiaohang";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, delete);

                            if (KTTTGHSP() == false)
                            {
                                #region
                                //for (int i = 0; i < gridVDONHANG.RowCount; i++)
                                //{
                                //    NXH = dateNG.DateTime.ToString("yyyy-MM-dd");
                                //    string MaHang = gridVDONHANG.GetRowCellValue(i, "CUSTOMER_PART_NO").ToString();

                                //    DateTime GIO_XUAT_LIST = DateTime.Parse(gridVDONHANG.GetRowCellValue(i, "WANTED_DELIVERY_DATE").ToString());
                                //    //DateTime.ParseExact(
                                //    string GTXH = GIO_XUAT_LIST.ToLongDateString();// .ToShortDateString();//  .ToShortTimeString();
                                //    NXH = NXH + " " + GIO_XUAT_LIST.ToString("HH:mm:ss");
                                //    //, "MM/dd/yyyy hh:mm:ss tt", provider)
                                //    string GXH = GIO_XUAT_LIST.ToString("HH:mm");
                                //    int SLXUAT = int.Parse(gridVDONHANG.GetRowCellValue(i, "BUY_QTY_DUE").ToString());
                                //    string CUA = gridVDONHANG.GetRowCellValue(i, "PO").ToString();
                                //    string TRUYEN = gridVDONHANG.GetRowCellValue(i, "DOCK_CODE").ToString();
                                //    string TENHANG = gridVDONHANG.GetRowCellValue(i, "CATALOG_DESC").ToString();
                                //    object Ge = gridVDONHANG.GetRowCellValue(i, "Gear");
                                //    string Gear="";
                                //    if (Ge != null)
                                //    {
                                //        Gear = gridVDONHANG.GetRowCellValue(i, "Gear").ToString();
                                //    }
                                //    string LOT;
                                //    if (gridVDONHANG.GetRowCellValue(i, "LOT").ToString() != null)
                                //    {
                                //        LOT = gridVDONHANG.GetRowCellValue(i, "LOT").ToString();
                                //    }
                                //    else LOT = "";
                                //    string DV = gridVDONHANG.GetRowCellValue(i, "DV").ToString();
                                //    string STT = gridVDONHANG.GetRowCellValue(i, "STT").ToString();
                                //    string Status = gridVDONHANG.GetRowCellValue(i, "STATUS").ToString();

                                //    if (Status != "OK")
                                //    {

                                //    }
                                //}
                                #endregion
                                //this.Close();
                                loadG_SQL("SP");
                                KT = 1;

                            }
                            else
                            {
                                MessageBox.Show("Không thể DocQRCODE , Kiểm tra lại phiếu ! ", "Thông Báo!", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                            }
                        }
                        KT = 2;
                    }
                }
                else { MessageBox.Show("Bạn hãy chọn giờ xuất trước khi đoc QRcode !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                if (KT == 1 )
                {

                    YMVNDOCQRCODE FRM_DOCQRCODE = new YMVNDOCQRCODE();
                    FRM_DOCQRCODE.Show();
                    this.Close();
                }
                if (KT == 2)
                {

                    YAMAHAQRCDE_SP FRM_DOCQRCODE_SP = new YAMAHAQRCDE_SP();
                    FRM_DOCQRCODE_SP.Show();
                    this.Close();
                }
            }
            else
            {
                MessageBox.Show("Không thể đọc QRcode cho sự lựa chọn hiện tại ! Kiểm tra lại  !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private Boolean KTTTGH()
        {
            Boolean KQ;
            string sql = "select lot,status from YMVN_TMPPHIEUGIAOHANG where lot <> ''";
            DataTable SKQ = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);

            if (SKQ.Rows.Count > 1)
            {
                KQ = true;
            }
            else
            {
                KQ = false;
            }
            return KQ;
        }
        #endregion

        private void GIAOHANGYMN_FormClosing(object sender, FormClosingEventArgs e)
        {
            //YMVNDOCQRCODE FRM_DOCQRCODE = new YMVNDOCQRCODE();
            //FRM_DOCQRCODE.Show();
        }

        private void cmd_Update_Stock_Click(object sender, EventArgs e)
        {
            try
            {
                if (YMVN_CHONGIAO.MP_SP == "MP")
                {
                    if (CHOPHEPCN("YMVN_TMPPHIEUGIAOHANG", "LOT") == 1)
                    {
                        string sql = "select count(*) from YMVN_TMPPHIEUGIAOHANG where lot = '' or lot is null";
                        int KQ = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));

                        CAPNHAPK();

                    }
                }
                else
                {

                    if (CHOPHEPCN("SP_tmpphieugiaohang ", "LOTNO") == 1)
                    {

                        CAPNHAPK();

                    }
                }
                //if()
                //LOAD();
            }
            catch(Exception ex)
            {
                MessageBox.Show("Có lỗi " + ex + "sảy ra !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                GIAOHANGYMN YM = new GIAOHANGYMN();
                YM.Show();
            }
        }
     
        private void CAPNHAPK()
        {
            duyetlit();
            UPDQATESTOCK = "";
            
            string sql,sqldocqr,sqlphieugh, TTLOTSL, MAH,CUA,GG, sqlluudocqr;
            int STT, dem = 0;
            DataTable BANGTAM = new DataTable();
            if (YMVN_CHONGIAO.MP_SP == "MP")
            {
                sql = "select STT,LOT,MAHANG,GIOGIAO,CUA,STATUS from YMVN_TMPPHIEUGIAOHANG where LOT <> '' and (STATUS != 'OK' or STATUS is null )  order by STT";
            }
            else
            {
                sql = "select STT,LOTNO,MAHANG,GIOGIAO,PO_NO as CUA,STATUS from SP_TMPPHIEUGIAOHANG where LOTNO <> '' and (STATUS != 'OK' or STATUS is null )  order by STT";
            }
                BANGTAM = sqlBRV.ExecuteQuery(sqlBRV.B7R2_FCCdb, sql);
            if (BANGTAM.Rows.Count == 0)
            {
                MessageBox.Show("Bạn chưa cập nhập được kho vì chưa hoàn thành phiếu !", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                string NGAYGIAO = dateNG.DateTime.ToString("MM/dd/yyyy");
                string GIOGIAO = "";
                string[] GGiao = GioXuat.Split(',');
                for (int i = 0; i < GGiao.Length; i++)
                {
                    if (GIOGIAO == "")
                    {
                        GIOGIAO = GGiao[i].Replace("'", "").Substring(0,2);
                    }
                    else
                    {
                        GIOGIAO = GIOGIAO + "+" + GGiao[i].Replace("'", "").Substring(0, 2);
                    }
                }
                string NHAMAY;
                for (int i = 0; i < BANGTAM.Rows.Count; i++)
                {
                    STT = int.Parse(BANGTAM.Rows[i]["STT"].ToString());
                    NHAMAY = "YAMAHA - VIET NAM";
                    CUA = BANGTAM.Rows[i]["CUA"].ToString();
                    string PO = "";
                    try
                    {
                        CUA = int.Parse(CUA).ToString();
                    }
                    catch
                    {
                        CUA = CUA.ToString();
                    }
                    if (CUA.Length < 5)
                    {
                        if (CUA.Length == 4)
                            PO = "0" + CUA;
                        if (CUA.Length == 3)
                            PO = "00" + CUA;
                        if (CUA.Length == 2)
                            PO = "000" + CUA;
                        if (CUA.Length == 1)
                            PO = "0000" + CUA;
                       

                    }
                    else
                    {
                        PO = CUA;
                    }    
                    MAH = BANGTAM.Rows[i]["MAHANG"].ToString();
                    GG= BANGTAM.Rows[i]["GIOGIAO"].ToString();
                    GG = dateNG.DateTime.ToString("yyyy-MM-dd") + " " + GG + ":00";
                    if (YMVN_CHONGIAO.MP_SP == "MP")
                    {
                        TTLOTSL = BANGTAM.Rows[i]["LOT"].ToString();
                    }
                    else
                    {
                        TTLOTSL = BANGTAM.Rows[i]["LOTNO"].ToString();
                    }

                    #region Cap nhập

                    if (KTLOT_TONKHO(TTLOTSL, STT, MAH) == true)
                    {
                       
                        string[] _TACH = TTLOTSL.Split(',');

                        if (_TACH.Length == 1)
                        {
                            sql = TruTK(TTLOTSL, STT, GG);
                            if (UPDQATESTOCK == "")
                            {
                                UPDQATESTOCK = sql;
                            }
                            else
                            {
                                UPDQATESTOCK = UPDQATESTOCK + ";" + sql;
                            }
                        }
                        else
                        {
                            for (int m = 0; m < _TACH.Length; m++)
                            {
                                sql = TruTK(_TACH[m], STT, GG);

                                if (UPDQATESTOCK == "")
                                {
                                    UPDQATESTOCK = sql; ;
                                }
                                else
                                {
                                    UPDQATESTOCK = UPDQATESTOCK + ";" + sql;
                                }
                            }
                        }

                        /// lưu và xóa đọc QCcode

                        if (YMVN_CHONGIAO.MP_SP == "MP")
                        {
                            sqldocqr = "INSERT INTO LUUDOCQRCODE (LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,SLTEMHVN,STATUS,MAFCC,STT,KETQUA,NGAYXUAT,GIOXUAT,NHAMAY,GIOGIAO)" +
                               " SELECT LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,SLTEMHVN,STATUS,MAFCC,STT,KETQUA ,'" + NGAYGIAO + "','" + GIOGIAO + "','" + NHAMAY + "',LOTHVN" +
                                 " FROM YMVN_DOCQRCODE where MAHANGFCC = '" + MAH + "' and LOTHVN  = '" + PO + "' and KETQUA = 'DG' ";
                        }
                        else
                        {
                            sqldocqr = "INSERT INTO LUUDOCQRCODE (STT,LOTFCC,MAHANGFCC,SLTEMFCC,LOTHVN,MAHANGHVN,KETQUA,NGAYXUAT,GIOXUAT,NHAMAY)" +
                               " SELECT STT,LOTNO,MAHANG,SLTEM,PONO,Gear ,'OK','" + NGAYGIAO + "','" + GIOGIAO + "','" + NHAMAY + "' " +
                                 " FROM SP_DOCQRCODE where PONO  = '" + PO + "' and KETQUA = 'DG'";
                        }

                        if (sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldocqr) != -1)
                        {
                            if (YMVN_CHONGIAO.MP_SP == "MP")
                            {
                                sqlluudocqr = "delete from YMVN_DOCQRCODE where  MAHANGFCC = '" + MAH + "' and LOTHVN  = '" + PO + "' and KETQUA = 'DG'";
                            }
                            else
                            {
                                sqlluudocqr = "delete from SP_DOCQRCODE where PONO = '" + PO + "' and KETQUA = 'DG'";
                            }
                           // sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldocqr);
                        }
                        else
                        {
                            MessageBox.Show("Có lỗi xảy ra không lưu được đọc Qrcode ! Không thể cập nhập Kho ", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        /// lưu và xóa phiếu
                            if (YMVN_CHONGIAO.MP_SP == "MP")
                            {
                                sql = "INSERT INTO LUUPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,GearYMVN,NHAMAY,GIOGIAOFCC)" +
                              " SELECT STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,'OK' as STATUS,TTPHIEU,'" + NHAMAY + "', CONVERT(VARCHAR(8), GETDATE(), 108)" +
                                " FROM YMVN_TMPPHIEUGIAOHANG where STT = " + STT + " and STATUS = 'NG'";
                            }
                            else
                            {
                                sql = "INSERT INTO LUUPHIEUGIAOHANG (STT,CUA,TRUYEN,MAHANG,TENHANG,LOT,DV,SOLUONG,NGAYGIAO,GIOGIAO,STATUS,GearYMVN,NHAMAY,GIOGIAOFCC)" +
                                  " SELECT STT,PO_NO,CUA,MAHANG,TENHANG,LOTNO,'pcs' as DV,SLGIAO,NGAYGIAO,GIOGIAO,'OK' as STATUS,'','" + NHAMAY + "', CONVERT(VARCHAR(8), GETDATE(), 108) " +
                                    " FROM SP_TMPPHIEUGIAOHANG where STT = " + STT + " and STATUS = 'NG'";
                            }

                        
                        if (sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql) != -1)
                        {
                            #region
                            dem = dem + 1;
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqlluudocqr);
                            #endregion
                        }
                        else
                        {
                            MessageBox.Show("Có lỗi xảy ra luu phiếu giao ! Không thể cập nhập Kho ", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                    else
                    {  /// Kiem tra lai cap nhap kho
                        //MessageBox.Show("Kho cập nhập không thành công ! do thiếu tồn kho .");
                        PGH.frm_err_cnk frm_Err = new frm_err_cnk(eRR_CNKs);
                        frm_Err.ShowDialog();
                        eRR_CNKs.Clear();
                    }    

                }
                #endregion
                if (dem > 0)
                {
                    string upd = "";
                    string[] SQLUPDATESTOCK;
                    SQLUPDATESTOCK = UPDQATESTOCK.Split(';');
                    try
                    {
                        for (int i = 0; i < SQLUPDATESTOCK.Length; i++)
                        {
                            upd = SQLUPDATESTOCK[i];
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, upd);
                           
                        }
                        for (int i = 0;i < sttup.Count;i++)
                        {
                            if (YMVN_CHONGIAO.MP_SP == "MP")
                            {

                                sql = "update YMVN_TMPPHIEUGIAOHANG set status = 'OK' where STT= " + sttup[i] + "";
                            }
                            else
                            {

                                sql = "update SP_TMPPHIEUGIAOHANG set status = 'OK' where STT= " + sttup[i] + "";
                            }
                            gridVDONHANG.SetRowCellValue(sttup[i] - 1, gridVDONHANG.Columns["STATUS"], "OK");
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }    
                    }
                    catch (Exception E)
                    {

                        MessageBox.Show(E.Message);
                    }
                   

                    UPDQATESTOCK = "";
                    MessageBox.Show("Đã cập nhập : " + dem + " Mã của tổng số " + BANGTAM.Rows.Count + " Mã ", " Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    LOAD();
                    if (YMVN_CHONGIAO.MP_SP == "MP")
                    {
                        sqldocqr = "select count(*) from YMVN_DOCQRCODE ";
                        string KQBangQrcode = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sqldocqr);
                        if (int.Parse(KQBangQrcode) > 0)
                        {
                            DialogResult rs = MessageBox.Show("Dữ liệu đọc Qrcode vẫn còn ! bạn có muốn bỏ qua ?", "Thông Báo !", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            if (rs == DialogResult.Yes)
                            {
                                sqldocqr = "delete from YMVN_DOCQRCODE ";
                                sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sqldocqr);
                            }
                        }
                    }
                    else
                    {
                        //////////// Kiểm tra lại thông tin ////////////////////
                        MessageBox.Show("Không phải MP");
                    }
                }
                else
                {
                    MessageBox.Show("Không thể cập nhập Kho ", "Thông Báo !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }    
            }
        }
        public string UPDQATESTOCK;
        private Boolean KTTT()
        {
            Boolean KQ= false;
            string sql1= "select count (*) from YMVN_TMPPHIEUGIAOHANG where STATUS <> 'OK'",KQSQL;
            KQSQL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql1);
            if (KQSQL == "0")
                KQ = true;
            else
                KQ = false;
            return KQ;
        }
        List <int> sttup = new List<int>(); 
        private string TruTK(string LOTSL,int STT,string GG)
        {
           string sql;
            
           string[] _TACH = LOTSL.Split('-');
           string  LOT = _TACH[0];
           int  SL = int.Parse(_TACH[1]);
            if (YMVN_CHONGIAO.MP_SP == "MP")
            {
                if(sttup.Contains(STT)== false)
                sttup.Add(STT);
            }
            else
            {

                sttup.Add(STT);
            }
            //LOT = LOT.Substring(0, LOT.Length );
            if (LOT.Length >= 13) 
              LOT=  LOT.Substring(0, 13); 
            else LOT = LOT;
            sql = "update t set t.ngayxuat  = '"+ GG +"' ,t.slxuat = slxuat +" + SL + ",t.slconlai = slconlai - " + SL + " from "+
                   " ( select top 1 * from STOCKTP where LOT = '" + LOT + "' or substring(LOT,1,13) = '"+ LOT + "') t";
            
            return sql;

        }
     
        private Boolean KTLOT_TONKHO(string LOTSL, int STT, string MH)
        {
            Boolean KQ;
           
            string NGAYGIAO = dateNG.DateTime.ToString("MM/dd/yyyy");
            string GIOGIAO = "";
            string[] GGiao = GioXuat.Split(',');
            for (int i = 0; i < GGiao.Length; i++)
            {
                if (GIOGIAO == "")
                {
                    GIOGIAO = GGiao[i].Replace("'", "").Substring(0, 2);
                }
                else
                {
                    GIOGIAO = GIOGIAO + "+" + GGiao[i].Replace("'", "").Substring(0, 2);
                }
            }
            int SL, dem = 0;
            string LOT, sql;
            string[] _LOT, _TACH;
            _LOT = LOTSL.Split(',');
            if (_LOT.Length == 1)
            {
                if (KIEMTRA(LOTSL, STT, MH) == true)
                {
                    _TACH = LOTSL.Split('-');
                    LOT = _TACH[0];
                    SL = int.Parse(_TACH[1]);
                    KQ = true;
                   
                }
                else
                {
                   
                    KQ = false;
                }
            }
            else
            {
                for (int i = 0; i < _LOT.Length; i++)
                {
                    if (KIEMTRA(_LOT[i], STT, MH) == true)
                    {
                        dem = dem + 1;
                    }
                    else
                    {
                        
                        KQ = false;
                    }
                }
                if (dem == _LOT.Length)
                {
                    KQ = true;

                  
                }
                else
                {
                    KQ = false;
                }
            }
            return KQ;
        }
        private Boolean KIEMTRA(string LOTSL, int STT, string MH)
        {
            Boolean KQ;
            int SLCONLAI;
            LOTSL = LOTSL.Trim();
            string[] _LOTSL;
            _LOTSL = LOTSL.Split('-');
            string _SL, LOT = _LOTSL[0],LOT1= LOT.Substring(0, LOT.Length - 1);
            
            int SL = int.Parse(_LOTSL[1]);
            if (LOT.Length >= 13)
                LOT = LOT.Substring(0, 13);

            else LOT = LOT;
            string sql = "select slconlai from STOCKTP where  substring(LOT,1,13) = '"+ LOT + "' "; 
                //" like '" + LOT.Substring(0,(LOT.Length -1)) + "%' or  
            _SL = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
            if (_SL == "")
            {
                MessageBox.Show("Không tồn tại Lot :  \n Số TT Phiếu : " + STT + "\n MÃ hàng : " + MH + " \n LOT :" + LOT + "", "Thông Báo !   Không tồn tại hoặc số lượng không đủ !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            else
            {
                SLCONLAI = int.Parse(sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql));
            }
            if (SL > SLCONLAI)
            {
                KQ = false;
                eRR_CNKs.Add(new DS_ERR_CNK
                {
                    MH = MH,
                    LOT = LOT.ToString(),
                    SLC = SL,
                    SLTK = SLCONLAI,
                    SLT = SL - 0,
                    Ms = "Không đủ Tồn Kho"
                }) ;
                //MessageBox.Show("Kiểm tra lại : \n Số TT Phiếu : " + STT + " \n MÃ hàng : " + MH + " \n LOT :" + LOT + " \n Số lượng yêu cầu xuất :" + SL + " \n Số lượng còn lại : " + SLCONLAI + "", "Thông Báo ! - không đủ xuất !", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //LOT = "'" + LOT + "'";
                //TONKHOTP TK = new TONKHOTP(LOT);
                //TK.ShowDialog();
            }
            else
            {
                KQ = true;

            }
            return KQ;
        }

        private void listVKGX_Click(object sender, EventArgs ve)
        {
            //c_L = true;
            //Da_DUYET = false;
            //listVKGX.ItemChecked += (s, e) =>
            //{
            //    if(Da_DUYET == false)
            //    LOAD();

            //};
            
        }

        private void listVKGX_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void CheckGX_SelectedIndexChanged(object sender, EventArgs e)
        {
            //LOAD();
            

            gridVDONHANG.RefreshData();
            
            //GHEP_LOT(tbl);
        }
        public static int CHONYMVN = 0;
        private void CMD_INPHIEUGIAO_Click(object sender, EventArgs e)
        {
            LOAD_PHIEU_YMN();
            DataTable tbl_RP = new DataTable();
            tbl_RP = tbl.Copy();
            string ODN_N = "";
            DataRow row;
            Report.rpPhieuGiaoHangYAM report = new Report.rpPhieuGiaoHangYAM();
            for (int i = 0; i< tbl_RP.Rows.Count;i++)
            {

                object ODN = tbl_RP.Rows[i].Field<string>("PO");
                object Status = tbl_RP.Rows[i].Field<string>("STATUS");
                if (ODN.ToString().Length< 6)
                {
                    ODN_N = "#";
                    for (int j = 1;j < 6 - ODN.ToString().Length;j++)
                    {
                        ODN_N = ODN_N + "0";
                    }
                    ODN_N = ODN_N + ODN.ToString();
                    tbl_RP.Rows[i]["PO"] = ODN_N;
                }
                if(Status.ToString() == "OK")
                {
                    tbl_RP.Rows[i]["LOT"] = "OK Check";
                }
                else
                {
                    tbl_RP.Rows[i]["LOT"] = "Not Check";
                }
                
            }
            report.DataSource = tbl_RP;
            ReportPrintTool tool = new ReportPrintTool(report);
           // ReportPrintTool printTool = new ReportPrintTool(report);
            tool.PreviewForm.FormClosed += new FormClosedEventHandler(PreviewForm_FormClosed);
            tool.ShowPreview();
            return;
            //printTool.ShowPreviewDialog();
        }
        void PreviewForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            CHONYMVN = 0;
        }
        private void cmd_UploadMilkrunSP_Click(object sender, EventArgs e)
        {
            if (KTSP() == true)
            {
                UploadMIKR();
                N_XH = dateNG.DateTime.ToString("ddMMyyyy");
                c_L = false;
                DUYET = false;
                Da_DUYET = true;
                if (khoitao == 0)
                {
                    LOAD();
                }
            }
            else 
            {
                MessageBox.Show("Đơn hàng không phải là SP ! vui lòng kiểm tra lại .", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Kieemr tra SP
        private Boolean KTSP ()
        {
            Boolean KQ = false;
            string DOCK_CODE;
            for (int i = 0; i < gridVDONHANG.RowCount; i++)
            {
                DOCK_CODE = gridVDONHANG.GetRowCellValue(i, "DOCK_CODE").ToString();
                if (DOCK_CODE.Contains("VSP") == true)
                {
                    KQ = true;
                    break;
                }
            }
            return KQ;
        }
        // Upload Mirulk SP
        private void UploadMIKR()
        {
            string DOCK_CODE,POSP_NO,PSP_NO, PSP_NAME;
            string sql;
            string SP_Ngay_Giao = dateNG.DateTime.ToString("yyyy-MM-dd 08:00:00");
            int  SLSP_GIAO;
            for (int i = 0; i < gridVDONHANG.RowCount; i++)
            {
                DOCK_CODE = gridVDONHANG.GetRowCellValue(i, "DOCK_CODE").ToString();
                POSP_NO = gridVDONHANG.GetRowCellValue(i, "PO").ToString();
                PSP_NO = gridVDONHANG.GetRowCellValue(i, "CUSTOMER_PART_NO").ToString();
                PSP_NAME = gridVDONHANG.GetRowCellValue(i, "CATALOG_DESC").ToString();
                SLSP_GIAO = int.Parse(gridVDONHANG.GetRowCellValue(i, "BUY_QTY_DUE").ToString());

                if(DOCK_CODE.Contains("VSP") == true)
                {
                    sql = "select count(*) from Purchase_Order_YMVN where Oder_no = '" + POSP_NO + "' and Part_no = '" + PSP_NO + "' ";
                    string DEM = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb, sql);
                    if (DEM == "0")
                    {
                        sql = "insert into Purchase_Order_YMVN (Oder_no, Part_no, Part_name, NgayGiao, Slgiao,QCDG,CUA) values ( '" + POSP_NO + "','" + PSP_NO + "','" + PSP_NAME + "','" + SP_Ngay_Giao + "'," + SLSP_GIAO + ",0,'VSP1')";
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                    }
                    else
                    {
                        DialogResult rs = MessageBox.Show("Đã tồn tại Milkrun Mã hàng : " + PSP_NO  + " Số PO : " + POSP_NO + "Bạn có muốn Update thông tin ? ", "Thông Báo", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                        if(rs == DialogResult.Yes)
                        {
                            sql = "delete Purchase_Order_YMVN  where Oder_no = '" + POSP_NO + "' and Part_no = '" + PSP_NO + "' ";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                            sql = "insert into Purchase_Order_YMVN (Oder_no, Part_no, Part_name, NgayGiao, Slgiao,QCDG,CUA) values ( '" + POSP_NO + "','" + PSP_NO + "','" + PSP_NAME + "','" + SP_Ngay_Giao + "'," + SLSP_GIAO + ",0,'VSP1')";
                            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, sql);
                        }    
                        else
                        {
                            if (rs == DialogResult.Cancel)
                                break;
                        }    
                    }    


                }
            }
            MessageBox.Show("Done !!! ", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void gridVDONHANG_RowCellStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowCellStyleEventArgs e)
        {
         
            GridView View = sender as GridView;
            string G = "";
            if (e.RowHandle >= 0)
            {
                string GI = View.GetRowCellDisplayText(e.RowHandle, View.Columns["WANTED_DELIVERY_DATE"]);
                string STS = View.GetRowCellDisplayText(e.RowHandle, View.Columns["STATUS"]);
                for (int j = 0; j < CheckGX.Items.Count; j++)
                {
                    if (CheckGX.GetItemCheckState(j) == CheckState.Checked)
                    {
                        object IT = CheckGX.Items[j];
                        // For every other item in the list, set as checked.
                        G = IT.ToString();

                        if (G.Contains(GI) == true)
                        {
                            
                            e.Appearance.BackColor = Color.Black;
                            e.Appearance.ForeColor = Color.White;
                            e.Appearance.FontStyleDelta = FontStyle.Bold;
                            if (e.Column.FieldName == "STATUS" || e.Column.FieldName == "LOT")
                            {
                                string status = View.GetRowCellDisplayText(e.RowHandle, View.Columns["STATUS"]);

                                if (status == "NG")
                                {
                                    e.Appearance.BackColor = Color.FromArgb(150, Color.Salmon);
                                    e.Appearance.BackColor2 = Color.FromArgb(150, Color.Salmon);
                                    e.Appearance.ForeColor = Color.Yellow;
                                }
                                string LO = View.GetRowCellDisplayText(e.RowHandle, View.Columns["LOT"]);
                                if (LO == "")
                                {
                                    e.Appearance.BackColor = Color.FromArgb(150, Color.Salmon);
                                    e.Appearance.BackColor2 = Color.FromArgb(150, Color.Salmon);
                                    e.Appearance.ForeColor = Color.Yellow;
                                }
                                //e.Appearance.BackColor2 = Color.SeaShell;
                            }
                            
                        }
                    }
                    if(STS == "OK")
                    {
                        e.Appearance.BackColor = Color.DeepSkyBlue;
                        e.Appearance.ForeColor = Color.Yellow;
                        e.Appearance.FontStyleDelta = FontStyle.Bold;
                    }    
                }
            }
        }

        #region Ghep Lot

        #endregion

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            string delete = "delete YMVN_TMPPHIEUGIAOHANG";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, delete);
            loadG_SQL("MP");
            DataSet GHEPLOT = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_Qrcode_gheplotYM2");
            int SLLG = GHEPLOT.Tables[0].Rows.Count ;
            if( SLLG != 0)
            {
                XtraMessageBox.Show("Có : " + SLLG + " Lot cần ghép!");
            }
            else
                XtraMessageBox.Show("Không Có  Lot cần ghép !");
            gridCTTGL.DataSource = GHEPLOT.Tables[0];
        }
    }
}