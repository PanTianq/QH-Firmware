using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace QH_Firmware.Other_UI
{
    public partial class Reset : Form
    {
        // 从主窗体传进来的对象
        private readonly SerialCommunication _serialComm;
        private readonly LogOutput _logOutput;
        private readonly ProtocolLoading _protocolLoader;

        // ✅ 构造函数传入 3 个对象（你用到了 protocolLoader）
        public Reset(SerialCommunication serialComm, LogOutput logOutput, ProtocolLoading protocolLoader)
        {
            InitializeComponent();
            _serialComm = serialComm;
            _logOutput = logOutput;
            _protocolLoader = protocolLoader;
        }

        private void Reset_Load(object sender, EventArgs e)
        {
        }

        // 软重启
        private void button1_Click(object sender, EventArgs e)
        {
            // 串口判断
            if (_serialComm == null || !_serialComm.IsOpen)
            {
                MessageBox.Show("请先打开串口！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 协议空判断（防止没加载协议）
            if (_protocolLoader == null || string.IsNullOrEmpty(_protocolLoader.Cmd_Reboot))
            {
                MessageBox.Show("协议未加载！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 发送重启指令
            string cmd = _protocolLoader.Cmd_Reboot;
            _serialComm.SendString(cmd);

            // 临时监听回复
            void ResponseHandler(byte[] buffer)
            {
                try
                {
                    string recv = Encoding.ASCII.GetString(buffer).Trim();
                    string expectAck = _protocolLoader.Ack_Reboot?.Trim();

                    // 匹配回复
                    if (!string.IsNullOrEmpty(expectAck) && recv.Contains(expectAck))
                    {
                        // 用完立即注销，防止冲突
                        _serialComm.DataReceived -= ResponseHandler;

                        // UI 线程更新日志
                        Invoke((Action)delegate
                        {
                            _logOutput.Append("软重启成功", Color.LimeGreen);
                            this.Close();
                        });
                    }
                }
                catch { }
            }

            // 注册监听
            _serialComm.DataReceived += ResponseHandler;
        }

        // 断电重启
        private void button2_Click(object sender, EventArgs e)
        {
            _logOutput.Append("请关闭软件串口后，手动断电重启！", Color.LimeGreen);
            MessageBox.Show("请关闭软件串口后，手动断电重启！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }
}