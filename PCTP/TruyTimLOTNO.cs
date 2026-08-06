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
using PCTP.ClassSQL;
using System.Diagnostics;
using DevExpress.XtraGrid.Drawing;
using DevExpress.XtraPrinting;
using System.Web.Mvc;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraPrintingLinks;
using PCTP.QRCODE_HVN;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using System.Data.SqlClient;

namespace PCTP
{
    public partial class TruyTimLOTNO : DevExpress.XtraEditors.XtraForm
    {
        //clsResize _form_resize;
        private static TruyTimLOTNO _defaultInstance;
        private int Type = 0;
        public static TruyTimLOTNO Instance
        {
            get
            {
                if (_defaultInstance == null)
                {
                    _defaultInstance = new TruyTimLOTNO();
                }
                return _defaultInstance;
            }
            set => _defaultInstance = value;
        }
        public TruyTimLOTNO()
        {

            InitializeComponent();
            //_form_resize = new clsResize(this);
            //this.Load += _Load;
            //this.Resize += _Resize;

        }
        //private void _Load(object sender, EventArgs e)
        //{
        //    _form_resize._get_initial_size();
        //}

        //private void _Resize(object sender, EventArgs e)
        //{
        //    _form_resize._resize();
        //}
        SQLPROVIDER SQLPROVIDER = new SQLPROVIDER();
        public string MHXUAT = "", SQLTTKHO = "select * from STOCKTP where ", SQLTTQRCODE = "select * from luudocqrcode where ";
        public DateTime NgayXuat;
        public string GIOXUAT = "", SQLTONGX = "select LOT,MAHANG, NGAYGIAO,GIOGIAO, GIOGIAOFCC,'' as SLG,NHAMAY,SOLUONG from LUUPHIEUGIAOHANG  where ";
        public static string _ID, LOT_TACH, DSLOTNO;
        public static int SL;
        private void TIMDS(DataTable tb)
        {
            DSLOTNO = "";
            for (int i = 0; i < tb.Rows.Count; i++)
            {
                string[] LTTIMK = tb.Rows[i]["LOT"].ToString().Split(',');
                if (LTTIMK.Length > 1)
                {
                    for (int j = 0; j < LTTIMK.Length; j++)
                    {
                        TACH_SL_LOT(LTTIMK[j]);
                        if (DSLOTNO == "")
                        {
                            DSLOTNO = "'" + LOT_TACH + "'";
                        }
                        else
                        {
                            if (DSLOTNO.Contains(LOT_TACH) == false)
                            {
                                DSLOTNO = DSLOTNO + ",'" + LOT_TACH + "'";
                            }
                        }
                    }
                }
                else
                {
                    TACH_SL_LOT(LTTIMK[0]);
                    if (DSLOTNO == "")
                    {
                        DSLOTNO = "'" + LOT_TACH + "'";
                    }
                    else
                    {
                        if (DSLOTNO.Contains(LOT_TACH) == false)
                        {
                            DSLOTNO = DSLOTNO + ",'" + LOT_TACH + "'";
                        }
                    }
                }
            }

        }
        private void LOADTT()
        {
            int _Tabpage = XtraTabPHUONGTHUCTK.SelectedTabPageIndex;
            string _sqlttk = "", _sqlttqr = "", _sqltttongx = "", _LOT = "", LOTOK;
            string[] _LOTOK, LOTSL;
            DateTime NSX = new DateTime();
            DataTable TIMTRONGLOT = new DataTable();
            DataSet dataSet = new DataSet();
            switch (_Tabpage)
            {
                case 0:
                    DataTable TTLOT;
                    string _LOTNO = txtLOTNO.Text.Trim();
                    if (_LOTNO != "")
                    {


                        _sqlttk = SQLTTKHO + " lot = '" + _LOTNO + "'";
                        _sqlttqr = SQLTTQRCODE + " lotfcc like '%" + _LOTNO + "%'";
                        _sqltttongx = SQLTONGX + " lot like '%" + _LOTNO + "%' order by NGAYGIAO,GIOGIAO";
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqlttk);
                        gridCtrTTKHO.DataSource = TTLOT;
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqlttqr);
                        gridCtrQRCODE.DataSource = TTLOT;
                        for (int i = 0; i < bandedGridView2.RowCount; i++)
                        {
                            LOTOK = bandedGridView2.GetRowCellValue(i, "LOTFCC").ToString();
                            _LOTOK = LOTOK.Split(',');
                            if (_LOTOK.Length > 1)
                            {
                                for (int j = 0; j < _LOTOK.Length; j++)
                                {
                                    if (_LOTOK[j].Contains(_LOTNO) == true)
                                    {
                                        LOTSL = _LOTOK[j].Split('-');
                                        bandedGridView2.SetRowCellValue(i, "SLTEMFCC", LOTSL[1]);
                                    }
                                }
                            }
                        }
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqltttongx);


