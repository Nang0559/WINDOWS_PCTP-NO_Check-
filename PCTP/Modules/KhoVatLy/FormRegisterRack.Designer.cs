namespace PCTP.VIEWSTOCK
{
    partial class FormRegisterRack
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
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.txtRackName = new DevExpress.XtraEditors.TextEdit();
            this.labelControl2 = new DevExpress.XtraEditors.LabelControl();
            this.spinSlotCount = new DevExpress.XtraEditors.SpinEdit();
            this.btnOK = new DevExpress.XtraEditors.SimpleButton();
            this.btnCancel = new DevExpress.XtraEditors.SimpleButton();
            this.labelControl3 = new DevExpress.XtraEditors.LabelControl();
            this.labelControl4 = new DevExpress.XtraEditors.LabelControl();
            this.cmbRack = new DevExpress.XtraEditors.ListBoxControl();
            this.txtWarehouseName = new DevExpress.XtraEditors.TextEdit();
            this.labelControl5 = new DevExpress.XtraEditors.LabelControl();
            this.spinRowCount = new DevExpress.XtraEditors.SpinEdit();
            this.labelControl6 = new DevExpress.XtraEditors.LabelControl();
            this.spinColumnCount = new DevExpress.XtraEditors.SpinEdit();
            this.labelControl7 = new DevExpress.XtraEditors.LabelControl();
            this.spinCapacity = new DevExpress.XtraEditors.SpinEdit();
            ((System.ComponentModel.ISupportInitialize)(this.txtRackName.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinSlotCount.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cmbRack)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtWarehouseName.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinRowCount.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinColumnCount.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinCapacity.Properties)).BeginInit();
            this.SuspendLayout();
            // 
            // labelControl1
            // 
            this.labelControl1.Location = new System.Drawing.Point(72, 102);
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(54, 16);
            this.labelControl1.TabIndex = 0;
            this.labelControl1.Text = "Tên Rack";
            // 
            // txtRackName
            // 
            this.txtRackName.Location = new System.Drawing.Point(176, 102);
            this.txtRackName.Name = "txtRackName";
            this.txtRackName.Size = new System.Drawing.Size(332, 22);
            this.txtRackName.TabIndex = 1;
            // 
            // labelControl2
            // 
            this.labelControl2.Location = new System.Drawing.Point(72, 145);
            this.labelControl2.Name = "labelControl2";
            this.labelControl2.Size = new System.Drawing.Size(80, 16);
            this.labelControl2.TabIndex = 0;
            this.labelControl2.Text = "Số Lượng Slot";
            // 
            // spinSlotCount
            // 
            this.spinSlotCount.EditValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinSlotCount.Location = new System.Drawing.Point(176, 137);
            this.spinSlotCount.Name = "spinSlotCount";
            this.spinSlotCount.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.spinSlotCount.Properties.MaxValue = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.spinSlotCount.Properties.MinValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinSlotCount.Size = new System.Drawing.Size(113, 24);
            this.spinSlotCount.TabIndex = 2;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(83, 227);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(124, 31);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(413, 227);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(124, 31);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // labelControl3
            // 
            this.labelControl3.Appearance.Font = new System.Drawing.Font("Times New Roman", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(163)));
            this.labelControl3.Appearance.Options.UseFont = true;
            this.labelControl3.Location = new System.Drawing.Point(124, 12);
            this.labelControl3.Name = "labelControl3";
            this.labelControl3.Size = new System.Drawing.Size(375, 45);
            this.labelControl3.TabIndex = 4;
            this.labelControl3.Text = "khai báo thông tin slot";
            // 
            // labelControl4
            // 
            this.labelControl4.Location = new System.Drawing.Point(72, 66);
            this.labelControl4.Name = "labelControl4";
            this.labelControl4.Size = new System.Drawing.Size(92, 16);
            this.labelControl4.TabIndex = 0;
            this.labelControl4.Text = "Tên WareHouse";
            // 
            // cmbRack
            // 
            this.cmbRack.Location = new System.Drawing.Point(353, 66);
            this.cmbRack.Name = "cmbRack";
            this.cmbRack.Size = new System.Drawing.Size(155, 26);
            this.cmbRack.TabIndex = 5;
            // 
            // txtWarehouseName
            // 
            this.txtWarehouseName.Location = new System.Drawing.Point(176, 69);
            this.txtWarehouseName.Name = "txtWarehouseName";
            this.txtWarehouseName.Size = new System.Drawing.Size(162, 22);
            this.txtWarehouseName.TabIndex = 6;
            this.txtWarehouseName.Leave += new System.EventHandler(this.txtWarehouseName_Leave);
            // 
            // labelControl5
            // 
            this.labelControl5.Location = new System.Drawing.Point(72, 181);
            this.labelControl5.Name = "labelControl5";
            this.labelControl5.Size = new System.Drawing.Size(87, 16);
            this.labelControl5.TabIndex = 0;
            this.labelControl5.Text = "Số Lượng Hàng";
            // 
            // spinRowCount
            // 
            this.spinRowCount.EditValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinRowCount.Location = new System.Drawing.Point(176, 173);
            this.spinRowCount.Name = "spinRowCount";
            this.spinRowCount.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.spinRowCount.Properties.MaxValue = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.spinRowCount.Properties.MinValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinRowCount.Size = new System.Drawing.Size(113, 24);
            this.spinRowCount.TabIndex = 2;
            // 
            // labelControl6
            // 
            this.labelControl6.Location = new System.Drawing.Point(315, 181);
            this.labelControl6.Name = "labelControl6";
            this.labelControl6.Size = new System.Drawing.Size(77, 16);
            this.labelControl6.TabIndex = 0;
            this.labelControl6.Text = "Số Lượng Cột";
            // 
            // spinColumnCount
            // 
            this.spinColumnCount.EditValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinColumnCount.Location = new System.Drawing.Point(413, 173);
            this.spinColumnCount.Name = "spinColumnCount";
            this.spinColumnCount.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.spinColumnCount.Properties.MaxValue = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.spinColumnCount.Properties.MinValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinColumnCount.Size = new System.Drawing.Size(95, 24);
            this.spinColumnCount.TabIndex = 2;
            // 
            // labelControl7
            // 
            this.labelControl7.Location = new System.Drawing.Point(312, 145);
            this.labelControl7.Name = "labelControl7";
            this.labelControl7.Size = new System.Drawing.Size(105, 16);
            this.labelControl7.TabIndex = 0;
            this.labelControl7.Text = "Sức chứa mỗi Slot";
            // 
            // spinCapacity
            // 
            this.spinCapacity.EditValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinCapacity.Location = new System.Drawing.Point(436, 137);
            this.spinCapacity.Name = "spinCapacity";
            this.spinCapacity.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] {
            new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo)});
            this.spinCapacity.Properties.MaxValue = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.spinCapacity.Properties.MinValue = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.spinCapacity.Size = new System.Drawing.Size(72, 24);
            this.spinCapacity.TabIndex = 2;
            // 
            // FormRegisterRack
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 292);
            this.Controls.Add(this.txtWarehouseName);
            this.Controls.Add(this.cmbRack);
            this.Controls.Add(this.labelControl3);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.spinColumnCount);
            this.Controls.Add(this.spinRowCount);
            this.Controls.Add(this.spinCapacity);
            this.Controls.Add(this.spinSlotCount);
            this.Controls.Add(this.labelControl6);
            this.Controls.Add(this.txtRackName);
            this.Controls.Add(this.labelControl5);
            this.Controls.Add(this.labelControl4);
            this.Controls.Add(this.labelControl7);
            this.Controls.Add(this.labelControl2);
            this.Controls.Add(this.labelControl1);
            this.Name = "FormRegisterRack";
            this.Text = "FormRegisterRack";
            ((System.ComponentModel.ISupportInitialize)(this.txtRackName.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinSlotCount.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cmbRack)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtWarehouseName.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinRowCount.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinColumnCount.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinCapacity.Properties)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.TextEdit txtRackName;
        private DevExpress.XtraEditors.LabelControl labelControl2;
        private DevExpress.XtraEditors.SpinEdit spinSlotCount;
        private DevExpress.XtraEditors.SimpleButton btnOK;
        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.LabelControl labelControl3;
        private DevExpress.XtraEditors.LabelControl labelControl4;
        private DevExpress.XtraEditors.ListBoxControl cmbRack;
        private DevExpress.XtraEditors.TextEdit txtWarehouseName;
        private DevExpress.XtraEditors.LabelControl labelControl5;
        private DevExpress.XtraEditors.SpinEdit spinRowCount;
        private DevExpress.XtraEditors.LabelControl labelControl6;
        private DevExpress.XtraEditors.SpinEdit spinColumnCount;
        private DevExpress.XtraEditors.LabelControl labelControl7;
        private DevExpress.XtraEditors.SpinEdit spinCapacity;
    }
}