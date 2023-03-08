namespace PhilipsPatternRom.StripeGenerator
{
    partial class StripeGeneratorForm
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
            this.imgPattern = new System.Windows.Forms.PictureBox();
            this.btnLoadPattern = new System.Windows.Forms.Button();
            this.ddlGeneratorType = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.nudVerticalStart = new System.Windows.Forms.NumericUpDown();
            this.nudVerticalEnd = new System.Windows.Forms.NumericUpDown();
            this.nudHorizontalEnd = new System.Windows.Forms.NumericUpDown();
            this.nudHorizontalStart = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.btnDraw = new System.Windows.Forms.Button();
            this.btnWritePng = new System.Windows.Forms.Button();
            this.btnSaveAs = new System.Windows.Forms.Button();
            this.btnLoad = new System.Windows.Forms.Button();
            this.chkClockCutout = new System.Windows.Forms.CheckBox();
            this.nudManualOffset = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.imgPattern)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVerticalStart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVerticalEnd)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudHorizontalEnd)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudHorizontalStart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudManualOffset)).BeginInit();
            this.SuspendLayout();
            // 
            // imgPattern
            // 
            this.imgPattern.Location = new System.Drawing.Point(13, 13);
            this.imgPattern.Name = "imgPattern";
            this.imgPattern.Size = new System.Drawing.Size(864, 579);
            this.imgPattern.TabIndex = 0;
            this.imgPattern.TabStop = false;
            // 
            // btnLoadPattern
            // 
            this.btnLoadPattern.Location = new System.Drawing.Point(165, 732);
            this.btnLoadPattern.Name = "btnLoadPattern";
            this.btnLoadPattern.Size = new System.Drawing.Size(110, 23);
            this.btnLoadPattern.TabIndex = 1;
            this.btnLoadPattern.Text = "Load ROMs...";
            this.btnLoadPattern.UseVisualStyleBackColor = true;
            this.btnLoadPattern.Click += new System.EventHandler(this.btnLoadPattern_Click);
            // 
            // ddlGeneratorType
            // 
            this.ddlGeneratorType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ddlGeneratorType.FormattingEnabled = true;
            this.ddlGeneratorType.Items.AddRange(new object[] {
            "Please select",
            "PM5644G/00",
            "PM5644G/Multi"});
            this.ddlGeneratorType.Location = new System.Drawing.Point(11, 732);
            this.ddlGeneratorType.Name = "ddlGeneratorType";
            this.ddlGeneratorType.Size = new System.Drawing.Size(141, 21);
            this.ddlGeneratorType.TabIndex = 2;
            this.ddlGeneratorType.SelectedIndexChanged += new System.EventHandler(this.ddlGeneratorType_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(11, 713);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(84, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Generator Type:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 606);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(68, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Vertical start:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(190, 608);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(66, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Vertical end:";
            // 
            // nudVerticalStart
            // 
            this.nudVerticalStart.Location = new System.Drawing.Point(97, 605);
            this.nudVerticalStart.Maximum = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            this.nudVerticalStart.Name = "nudVerticalStart";
            this.nudVerticalStart.Size = new System.Drawing.Size(87, 20);
            this.nudVerticalStart.TabIndex = 6;
            // 
            // nudVerticalEnd
            // 
            this.nudVerticalEnd.Location = new System.Drawing.Point(274, 605);
            this.nudVerticalEnd.Maximum = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            this.nudVerticalEnd.Name = "nudVerticalEnd";
            this.nudVerticalEnd.Size = new System.Drawing.Size(85, 20);
            this.nudVerticalEnd.TabIndex = 7;
            // 
            // nudHorizontalEnd
            // 
            this.nudHorizontalEnd.Location = new System.Drawing.Point(274, 640);
            this.nudHorizontalEnd.Maximum = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            this.nudHorizontalEnd.Name = "nudHorizontalEnd";
            this.nudHorizontalEnd.Size = new System.Drawing.Size(85, 20);
            this.nudHorizontalEnd.TabIndex = 11;
            // 
            // nudHorizontalStart
            // 
            this.nudHorizontalStart.Location = new System.Drawing.Point(97, 640);
            this.nudHorizontalStart.Maximum = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            this.nudHorizontalStart.Name = "nudHorizontalStart";
            this.nudHorizontalStart.Size = new System.Drawing.Size(87, 20);
            this.nudHorizontalStart.TabIndex = 10;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(190, 643);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(78, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "Horizontal end:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(12, 641);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(80, 13);
            this.label5.TabIndex = 8;
            this.label5.Text = "Horizontal start:";
            // 
            // btnDraw
            // 
            this.btnDraw.Location = new System.Drawing.Point(376, 606);
            this.btnDraw.Name = "btnDraw";
            this.btnDraw.Size = new System.Drawing.Size(100, 54);
            this.btnDraw.TabIndex = 12;
            this.btnDraw.Text = "Draw";
            this.btnDraw.UseVisualStyleBackColor = true;
            this.btnDraw.Click += new System.EventHandler(this.btnRedraw_Click);
            // 
            // btnWritePng
            // 
            this.btnWritePng.Location = new System.Drawing.Point(483, 606);
            this.btnWritePng.Name = "btnWritePng";
            this.btnWritePng.Size = new System.Drawing.Size(132, 54);
            this.btnWritePng.TabIndex = 13;
            this.btnWritePng.Text = "Write PNG to CWD";
            this.btnWritePng.UseVisualStyleBackColor = true;
            this.btnWritePng.Click += new System.EventHandler(this.btnWritePng_Click);
            // 
            // btnSaveAs
            // 
            this.btnSaveAs.Location = new System.Drawing.Point(441, 677);
            this.btnSaveAs.Name = "btnSaveAs";
            this.btnSaveAs.Size = new System.Drawing.Size(174, 27);
            this.btnSaveAs.TabIndex = 14;
            this.btnSaveAs.Text = "Save stripe definition to file...";
            this.btnSaveAs.UseVisualStyleBackColor = true;
            this.btnSaveAs.Click += new System.EventHandler(this.btnSaveAs_Click);
            // 
            // btnLoad
            // 
            this.btnLoad.Location = new System.Drawing.Point(261, 677);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(174, 27);
            this.btnLoad.TabIndex = 15;
            this.btnLoad.Text = "Load stripe definition from file...";
            this.btnLoad.UseVisualStyleBackColor = true;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);
            // 
            // chkClockCutout
            // 
            this.chkClockCutout.AutoSize = true;
            this.chkClockCutout.Location = new System.Drawing.Point(11, 677);
            this.chkClockCutout.Name = "chkClockCutout";
            this.chkClockCutout.Size = new System.Drawing.Size(89, 17);
            this.chkClockCutout.TabIndex = 16;
            this.chkClockCutout.Text = "Clock cut-out";
            this.chkClockCutout.UseVisualStyleBackColor = true;
            this.chkClockCutout.CheckedChanged += new System.EventHandler(this.chkClockCutout_CheckedChanged);
            // 
            // nudManualOffset
            // 
            this.nudManualOffset.Location = new System.Drawing.Point(632, 639);
            this.nudManualOffset.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.nudManualOffset.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.nudManualOffset.Name = "nudManualOffset";
            this.nudManualOffset.Size = new System.Drawing.Size(120, 20);
            this.nudManualOffset.TabIndex = 17;
            this.nudManualOffset.ValueChanged += new System.EventHandler(this.nudManualOffset_ValueChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(630, 620);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(136, 13);
            this.label6.TabIndex = 18;
            this.label6.Text = "Experimental manual offset:";
            // 
            // StripeGeneratorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(888, 769);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.nudManualOffset);
            this.Controls.Add(this.chkClockCutout);
            this.Controls.Add(this.btnLoad);
            this.Controls.Add(this.btnSaveAs);
            this.Controls.Add(this.btnWritePng);
            this.Controls.Add(this.btnDraw);
            this.Controls.Add(this.nudHorizontalEnd);
            this.Controls.Add(this.nudHorizontalStart);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.nudVerticalEnd);
            this.Controls.Add(this.nudVerticalStart);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ddlGeneratorType);
            this.Controls.Add(this.btnLoadPattern);
            this.Controls.Add(this.imgPattern);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "StripeGeneratorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Stripe Generator";
            this.Load += new System.EventHandler(this.StripeGeneratorForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.imgPattern)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVerticalStart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVerticalEnd)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudHorizontalEnd)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudHorizontalStart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudManualOffset)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox imgPattern;
        private System.Windows.Forms.Button btnLoadPattern;
        private System.Windows.Forms.ComboBox ddlGeneratorType;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown nudVerticalStart;
        private System.Windows.Forms.NumericUpDown nudVerticalEnd;
        private System.Windows.Forms.NumericUpDown nudHorizontalEnd;
        private System.Windows.Forms.NumericUpDown nudHorizontalStart;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button btnDraw;
        private System.Windows.Forms.Button btnWritePng;
        private System.Windows.Forms.Button btnSaveAs;
        private System.Windows.Forms.Button btnLoad;
        private System.Windows.Forms.CheckBox chkClockCutout;
        private System.Windows.Forms.NumericUpDown nudManualOffset;
        private System.Windows.Forms.Label label6;
    }
}

