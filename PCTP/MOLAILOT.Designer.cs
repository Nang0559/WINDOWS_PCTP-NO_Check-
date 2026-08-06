namespace PCTP
{
    partial class MOLAILOT
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.TXT_DOCQRCODE = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.GW_MOLOT = new System.Windows.Forms.DataGridView();
            this.CMD_OK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.GW_MOLOT)).BeginInit();
            this.SuspendLayout();
            // 
            // TXT_DOCQRCODE
            // 
            this.TXT_DOCQRCODE.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TXT_DOCQRCODE.Location = new System.Drawing.Point(295, 29);
            this.TXT_DOCQRCODE.Name = "TXT_DOCQRCODE";
            this.TXT_DOCQRCODE.Size = new System.Drawing.Size(580, 30);
            this.TXT_DOCQRCODE.TabIndex = 3;
            this.TXT_DOCQRCODE.TextChanged += new System.EventHandler(this.TXT_DOCQRCODE_TextChanged);
            this.TXT_DOCQRCODE.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.TXT_DOCQRCODE_KeyPress);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(17, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(187, 29);
            this.label1.TabIndex = 2;
            this.label1.Text = "ĐỌC QRCODE";
            // 
            // GW_MOLOT
            // 
            this.GW_MOLOT.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.GW_MOLOT.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Menu;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.GW_MOLOT.DefaultCellStyle = dataGridViewCellStyle1;
            this.GW_MOLOT.Location = new System.Drawing.Point(22, 87);
            this.GW_MOLOT.Name = "GW_MOLOT";
            this.GW_MOLOT.RowHeadersWidth = 51;
            this.GW_MOLOT.RowTemplate.Height = 24;
            this.GW_MOLOT.Size = new System.Drawing.Size(1238, 170);
            this.GW_MOLOT.TabIndex = 4;
            // 
            // CMD_OK
            // 
            this.CMD_OK.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.CMD_OK.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CMD_OK.Location = new System.Drawing.Point(1115, 288);
            this.CMD_OK.Name = "CMD_OK";
            this.CMD_OK.Size = new System.Drawing.Size(145, 46);
            this.CMD_OK.TabIndex = 5;
            this.CMD_OK.Text = "OK";
            this.CMD_OK.UseVisualStyleBackColor = true;
            this.CMD_OK.Click += new System.EventHandler(this.CMD_OK_Click);
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 16.2F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.Color.Red;
            this.label2.Location = new System.Drawing.Point(15, 302);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(1030, 32);
            this.label2.TabIndex = 6;
            this.label2.Text = "NHẬP GIÁ TRỊ : \"0\" VÀO Ô STATUS , SAU ĐÓ CHỌN \"OK\" ĐỂ MỞ LẠI LOT";
            // 
            // MOLAILOT
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1286, 370);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.CMD_OK);
            this.Controls.Add(this.GW_MOLOT);
            this.Controls.Add(this.TXT_DOCQRCODE);
            this.Controls.Add(this.label1);
            this.Name = "MOLAILOT";
            this.Text = "MOLAILOT";
            ((System.ComponentModel.ISupportInitialize)(this.GW_MOLOT)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox TXT_DOCQRCODE;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView GW_MOLOT;
        private System.Windows.Forms.Button CMD_OK;
        private System.Windows.Forms.Label label2;
    }
}