                        for (int i = 0; i < TTLOT.Rows.Count; i++)
                        {
                            string[] LOTGHEPSL = TTLOT.Rows[i]["LOT"].ToString().Split(',');
                            if (LOTGHEPSL.Length > 1)
                            {
                                for (int j = 0; j < LOTGHEPSL.Length; j++)
                                {
                                    TACH_SL_LOT(LOTGHEPSL[j].ToString());
                                    if (LOT_TACH == _LOTNO)
                                    {
                                        TTLOT.Rows[i]["LOT"] = LOT_TACH;
                                        TTLOT.Rows[i]["SLG"] = SL;
                                    }
                                }
                            }
                            else
                            {
                                TACH_SL_LOT(TTLOT.Rows[i]["LOT"].ToString());
                                TTLOT.Rows[i]["LOT"] = LOT_TACH;
                                TTLOT.Rows[i]["SLG"] = SL;
                            }
                        }
                        gridCtrTTTONGXUAT.DataSource = TTLOT;
                    }
                    else
                        MessageBox.Show("Chưa chọn thông tin để tìm kiếm", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case 1:

                    string MH = lookUpMHSX.Text;
                    string NSX_TEX = "", NSX_STOCK = "";
                    if (MH == "Chọn Mã Hàng Sản Xuất")
                    {
                        MH = null;
                    }
                    if (dateSanXuat.Text != "")
                    {
                        NSX = Convert.ToDateTime(dateSanXuat.Text);
                        NSX_TEX = NSX.ToString("yyMMdd");
                        NSX_STOCK = NSX.ToString("yyyyMMdd");
                    }
                    else
                        NSX = default(DateTime);

                    if (MH == null && NSX == default(DateTime))
                    {
                        MessageBox.Show("Chưa chọn thông tin để tìm kiếm", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        if (MH != null && NSX == default(DateTime))
                        {

                            string sql = "select id from b20item where code = '" + MH + "'";
                            _ID = SQLPROVIDER.ExecuteReader(SQLPROVIDER.B7R2_FCCdb, sql);
                            _sqlttk = SQLTTKHO + " PART = '" + MH + "'";
                            _sqlttqr = SQLTTQRCODE + " MAHANGFCC = '" + MH + "' order by NGAYXUAT,GIOXUAT ";
                            _sqltttongx = SQLTONGX + " MAHANG = '" + MH + "' order by NGAYGIAO,giogiao";

                        }
                        if (MH != null && NSX != default(DateTime))
                        {

                            string sql = "select id from b20item where code = '" + MH + "'";
                            _ID = SQLPROVIDER.ExecuteReader(SQLPROVIDER.B7R2_FCCdb, sql);

                            _sqlttk = SQLTTKHO + " PART = '" + MH + "' and  Convert(CHAR(8),NGAYSX,112) = '" + NSX_STOCK + "'";
                            _sqlttqr = SQLTTQRCODE + " MAHANGFCC = '" + MH + "' and CHARINDEX('" + NSX_TEX + "',' ' + REPLACE(REPLACE(LOTFCC,',',' '),'.',' ') + ' ') <>0 order by NGAYXUAT,GIOXUAT ";
                            _sqltttongx = SQLTONGX + " MAHANG = '" + MH + "' and CHARINDEX('" + NSX_TEX + "',' ' + REPLACE(REPLACE(LOT,',',' '),'.',' ') + ' ') <>0 order by NGAYGIAO,giogiao";

                        }
                        if (MH == null && NSX != default(DateTime))
                        {
                            string TML = "select lot from stocktp where Convert(CHAR(6),NGAYSX,112) = '" + NSX_TEX + "'";

                            _sqlttk = SQLTTKHO + " Convert(CHAR(8),NGAYSX,112) = '" + NSX_STOCK + "' ";
                            _sqlttqr = SQLTTQRCODE + " CHARINDEX('" + NSX_TEX + "',' ' + REPLACE(REPLACE(LOTFCC,',',' '),'.',' ') + ' ') <>0 ";
                            _sqltttongx = SQLTONGX + " CHARINDEX('" + NSX_TEX + "',' ' + REPLACE(REPLACE(LOT,',',' '),'.',' ') + ' ') <>0 order by NGAYGIAO,giogiao";
                        }
                        int TTONGSLXUAT = 0;
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqlttk);
                        gridCtrTTKHO.DataSource = TTLOT;
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqlttqr);
                        for (int i = 0; i < TTLOT.Rows.Count; i++)
                        {
                            string[] LOTGHEPSL = TTLOT.Rows[i]["LOTFCC"].ToString().Split(',');
                            TTONGSLXUAT = 0;
                            if (LOTGHEPSL.Length > 1)
                            {

                                for (int j = 0; j < LOTGHEPSL.Length; j++)
                                {
                                    TACH_SL_LOT(LOTGHEPSL[j].ToString());
                                    if (LOT_TACH.Substring(0, 6) == NTN(NSX))
                                    {

                                        TTONGSLXUAT = TTONGSLXUAT + SL;

                                    }
                                    TTLOT.Rows[i]["SLTEMFCC"] = TTONGSLXUAT;
                                }
                            }
                            else
                            {
                                //TACH_SL_LOT(TTLOT.Rows[i]["LOTFCC"].ToString());

                                //TTLOT.Rows[i]["SLG"] = TTONGSLXUAT + SL;
                            }
                        }
                        gridCtrQRCODE.DataSource = TTLOT;
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqltttongx);

