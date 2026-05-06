using System.Windows.Forms;

namespace QH_Firmware.Other_UI
{
    partial class Advanced
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.CNtextBox = new System.Windows.Forms.TextBox();
            this.PNtextBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.PMtextBox = new System.Windows.Forms.TextBox();
            this.writebutton = new System.Windows.Forms.Button();
            this.clearbutton = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanel1.Controls.Add(this.CNtextBox, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.PNtextBox, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label5, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.PMtextBox, 1, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 28);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(344, 182);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // CNtextBox
            // 
            this.CNtextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CNtextBox.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.CNtextBox.Location = new System.Drawing.Point(140, 123);
            this.CNtextBox.Name = "CNtextBox";
            this.CNtextBox.Size = new System.Drawing.Size(201, 32);
            this.CNtextBox.TabIndex = 9;
            // 
            // PNtextBox
            // 
            this.PNtextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PNtextBox.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.PNtextBox.Location = new System.Drawing.Point(140, 63);
            this.PNtextBox.Name = "PNtextBox";
            this.PNtextBox.Size = new System.Drawing.Size(201, 32);
            this.PNtextBox.TabIndex = 8;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label5.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.label5.Location = new System.Drawing.Point(0, 120);
            this.label5.Margin = new System.Windows.Forms.Padding(0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(137, 62);
            this.label5.TabIndex = 4;
            this.label5.Text = "电路编号：";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.label3.Location = new System.Drawing.Point(0, 60);
            this.label3.Margin = new System.Windows.Forms.Padding(0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(137, 60);
            this.label3.TabIndex = 2;
            this.label3.Text = "产品编号：";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Margin = new System.Windows.Forms.Padding(0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(137, 60);
            this.label1.TabIndex = 0;
            this.label1.Text = "产品型号：";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // PMtextBox
            // 
            this.PMtextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PMtextBox.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.PMtextBox.Location = new System.Drawing.Point(140, 3);
            this.PMtextBox.Name = "PMtextBox";
            this.PMtextBox.Size = new System.Drawing.Size(201, 32);
            this.PMtextBox.TabIndex = 7;
            // 
            // writebutton
            // 
            this.writebutton.BackColor = System.Drawing.Color.ForestGreen;
            this.writebutton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.writebutton.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.writebutton.ForeColor = System.Drawing.Color.White;
            this.writebutton.Location = new System.Drawing.Point(25, 243);
            this.writebutton.Name = "writebutton";
            this.writebutton.Size = new System.Drawing.Size(143, 50);
            this.writebutton.TabIndex = 5;
            this.writebutton.Text = "确认写入";
            this.writebutton.UseVisualStyleBackColor = false;
            // 
            // clearbutton
            // 
            this.clearbutton.BackColor = System.Drawing.Color.SteelBlue;
            this.clearbutton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearbutton.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.clearbutton.ForeColor = System.Drawing.Color.White;
            this.clearbutton.Location = new System.Drawing.Point(185, 243);
            this.clearbutton.Name = "clearbutton";
            this.clearbutton.Size = new System.Drawing.Size(143, 50);
            this.clearbutton.TabIndex = 6;
            this.clearbutton.Text = "清空";
            this.clearbutton.UseVisualStyleBackColor = false;
            // 
            // Advanced
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(351, 328);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.writebutton);
            this.Controls.Add(this.clearbutton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Advanced";
            this.Text = "高级设置";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label3;
        private Button writebutton;
        private Button clearbutton;
        private TextBox PMtextBox;
        private TextBox CNtextBox;
        private TextBox PNtextBox;
    }
}