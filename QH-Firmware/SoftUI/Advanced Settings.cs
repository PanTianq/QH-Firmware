using System;
using System.Windows.Forms;

namespace QH_Firmware.Other_UI
{
    public partial class Advanced : Form
    {
        private readonly Form1 _mainForm;

        public Advanced(Form1 mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm;

            writebutton.Click += Writebutton_Click;
            clearbutton.Click += Clearbutton_Click;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 打开时自动加载当前设备信息
            if (_mainForm.DeviceInfo != null)
            {
                PMtextBox.Text = _mainForm.DeviceInfo.ContainsKey("PM") ? _mainForm.DeviceInfo["PM"] : "";
                PNtextBox.Text = _mainForm.DeviceInfo.ContainsKey("PN") ? _mainForm.DeviceInfo["PN"] : "";
                CNtextBox.Text = _mainForm.DeviceInfo.ContainsKey("CN") ? _mainForm.DeviceInfo["CN"] : "";
            }
        }

        /// <summary>
        /// 写入按钮（严格按协议第8项）
        /// </summary>
        private void Writebutton_Click(object sender, EventArgs e)
        {
            // 非空校验
            if (string.IsNullOrWhiteSpace(PMtextBox.Text) ||
                string.IsNullOrWhiteSpace(PNtextBox.Text) ||
                string.IsNullOrWhiteSpace(CNtextBox.Text))
            {
                MessageBox.Show("产品型号、产品编号、电路编号，不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 串口必须打开
            if (!_mainForm.serialComm.IsOpen)
            {
                MessageBox.Show("请先打开串口", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string pm = PMtextBox.Text.Trim();
                string pn = PNtextBox.Text.Trim();
                string cn = CNtextBox.Text.Trim();

                // 拼接字符串
                string infoContent = $"#DEV_INFO:PM={pm}&PN={pn}&CN={cn};";
                string prefix = _mainForm.protocolLoader.Cmd_SetInfo.Split('{')[0].Trim();
                string sendCmd = $"{prefix} {{{infoContent}}}";

                // 发送
                _mainForm.serialComm.SendString(sendCmd);

                // 监听硬件回复
                _mainForm.serialComm.DataReceived += ListenWriteResponse;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 监听硬件写入回复：update inform 1 ok
        /// </summary>
        private void ListenWriteResponse(byte[] buffer)
        {
            string recv = System.Text.Encoding.ASCII.GetString(buffer).Trim();
            string AckSetInfo = _mainForm.protocolLoader.Ack_SetInfo;
            // 匹配协议回复
            if (recv.Contains(AckSetInfo))
            {
                // 收到正确回应 → 写入成功
                _mainForm.serialComm.DataReceived -= ListenWriteResponse;

                _mainForm._logOutput.Append("设备信息写入成功", System.Drawing.Color.LimeGreen);
            }
            else
            {
                _mainForm._logOutput.Append("硬件错误", System.Drawing.Color.Orange);
            }
        }

        /// <summary>
        /// 清空按钮
        /// </summary>
        private void Clearbutton_Click(object sender, EventArgs e)
        {
            PMtextBox.Clear();
            PNtextBox.Clear();
            CNtextBox.Clear();
            PMtextBox.Focus();
        }
    }
}