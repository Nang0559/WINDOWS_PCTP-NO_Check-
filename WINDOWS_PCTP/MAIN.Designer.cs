namespace WINDOWS_PCTP
{
    partial class MAIN
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.LW_PGH = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // LW_PGH
            // 
            this.LW_PGH.Location = new System.Drawing.Point(12, 134);
            this.LW_PGH.Name = "LW_PGH";
            this.LW_PGH.Size = new System.Drawing.Size(1124, 325);
            this.LW_PGH.TabIndex = 0;
            this.LW_PGH.UseCompatibleStateImageBehavior = false;
            // 
            // MAIN
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1167, 671);
            this.Controls.Add(this.LW_PGH);
            this.Name = "MAIN";
            this.Text = "KIEM TRA XUAT HANG PCTP";
            this.Load += new System.EventHandler(this.MAIN_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView LW_PGH;
    }
}

