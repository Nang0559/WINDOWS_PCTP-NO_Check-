namespace PCTP
{
    partial class LISTNHAPKHO
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
            this.LW_NHAP_KHO = new System.Windows.Forms.ListView();
            this.CMD_OK = new System.Windows.Forms.Button();
            this.CMD_XOA = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // LW_NHAP_KHO
            // 
            this.LW_NHAP_KHO.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LW_NHAP_KHO.BackColor = System.Drawing.Color.AntiqueWhite;
            this.LW_NHAP_KHO.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LW_NHAP_KHO.HideSelection = false;
            this.LW_NHAP_KHO.Location = new System.Drawing.Point(15, 21);
            this.LW_NHAP_KHO.Name = "LW_NHAP_KHO";
            this.LW_NHAP_KHO.Size = new System.Drawing.Size(1368, 354);
            this.LW_NHAP_KHO.TabIndex = 0;
            this.LW_NHAP_KHO.UseCompatibleStateImageBehavior = false;
            // 
            // CMD_OK
            // 
            this.CMD_OK.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.CMD_OK.Location = new System.Drawing.Point(758, 399);
            this.CMD_OK.Name = "CMD_OK";
            this.CMD_OK.Size = new System.Drawing.Size(142, 51);
            this.CMD_OK.TabIndex = 1;
            this.CMD_OK.Text = "OK";
            this.CMD_OK.UseVisualStyleBackColor = true;
            this.CMD_OK.Click += new System.EventHandler(this.CMD_OK_Click);
            // 
            // CMD_XOA
            // 
            this.CMD_XOA.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.CMD_XOA.Location = new System.Drawing.Point(446, 402);
            this.CMD_XOA.Name = "CMD_XOA";
            this.CMD_XOA.Size = new System.Drawing.Size(125, 48);
            this.CMD_XOA.TabIndex = 2;
            this.CMD_XOA.Text = "XÓA";
            this.CMD_XOA.UseVisualStyleBackColor = true;
            this.CMD_XOA.Click += new System.EventHandler(this.CMD_XOA_Click);
            // 
            // LISTNHAPKHO
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.ClientSize = new System.Drawing.Size(1404, 462);
            this.Controls.Add(this.CMD_XOA);
            this.Controls.Add(this.CMD_OK);
            this.Controls.Add(this.LW_NHAP_KHO);
            this.Name = "LISTNHAPKHO";
            this.Text = "LISTNHAPKHO";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.LISTNHAPKHO_FormClosed);
            this.Load += new System.EventHandler(this.LISTNHAPKHO_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView LW_NHAP_KHO;
        private System.Windows.Forms.Button CMD_OK;
        private System.Windows.Forms.Button CMD_XOA;
    }
}