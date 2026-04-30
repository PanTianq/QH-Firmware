using System;
using System.Drawing;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
// 自动发送定时器：握手 → 验证应答 → 定时获取信息
namespace QH_Firmware
{
    /// <summary>
    /// 自动发送定时器（握手 → 验证应答 → 定时获取信息）
    /// </summary>
    public class AutoSendTimer
    {
        private readonly Timer _timer;
        private readonly SerialCommunication _serialComm;
        private readonly ProtocolLoading _protocolLoader;
        private readonly LogOutput _logOutput;

        private bool _isHandshakeSuccess;


        // 保存握手所需的参数
        private string _deviceModel;
        private string _firmwareRegion;

        public AutoSendTimer(SerialCommunication serialComm, ProtocolLoading protocolLoader, LogOutput logOutput)
        {
            _serialComm = serialComm;
            _protocolLoader = protocolLoader;
            _logOutput = logOutput;

            _timer = new Timer();
            _timer.Tick += OnTimerTick;
            _isHandshakeSuccess = false;
        }

        /// <summary>
        /// 启动握手流程，并传入参数
        /// </summary>
        public void StartHandshake(string deviceModel, string firmwareRegion)
        {
            Stop();
            _isHandshakeSuccess = false;

            // 保存传入的参数
            _deviceModel = deviceModel;
            _firmwareRegion = firmwareRegion;

            int interval = _protocolLoader.Interval_Handshake > 0 ? _protocolLoader.Interval_Handshake : 1000;
            _timer.Interval = interval;
            _timer.Start();

            //_logOutput.Append("[自动流程] 开始握手...", Color.Cyan);
        }

        /// <summary>
        /// 握手成功，切换到获取设备信息模式
        /// </summary>
        public void SwitchToGetInfoMode()
        {
            if (_isHandshakeSuccess) return;

            // 标记握手成功
            _isHandshakeSuccess = true;

            //_logOutput.Append("[系统] 握手成功 → 切换获取设备信息", Color.LimeGreen);

            // 2. 使用协议中的 Interval_GetInfo 启动定时发送
            int interval = _protocolLoader.Interval_GetInfo > 0 ? _protocolLoader.Interval_GetInfo : 1000;
            _timer.Interval = interval;
            _timer.Start();
        }

        /// <summary>
        /// 停止所有自动发送
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>
        /// 定时器核心发送逻辑
        /// </summary>
        private void OnTimerTick(object sender, EventArgs e)
        {
            if (!_serialComm.IsOpen)
            {
                Stop();
                return;
            }

            if (!_isHandshakeSuccess)
            {
                // 发送握手指令
                if (!string.IsNullOrEmpty(_protocolLoader.Cmd_Handshake))
                {

                    // 从类的私有字段获取参数，不再直接访问UI控件
                    // 【只发握手】
                    string finalCmd = _protocolLoader.Cmd_Handshake
                        .Replace("{device_model}", _deviceModel)
                        .Replace("{firmware_region}", _firmwareRegion)
                        +"\r\n";

                    _serialComm.SendString(finalCmd);
                }
            }
            else
            {
                // 【握手成功后 → 只发获取信息，再也不发握手】
                _serialComm.SendString(_protocolLoader.Cmd_GetInfo);
            }
        }

        /// <summary>
        /// 验证接收的字符串是否为握手成功应答（严格大小写 + 清除空白字符）
        /// </summary>
        public bool CheckHandshakeAck(string recvData)
        {
            if (_isHandshakeSuccess) return true;
            if (string.IsNullOrEmpty(_protocolLoader.Ack_Handshake)) return false;

            try
            {
                // 1. 生成【预期的正确应答】
                string expectedAck = _protocolLoader.Ack_Handshake
                    .Replace("{device_model}", _deviceModel)
                    .Replace("{firmware_region}", _firmwareRegion);

                // 2. 清除所有空白：空格、换行、回车、制表符
                string recvClean = RemoveAllWhitespace(recvData);
                string expectClean = RemoveAllWhitespace(expectedAck);

                // 3. 严格大小写对比
                bool isMatch = recvClean.Equals(expectClean);

                if (isMatch)
                {
                    _logOutput.Append("[握手] 验证成功 → 准备获取设备信息", Color.LimeGreen);
                }

                return isMatch;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清除所有空白字符（空格、\r、\n、\t）
        /// </summary>
        private string RemoveAllWhitespace(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace(" ", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "");
        }


    }
}