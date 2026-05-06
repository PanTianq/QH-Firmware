namespace QH_Firmware.Other_UI
{
    partial class password
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
            this.label1 = new System.Windows.Forms.Label();
            this.passwordtextBox = new System.Windows.Forms.TextBox();
            this.verifybutton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.label1.Location = new System.Drawing.Point(12, 43);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(107, 25);
            this.label1.TabIndex = 0;
            this.label1.Text = "输入密码：";
            // 
            // passwordtextBox
            // 
            this.passwordtextBox.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.passwordtextBox.Location = new System.Drawing.Point(122, 40);
            this.passwordtextBox.Name = "passwordtextBox";
            this.passwordtextBox.Size = new System.Drawing.Size(148, 32);
            this.passwordtextBox.TabIndex = 1;
            this.passwordtextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBoxPassword_KeyPress);
            // 
            // verifybutton
            // 
            this.verifybutton.BackColor = System.Drawing.Color.ForestGreen;
            this.verifybutton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.verifybutton.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.verifybutton.ForeColor = System.Drawing.Color.White;
            this.verifybutton.Location = new System.Drawing.Point(82, 94);
            this.verifybutton.Name = "verifybutton";
            this.verifybutton.Size = new System.Drawing.Size(142, 43);
            this.verifybutton.TabIndex = 2;
            this.verifybutton.Text = "确认";
            this.verifybutton.UseVisualStyleBackColor = false;
            this.verifybutton.Click += new System.EventHandler(this.verifybutton_Click);
            // 
            // password
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(294, 157);
            this.Controls.Add(this.verifybutton);
            this.Controls.Add(this.passwordtextBox);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "password";
            this.Text = "密码验证";
            this.Load += new System.EventHandler(this.password_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox passwordtextBox;
        private System.Windows.Forms.Button verifybutton;
    }
}