                        for (int i = 0; i < TTLOT.Rows.Count; i++)
                        {
                            string[] LOTGHEPSL = TTLOT.Rows[i]["LOT"].ToString().Split(',');
                            TTONGSLXUAT = 0;
                            if (LOTGHEPSL.Length > 1)
                            {

                                for (int j = 0; j < LOTGHEPSL.Length; j++)
                                {
                                    TACH_SL_LOT(LOTGHEPSL[j].ToString());
                                    if (LOT_TACH.Substring(0, 6) == NTN(NSX))
                                    {

                                        TTONGSLXUAT = TTONGSLXUAT + SL;

                                    }
                                    TTLOT.Rows[i]["SLG"] = TTONGSLXUAT;
                                }
                            }
                            else
                            {
                                TACH_SL_LOT(TTLOT.Rows[i]["LOT"].ToString());

                                TTLOT.Rows[i]["SLG"] = TTONGSLXUAT + SL;
                            }
                        }

                        gridCtrTTTONGXUAT.DataSource = TTLOT;
                    }

                    break;
                case 2:
                    MH = lookUpMHX.Text;
                    DateTime NXH = new DateTime();
                    string KGX = lookUpGXH.Text;
                    DataTable DSLOT = new DataTable();
                    string NX_TEX = "", NX_STOCK = "";
                    if (MH == "Chọn Mã Hàng Xuất")
                    {
                        MH = null;
                    }
                    if (KGX == "Chọn Khung Giờ Xuất")
                    {
                        KGX = null;
                    }
                    if (dateNXH.Text != "")
                    {
                        NXH = Convert.ToDateTime(dateNXH.Text);
                        NX_TEX = NXH.ToString("yyMMdd");
                        NX_STOCK = NXH.ToString("yyyyMMdd");
                    }
                    else
                        NXH = default(DateTime);
                    if (MH == null && NXH == null && KGX == null)
                    {
                        MessageBox.Show("Chưa chọn thông tin để tìm kiếm", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        if (MH != null && NXH == default(DateTime) & KGX == null)
                        {
                            _sqlttk = SQLTTKHO + " PART = '" + MH + "'";
                            _sqlttqr = SQLTTQRCODE + " rtrim(mahangfcc) =  '" + MH.Trim() + "' order by NGAYXUAT,GIOXUAT";
                            _sqltttongx = SQLTONGX + " MAHANG = '" + MH + "' order by NGAYGIAO,GIOGIAO";

                        }
                        if (MH != null && NXH != default(DateTime) && KGX == null)
                        {


                            string sql = "select LOT from LUUPHIEUGIAOHANG  where MAHANG = '" + MH + "' and Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "'";

                            DSLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
                            TIMDS(DSLOT);
                            DSLOTNO = DSLOTNO == "" ? "''" : DSLOTNO;
                            _sqlttk = SQLTTKHO + " lot in (" + DSLOTNO + ")";
                            _sqlttqr = SQLTTQRCODE + "  rtrim(mahangfcc) =  '" + MH.Trim() + "' and Convert(CHAR(8),ngayxuat,112) =  '" + NX_STOCK + "' order by NGAYXUAT,GIOXUAT";
                            _sqltttongx = SQLTONGX + " rtrim(MAHANG) = '" + MH + "' and Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "' order by NGAYGIAO,GIOGIAO";

                        }
                        if (MH == null && NXH != default(DateTime) && KGX == null)
                        {


                            string sql = "select LOT from LUUPHIEUGIAOHANG  where  Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "'";


                            DSLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
                            TIMDS(DSLOT);
                            DSLOTNO = DSLOTNO == "" ? "''" : DSLOTNO;
                            _sqlttk = SQLTTKHO + " lot in (" + DSLOTNO + ")";
                            _sqlttqr = SQLTTQRCODE + "  Convert(CHAR(8),ngayxuat,112) =  '" + NX_STOCK + "' order by NGAYXUAT,GIOXUAT ";
                            _sqltttongx = SQLTONGX + "Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "'  order by NGAYGIAO,GIOGIAO";

                        }
                        if (MH != null && NXH != default(DateTime) && KGX != null)
                        {
                            string sql;
                            sql = "select LOT from LUUPHIEUGIAOHANG  where rtrim(mahang) =  '" + MH.Trim() + "' and Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "' and GIOGIAOFCC = '" + KGX.Trim() + "'";

                            DSLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
                            TIMDS(DSLOT);
                            DSLOTNO = DSLOTNO == "" ? "''" : DSLOTNO;
                            _sqlttk = SQLTTKHO + " lot in (" + DSLOTNO + ")";
                            _sqlttqr = SQLTTQRCODE + " rtrim(mahangfcc) =  '" + MH.Trim() + "' and Convert(CHAR(8),ngayxuat,112) =  '" + NX_STOCK + "' and GioXUAT = '" + KGX.Trim() + "' order by NGAYXUAT,GIOXUAT";
                            _sqltttongx = SQLTONGX + " MAHANG = '" + MH + "' and Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "' and GioGiaoFCC = '" + KGX.Trim() + "' order by NGAYGIAO,GIOGIAO";

                        }
                        if (MH == null && NXH != default(DateTime) && KGX != null)
                        {


                            string sql = "select LOT from LUUPHIEUGIAOHANG  where  Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "' and GioGiaofcc = '" + KGX.Trim() + "'";

                            DSLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);

                            TIMDS(DSLOT);
                            DSLOTNO = DSLOTNO == "" ? "''" : DSLOTNO;
                            _sqlttk = SQLTTKHO + " lot in (" + DSLOTNO + ")";
                            _sqlttqr = SQLTTQRCODE + " Convert(CHAR(8),ngayxuat,112) =  '" + NX_STOCK + "' and GioXUAT = '" + KGX.Trim() + "' order by NGAYXUAT,GIOXUAT";
                            _sqltttongx = SQLTONGX + "  Convert(CHAR(8),NGAYGIAO,112) =  '" + NX_STOCK + "' and GioGiaoFCC = '" + KGX.Trim() + "' order by NGAYGIAO,GIOGIAO";

                        }
                        if (MH != null && NXH == default(DateTime) && KGX != null)
                        {

                            string sql;
                            sql = "select LOT from LUUPHIEUGIAOHANG  where rtrim(mahang) =  '" + MH.Trim() + "' and  GIOGIAOFCC = '" + KGX.Trim() + "' group by LOT";

                            DSLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
                            TIMDS(DSLOT);
                            DSLOTNO = DSLOTNO == "" ? "''" : DSLOTNO;
                            _sqlttk = SQLTTKHO + " lot in (" + DSLOTNO + ")";
                            _sqlttqr = SQLTTQRCODE + "  rtrim(mahangfcc) =  '" + MH.Trim() + "' and GioXUAT = '" + KGX.Trim() + "' order by NGAYXUAT,GIOXUAT ";

                            _sqltttongx = SQLTONGX + " MAHANG = '" + MH + "' and GioGiaoFCC = '" + KGX.Trim() + "' order by NGAYGIAO,GIOGIAO";

                        }
                        if (MH == null && NXH == default(DateTime) && KGX != null)
                        {

                            string sql;
                            sql = "select LOT from LUUPHIEUGIAOHANG  where   GIOGIAOFCC = '" + KGX.Trim() + "' group by LOT";

                            DSLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, sql);
                            TIMDS(DSLOT);
                            DSLOTNO = DSLOTNO == "" ? "''" : DSLOTNO;
                            _sqlttk = SQLTTKHO + " lot in (" + DSLOTNO + ")";
                            _sqlttqr = SQLTTQRCODE + "  GioXUAT = '" + KGX.Trim() + "' order by NGAYXUAT,GIOXUAT ";

                            _sqltttongx = SQLTONGX + "  GioGiaoFCC = '" + KGX.Trim() + "' order by NGAYGIAO,GIOGIAO";

                        }
                        WaitForm2.SO = 1;
                        splashScreenManager2.ShowWaitForm();
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqlttk);
                        gridCtrTTKHO.DataSource = TTLOT;
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqlttqr);
                        gridCtrQRCODE.DataSource = TTLOT;
                        TTLOT = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, _sqltttongx);
                        int TTONGSLXUAT;
                        for (int i = 0; i < TTLOT.Rows.Count; i++)
                        {
                            if (TTLOT.Rows[i]["LOT"].ToString() != "K")
                            {
                                string[] LOTGHEPSL = TTLOT.Rows[i]["LOT"].ToString().Split(',');

                                TTONGSLXUAT = 0;
                                if (LOTGHEPSL.Length > 1)
                                {

                                    for (int j = 0; j < LOTGHEPSL.Length; j++)
                                    {
                                        if (LOTGHEPSL[j].ToString() != "")
                                        {
                                            TACH_SL_LOT(LOTGHEPSL[j].ToString());


                                            TTONGSLXUAT = TTONGSLXUAT + SL;


                                            TTLOT.Rows[i]["SLG"] = TTONGSLXUAT;
                                        }
                                    }
                                }
                                else
                                {
                                    if (TTLOT.Rows[i]["LOT"].ToString() != "")
                                    {
                                        TACH_SL_LOT(TTLOT.Rows[i]["LOT"].ToString());

                                        TTLOT.Rows[i]["SLG"] = TTONGSLXUAT + SL;
                                    }
                                }
                            }
                            else
                            {
                                TTLOT.Rows[i]["SLG"] = TTLOT.Rows[i]["SOLUONG"].ToString();
                            }
                        }
                        gridCtrTTTONGXUAT.DataSource = TTLOT;
                        // Make the group footers always visible.
                        //GridViewTX.OptionsView.GroupFooterShowMode = GroupFooterShowMode.VisibleAlways;
                        //// Create and setup the first summary item.
                        //GridGroupSummaryItem item = new GridGroupSummaryItem();
                        //item.FieldName = "GIOXUATFCC";
                        //item.SummaryType = DevExpress.Data.SummaryItemType.Count;
                        //GridViewTX.GroupSummary.Add(item);
                        //// Create and setup the second summary item.
                        //GridGroupSummaryItem item1 = new GridGroupSummaryItem();
                        //item1.FieldName = "SLG";
                        //item1.SummaryType = DevExpress.Data.SummaryItemType.Sum;
                        //item1.DisplayFormat = "Tổng Giao  {0:0.##}";
                        //item1.ShowInGroupColumnFooter = GridViewTX.Columns["SLG"];
                        //GridViewTX.GroupSummary.Add(item1);
                        splashScreenManager2.CloseWaitForm();
                        //if(bandedGridView1.RowCount== 0)
                        //{
                        //    MessageBox.Show("Không tìm thấy thông tin yêu cầu !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        //}
                    }
                    GridViewTX.OptionsView.GroupFooterShowMode = GroupFooterShowMode.VisibleAlways;
                    // Create and setup the first summary item.
                    GridGroupSummaryItem item = new GridGroupSummaryItem();
                    item.FieldName = "GIOXUATFCC";
                    item.SummaryType = DevExpress.Data.SummaryItemType.Count;
                    GridViewTX.GroupSummary.Add(item);
                    // Create and setup the second summary item.
                    GridGroupSummaryItem item1 = new GridGroupSummaryItem();
                    item1.FieldName = "SOLUONG";
                    item1.SummaryType = DevExpress.Data.SummaryItemType.Sum;
                    item1.DisplayFormat = "Tổng Giao  {0:0.##}";
                    item1.ShowInGroupColumnFooter = GridViewTX.Columns["SOLUONG"];
                    GridViewTX.GroupSummary.Add(item1);
                    break;


            }
            if (bandedGridView1.RowCount == 0)
            {
                MessageBox.Show("Không tìm thấy thông tin yêu cầu !", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        public static void TACH_SL_LOT(string DauVao)
        {

            string[] DV = DauVao.Split('-');
            LOT_TACH = DV[0].ToString();
            SL = DV[1].ToString() != "" ? int.Parse(DV[1].ToString()) : 0;
        }
        #region tách ngày
        public string NTN(DateTime DT)
        {

            string _NTN;
            string str = string.Format("{0:MM/dd/yy}", DT);

            string Y = str.Substring(6, 2);
            string M = str.Substring(0, 2);
            string D = str.Substring(3, 2);

            return _NTN = Y + M + D;
        }
        #region Xử lý Form
        private void txtLOTNO_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {

            }
        }
        #endregion
        private void TruyTimLOTNO_Load(object sender, EventArgs e)
        {
            // _form_resize._get_initial_size();
            // TODO: This line of code loads data into the 'b7R2_FCCDataSet1.STOCKTP' table. You can move, or remove it, as needed.
            //this.sTOCKTPTableAdapter.Fill(this.b7R2_FCCDataSet1.STOCKTP);
            // TODO: This line of code loads data into the 'b7R2_FCCDataSet.TMPPHIEUGIAOHANGTT' table. You can move, or remove it, as needed.
            //this.tMPPHIEUGIAOHANGTTTableAdapter.Fill(this.b7R2_FCCDataSet.TMPPHIEUGIAOHANGTT);
            MAHANG();
            KHUNGGIOX();
            Csx();
            NM();
            dateSanXuat.EditValue = DateTime.Now;
            dateNXH.EditValue = DateTime.Now;
           
        }

        private void gridCtrQRCODE_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region Load TT mã hàng và giờ xuất
        private void MAHANG()
        {

            DataTable MH;


            //"SELECT PART,NAME FROM B GROUP BY PART,NAME";

            string SQL = "SELECT K.Code,K.Name FROM B20Item K left join STOCKTP TP " +
            "on k.Code = TP.PART " +
            "where k.IsGroup = 0   group BY k.Code,k.NAME";
            MH = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, SQL);
            lookUpMHSX.Properties.DataSource = MH;
            lookUpMHSX.Properties.DisplayMember = "Code";
            lookUpMHSX.Properties.ValueMember = "Code";
            lookUpMHX.Properties.DataSource = MH;
            lookUpMHX.Properties.DisplayMember = "Code";
            lookUpMHX.Properties.ValueMember = "Code";


        }
        private void Csx()
        {

            DataTable Csx;


            //"SELECT PART,NAME FROM B GROUP BY PART,NAME";

            string SQL = "select Code,Name from B20Shift where IsActive = 1";
            Csx = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, SQL);
            LookupCasx.Properties.DataSource = Csx;
            LookupCasx.Properties.DisplayMember = "Code";
            LookupCasx.Properties.ValueMember = "Code";



        }
        private void NM()
        {

            DataTable NM;


            //"SELECT PART,NAME FROM B GROUP BY PART,NAME";

            string SQL = "select distinct NHAMAY  from  [LUUPHIEUGIAOHANG]";
            NM = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, SQL);
            lookUpNhaMay.Properties.DataSource = NM;
            lookUpNhaMay.Properties.DisplayMember = "NHAMAY";
            lookUpNhaMay.Properties.ValueMember = "NHAMAY";



        }

        private void btExportExcel_Click(object sender, EventArgs e)
        {




        }

        private void gridCtrTTKHO_Click(object sender, EventArgs e)
        {
            TTTracuu tTTracuu = new TTTracuu();
            tTTracuu.Show();
        }

        private void XtraTabPHUONGTHUCTK_Click(object sender, EventArgs e)
        {
            if (XtraTabPHUONGTHUCTK.SelectedTabPage == TabLOTNO)
                Type = 0;
            if (XtraTabPHUONGTHUCTK.SelectedTabPage == TabTTSanXuat)
                Type = 1;
            if (XtraTabPHUONGTHUCTK.SelectedTabPage == TabTTXuatHang)
                Type = 2;
        }
        //------------------------------------------------------------ Dang lam den day -----------------------------------------------------------------------------------

        private void XuLyTT()
        {
            DataSet KQTV = new DataSet();
            int? Casx = (LookupCasx.EditValue != null) ? int.Parse(LookupCasx.EditValue.ToString()) : (int?)null;
            string PartSx = lookUpMHSX.Text;
            string NSX = dateSanXuat.EditValue.ToString();
            string NGH = dateNXH.EditValue.ToString();
            string LotNo = (txtLOTNO.Text != "") ? txtLOTNO.Text : null;
            string PartSX = (lookUpMHSX.Text != "Chọn Mã Hàng Sản Xuất") ? lookUpMHSX.Text : null;
            string PartXH = (lookUpMHX.Text != "Chọn Mã Hàng Xuất") ? lookUpMHX.Text : null;
            string kgx = (lookUpGXH.EditValue != null) ? lookUpGXH.EditValue.ToString() : null;
            string nm = (lookUpNhaMay.EditValue != null) ? lookUpNhaMay.EditValue.ToString() : null;
            splashScreenManager1.ShowWaitForm();
            gridCtrTTTONGXUAT.DataSource = null;
            gridCtrQRCODE.DataSource = null;
            gridCtrTTTONGXUAT.DataSource = null;
            WaitForm2.SO = 1;
            try
            {
                KQTV = SQLPROVIDER.ExecuteProcedureReturnDataSet(SQLPROVIDER.B7R2_FCCdb, "usp_HVN_HisLot",
                    new SqlParameter("@_Type", Type),
                    new SqlParameter("@_Casx", Casx),
                    new SqlParameter("@_LotNo", LotNo),
                    new SqlParameter("@_PartXH", PartXH),
                    new SqlParameter("@_PartSx", PartSX),
                    
                    new SqlParameter("@_NgaySx", SqlDbType.NVarChar) { Value = NSX },
                    
                    new SqlParameter("@_NgayGiao", SqlDbType.NVarChar) { Value = NGH },
                   
                    new SqlParameter("@_GioGiao", kgx),
                    new SqlParameter("@_NhaMay", nm));

                int bang = KQTV.Tables.Count;
                //int sptb1 = KQTV.Tables[1].Rows.Count;
                if (NSX.Contains(">"))
                {
                    if (Type == 0 || Type == 1)
                    {

                        gridCtrTTKHO.DataSource = KQTV.Tables[1];
                        gridCtrQRCODE.DataSource = KQTV.Tables[3];
                        gridCtrTTTONGXUAT.DataSource = KQTV.Tables[2];
                    }
                    else
                    {
                        gridCtrTTKHO.DataSource = KQTV.Tables[3];
                        gridCtrQRCODE.DataSource = KQTV.Tables[2];
                        gridCtrTTTONGXUAT.DataSource = KQTV.Tables[1];
                    }
                }
                else
                {
                    if (Type == 0 || Type == 1)
                    {

                        gridCtrTTKHO.DataSource = KQTV.Tables[0];
                        gridCtrQRCODE.DataSource = KQTV.Tables[2];
                        gridCtrTTTONGXUAT.DataSource = KQTV.Tables[1];
                    }
                    else
                    {
                        gridCtrTTKHO.DataSource = KQTV.Tables[2];
                        gridCtrQRCODE.DataSource = KQTV.Tables[1];
                        gridCtrTTTONGXUAT.DataSource = KQTV.Tables[0];
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show("Lỗi truy vấn " + ex);
            }
            finally
            {
                splashScreenManager1.CloseWaitForm();
            }



        }

        private void XtraTabPHUONGTHUCTK_Click_1(object sender, EventArgs e)
        {
            if (XtraTabPHUONGTHUCTK.SelectedTabPage == TabLOTNO)
                Type = 0;
            if (XtraTabPHUONGTHUCTK.SelectedTabPage == TabTTSanXuat)
                Type = 1;
            if (XtraTabPHUONGTHUCTK.SelectedTabPage == TabTTXuatHang)
                Type = 2;
        }

        private void cmdEP_Click(object sender, EventArgs e)
        {
            exportToExcel();
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {

            //LOADTT();
            XuLyTT();


        }


        #region export file
        public void exportToExcel()
        {
            string filePath = "";
            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Excel(.xlsx) | *.xlsx";
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    var printingSystem = new PrintingSystemBase();
                    var compositeLink = new CompositeLinkBase();
                    compositeLink.PrintingSystemBase = printingSystem;
                    var link1 = new PrintableComponentLinkBase();
                    link1.Component = gridCtrTTKHO;
                    var link2 = new PrintableComponentLinkBase();
                    link2.Component = gridCtrQRCODE;
                    var link3 = new PrintableComponentLinkBase();
                    link3.Component = gridCtrTTTONGXUAT;
                    compositeLink.Links.Add(link1);
                    compositeLink.Links.Add(link2);
                    compositeLink.Links.Add(link3);
                    var options = new XlsxExportOptions();
                    options.ExportMode = XlsxExportMode.SingleFile;
                    //options.ExportMode = XlsxExportMode.SingleFilePageByPage;
                    options.SheetName = "Thông Tin Tìm Kiếm";
                    //compositeLink.
                    //compositeLink.CreatePageForEachLink();
                    compositeLink.ExportToXlsx(saveDialog.FileName, options);
                    filePath = saveDialog.FileName;
                    DialogResult dlr = MessageBox.Show("Bạn có muốn mở file?", "Xuất file thành công!", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dlr == DialogResult.Yes)
                    {
                        Process.Start(filePath);
                    }
                }
            }
        }
        #endregion
        private void KHUNGGIOX()
        {

            DataTable KGX;


            string SQL = "SELECT giogiaofcc FROM luuphieugiaohang GROUP BY giogiaofcc order by giogiaofcc";
            KGX = SQLPROVIDER.ExecuteQuery(SQLPROVIDER.B7R2_FCCdb, SQL);
            lookUpGXH.Properties.DataSource = KGX;
            lookUpGXH.Properties.DisplayMember = "giogiaofcc";
            lookUpGXH.Properties.ValueMember = "giogiaofcc";


        }
        #endregion


        private void button1_Click(object sender, EventArgs e)
        {

        }

    }
}