namespace PCTP
{
    partial class UF_TACHLOT
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
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.TXT_QRCODE = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.RDO_3 = new System.Windows.Forms.RadioButton();
            this.RDO_2 = new System.Windows.Forms.RadioButton();
            this.label2 = new System.Windows.Forms.Label();
            this.txt_sllot1 = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.txt_sllot2 = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.txt_sllot3 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.lw_qrcode = new System.Windows.Forms.ListView();
            this.CMD_XUATDS = new System.Windows.Forms.Button();
            this.CMD_XOA = new System.Windows.Forms.Button();
            this.CMD_SUA = new System.Windows.Forms.Button();
            this.errorProvider1 = new System.Windows.Forms.ErrorProvider(this.components);
            this.MaxVL = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.eProvider)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(118, 36);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(324, 24);
            this.label1.TabIndex = 0;
            this.label1.Text = "ĐỌC QRCODE TEM MUỐN TÁCH";
            // 
            // TXT_QRCODE
            // 
            this.TXT_QRCODE.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TXT_QRCODE.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TXT_QRCODE.Location = new System.Drawing.Point(501, 36);
            this.TXT_QRCODE.Name = "TXT_QRCODE";
            this.TXT_QRCODE.Size = new System.Drawing.Size(485, 34);
            this.TXT_QRCODE.TabIndex = 1;
            this.TXT_QRCODE.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TXT_QRCODE_KeyDown);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBox1.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.groupBox1.Controls.Add(this.RDO_3);
            this.groupBox1.Controls.Add(this.RDO_2);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(12, 357);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(201, 120);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "CHỌN HÌNH THỨC TÁCH";
            // 
            // RDO_3
            // 
            this.RDO_3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.RDO_3.AutoSize = true;
            this.RDO_3.BackColor = System.Drawing.Color.YellowGreen;
            this.RDO_3.Location = new System.Drawing.Point(17, 78);
            this.RDO_3.Name = "RDO_3";
            this.RDO_3.Size = new System.Drawing.Size(178, 21);
            this.RDO_3.TabIndex = 4;
            this.RDO_3.TabStop = true;
            this.RDO_3.Text = "TÁCH THÀNH 3 LOT";
            this.RDO_3.UseVisualStyleBackColor = false;
            // 
            // RDO_2
            // 
            this.RDO_2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.RDO_2.AutoSize = true;
            this.RDO_2.BackColor = System.Drawing.Color.PeachPuff;
            this.RDO_2.Location = new System.Drawing.Point(17, 36);
            this.RDO_2.Name = "RDO_2";
            this.RDO_2.Size = new System.Drawing.Size(178, 21);
            this.RDO_2.TabIndex = 5;
            this.RDO_2.TabStop = true;
            this.RDO_2.Text = "TÁCH THÀNH 2 LOT";
            this.RDO_2.UseVisualStyleBackColor = false;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 44);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(140, 17);
            this.label2.TabIndex = 5;
            this.label2.Text = "SỐ LƯỢNG LOT 1";
            // 
            // txt_sllot1
            // 
            this.txt_sllot1.Location = new System.Drawing.Point(154, 41);
            this.txt_sllot1.Name = "txt_sllot1";
            this.txt_sllot1.Size = new System.Drawing.Size(80, 22);
            this.txt_sllot1.TabIndex = 6;
            this.txt_sllot1.TextChanged += new System.EventHandler(this.txt_sllot1_TextChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.BackColor = System.Drawing.Color.MistyRose;
            this.groupBox2.Controls.Add(this.txt_sllot1);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(242, 378);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(263, 78);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "THÔNG TIN LOT 1";
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.BackColor = System.Drawing.Color.MistyRose;
            this.groupBox3.Controls.Add(this.txt_sllot2);
            this.groupBox3.Controls.Add(this.label3);
            this.groupBox3.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox3.Location = new System.Drawing.Point(511, 378);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(263, 78);
            this.groupBox3.TabIndex = 7;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "THÔNG TIN LOT 2";
            // 
            // txt_sllot2
            // 
            this.txt_sllot2.Location = new System.Drawing.Point(154, 41);
            this.txt_sllot2.Name = "txt_sllot2";
            this.txt_sllot2.Size = new System.Drawing.Size(80, 22);
            this.txt_sllot2.TabIndex = 6;
            this.txt_sllot2.TextChanged += new System.EventHandler(this.txt_sllot2_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 44);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(140, 17);
            this.label3.TabIndex = 5;
            this.label3.Text = "SỐ LƯỢNG LOT 2";
            // 
            // groupBox4
            // 
            this.groupBox4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox4.BackColor = System.Drawing.Color.MistyRose;
            this.groupBox4.Controls.Add(this.txt_sllot3);
            this.groupBox4.Controls.Add(this.label4);
            this.groupBox4.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox4.Location = new System.Drawing.Point(780, 378);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(263, 78);
            this.groupBox4.TabIndex = 7;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "THÔNG TIN LOT 3";
            // 
            // txt_sllot3
            // 
            this.txt_sllot3.Location = new System.Drawing.Point(154, 41);
            this.txt_sllot3.Name = "txt_sllot3";
            this.txt_sllot3.Size = new System.Drawing.Size(80, 22);
            this.txt_sllot3.TabIndex = 6;
            this.txt_sllot3.TextChanged += new System.EventHandler(this.txt_sllot3_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(8, 44);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(140, 17);
            this.label4.TabIndex = 5;
            this.label4.Text = "SỐ LƯỢNG LOT 3";
            // 
            // lw_qrcode
            // 
            this.lw_qrcode.AllowDrop = true;
            this.lw_qrcode.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lw_qrcode.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lw_qrcode.GridLines = true;
            this.lw_qrcode.HideSelection = false;
            this.lw_qrcode.Location = new System.Drawing.Point(36, 115);
            this.lw_qrcode.Name = "lw_qrcode";
            this.lw_qrcode.Size = new System.Drawing.Size(985, 183);
            this.lw_qrcode.TabIndex = 8;
            this.lw_qrcode.UseCompatibleStateImageBehavior = false;
            this.lw_qrcode.SelectedIndexChanged += new System.EventHandler(this.lw_qrcode_SelectedIndexChanged);
            // 
            // CMD_XUATDS
            // 
            this.CMD_XUATDS.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.CMD_XUATDS.Location = new System.Drawing.Point(430, 475);
            this.CMD_XUATDS.Name = "CMD_XUATDS";
            this.CMD_XUATDS.Size = new System.Drawing.Size(190, 43);
            this.CMD_XUATDS.TabIndex = 10;
            this.CMD_XUATDS.Text = "IN TEM";
            this.CMD_XUATDS.UseVisualStyleBackColor = true;
            this.CMD_XUATDS.Click += new System.EventHandler(this.CMD_XUATDS_Click);
            // 
            // CMD_XOA
            // 
            this.CMD_XOA.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.CMD_XOA.Image = global::PCTP.Properties.Resources.delete_16x16;
            this.CMD_XOA.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.CMD_XOA.Location = new System.Drawing.Point(635, 317);
            this.CMD_XOA.Name = "CMD_XOA";
            this.CMD_XOA.Size = new System.Drawing.Size(139, 36);
            this.CMD_XOA.TabIndex = 11;
            this.CMD_XOA.Text = "XÓA";
            this.CMD_XOA.UseVisualStyleBackColor = true;
            this.CMD_XOA.Click += new System.EventHandler(this.CMD_XOA_Click);
            // 
            // CMD_SUA
            // 
            this.CMD_SUA.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.CMD_SUA.Image = global::PCTP.Properties.Resources.edit_16x16;
            this.CMD_SUA.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.CMD_SUA.Location = new System.Drawing.Point(368, 317);
            this.CMD_SUA.Name = "CMD_SUA";
            this.CMD_SUA.Size = new System.Drawing.Size(136, 40);
            this.CMD_SUA.TabIndex = 9;
            this.CMD_SUA.Text = "SUA";
            this.CMD_SUA.UseVisualStyleBackColor = true;
            this.CMD_SUA.Click += new System.EventHandler(this.CMD_SUA_Click);
            // 
            // errorProvider1
            // 
            this.errorProvider1.ContainerControl = this;
            // 
            // MaxVL
            // 
            this.MaxVL.Location = new System.Drawing.Point(749, 484);
            this.MaxVL.Name = "MaxVL";
            this.MaxVL.Size = new System.Drawing.Size(116, 22);
            this.MaxVL.TabIndex = 12;
            this.MaxVL.Visible = false;
            // 
            // UF_TACHLOT
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1073, 530);
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
            this.Load += new System.EventHandler(this.UF_TACHLOT_Load);
            ((System.ComponentModel.ISupportInitialize)(this.eProvider)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox TXT_QRCODE;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton RDO_3;
        private System.Windows.Forms.RadioButton RDO_2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txt_sllot1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TextBox txt_sllot2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.TextBox txt_sllot3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ListView lw_qrcode;
        private System.Windows.Forms.Button CMD_SUA;
        private System.Windows.Forms.Button CMD_XUATDS;
        private System.Windows.Forms.Button CMD_XOA;
        private System.Windows.Forms.ErrorProvider errorProvider1;
        private System.Windows.Forms.TextBox MaxVL;
    }
}