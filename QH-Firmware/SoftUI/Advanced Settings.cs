using System;
using System.Windows.Forms;

namespace QH_Firmware.Other_UI
{
    /// <summary>
    /// 高级设置窗口
    /// 功能：修改设备信息（PM/PN/CN）并写入设备，带20秒超时自动关闭
    /// </summary>
    public partial class Advanced : Form
    {
        // 主窗体引用，用于访问串口、日志、设备信息等
        private readonly Form1 _mainForm;

        // 超时定时器（20秒）
        private System.Windows.Forms.Timer _writeTimeoutTimer;
        private const int WRITE_TIMEOUT_MS = 20000; // 20秒超时时间

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="mainForm">主窗体对象</param>
        public Advanced(Form1 mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm;

            // 绑定按钮点击事件
            writebutton.Click += Writebutton_Click;
            clearbutton.Click += Clearbutton_Click;
        }

        /// <summary>
        /// 窗口加载时，自动填充当前设备信息
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            if (_mainForm.DeviceInfo != null)
            {
                PMtextBox.Text = _mainForm.DeviceInfo.ContainsKey("PM") ? _mainForm.DeviceInfo["PM"] : "";
                PNtextBox.Text = _mainForm.DeviceInfo.ContainsKey("PN") ? _mainForm.DeviceInfo["PN"] : "";
                CNtextBox.Text = _mainForm.DeviceInfo.ContainsKey("CN") ? _mainForm.DeviceInfo["CN"] : "";
            }
        }

        /// <summary>
        /// 写入按钮点击事件
        /// 功能：校验输入 → 拼接协议指令 → 发送串口数据 → 启动超时
        /// </summary>
        private void Writebutton_Click(object sender, EventArgs e)
        {
            // 输入框非空校验
            if (string.IsNullOrWhiteSpace(PMtextBox.Text) ||
                string.IsNullOrWhiteSpace(PNtextBox.Text) ||
                string.IsNullOrWhiteSpace(CNtextBox.Text))
            {
                MessageBox.Show("产品型号、产品编号、电路编号，不能为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 校验串口是否已打开
            if (!_mainForm.serialComm.IsOpen)
            {
                MessageBox.Show("请先打开串口", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // 获取输入框内容并去除空格
                string pm = PMtextBox.Text.Trim();
                string pn = PNtextBox.Text.Trim();
                string cn = CNtextBox.Text.Trim();

                // 按协议格式拼接写入指令
                string infoContent = $"#DEV_INFO:PM={pm}&PN={pn}&CN={cn};";
                string prefix = _mainForm.protocolLoader.Cmd_SetInfo.Split('{')[0].Trim();
                string sendCmd = $"{prefix} {{{infoContent}}}";

                // 标记：高级设置正在写入，屏蔽固件模块监听
                _mainForm.IsAdvancedWriting = true;

                // 发送指令到串口
                _mainForm.serialComm.SendString(sendCmd);

                // 注册串口接收监听，等待设备回复
                _mainForm.serialComm.DataReceived += ListenWriteResponse;

                // 启动20秒超时定时器
                StartWriteTimeoutTimer();
            }
            catch (Exception ex)
            {
                // 发送异常提示
                MessageBox.Show($"发送失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 异常时恢复标志位
                _mainForm.IsAdvancedWriting = false;
            }
        }

        /// <summary>
        /// 监听设备写入结果回复
        /// </summary>
        private void ListenWriteResponse(byte[] buffer)
        {
            // 将接收的字节数据转为字符串
            string recv = System.Text.Encoding.ASCII.GetString(buffer).Trim();
            string AckSetInfo = _mainForm.protocolLoader.Ack_SetInfo;

            // 判断是否收到正确的成功应答
            if (recv.Contains(AckSetInfo))
            {
                _mainForm._logOutput.Append("设备信息写入成功", System.Drawing.Color.LimeGreen);
            }
            else
            {
                _mainForm._logOutput.Append("硬件错误", System.Drawing.Color.Orange);
            }

            // 写入完成，恢复标志位
            _mainForm.IsAdvancedWriting = false;

            // 移除监听、停止定时器、关闭当前窗口
            _mainForm.serialComm.DataReceived -= ListenWriteResponse;
            StopWriteTimeoutTimer();
            this.Invoke(new Action(() => this.Close()));
        }

        /// <summary>
        /// 启动20秒写入超时定时器
        /// </summary>
        private void StartWriteTimeoutTimer()
        {
            // 先清理已存在的定时器
            StopWriteTimeoutTimer();

            _writeTimeoutTimer = new System.Windows.Forms.Timer();
            _writeTimeoutTimer.Interval = WRITE_TIMEOUT_MS;
            _writeTimeoutTimer.Tick += (s, args) =>
            {
                // 超时后停止定时器、移除监听
                StopWriteTimeoutTimer();
                _mainForm.serialComm.DataReceived -= ListenWriteResponse;

                // 恢复标志位
                _mainForm.IsAdvancedWriting = false;

                // 输出超时日志
                _mainForm._logOutput.Append($"[超时] 设备信息写入超时（{WRITE_TIMEOUT_MS / 1000}s）", System.Drawing.Color.Orange);

                // 超时自动关闭窗口
                this.Invoke(new Action(() => this.Close()));
            };
            _writeTimeoutTimer.Start();
        }

        /// <summary>
        /// 停止并销毁超时定时器
        /// </summary>
        private void StopWriteTimeoutTimer()
        {
            if (_writeTimeoutTimer != null)
            {
                _writeTimeoutTimer.Stop();
                _writeTimeoutTimer.Dispose();
                _writeTimeoutTimer = null;
            }
        }

        /// <summary>
        /// 清空按钮：清空所有输入框
        /// </summary>
        private void Clearbutton_Click(object sender, EventArgs e)
        {
            PMtextBox.Clear();
            PNtextBox.Clear();
            CNtextBox.Clear();
            PMtextBox.Focus();
        }

        /// <summary>
        /// 窗口关闭时，自动清理资源、恢复标志位
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _mainForm.IsAdvancedWriting = false;
            StopWriteTimeoutTimer();
            base.OnFormClosed(e);
        }
    }
}