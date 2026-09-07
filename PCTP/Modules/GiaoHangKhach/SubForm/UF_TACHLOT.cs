using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using DevExpress.XtraReports.UI;
using PCTP.ClassSQL;
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
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();

            // label1
            this.label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.label1.AutoSize = true;
            this.label1.Font = new Font("Arial", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            this.label1.Location = new Point(118, 36);
            this.label1.Name = "label1";
            this.label1.Size = new Size(324, 24);
            this.label1.TabIndex = 0;
            this.label1.Text = "ĐỌC QRCODE TEM MUỐN TÁCH";

            // TXT_QRCODE
            this.TXT_QRCODE.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.TXT_QRCODE.Font = new Font("Microsoft Sans Serif", 13.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            this.TXT_QRCODE.Location = new Point(501, 36);
            this.TXT_QRCODE.Name = "TXT_QRCODE";
            this.TXT_QRCODE.Size = new Size(485, 34);
            this.TXT_QRCODE.TabIndex = 1;
            this.TXT_QRCODE.KeyDown += new KeyEventHandler(this.TXT_QRCODE_KeyDown);

            // groupBox1 (chọn hình thức tách)
            this.groupBox1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.groupBox1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBox1.BackColor = SystemColors.InactiveCaption;
            this.groupBox1.Controls.Add(this.RDO_3);
            this.groupBox1.Controls.Add(this.RDO_2);
            this.groupBox1.Font = new Font("Microsoft Sans Serif", 7.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            this.groupBox1.Location = new Point(12, 357);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new Size(201, 120);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "CHỌN HÌNH THỨC TÁCH";

            // RDO_3
            this.RDO_3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.RDO_3.AutoSize = true;
            this.RDO_3.BackColor = Color.YellowGreen;
            this.RDO_3.Location = new Point(17, 78);
            this.RDO_3.Name = "RDO_3";
            this.RDO_3.Size = new Size(178, 21);
            this.RDO_3.TabIndex = 4;
            this.RDO_3.TabStop = true;
            this.RDO_3.Text = "TÁCH THÀNH 3 LOT";
            this.RDO_3.UseVisualStyleBackColor = false;

            // RDO_2
            this.RDO_2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.RDO_2.AutoSize = true;
            this.RDO_2.BackColor = Color.PeachPuff;
            this.RDO_2.Location = new Point(17, 36);
            this.RDO_2.Name = "RDO_2";
            this.RDO_2.Size = new Size(178, 21);
            this.RDO_2.TabIndex = 5;
            this.RDO_2.TabStop = true;
            this.RDO_2.Text = "TÁCH THÀNH 2 LOT";
            this.RDO_2.UseVisualStyleBackColor = false;

            // label2 / txt_sllot1 / groupBox2 (thông tin LOT 1)
            this.label2.AutoSize = true;
            this.label2.Location = new Point(8, 44);
            this.label2.Name = "label2";
            this.label2.Size = new Size(140, 17);
            this.label2.TabIndex = 5;
            this.label2.Text = "SỐ LƯỢNG LOT 1";

            this.txt_sllot1.Location = new Point(154, 41);
            this.txt_sllot1.Name = "txt_sllot1";
            this.txt_sllot1.Size = new Size(80, 22);
            this.txt_sllot1.TabIndex = 6;
            this.txt_sllot1.TextChanged += new EventHandler(this.txt_sllot1_TextChanged);

            this.groupBox2.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.groupBox2.BackColor = Color.MistyRose;
            this.groupBox2.Controls.Add(this.txt_sllot1);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Font = new Font("Microsoft Sans Serif", 7.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            this.groupBox2.Location = new Point(242, 378);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new Size(263, 78);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "THÔNG TIN LOT 1";

            // label3 / txt_sllot2 / groupBox3 (thông tin LOT 2)
            this.label3.AutoSize = true;
            this.label3.Location = new Point(8, 44);
            this.label3.Name = "label3";
            this.label3.Size = new Size(140, 17);
            this.label3.TabIndex = 5;
            this.label3.Text = "SỐ LƯỢNG LOT 2";

            this.txt_sllot2.Location = new Point(154, 41);
            this.txt_sllot2.Name = "txt_sllot2";
            this.txt_sllot2.Size = new Size(80, 22);
            this.txt_sllot2.TabIndex = 6;
            this.txt_sllot2.TextChanged += new EventHandler(this.txt_sllot2_TextChanged);

            this.groupBox3.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.groupBox3.BackColor = Color.MistyRose;
            this.groupBox3.Controls.Add(this.txt_sllot2);
            this.groupBox3.Controls.Add(this.label3);
            this.groupBox3.Font = new Font("Microsoft Sans Serif", 7.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            this.groupBox3.Location = new Point(511, 378);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new Size(263, 78);
            this.groupBox3.TabIndex = 7;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "THÔNG TIN LOT 2";

            // label4 / txt_sllot3 / groupBox4 (thông tin LOT 3 — chỉ dùng khi RDO_3)
            this.label4.AutoSize = true;
            this.label4.Location = new Point(8, 44);
            this.label4.Name = "label4";
            this.label4.Size = new Size(140, 17);
            this.label4.TabIndex = 5;
            this.label4.Text = "SỐ LƯỢNG LOT 3";

            this.txt_sllot3.Location = new Point(154, 41);
            this.txt_sllot3.Name = "txt_sllot3";
            this.txt_sllot3.Size = new Size(80, 22);
            this.txt_sllot3.TabIndex = 6;
            this.txt_sllot3.TextChanged += new EventHandler(this.txt_sllot3_TextChanged);

            this.groupBox4.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.groupBox4.BackColor = Color.MistyRose;
            this.groupBox4.Controls.Add(this.txt_sllot3);
            this.groupBox4.Controls.Add(this.label4);
            this.groupBox4.Font = new Font("Microsoft Sans Serif", 7.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            this.groupBox4.Location = new Point(780, 378);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new Size(263, 78);
            this.groupBox4.TabIndex = 7;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "THÔNG TIN LOT 3";

            // lw_qrcode (danh sách tem đã bắn)
            this.lw_qrcode.AllowDrop = true;
            this.lw_qrcode.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.lw_qrcode.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            this.lw_qrcode.GridLines = true;
            this.lw_qrcode.HideSelection = false;
            this.lw_qrcode.Location = new Point(36, 115);
            this.lw_qrcode.Name = "lw_qrcode";
            this.lw_qrcode.Size = new Size(985, 183);
            this.lw_qrcode.TabIndex = 8;
            this.lw_qrcode.UseCompatibleStateImageBehavior = false;
            this.lw_qrcode.SelectedIndexChanged += new EventHandler(this.lw_qrcode_SelectedIndexChanged);

            // CMD_XUATDS (in tem)
            this.CMD_XUATDS.Anchor = AnchorStyles.Bottom;
            this.CMD_XUATDS.Location = new Point(430, 475);
            this.CMD_XUATDS.Name = "CMD_XUATDS";
            this.CMD_XUATDS.Size = new Size(190, 43);
            this.CMD_XUATDS.TabIndex = 10;
            this.CMD_XUATDS.Text = "IN TEM";
            this.CMD_XUATDS.UseVisualStyleBackColor = true;
            this.CMD_XUATDS.Click += new EventHandler(this.CMD_XUATDS_Click);

            // CMD_XOA
            this.CMD_XOA.Anchor = AnchorStyles.Bottom;
            this.CMD_XOA.Image = global::PCTP.Properties.Resources.delete_16x16;
            this.CMD_XOA.ImageAlign = ContentAlignment.MiddleLeft;
            this.CMD_XOA.Location = new Point(635, 317);
            this.CMD_XOA.Name = "CMD_XOA";
            this.CMD_XOA.Size = new Size(139, 36);
            this.CMD_XOA.TabIndex = 11;
            this.CMD_XOA.Text = "XÓA";
            this.CMD_XOA.UseVisualStyleBackColor = true;
            this.CMD_XOA.Click += new EventHandler(this.CMD_XOA_Click);

            // CMD_SUA
            this.CMD_SUA.Anchor = AnchorStyles.Bottom;
            this.CMD_SUA.Image = global::PCTP.Properties.Resources.edit_16x16;
            this.CMD_SUA.ImageAlign = ContentAlignment.MiddleLeft;
            this.CMD_SUA.Location = new Point(368, 317);
            this.CMD_SUA.Name = "CMD_SUA";
            this.CMD_SUA.Size = new Size(136, 40);
            this.CMD_SUA.TabIndex = 9;
            this.CMD_SUA.Text = "SUA";
            this.CMD_SUA.UseVisualStyleBackColor = true;
            this.CMD_SUA.Click += new EventHandler(this.CMD_SUA_Click);

            // MaxVL (ẩn — chỉ lưu SL LOT gốc của dòng đang chọn)
            this.MaxVL.Location = new Point(749, 484);
            this.MaxVL.Name = "MaxVL";
            this.MaxVL.Size = new Size(116, 22);
            this.MaxVL.TabIndex = 12;
            this.MaxVL.Visible = false;

            // UF_TACHLOT (form)
            this.AllowDrop = true;
            this.AutoScaleDimensions = new SizeF(8F, 16F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1073, 530);
            this.Controls.Add(this.MaxVL);
            this.Controls.Add(this.CMD_XOA);
            this.Controls.Add(this.CMD_XUATDS);
            this.Controls.Add(this.CMD_SUA);
            this.Controls.Add(this.lw_qrcode);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.TXT_QRCODE);
            this.Controls.Add(this.label1);
            this.Name = "UF_TACHLOT";
            this.Text = "IN TÁCH LOT";
            this.Load += new EventHandler(this.UF_TACHLOT_Load);

            ((ISupportInitialize)(this.dxValidationProvider1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
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
            lw_qrcode.GridLines = true;
            lw_qrcode.FullRowSelect = true;
            lw_qrcode.View = View.Details;
        }

        /// <summary>Bắn QR — dùng QRCodeParser.ParseTachLotQr (helper chung).</summary>
        private void TXT_QRCODE_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            try
            {
                var info = QRCodeParser.ParseTachLotQr(TXT_QRCODE.Text);
                ADDTOLIST(info.LotNo, info.ItemCode, info.NgaySX, info.SlTemFccRaw, "0", "0", "0");
                TXT_QRCODE.Text = "";
            }
            catch (FormatException ex)
            {
                MessageBox.Show(ex.Message, "THÔNG BÁO", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        }

        /// <summary>Lưu số lượng vừa nhập — validate qua DXValidationProvider (KTTEXT()).</summary>
        private void CMD_SUA_Click(object sender, EventArgs e)
        {
            if (!ValidateAll())
            {
                MessageBox.Show("Không thể nhập số lượng lớn hơn số lượng hiện tại của TEM",
                    "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

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
            if (!RDO_2.Checked)
                item.SubItems[7].Text = txt_sllot3.Text;

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

            return SL1 + SL2 + SL3 <= SLL;
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

        private void txt_sllot1_TextChanged(object sender, EventArgs e) { }
        private void txt_sllot2_TextChanged(object sender, EventArgs e) { }
        private void txt_sllot3_TextChanged(object sender, EventArgs e) { }
    }
}