using DevExpress.XtraWaitForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.FunctionForm
{
    public partial class WaitFormExp : WaitForm
    {
        public WaitFormExp()
        {
            InitializeComponent();
            this.progressPanel1.AutoHeight = true;
        }

        #region Overrides

        public override void SetCaption(string caption)
        {
            base.SetCaption(caption);
            this.progressPanel1.Caption = caption;
        }
        public override void SetDescription(string description)
        {
            base.SetDescription(description);
            this.progressPanel1.Description = description;
        }
        public override void ProcessCommand(Enum cmd, object arg)
        {
            base.ProcessCommand(cmd, arg);
        }
        private void WaitFormExp_Load(object sender, EventArgs e)
        {
            // 👇 Cấu hình hiển thị đầy đủ
            progressPanel1.AutoHeight = true;
            progressPanel1.AppearanceCaption.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            progressPanel1.AppearanceDescription.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;

            // Tuỳ chọn: căn chỉnh text
            progressPanel1.AppearanceCaption.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            progressPanel1.AppearanceDescription.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;

            // 👇 Đảm bảo panel đủ rộng để wrap
            progressPanel1.MaximumSize = new Size(500, 0); // hoặc chiều rộng bạn mong muốn
            progressPanel1.MinimumSize = new Size(300, 0);
        }

        #endregion

        public enum WaitFormCommand
        {
        }
    }
}