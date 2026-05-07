using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QH_Firmware.Other_UI
{
    public partial class password : Form
    {
        public password()
        {
            InitializeComponent();
        }
        private string _validPassword = "0000";
        private void password_Load(object sender, EventArgs e)
        {
            // 输入框设置为显示星号
            passwordtextBox.PasswordChar = '*';
            // 窗口居中
            this.StartPosition = FormStartPosition.CenterParent;
        }

        // 确认按钮点击事件
        private void verifybutton_Click(object sender, EventArgs e)
        {
            CheckPassword();
        }

        // 输入框回车事件（和确认按钮功能一致）
        private void textBoxPassword_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true; // 防止回车发出响铃
                CheckPassword();
            }
        }

        // 核心密码校验逻辑
        private void CheckPassword()
        {
            string inputPwd = passwordtextBox.Text.Trim();

            if (inputPwd == _validPassword)
            {
                // 密码正确，打开高级设置窗口
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("密码错误，请重新输入！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                passwordtextBox.Clear();
                passwordtextBox.Focus();
            }
        }
    }
}
