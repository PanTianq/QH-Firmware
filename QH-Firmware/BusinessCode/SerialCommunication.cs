using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

// 串口通信封装类（高性能异步收发，接收响应极快）
namespace QH_Firmware
{
    /// <summary>
    /// 串口通信管理类：高性能、异步收发、接收低延迟
    /// </summary>
    public class SerialCommunication : IDisposable
    {
        // 串口对象（私有封装 + 公开访问）
        private readonly SerialPort _serialPort = new SerialPort();
        public SerialPort SerialPort => _serialPort;

        // 串口状态
        public bool IsOpen => _serialPort.IsOpen;

        // 外部事件
        public event Action<string, Color> LogReceived;
        public event Action<bool> PortStateChanged;
        public event Action<byte[]> DataReceived;

        // 接收独立线程（保证响应速度，不被UI/发送阻塞）
        private Thread _receiveThread;
        private bool _isReceiving;

        public SerialCommunication()
        {
            // 禁用系统自带DataReceived事件（不稳定、延迟高）
            // 使用独立线程接收 → 速度提升数倍，适合协议判断
        }

        /// <summary>
        /// 打开串口（独立接收线程启动）
        /// </summary>
        public bool Open(string portName, int baudRate)
        {
            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();

                // 基础参数
                _serialPort.PortName = portName;
                _serialPort.BaudRate = baudRate;
                _serialPort.DataBits = 8;
                _serialPort.StopBits = StopBits.One;
                _serialPort.Parity = Parity.None;
                _serialPort.Handshake = Handshake.None;

                // 关键：关闭系统同步阻塞，提高实时性
                _serialPort.ReadTimeout = 50;
                _serialPort.WriteTimeout = 50;

                _serialPort.Open();

                // 启动独立高速接收线程
                StartReceiveThread();

                PortStateChanged?.Invoke(true);
                //LogReceived?.Invoke("打开串口成功", Color.LimeGreen);
                return true;
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 打开串口失败: {GetErrorMessageInChinese(ex)}", Color.Orange);
                return false;
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void Close()
        {
            try
            {
                StopReceiveThread();

                if (_serialPort.IsOpen)
                    _serialPort.Close();

                PortStateChanged?.Invoke(false);
                //LogReceived?.Invoke("关闭串口成功", Color.LimeGreen);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 关闭串口失败: {GetErrorMessageInChinese(ex)}", Color.Orange);
            }
        }

        /// <summary>
        /// 发送数据（异步非阻塞，不影响接收）
        /// </summary>
        public void Send(byte[] data)
        {
            if (!SerialPort.IsOpen)
                throw new InvalidOperationException("串口未打开");

            //// -------------------- 【发送字符串日志】 --------------------
            //string sendText = System.Text.Encoding.UTF8.GetString(data);
            //LogReceived?.Invoke($"[发送] {sendText}", Color.Cyan);
            //// -----------------------------------------------------------------

            SerialPort.Write(data, 0, data.Length);
        }
        /// <summary>
        /// 发送字符串数据
        /// </summary>
        public void SendString(string str)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(str);
            Send(bytes); // 调用原来的Send方法
        }
        /// <summary>
        /// 启动独立高速接收线程
        /// 优点：响应极快、不卡顿、不丢包、收发互不影响
        /// </summary>
        private void StartReceiveThread()
        {
            StopReceiveThread();

            _isReceiving = true;
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal // 提高优先级 → 协议判断更及时
            };
            _receiveThread.Start();
        }

        /// <summary>
        /// 停止接收线程
        /// </summary>
        private void StopReceiveThread()
        {
            _isReceiving = false;
            _receiveThread?.Join(100);
            _receiveThread = null;
        }

        /// <summary>
        /// 独立循环高速接收（核心：实时性极强）
        /// </summary>
        private void ReceiveLoop()
        {
            while (_isReceiving && _serialPort.IsOpen)
            {
                try
                {
                    int bytesToRead = _serialPort.BytesToRead;
                    if (bytesToRead <= 0)
                    {
                        Thread.Sleep(1); // 极低延迟，不占CPU
                        continue;
                    }

                    byte[] buffer = new byte[bytesToRead];
                    int read = _serialPort.Read(buffer, 0, buffer.Length);

                    if (read > 0)
                    {
                        // -------------------- 【接收字符串日志】 --------------------
                        string recvText = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                        LogReceived?.Invoke($"[接收] {recvText}", Color.White);
                        // -----------------------------------------------------------------

                        DataReceived?.Invoke(buffer);
                    }
                }
                catch (TimeoutException)
                {
                    // 正常，无数据时触发，不处理
                }
                catch (Exception ex)
                {
                    if (_isReceiving)
                        LogReceived?.Invoke($"接收异常: {ex.Message}", Color.Orange);

                    break;
                }
            }
        }

        /// <summary>
        /// 中文错误提示
        /// </summary>
        private string GetErrorMessageInChinese(Exception ex)
        {
            if (ex is IOException ioEx && (uint)ioEx.HResult == 0x80070020)
                return "串口被其他程序占用";
            if (ex is UnauthorizedAccessException)
                return "无权限操作串口";
            if (ex is ArgumentException && ex.Message.Contains("PortName"))
                return "串口名称无效";
            if (ex is TimeoutException)
                return "操作超时";
            return $"未知错误：{ex.Message}";
        }

        public static string[] GetPortNames() => SerialPort.GetPortNames();

        public void Dispose()
        {
            Close();
            _serialPort?.Dispose();
        }
    }
}