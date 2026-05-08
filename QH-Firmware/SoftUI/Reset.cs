using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace QH_Firmware.Other_UI
{
    /// <summary>
    /// 设备重启窗口
    /// 功能：软重启（指令重启，带20秒超时监听）、断电重启（手动提示）
    /// </summary>
    public partial class Reset : Form
    {
        // 串口通信对象
        private readonly SerialCommunication _serialComm;
        // 日志输出工具类
        private readonly LogOutput _logOutput;
        // 协议加载工具类
        private readonly ProtocolLoading _protocolLoader;

        // 软重启超时定时器（20秒）
        private System.Windows.Forms.Timer _rebootTimeoutTimer;
        private const int REBOOT_TIMEOUT_MS = 20000;

        /// <summary>
        /// 构造函数
        /// </summary>
        public Reset(SerialCommunication serialComm, LogOutput logOutput, ProtocolLoading protocolLoader)
        {
            InitializeComponent();
            _serialComm = serialComm;
            _logOutput = logOutput;
            _protocolLoader = protocolLoader;
        }

        /// <summary>
        /// 窗口加载（无初始化逻辑）
        /// </summary>
        private void Reset_Load(object sender, EventArgs e)
        {
        }

        /// <summary>
        /// 软重启按钮（发送重启指令，等待设备回复，20秒超时）
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            // 校验串口是否已打开
            if (_serialComm == null || !_serialComm.IsOpen)
            {
                MessageBox.Show("请先打开串口！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 校验协议是否加载完成
            if (_protocolLoader == null || string.IsNullOrEmpty(_protocolLoader.Cmd_Reboot))
            {
                MessageBox.Show("协议未加载！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 输出提示日志
            _logOutput.Append("软重启中，请等待...", Color.LimeGreen);

            // 发送软重启协议指令
            string cmd = _protocolLoader.Cmd_Reboot;
            _serialComm.SendString(cmd);

            // 注册串口数据监听，等待设备回复
            _serialComm.DataReceived += ResponseHandler;

            // 启动20秒超时定时器
            StartRebootTimeoutTimer();
        }

        /// <summary>
        /// 监听软重启设备回复
        /// </summary>
        private void ResponseHandler(byte[] buffer)
        {
            try
            {
                // 解析设备返回数据
                string recv = Encoding.ASCII.GetString(buffer).Trim();
                string expectAck = _protocolLoader.Ack_Reboot?.Trim();

                // 判断是否收到正确的重启成功应答
                if (!string.IsNullOrEmpty(expectAck) && recv.Contains(expectAck))
                {
                    // 注销监听，停止定时器
                    _serialComm.DataReceived -= ResponseHandler;
                    StopRebootTimeoutTimer();

                    // 跨线程更新日志并关闭窗口
                    Invoke((Action)delegate
                    {
                        _logOutput.Append("软重启成功", Color.LimeGreen);
                        this.Close();
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// 启动20秒超时定时器
        /// </summary>
        private void StartRebootTimeoutTimer()
        {
            // 先清理旧定时器
            StopRebootTimeoutTimer();

            _rebootTimeoutTimer = new System.Windows.Forms.Timer();
            _rebootTimeoutTimer.Interval = REBOOT_TIMEOUT_MS;
            _rebootTimeoutTimer.Tick += (s, args) =>
            {
                // 超时后清理资源
                StopRebootTimeoutTimer();
                _serialComm.DataReceived -= ResponseHandler;

                // 输出超时日志并关闭窗口
                Invoke((Action)delegate
                {
                    _logOutput.Append("[超时] 软重启等待响应超时（20s）", Color.Orange);
                    this.Close();
                });
            };
            _rebootTimeoutTimer.Start();
        }

        /// <summary>
        /// 停止并销毁超时定时器
        /// </summary>
        private void StopRebootTimeoutTimer()
        {
            if (_rebootTimeoutTimer != null)
            {
                _rebootTimeoutTimer.Stop();
                _rebootTimeoutTimer.Dispose();
                _rebootTimeoutTimer = null;
            }
        }

        /// <summary>
        /// 断电重启按钮（提示手动操作）
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            _logOutput.Append("请关闭软件串口后，手动断电重启！", Color.LimeGreen);
            this.Close();
        }

        /// <summary>
        /// 窗口关闭时，自动清理定时器和监听事件，防止内存泄漏
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopRebootTimeoutTimer();
            _serialComm.DataReceived -= ResponseHandler;
            base.OnFormClosed(e);
        }
    }
}