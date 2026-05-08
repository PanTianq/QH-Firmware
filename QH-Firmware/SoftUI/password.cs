using System;
using System.Windows.Forms;

namespace QH_Firmware.Other_UI
{
    /// <summary>
    /// 密码验证窗口（高级设置权限验证）
    /// 密码：0000
    /// </summary>
    public partial class password : Form
    {
        // 正确密码（可直接修改）
        private readonly string _validPassword = "0000";

        public password()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗口加载初始化
        /// </summary>
        private void password_Load(object sender, EventArgs e)
        {
            // 密码框显示为星号
            passwordtextBox.PasswordChar = '*';
            // 窗口居中显示
            this.StartPosition = FormStartPosition.CenterParent;
        }

        /// <summary>
        /// 确认按钮：验证密码
        /// </summary>
        private void verifybutton_Click(object sender, EventArgs e)
        {
            CheckPassword();
        }

        /// <summary>
        /// 密码框回车事件：按回车直接验证
        /// </summary>
        private void textBoxPassword_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                CheckPassword();
            }
        }

        /// <summary>
        /// 密码验证核心逻辑
        /// </summary>
        private void CheckPassword()
        {
            string inputPwd = passwordtextBox.Text.Trim();

            // 密码正确
            if (inputPwd == _validPassword)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            // 密码错误
            else
            {
                MessageBox.Show("密码错误，请重新输入！", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                passwordtextBox.Clear();
                passwordtextBox.Focus();
            }
        }
    }
}