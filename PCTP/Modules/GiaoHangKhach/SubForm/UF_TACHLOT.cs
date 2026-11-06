using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraReports.UI;
using PCTP.ClassSQL;
using PCTP.Shared.Helpers;
using PCTP.Shared.Validation;
using PCTP.VIEWSTOCK.Fuction;
using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.SubForm
{
    /// <summary>
    /// Form "IN TÁCH LOT" — bắn QR mã tem gốc, cho phép tách 1 tem thành 2 hoặc 3
    /// tem con cùng LOT gốc nhưng chia nhỏ số lượng, sau đó in tem GHÉPLOT.
    ///
    /// ĐÃ SỬA:
    ///  - Bỏ ValidatedForm (MyValidation) → kế thừa DevExpress.XtraEditors.XtraForm.
    ///  - Bỏ ErrorProvider tự viết → dùng DXValidationProvider với
    ///    CustomValidationRule (đúng API thật của DevExpress — không có
    ///    ConditionValidationRule.ValidationType.Custom như bản trước, phải
    ///    kế thừa ValidationRule và override Validate()).
    ///  - Bỏ parse QR tay → dùng chung QRCodeParser.ParseTachLotQr().
    /// </summary>
    public class UF_TACHLOT : ValidatableXtraForm
    {
        // ============================================================
        // State / dependency
        // ============================================================
        public int Maxvalue;
        public int STT;

        private readonly SQLPROVIDER sqlBRV = new SQLPROVIDER();

        // ============================================================
        // Controls
        // ============================================================
        private IContainer components = null;

        private Label label1;
        private TextBox TXT_QRCODE;
        private GroupBox groupBox1;
        private RadioButton RDO_3;
        private RadioButton RDO_2;
        private Label label2;
        private TextBox txt_sllot1;
        private GroupBox groupBox2;
        private GroupBox groupBox3;
        private TextBox txt_sllot2;
        private Label label3;
        private GroupBox groupBox4;
        private TextBox txt_sllot3;
        private Label label4;
        private ListView lw_qrcode;
        private Button CMD_SUA;
        private Button CMD_XUATDS;
        private Button CMD_XOA;
        private TextBox MaxVL;

        private DXValidationProvider dxValidationProvider1;
        private bool _updatingQuantities;

        public UF_TACHLOT()
        {
            InitializeComponent();
            SetupValidation();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // InitializeComponent
        // ============================================================
        private void InitializeComponent()
        {
            this.components = new Container();
            this.label1 = new Label();
            this.TXT_QRCODE = new TextBox();
            this.groupBox1 = new GroupBox();
            this.RDO_3 = new RadioButton();
            this.RDO_2 = new RadioButton();
            this.label2 = new Label();
            this.txt_sllot1 = new TextBox();
            this.groupBox2 = new GroupBox();
            this.groupBox3 = new GroupBox();
            this.txt_sllot2 = new TextBox();
            this.label3 = new Label();
            this.groupBox4 = new GroupBox();
            this.txt_sllot3 = new TextBox();
            this.label4 = new Label();
            this.lw_qrcode = new ListView();
            this.CMD_XUATDS = new Button();
            this.CMD_XOA = new Button();
            this.CMD_SUA = new Button();
            this.MaxVL = new TextBox();
            this.dxValidationProvider1 = new DXValidationProvider(this.components);

            ((ISupportInitialize)(this.dxValidationProvider1)).BeginInit();
            this.SuspendLayout();

            // Form shell — responsive layout, same sizing conventions as WMS forms.
            this.Name = "UF_TACHLOT";
            this.Text = "IN TÁCH LOT";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1000, 760);
            this.Size = new Size(1320, 900);
            this.AutoScaleDimensions = new SizeF(8F, 16F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AllowDrop = true;
            this.KeyPreview = true;
            this.Load += new EventHandler(this.UF_TACHLOT_Load);

            // Resizable shell: drag the splitters to resize scan / worklist / editor.
            var root = new SplitContainer
            {
                Name = "tachLot_root",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel1,
                IsSplitterFixed = false,
                SplitterWidth = 7,
                Size = new Size(1280, 850),
                Panel1MinSize = 118,
                Panel2MinSize = 390,
                SplitterDistance = 132
            };
            this.Controls.Add(root);

            // Scan toolbar.
            var scanGroup = new DevExpress.XtraEditors.GroupControl
            {
                Text = "TEM GỐC — QUÉT QR",
                Dock = DockStyle.Fill
            };
            scanGroup.AppearanceCaption.Font = new Font("Tahoma", 10F, FontStyle.Bold);
            scanGroup.AppearanceCaption.Options.UseFont = true;
            var scanLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 4, 10, 8),
                ColumnCount = 2,
                RowCount = 2
            };
            scanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
            scanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            scanLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            this.label1.Text = "ĐỌC QRCODE TEM MUỐN TÁCH";
            this.label1.Dock = DockStyle.Fill;
            this.label1.TextAlign = ContentAlignment.MiddleLeft;
            this.label1.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            this.TXT_QRCODE.Dock = DockStyle.Fill;
            this.TXT_QRCODE.Font = new Font("Tahoma", 12F, FontStyle.Bold);
            this.TXT_QRCODE.Name = "tachLot_qrInput";
            this.TXT_QRCODE.KeyDown += new KeyEventHandler(this.TXT_QRCODE_KeyDown);
            scanLayout.Controls.Add(this.label1, 0, 0);
            scanLayout.Controls.Add(this.TXT_QRCODE, 1, 0);
            var scanHint = new Label
            {
                Text = "Quét tem gốc để đưa vào danh sách. Chọn một dòng để chia số lượng thành 2 hoặc 3 LOT con.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText
            };
            scanLayout.Controls.Add(scanHint, 0, 1);
            scanLayout.SetColumnSpan(scanHint, 2);
            scanGroup.Controls.Add(scanLayout);
            root.Panel1.Controls.Add(scanGroup);

            // Worklist grid. Keep the existing ListView data contract to avoid changing QR/print behavior.
            var listGroup = new DevExpress.XtraEditors.GroupControl
            {
                Text = "DANH SÁCH TEM ĐÃ QUÉT — CHỌN DÒNG ĐỂ CHỈNH SỐ LƯỢNG",
                Dock = DockStyle.Fill
            };
            this.lw_qrcode.Name = "tachLot_worklist";
            this.lw_qrcode.Dock = DockStyle.Fill;
            this.lw_qrcode.Font = new Font("Tahoma", 9F);
            this.lw_qrcode.View = View.Details;
            this.lw_qrcode.FullRowSelect = true;
            this.lw_qrcode.GridLines = true;
            this.lw_qrcode.HideSelection = false;
            this.lw_qrcode.MultiSelect = false;
            this.lw_qrcode.UseCompatibleStateImageBehavior = false;
            this.lw_qrcode.SelectedIndexChanged += new EventHandler(this.lw_qrcode_SelectedIndexChanged);
            listGroup.Controls.Add(this.lw_qrcode);
            var workArea = new SplitContainer
            {
                Name = "tachLot_workArea",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                IsSplitterFixed = false,
                SplitterWidth = 7,
                Size = new Size(1280, 700),
                Panel1MinSize = 180,
                Panel2MinSize = 235,
                SplitterDistance = 390
            };
            workArea.Panel1.Controls.Add(listGroup);
            root.Panel2.Controls.Add(workArea);

            // Bottom editor: split mode + three quantity cards + actions.
            var editorGroup = new DevExpress.XtraEditors.GroupControl
            {
                Text = "THÔNG TIN TÁCH LOT",
                Dock = DockStyle.Fill
            };
            var editorRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 1,
                RowCount = 2
            };
            editorRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editorRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            var editor = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));

            this.groupBox1.Text = "HÌNH THỨC TÁCH";
            this.groupBox1.Dock = DockStyle.Fill;
            this.groupBox1.Padding = new Padding(8);
            this.groupBox1.Controls.Add(this.RDO_2);
            this.groupBox1.Controls.Add(this.RDO_3);
            this.RDO_2.Text = "Tách thành 2 LOT";
            this.RDO_2.AutoSize = true;
            this.RDO_2.Location = new Point(14, 32);
            this.RDO_2.Checked = true;
            this.RDO_3.Text = "Tách thành 3 LOT";
            this.RDO_3.AutoSize = true;
            this.RDO_3.Location = new Point(14, 66);
            this.RDO_2.CheckedChanged += new EventHandler(this.SplitMode_CheckedChanged);
            this.RDO_3.CheckedChanged += new EventHandler(this.SplitMode_CheckedChanged);
            editor.Controls.Add(this.groupBox1, 0, 0);

            ConfigureQuantityGroup(this.groupBox2, this.label2, this.txt_sllot1, "LOT con 1", "Số lượng LOT 1");
            ConfigureQuantityGroup(this.groupBox3, this.label3, this.txt_sllot2, "LOT con 2", "Số lượng LOT 2");
            ConfigureQuantityGroup(this.groupBox4, this.label4, this.txt_sllot3, "LOT con 3", "Số lượng LOT 3");
            this.txt_sllot1.TextChanged += new EventHandler(this.QuantityInput_TextChanged);
            this.txt_sllot2.TextChanged += new EventHandler(this.QuantityInput_TextChanged);
            editor.Controls.Add(this.groupBox2, 1, 0);
            editor.Controls.Add(this.groupBox3, 2, 0);
            editor.Controls.Add(this.groupBox4, 3, 0);

            var actionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0)
            };
            StyleActionButton(this.CMD_XUATDS, "IN TEM", 150);
            this.CMD_XUATDS.Click += new EventHandler(this.CMD_XUATDS_Click);
            StyleActionButton(this.CMD_XOA, "XÓA DÒNG", 120);
            this.CMD_XOA.Click += new EventHandler(this.CMD_XOA_Click);
            StyleActionButton(this.CMD_SUA, "CẬP NHẬT SỐ LƯỢNG", 190);
            this.CMD_SUA.Click += new EventHandler(this.CMD_SUA_Click);
            actionBar.Controls.Add(this.CMD_XUATDS);
            actionBar.Controls.Add(this.CMD_XOA);
            actionBar.Controls.Add(this.CMD_SUA);
            editorRoot.Controls.Add(editor, 0, 0);
            editorRoot.Controls.Add(actionBar, 0, 1);
            editorGroup.Controls.Add(editorRoot);
            workArea.Panel2.Controls.Add(editorGroup);

            this.txt_sllot3.ReadOnly = true;
            this.txt_sllot3.BackColor = SystemColors.Control;
            this.MaxVL.Visible = false;
            this.MaxVL.Name = "tachLot_originalQuantity";
            this.Controls.Add(this.MaxVL);

            ((ISupportInitialize)(this.dxValidationProvider1)).EndInit();
            this.ResumeLayout(false);
        }

        private static void ConfigureQuantityGroup(GroupBox group, Label label, TextBox editor, string title, string caption)
        {
            group.Text = title;
            group.Dock = DockStyle.Fill;
            group.Padding = new Padding(8);
            label.Text = caption;
            label.Dock = DockStyle.Top;
            label.Height = 24;
            label.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            editor.Dock = DockStyle.Top;
            editor.Font = new Font("Tahoma", 13F, FontStyle.Bold);
            editor.TextAlign = HorizontalAlignment.Center;
            editor.Height = 34;
            group.Controls.Add(editor);
            group.Controls.Add(label);
        }

        private static void StyleActionButton(Button button, string text, int width)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 36;
            button.Margin = new Padding(6, 0, 0, 0);
            button.Font = new Font("Tahoma", 9F, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = true;
        }

        // ============================================================
        // Validation — DXValidationProvider (API thật: kế thừa ValidationRule)
        // ============================================================

        /// <summary>
        /// Rule custom dùng chung cho txt_sllot1/2/3 — gọi lại KTTEXT() của
        /// form cha. Là nested class nên truy cập được private method của
        /// UF_TACHLOT bình thường.
        /// </summary>
        private sealed class SlLotValidationRule : ValidationRule
        {
            private readonly UF_TACHLOT _owner;
            public SlLotValidationRule(UF_TACHLOT owner) => _owner = owner;

            public override bool Validate(Control control, object value)
            {
                return _owner.KTTEXT();
            }
        }

        private void SetupValidation()
        {
            const string msg = "Không thể nhập số lượng lớn hơn số lượng hiện tại của TEM";

            var sumRule = new SumNotExceedRule(
                getMax: () => lw_qrcode.SelectedItems.Count > 0
                    ? Convert.ToInt32(lw_qrcode.SelectedItems[0].SubItems[4].Text)
                    : 0,
                getParticipants: () => new Control[] { txt_sllot1, txt_sllot2, txt_sllot3 });

            AddRule(txt_sllot1, sumRule, msg);
            AddRule(txt_sllot2, sumRule, msg);
            AddRule(txt_sllot3, sumRule, msg);
        }

        // ============================================================
        // Logic nghiệp vụ (nguyên bản từ uF_TACHLOT.cs)
        // ============================================================

        private void UF_TACHLOT_Load(object sender, EventArgs e)
        {
            RDO_2.Checked = true;
            ApplySplitMode();
            lw_qrcode.Items.Clear();
            lw_qrcode.View = View.Details;
            lw_qrcode.Columns.Add("STT", 50);
            lw_qrcode.Columns.Add("LOT NO", 200);
            lw_qrcode.Columns.Add("MA SP", 170);
            lw_qrcode.Columns.Add("NGAY SAN XUAT", 100);
            lw_qrcode.Columns.Add("SL LOT", 100);
            lw_qrcode.Columns.Add("SL LOT 1", 100);
            lw_qrcode.Columns.Add("SL LOT 2", 100);
            lw_qrcode.Columns.Add("SL LOT 3", 100);
            lw_qrcode.Columns[1].Width = 220;
            lw_qrcode.Columns[2].Width = 190;
            lw_qrcode.Columns[3].Width = 125;
            lw_qrcode.Columns[4].Width = 90;
            lw_qrcode.Columns[5].Width = 90;
            lw_qrcode.Columns[6].Width = 90;
            lw_qrcode.Columns[7].Width = 90;
            lw_qrcode.GridLines = true;
            lw_qrcode.FullRowSelect = true;
            lw_qrcode.View = View.Details;
        }

        /// <summary>Bắn QR — dùng QRCodeParser.ParseTachLotQr (helper chung).</summary>
        private void TXT_QRCODE_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            // Barcode scanners commonly append Enter; consume it so the form does not beep
            // or trigger a default button before the scanned row is committed.
            e.Handled = true;
            e.SuppressKeyPress = true;

            string rawQr = TXT_QRCODE.Text.Trim();
            if (rawQr.Length == 0) return;

            try
            {
                var info = QRCodeParser.ParseTachLotQr(rawQr);
                ADDTOLIST(info.LotNo, info.ItemCode, info.NgaySX, info.SlTemFccRaw, "0", "0", "0");
                TXT_QRCODE.Clear();
                TXT_QRCODE.Focus();
            }
            catch (FormatException ex)
            {
                MessageBox.Show(ex.Message, "QR TÁCH LOT KHÔNG HỢP LỆ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                TXT_QRCODE.SelectAll();
                TXT_QRCODE.Focus();
            }
        }

        private void ADDTOLIST(string LOT, string FCCPart_NO1, string NSX, string SLTEM, string SLLOT1, string SLLOT2, string SLLOT3)
        {
            int stt = lw_qrcode.Items.Count;
            ListViewItem lw_docqrcode = new ListViewItem((stt + 1).ToString());
            lw_docqrcode.SubItems.Add(LOT);
            lw_docqrcode.SubItems.Add(FCCPart_NO1);
            lw_docqrcode.SubItems.Add(NSX);
            lw_docqrcode.SubItems.Add(SLTEM);
            lw_docqrcode.SubItems.Add(SLLOT1);
            lw_docqrcode.SubItems.Add(SLLOT2);
            lw_docqrcode.SubItems.Add(SLLOT3);
            lw_qrcode.Items.Add(lw_docqrcode);
            lw_qrcode.Refresh();
        }

        private void lw_qrcode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (RDO_2.Checked)
            {
                if (lw_qrcode.SelectedItems.Count > 0)
                {
                    ListViewItem item = lw_qrcode.SelectedItems[0];
                    txt_sllot1.Text = item.SubItems[5].Text;
                    txt_sllot2.Text = item.SubItems[6].Text;
                    STT = Convert.ToInt32(item.SubItems[0].Text);
                    MaxVL.Text = item.SubItems[4].Text;
                    RecalculateSplitQuantities();
                }
                else
                {
                    txt_sllot1.Text = string.Empty;
                    txt_sllot2.Text = string.Empty;
                }
            }
            else
            {
                if (lw_qrcode.SelectedItems.Count > 0)
                {
                    ListViewItem item = lw_qrcode.SelectedItems[0];
                    txt_sllot1.Text = item.SubItems[5].Text;
                    txt_sllot2.Text = item.SubItems[6].Text;
                    txt_sllot3.Text = item.SubItems[7].Text;
                    STT = Convert.ToInt32(item.SubItems[0].Text);
                    MaxVL.Text = item.SubItems[4].Text;
                }
                else
                {
                    txt_sllot1.Text = string.Empty;
                    txt_sllot2.Text = string.Empty;
                    txt_sllot3.Text = string.Empty;
                }
            }
            ApplySplitMode();
        }

        /// <summary>Lưu số lượng vừa nhập — validate qua DXValidationProvider (KTTEXT()).</summary>
        private void CMD_SUA_Click(object sender, EventArgs e)
        {
            if (!ValidateSplitQuantities(showMessage: true)) return;

            if (lw_qrcode.SelectedItems.Count == 0)
            {
                txt_sllot1.Text = string.Empty;
                txt_sllot2.Text = string.Empty;
                if (!RDO_2.Checked) txt_sllot3.Text = string.Empty;
                return;
            }

            ListViewItem item = lw_qrcode.SelectedItems[0];
            item.SubItems[5].Text = txt_sllot1.Text;
            item.SubItems[6].Text = txt_sllot2.Text;
            item.SubItems[7].Text = RDO_2.Checked ? "0" : txt_sllot3.Text;

            STT = Convert.ToInt32(item.SubItems[0].Text);
        }

        /// <summary>Kiểm tra tổng SL LOT 1+2+(3) không vượt quá SL LOT gốc của dòng đang chọn.</summary>
        private bool KTTEXT()
        {
            if (lw_qrcode.SelectedItems.Count == 0)
                return false;

            int SL1 = string.IsNullOrEmpty(txt_sllot1.Text) ? 0 : int.Parse(txt_sllot1.Text);
            int SL2 = string.IsNullOrEmpty(txt_sllot2.Text) ? 0 : int.Parse(txt_sllot2.Text);
            int SL3 = string.IsNullOrEmpty(txt_sllot3.Text) ? 0 : int.Parse(txt_sllot3.Text);

            ListViewItem item = lw_qrcode.SelectedItems[0];
            int SLL = Convert.ToInt32(item.SubItems[4].Text);

            return SLL > 0 && SL1 >= 0 && SL2 >= 0 && SL3 >= 0
                && SL1 + SL2 + SL3 == SLL
                && SL1 > 0 && SL2 > 0 && (RDO_2.Checked || SL3 > 0);
        }

        private string Dien0tolot(int sllot)
        {
            string s = sllot.ToString();
            switch (s.Length)
            {
                case 1: return "000" + s;
                case 2: return "00" + s;
                case 3: return "0" + s;
                default: return s;
            }
        }

        private DataTable loadDATArt()
        {
            DataSet DTS = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_BarCodeView_ThongKe_Tmp5");
            return DTS.Tables[0];
        }

        private DataTable loadDATArtYMVN()
        {
            DataSet DTS = sqlBRV.ExecuteProcedureReturnDataSet(sqlBRV.B7R2_FCCdb, "Usp_BarCodeView_ThongKe_Tmp5_YMVN");
            return DTS.Tables[0];
        }

        private void CMD_XUATDS_Click(object sender, EventArgs e)
        {
            string MAHANG = "";
            sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb, "delete from TMPLOTTACH");

            foreach (ListViewItem it in lw_qrcode.Items)
            {
                int i = 1;
                string lotno = it.SubItems[1].Text;
                MAHANG = it.SubItems[2].Text;

                int sllot1 = Convert.ToInt32(it.SubItems[5].Text);
                string sl1 = Dien0tolot(sllot1);
                int sllot2 = Convert.ToInt32(it.SubItems[6].Text);
                string sl2 = Dien0tolot(sllot2);
                int sllot3 = Convert.ToInt32(it.SubItems[7].Text);
                string sl3 = Dien0tolot(sllot3);

                if (Convert.ToInt32(it.SubItems[4].Text) <= 0 || sllot1 < 0 || sllot2 < 0 || sllot3 < 0
                    || sllot1 + sllot2 + (RDO_2.Checked ? 0 : sllot3) != Convert.ToInt32(it.SubItems[4].Text)
                    || sllot1 <= 0 || sllot2 <= 0 || (!RDO_2.Checked && sllot3 <= 0))
                {
                    MessageBox.Show("Không thể tách tem có số lượng 0 hoặc số lượng các LOT con không hợp lệ. Vui lòng cập nhật lại thông tin tách.",
                        "Không thể in tem tách LOT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (RDO_2.Checked)
                {
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                        "insert into TMPLOTTACH (STT,LOT,MAHANG,SL,flag) values (" + i + ",'" + lotno + sl1 + "','" + MAHANG + "'," + sllot1 + " , 0)");
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                        "insert into TMPLOTTACH(STT,LOT,MAHANG,SL,flag) values ( " + (i + 1) + ",'" + lotno + sl2 + "','" + MAHANG + "'," + sllot2 + " , 0)");
                }
                else
                {
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                        "insert into TMPLOTTACH (STT,LOT,MAHANG,SL,flag) values (" + i + ",'" + lotno + sl1 + "','" + MAHANG + "'," + sllot1 + " , 0)");
                    sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                        "insert into TMPLOTTACH(STT,LOT,MAHANG,SL,flag) values ( " + (i + 1) + ",'" + lotno + sl2 + "','" + MAHANG + "'," + sllot2 + " , 0)");
                    if (sllot3 > 0)
                        sqlBRV.ExecuteNonQuery(sqlBRV.B7R2_FCCdb,
                            "insert into TMPLOTTACH(STT,LOT,MAHANG,SL,flag) values ( " + (i + 2) + ",'" + lotno + sl3 + "','" + MAHANG + "'," + sllot3 + " , 0)");
                }
                i++;
            }

            string customerCode = sqlBRV.ExecuteReader(sqlBRV.B7R2_FCCdb,
                "select CustomerCode from B20ItemQuyCach where itemcode = '" + MAHANG + "'");

            if (customerCode == "0100002")
            {
                PCTP.QRCODE_HVN.Report.GHEPLOT_YMVN report = new PCTP.QRCODE_HVN.Report.GHEPLOT_YMVN();
                report.DataSource = loadDATArtYMVN();
                ReportPrintTool printTool = new ReportPrintTool(report);
                printTool.ShowPreviewDialog();
            }
            else
            {
                PCTP.QRCODE_HVN.Report.GHEPLOT report = new PCTP.QRCODE_HVN.Report.GHEPLOT();
                report.DataSource = loadDATArt();
                ReportPrintTool printTool = new ReportPrintTool(report);
                printTool.ShowPreviewDialog();
            }
        }

        private void CMD_XOA_Click(object sender, EventArgs e)
        {
            if (lw_qrcode.SelectedItems.Count > 0)
            {
                ListViewItem item = lw_qrcode.SelectedItems[0];
                STT = Convert.ToInt32(item.SubItems[0].Text);
                var result = MessageBox.Show("BẠN MUỐN XÓA ITEM THỨ : " + STT + "!", "THÔNG BÁO",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (result == DialogResult.OK)
                {
                    for (int i = 0; i < lw_qrcode.SelectedItems.Count; i++)
                        lw_qrcode.Items.Remove(lw_qrcode.SelectedItems[i]);
                }
            }
            else
            {
                MessageBox.Show("CHƯA CÓ ITEM NÀO ĐƯỢC CHỌN", "THÔNG BÁO");
            }
        }

        private void SplitMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_updatingQuantities) return;
            ApplySplitMode();
            RecalculateSplitQuantities();
        }

        private void ApplySplitMode()
        {
            bool three = RDO_3.Checked;
            if (three) txt_sllot2.Tag = null;
            groupBox3.Text = three ? "LOT con 2 — có thể chỉnh" : "LOT con 2 — tự động";
            groupBox4.Enabled = three;
            txt_sllot2.ReadOnly = !three;
            txt_sllot2.BackColor = three ? SystemColors.Window : SystemColors.Control;
            txt_sllot3.ReadOnly = true;
            txt_sllot3.BackColor = SystemColors.Control;
            if (!three)
            {
                _updatingQuantities = true;
                try { txt_sllot3.Text = "0"; }
                finally { _updatingQuantities = false; }
            }
        }

        private void QuantityInput_TextChanged(object sender, EventArgs e)
        {
            if (_updatingQuantities) return;
            if (ReferenceEquals(sender, txt_sllot2) && RDO_3.Checked)
                txt_sllot2.Tag = "user";
            RecalculateSplitQuantities();
        }

        // 2 LOT: LOT 2 luôn là phần còn lại và khóa sửa.
        // 3 LOT: LOT 2 khởi tạo bằng phần còn lại, người dùng được chỉnh;
        //        LOT 3 tự nhận phần còn lại cuối cùng và luôn khóa sửa.
        private void RecalculateSplitQuantities()
        {
            if (_updatingQuantities || lw_qrcode.SelectedItems.Count == 0) return;
            if (!int.TryParse(MaxVL.Text, out int total) || total < 0) return;
            if (!int.TryParse(txt_sllot1.Text, out int first) || first < 0 || first > total) return;

            _updatingQuantities = true;
            try
            {
                int remainder = total - first;
                if (!RDO_3.Checked)
                {
                    txt_sllot2.Text = remainder.ToString();
                    txt_sllot3.Text = "0";
                }
                else
                {
                    // Khi đổi từ 2 LOT sang 3 LOT, mặc định dồn phần còn lại vào LOT 2.
                    if (txt_sllot2.Text.Length == 0 || txt_sllot2.Tag == null)
                        txt_sllot2.Text = remainder.ToString();
                    if (!int.TryParse(txt_sllot2.Text, out int second) || second < 0 || second > remainder)
                    {
                        txt_sllot2.Text = remainder.ToString();
                        second = remainder;
                    }
                    txt_sllot3.Text = (remainder - second).ToString();
                }
            }
            finally { _updatingQuantities = false; }
        }

        private bool ValidateSplitQuantities(bool showMessage)
        {
            if (lw_qrcode.SelectedItems.Count == 0)
            {
                if (showMessage) MessageBox.Show("Vui lòng chọn một tem gốc trong danh sách.", "Thiếu tem gốc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!int.TryParse(MaxVL.Text, out int total) || total <= 0)
            {
                if (showMessage) MessageBox.Show("Tem gốc có số lượng 0 hoặc không hợp lệ nên không thể tách.", "Không thể tách tem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!int.TryParse(txt_sllot1.Text, out int first) || !int.TryParse(txt_sllot2.Text, out int second)
                || !int.TryParse(txt_sllot3.Text, out int third))
            {
                if (showMessage) MessageBox.Show("Vui lòng nhập số lượng hợp lệ cho các LOT con.", "Số lượng không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            int activeThird = RDO_3.Checked ? third : 0;
            bool valid = first > 0 && second > 0 && activeThird >= 0
                && first + second + activeThird == total
                && third >= 0;
            if (!valid && showMessage)
                MessageBox.Show("Không thể tách tem gốc có số lượng 0 hoặc tạo LOT con có số lượng âm. Tổng số lượng các LOT con phải bằng đúng số lượng tem gốc (" + total + "). LOT con có số lượng 0 sẽ không được tạo/in tem.",
                    "Số lượng tách LOT không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return valid;
        }

        private void txt_sllot1_TextChanged(object sender, EventArgs e) { }
        private void txt_sllot2_TextChanged(object sender, EventArgs e) { }
        private void txt_sllot3_TextChanged(object sender, EventArgs e) { }
    }
}