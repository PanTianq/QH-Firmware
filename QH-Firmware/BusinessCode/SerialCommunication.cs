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

                // 每次打开串口，重置状态
                _isOnline = false;
                _lastReceiveTime = DateTime.Now;

                // 启动独立高速接收线程
                StartReceiveThread();

                PortStateChanged?.Invoke(true);
                return true;
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 打开串口失败: {ex.Message}", Color.Orange);
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
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 关闭串口失败: {ex.Message}", Color.Orange);
            }
        }

        /// <summary>
        /// 发送数据（异步非阻塞，不影响接收）
        /// </summary>
        public void Send(byte[] data)
        {
            try
            {
                if (!SerialPort.IsOpen)
                    throw new InvalidOperationException("串口未打开");

                //// -------------------- 【发送字符串日志】 --------------------
                //string sendText = System.Text.Encoding.UTF8.GetString(data);
                //LogReceived?.Invoke($"[发送] {sendText}", Color.Cyan);
                //// -----------------------------------------------------------------
                SerialPort.Write(data, 0, data.Length);
                
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 发送数据失败: {ex.Message}", Color.Orange);
                Close();
                return;
            }

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
        private bool _isOnline = false;  // 联机状态：true=解析完成后永久在线
        // 给外部提供读写接口
        public bool IsOnline
        {
            get { return _isOnline; }
            set { _isOnline = value; }
        }
        private DateTime _lastReceiveTime = DateTime.Now;
        private const int ReceiveTimeoutSeconds = 20;//20s超时断开（仅未联机时）                                          
        public const int GLOBAL_COMMAND_ACK_TIMEOUT = 20; // 发送后等待应答超时，用在固件升级中
        private bool _deviceInfoReceived = false;  // 开关

        private void ReceiveLoop()
        {
            _lastReceiveTime = DateTime.Now;

            while (_isReceiving && _serialPort.IsOpen)
            {
                try
                {
                    if (_serialPort.BytesToRead == 0)
                    {
                        if (_serialPort.BaseStream.ReadTimeout > 0)
                            _serialPort.BaseStream.ReadTimeout = 10;

                        // 核心修改：只有未联机时，才判断20秒超时
                        if (!_isOnline &&
                            (DateTime.Now - _lastReceiveTime).TotalSeconds > ReceiveTimeoutSeconds)
                        {
                            LogReceived?.Invoke($"[超时] 20秒未接收数据，连接已断开", Color.Orange);
                            Close();
                            break;
                        }

                        continue;
                    }

                    _lastReceiveTime = DateTime.Now;

                    byte[] buffer = new byte[_serialPort.BytesToRead];
                    int read = _serialPort.Read(buffer, 0, buffer.Length);

                    if (read > 0)
                    {
                        //string recvText = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                        //LogReceived?.Invoke($"[接收] {recvText}", Color.White);
                        DataReceived?.Invoke(buffer);
                    }
                }
                catch (TimeoutException)
                {
                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    if (_isReceiving)
                        LogReceived?.Invoke($"接收异常: {ex.Message}", Color.Orange);
                    break;
                }
            }
        }
        public static string[] GetPortNames() => SerialPort.GetPortNames();

        public void Dispose()
        {
            Close();
            _serialPort?.Dispose();
        }
    }
}