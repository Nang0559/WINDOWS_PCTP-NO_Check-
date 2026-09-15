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
        private void btnRegisterRack_Click(object sender, EventArgs e)
                    {
                        using (var form = new FormRegisterRack(_warehouseService))
                        {
                            if (form.ShowDialog() != DialogResult.OK) return;

                            string whName = form.WhName;
                            string rackName = form.RackName;
                        int rowCount = form.RowCount;
                        int columnCount = form.ColumnCount;
                        int slotCapacity = form.SlotCapacity;

                            try
                            {
                                // Giả định layout 1 hàng x slotCount cột — giữ đúng hành vi cũ (danh sách
                                // Slot phẳng đánh số 1..slotCount, không phân hàng/cột thật).
                                _warehouseService.RegisterWarehouseAndRack(
                                    whName, rackName, rowCount: rowCount, columnCount: columnCount, slotCapacity: slotCapacity);

                                _ = LoadAllWarehouses();
                            }
                            catch (Exception ex)
                            {
                                XtraMessageBox.Show($"Lỗi đăng ký Rack:\n{ex.Message}", "Lỗi",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
        public List<Slot> AllSlots
                    {
                        get
                        {
                            var list = new List<Slot>();
                            foreach (var rack in _rackLayouts)
                            {
                                if (rack.RackData?.Slots != null)
                                    list.AddRange(rack.RackData.Slots.Select(s => s.Slot));
                            }
                            return list;
                        }
                    }
        private void btnEnterItem_Click(object sender, EventArgs e)
                    {
                        using (var form = new FormEnterItemSV(this))
                        {
                            form.ShowDialog(this);
                        }
                    }
        private void simpleButton1_Click(object sender, EventArgs e)
                    {
                        FormStockHistory shs = new FormStockHistory();
                        shs.Show();
                    }
    }
}
