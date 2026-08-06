using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraWaitForm;

namespace PCTP.QRCODE_HVN
{
    public partial class WaitForm2 : WaitForm
    {
        public WaitForm2()
        {
            InitializeComponent();
            this.progressPanel1.AutoHeight = true;
            
            this.progressPanel1.Caption = "Vui lòng đợi";
            this.progressPanel1.ShowCaption = true;
            //if (SO == 1)
            //{
            //    this.progressPanel1.Description = "Đang tải dữ liệu...";
            //}
            //else
            //{
            //    this.progressPanel1.Description = "Đang tính toán dữ liệu bắn QRcode...";
            //}
            this.progressPanel1.ShowDescription = true;
            this.progressPanel1.ToolTip = "My tooltip";
            this.progressPanel1.ShowToolTips = true;
            this.progressPanel1.WaitAnimationType = DevExpress.Utils.Animation.WaitingAnimatorType.Ring;
            this.progressPanel1.CaptionToDescriptionDistance = 5;
            this.progressPanel1.AutoHeight = true;
        }
        public static int SO;
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

        #endregion

        public enum WaitFormCommand
        {
        }
    }
}