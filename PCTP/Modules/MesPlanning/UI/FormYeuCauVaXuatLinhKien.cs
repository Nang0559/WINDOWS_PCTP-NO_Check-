using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
namespace PCTP.Modules.MesPlanning.UI
{
    public sealed class FormYeuCauVaXuatLinhKien : Form
    {
        private readonly IComponentDemandService _service;
        private readonly DataGridView _plans=new DataGridView(),_demands=new DataGridView();
        private readonly TextBox _site=new TextBox(),_issueNo=new TextBox();
        private readonly ComboBox _lots=new ComboBox(),_wipSlots=new ComboBox();
        private readonly NumericUpDown _qty=new NumericUpDown();
        private readonly Label _status=new Label();
        public FormYeuCauVaXuatLinhKien():this(new ComponentDemandService(new ComponentDemandRepository())){}
        public FormYeuCauVaXuatLinhKien(IComponentDemandService service)
        {
            _service=service??throw new ArgumentNullException(nameof(service));
            Text="Yêu cầu và xuất linh kiện sang WIP"; StartPosition=FormStartPosition.CenterParent;
            Size=new Size(1280,820); MinimumSize=new Size(1000,650); BuildUi();
            Load+= (s,e)=>LoadPlans();
        }
        private void BuildUi()
        {
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(10)};
            root.RowCount=5; root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));root.RowStyles.Add(new RowStyle(SizeType.Percent,46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,39));root.RowStyles.Add(new RowStyle(SizeType.Absolute,105));root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
            var top=new FlowLayoutPanel{Dock=DockStyle.Fill};
            top.Controls.Add(new Label{Text="Site",AutoSize=true,Padding=new Padding(0,8,0,0)});
            _site.Width=100;top.Controls.Add(_site);
            var reload=new Button{Text="Tải kế hoạch"};reload.Click+=(s,e)=>LoadPlans();top.Controls.Add(reload);
            var gen=new Button{Text="Tạo yêu cầu linh kiện theo BOM"};gen.Click+=GenerateClick;top.Controls.Add(gen);
            _plans.Dock=DockStyle.Fill;_plans.ReadOnly=true;_plans.AllowUserToAddRows=false;_plans.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_plans.MultiSelect=false;_plans.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _plans.SelectionChanged+=(s,e)=>LoadDemand();
            _demands.Dock=DockStyle.Fill;_demands.ReadOnly=true;_demands.AllowUserToAddRows=false;_demands.SelectionMode=DataGridViewSelectionMode.FullRowSelect;_demands.MultiSelect=false;_demands.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _demands.SelectionChanged+=(s,e)=>LoadIssueOptions();
            var issue=new FlowLayoutPanel{Dock=DockStyle.Fill};
            issue.Controls.Add(new Label{Text="LOT / Slot nguồn PART",AutoSize=true,Padding=new Padding(0,8,0,0)});
            _lots.Width=300;_lots.DropDownStyle=ComboBoxStyle.DropDownList;issue.Controls.Add(_lots);
            issue.Controls.Add(new Label{Text="Slot đích WIP",AutoSize=true,Padding=new Padding(0,8,0,0)});
            _wipSlots.Width=300;_wipSlots.DropDownStyle=ComboBoxStyle.DropDownList;issue.Controls.Add(_wipSlots);
            issue.Controls.Add(new Label{Text="SL",AutoSize=true,Padding=new Padding(0,8,0,0)});
            _qty.Width=110;_qty.DecimalPlaces=3;_qty.Maximum=1000000000;_qty.Minimum=0.001M;_qty.Value=1;issue.Controls.Add(_qty);
            issue.Controls.Add(new Label{Text="Số phiếu",AutoSize=true,Padding=new Padding(0,8,0,0)});
            _issueNo.Width=150;_issueNo.Text="WIP-"+DateTime.Now.ToString("yyyyMMddHHmmss");issue.Controls.Add(_issueNo);
            var post=new Button{Text="XUẤT PART → WIP",Width=170,Height=32};post.Click+=PostClick;issue.Controls.Add(post);
            _status.Dock=DockStyle.Fill;_status.TextAlign=ContentAlignment.MiddleLeft;
            root.Controls.Add(top,0,0);root.Controls.Add(_plans,0,1);root.Controls.Add(_demands,0,2);root.Controls.Add(issue,0,3);
            root.Controls.Add(_status,0,4); Controls.Add(root);
        }
        private DataGridViewRow Selected(DataGridView grid)=>grid.SelectedRows.Count>0?grid.SelectedRows[0]:null;
        private long CellLong(DataGridViewRow row,string name)=>row==null?0:Convert.ToInt64(row.Cells[name].Value);
        private decimal CellDecimal(DataGridViewRow row,string name)=>row==null?0:Convert.ToDecimal(row.Cells[name].Value);
        private string CellText(DataGridViewRow row,string name)=>row==null?"":Convert.ToString(row.Cells[name].Value);
        private void LoadPlans()
        {
            try{_plans.DataSource=_service.GetPlanLines(_site.Text);_status.Text="Kế hoạch theo ca đã tải.";LoadDemand();}
            catch(Exception ex){_status.Text=ex.Message;}
        }
        private void GenerateClick(object sender,EventArgs e)
        {
            try{var row=Selected(_plans);var id=CellLong(row,"ProductionPlanLineId");if(id<=0)throw new InvalidOperationException("Chọn một dòng kế hoạch.");
                var demandId=_service.GenerateDemand(id,Environment.UserName);_status.Text="Đã tạo yêu cầu linh kiện #"+demandId+" từ BOM IFS.";LoadDemand();}
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Tạo yêu cầu linh kiện",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        private void LoadDemand()
        {
            try{var id=CellLong(Selected(_plans),"ProductionPlanLineId");_demands.DataSource=id>0?_service.GetDemandLines(id):new DataTable();LoadIssueOptions();}
            catch(Exception ex){_status.Text=ex.Message;}
        }
        private void LoadIssueOptions()
        {
            try
            {
                var plan=Selected(_plans);var demand=Selected(_demands);
                var site=CellText(plan,"SiteCode");var part=CellText(demand,"ComponentPartNo");
                _lots.DataSource=(!string.IsNullOrWhiteSpace(site)&&!string.IsNullOrWhiteSpace(part))?_service.GetPartLots(site,part):new DataTable();
                _lots.DisplayMember="LotSlotDisplay";_lots.ValueMember="MaterialLotId";
                _wipSlots.DataSource=!string.IsNullOrWhiteSpace(site)?_service.GetWipSlots(site):new DataTable();
                _wipSlots.DisplayMember="SlotDisplay";_wipSlots.ValueMember="SlotId";
                var outstanding=CellDecimal(demand,"OutstandingQty");
                var lot=_lots.SelectedItem as DataRowView;
                var available=lot==null?0M:Convert.ToDecimal(lot["AvailableQty"]);
                var suggested=Math.Min(outstanding,available);
                if(suggested>=_qty.Minimum)_qty.Value=Math.Min(_qty.Maximum,suggested);
            }
            catch(Exception ex){_status.Text=ex.Message;}
        }
        private void PostClick(object sender,EventArgs e)
        {
            try
            {
                var plan=Selected(_plans);var demand=Selected(_demands);
                if(demand==null)throw new InvalidOperationException("Chọn dòng yêu cầu linh kiện.");
                var lot=_lots.SelectedItem as DataRowView;
                if(lot==null)throw new InvalidOperationException("Không có LOT khả dụng trong kho PART.");
                var result=_service.PostIssue(CellText(plan,"SiteCode"),_issueNo.Text,CellLong(demand,"ComponentDemandLineId"),
                    Convert.ToInt64(lot["MaterialLotId"]),Convert.ToInt32(lot["SlotId"]),
                    Convert.ToInt32(_wipSlots.SelectedValue),_qty.Value,Environment.UserName);
                _status.Text="Đã chuyển "+_qty.Value.ToString("N3")+" từ PART sang WIP. Giao dịch MES #"+result.Rows[0]["InventoryTransactionId"]+".";
                LoadDemand();
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Xuất linh kiện sang WIP",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
    }
}