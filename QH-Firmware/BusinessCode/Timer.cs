using System;
using System.Drawing;
using System.Windows.Forms;

namespace QH_Firmware
{
    /// <summary>
    /// 自动发送定时器
    /// 功能：握手流程 → 验证应答 → 定时获取设备信息
    /// </summary>
    public class AutoSendTimer
    {
        #region 私有变量
        /// <summary>
        /// 定时发送组件
        /// </summary>
        private readonly Timer _timer;

        /// <summary>
        /// 串口通信对象
        /// </summary>
        private readonly SerialCommunication _serialComm;

        /// <summary>
        /// 协议加载对象
        /// </summary>
        private readonly ProtocolLoading _protocolLoader;

        /// <summary>
        /// 日志输出对象
        /// </summary>
        private readonly LogOutput _logOutput;

        /// <summary>
        /// 握手是否成功
        /// </summary>
        private bool _isHandshakeSuccess;

        /// <summary>
        /// 设备型号（握手参数）
        /// </summary>
        private string _deviceModel;

        /// <summary>
        /// 固件区域（握手参数）
        /// </summary>
        private string _firmwareRegion;
        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数：注入依赖
        /// </summary>
        public AutoSendTimer(SerialCommunication serialComm, ProtocolLoading protocolLoader, LogOutput logOutput)
        {
            _serialComm = serialComm;
            _protocolLoader = protocolLoader;
            _logOutput = logOutput;

            _timer = new Timer();
            _timer.Tick += OnTimerTick;
            _isHandshakeSuccess = false;
        }
        #endregion

        #region 启动 / 停止
        /// <summary>
        /// 启动握手流程
        /// </summary>
        /// <param name="deviceModel">设备型号</param>
        /// <param name="firmwareRegion">固件区域</param>
        public void StartHandshake(string deviceModel, string firmwareRegion)
        {
            Stop();
            _isHandshakeSuccess = false;

            // 保存握手参数
            _deviceModel = deviceModel;
            _firmwareRegion = firmwareRegion;

            // 设置握手间隔
            int interval = _protocolLoader.Interval_Handshake > 0 ? _protocolLoader.Interval_Handshake : 1000;
            _timer.Interval = interval;
            _timer.Start();
        }

        /// <summary>
        /// 握手成功，切换到定时获取设备信息模式
        /// </summary>
        public void SwitchToGetInfoMode()
        {
            if (_isHandshakeSuccess)
                return;

            _isHandshakeSuccess = true;
            _logOutput.Append("握手成功", Color.LimeGreen);

            // 设置获取信息的间隔
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
        #endregion

        #region 定时器核心逻辑
        /// <summary>
        /// 定时器触发：未握手 → 发握手；已握手 → 发获取信息
        /// </summary>
        private void OnTimerTick(object sender, EventArgs e)
        {
            // 串口未打开则停止
            if (!_serialComm.IsOpen)
            {
                Stop();
                return;
            }

            if (!_isHandshakeSuccess)
            {
                // 发送握手指令（替换参数）
                if (!string.IsNullOrEmpty(_protocolLoader.Cmd_Handshake))
                {
                    string finalCmd = _protocolLoader.Cmd_Handshake
                        .Replace("{device_model}", _deviceModel)
                        .Replace("{firmware_region}", _firmwareRegion) + "\r\n";

                    _serialComm.SendString(finalCmd);
                }
            }
            else
            {
                // 握手成功后，定时获取设备信息
                _serialComm.SendString(_protocolLoader.Cmd_GetInfo);
            }
        }
        #endregion

        #region 握手应答验证
        /// <summary>
        /// 验证接收数据是否匹配握手应答（清除空白 + 严格匹配）
        /// </summary>
        public bool CheckHandshakeAck(string recvData)
        {
            if (_isHandshakeSuccess)
                return true;

            if (string.IsNullOrEmpty(_protocolLoader.Ack_Handshake))
                return false;

            try
            {
                // 生成预期应答（替换参数）
                string expectedAck = _protocolLoader.Ack_Handshake
                    .Replace("{device_model}", _deviceModel)
                    .Replace("{firmware_region}", _firmwareRegion);

                // 清除空白字符后严格对比
                string recvClean = RemoveAllWhitespace(recvData);
                string expectClean = RemoveAllWhitespace(expectedAck);

                return recvClean.Equals(expectClean);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清除所有空白字符：空格、回车、换行、制表符
        /// </summary>
        private string RemoveAllWhitespace(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            return str.Replace(" ", "")
                      .Replace("\r", "")
                      .Replace("\n", "")
                      .Replace("\t", "");
        }
        #endregion
    }
}