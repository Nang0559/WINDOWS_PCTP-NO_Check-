using DevExpress.Utils.Extensions;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraVerticalGrid;
using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoCore.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuatKho.Services;
using PCTP.Shared.Common;
using PCTP.Shared.Services;
using PCTP.VIEWSTOCK.CanVas;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.UCControls;
using PCTP.VIEWSTOCK.ViewForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq.SqlClient;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
{
    public partial class MainStockSV
    {
        private void FilterSlotByTemCode(string temCode)
                    {
                        _currentFilterTemCode = temCode;
                        pnlMain.Invalidate();
                    }
        private void InitPEditInput(bool forceRefresh = false)
                    {
                        if (!forceRefresh && _cachedPEditData != null && (DateTime.Now - _cacheTime).TotalSeconds < CACHE_SECONDS)
                        {
                            BindPEditInput(_cachedPEditData);
                            return;
                        }
                        _cachedPEditData = _slotService.GetOccupiedSlotsForLookup();
                        _cacheTime = DateTime.Now;
                        BindPEditInput(_cachedPEditData);
                    }
        private void BindPEditInput(DataTable dt)
                    {
                        PEditInput.Properties.DataSource = null;
                        PEditInput.Properties.DataSource = dt;
                        PEditInput.Properties.DisplayMember = "ItemCode";
                        PEditInput.Properties.ValueMember = "ItemCode";

                        GridView view = PEditInput.Properties.View as GridView;
                        if (view == null) return;

                        view.Columns.Clear();
                        view.Columns.AddVisible("WhName", "Kho");
                        view.Columns.AddVisible("RackName", "Rack");
                        view.Columns.AddVisible("SlotNumber", "Vị trí");
                        view.Columns.AddVisible("LotNo", "Lot No");
                        view.Columns.AddVisible("TemCode", "TemCode");
                        view.Columns.AddVisible("ItemCode", "Mã Item");
                        view.Columns.AddVisible("ImportDate", "Ngày nhập");
                        view.Columns["ImportDate"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
                        view.Columns["ImportDate"].DisplayFormat.FormatString = "yyyy-MM-dd";
                    }
        private void PEditInput_TextChanged(object sender, EventArgs e)
                    {
                        GridView view = PEditInput.Properties.View as GridView;
                        if (view == null) return;

                        string keyword = PEditInput.Text.ToUpper().Trim();

                        if (keyword.Contains(":"))
                        {
                            view.ActiveFilterString = "";
                            PEditInput.ClosePopup();
                            return;
                        }

                        if (keyword.Length >= 1)
                        {
                            view.ActiveFilterString = $"[ItemCode] LIKE '{keyword}%'";
                            PEditInput.ShowPopup();
                        }
                        else
                        {
                            view.ActiveFilterString = "";
                            PEditInput.ClosePopup();
                        }
                    }
        private void PEditInput_Closed(object sender, ClosedEventArgs e)
                    {
                        if (e.CloseMode == PopupCloseMode.Normal)
                        {
                            GridView view = PEditInput.Properties.View as GridView;
                            if (view != null && view.FocusedRowHandle >= 0)
                            {
                                DataRow row = view.GetDataRow(view.FocusedRowHandle);
                                if (row != null)
                                {
                                    string temCode = row["TemCode"].ToString();
                                    if (!string.IsNullOrEmpty(temCode))
                                        FilterSlotByTemCode(temCode);
                                }
                            }
                        }
                    }
        private void PEditInput_KeyDown(object sender, KeyEventArgs e)
                    {
                        if (e.KeyCode == Keys.Enter)
                        {
                            string input = PEditInput.Text.Trim();
                            if (input.Contains(":"))
                            {
                                var parts = input.Split(':');
                                if (parts.Length >= 5)
                                {
                                    string temcode = parts.Last() + parts[4] + "-" + parts[3];
                                    FilterSlotByTemCode(temcode);
                                    e.Handled = true;
                                }
                                else
                                {
                                    MessageBox.Show("Mã QR không đúng định dạng cấu trúc.");
                                }
                                e.Handled = true;
                                e.SuppressKeyPress = true;
                            }
                            PEditInput.Text = "";
                            PEditInput.EditValue = null;
                        }
                    }
        private void GridView_KeyDown(object sender, KeyEventArgs e)
                    {
                        if (e.KeyCode == Keys.Enter)
                        {
                            if (PEditInput.Properties.View != null && PEditInput.Properties.View.FocusedRowHandle >= 0)
                            {
                                DataRow row = PEditInput.Properties.View.GetFocusedDataRow();
                                if (row != null)
                                    FilterSlotByTemCode(row["TemCode"].ToString());
                            }
                            PEditInput.ClosePopup();
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                    }
        private void PEditInput_MouseClick(object sender, MouseEventArgs e)
                    {
                        InitPEditInput();
                    }
        private void btnReset_Click(object sender, EventArgs e)
                    {
                        _currentFilterTemCode = "";
                        _currentSelectedSlotData = null;
                        pnlMain.Invalidate();
                    }
    }
}
