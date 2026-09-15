
using System;


namespace PCTP.Modules.KhoVatLy
{
    public partial class MainStockSV
    {
        private void btnDKMa_Click(object sender, EventArgs e)
                    {
                        using (var form = new FormInspectionConfig(_inspectionConfigService, _warehouseService))
                        {
                            form.ShowDialog(this);
                        }
                    }
      
    }
}
