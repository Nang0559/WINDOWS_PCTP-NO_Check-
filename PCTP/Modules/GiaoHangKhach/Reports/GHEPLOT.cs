using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using DevExpress.XtraReports.UI;

namespace PCTP.QRCODE_HVN.Report
{
    public partial class GHEPLOT : DevExpress.XtraReports.UI.XtraReport
    {
        public GHEPLOT()
        {
            InitializeComponent();
            
        }


        private void xrLabel1_BeforePrint(object sender, CancelEventArgs e)
        {
            XRLabel label = (XRLabel)sender;
            float maxWidth = label.WidthF;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                SizeF textSize = g.MeasureString(label.Text, label.Font);
                float currentSize = label.Font.Size;

                while (textSize.Width > maxWidth && currentSize > 6) // 6 là cỡ chữ nhỏ nhất cho phép
                {
                    currentSize -= 0.5f;
                    label.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                        System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                    textSize = g.MeasureString(label.Text, label.Font);
                }
            }
        }
    }
}
