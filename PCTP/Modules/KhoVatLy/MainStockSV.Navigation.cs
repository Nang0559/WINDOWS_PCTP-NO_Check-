using DevExpress.Utils.Extensions;
using DevExpress.XtraEditors;
using PCTP.Modules.KhoCore.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

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
       
    }
}
