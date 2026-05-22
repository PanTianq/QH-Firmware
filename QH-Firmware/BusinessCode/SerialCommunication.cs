using System;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace QH_Firmware
{
    /// <summary>
    /// 串口通信管理类
    /// 功能：高性能独立线程收发、低延迟、不卡顿、联机状态管理、超时自动断开
    /// </summary>
    public class SerialCommunication : IDisposable
    {
        #region 私有变量
        /// <summary>
        /// 串口实例
        /// </summary>
        private readonly SerialPort _serialPort = new SerialPort();

        /// <summary>
        /// 接收线程
        /// </summary>
        private Thread _receiveThread;

        /// <summary>
        /// 接收线程运行标志
        /// </summary>
        private bool _isReceiving;

        /// <summary>
        /// 设备联机状态（true=已成功联机，不再触发20s超时）
        /// </summary>
        private bool _isOnline;

        /// <summary>
        /// 最后一次接收数据时间
        /// </summary>
        private DateTime _lastReceiveTime;

        /// <summary>
        /// 未联机时接收超时时间（20秒）
        /// </summary>
        private const int ReceiveTimeoutSeconds = 20;
        #endregion

        #region 公共属性
        /// <summary>
        /// 公开串口对象
        /// </summary>
        public SerialPort SerialPort => _serialPort;

        /// <summary>
        /// 串口是否打开
        /// </summary>
        public bool IsOpen => _serialPort.IsOpen;

        /// <summary>
        /// 全局指令应答超时（用于固件升级）
        /// </summary>
        public const int GLOBAL_COMMAND_ACK_TIMEOUT = 20;

        /// <summary>
        /// 设备联机状态（外部可读写）
        /// </summary>
        public bool IsOnline
        {
            get => _isOnline;
            set => _isOnline = value;
        }
        #endregion

        #region 外部事件
        /// <summary>
        /// 日志输出事件
        /// </summary>
        public event Action<string, Color> LogReceived;

        /// <summary>
        /// 串口状态变化事件
        /// </summary>
        public event Action<bool> PortStateChanged;

        /// <summary>
        /// 数据接收事件
        /// </summary>
        public event Action<byte[]> DataReceived;
        #endregion

        public SerialCommunication()
        {
            // 禁用系统自带 DataReceived，使用独立线程保证低延迟
        }

        #region 打开 / 关闭串口
        /// <summary>
        /// 打开串口（自动启动高速接收线程）
        /// </summary>
        public bool Open(string portName, int baudRate)
        {
            try
            {
                // 已打开则先关闭
                if (_serialPort.IsOpen)
                    _serialPort.Close();

                // 基础通信参数
                _serialPort.PortName = portName;
                _serialPort.BaudRate = baudRate;
                _serialPort.DataBits = 8;
                _serialPort.StopBits = StopBits.One;
                _serialPort.Parity = Parity.None;
                _serialPort.Handshake = Handshake.None;

                // 超时设置（提高实时性）
                _serialPort.ReadTimeout = 50;
                _serialPort.WriteTimeout = 50;

                _serialPort.Open();

                // 重置状态
                _isOnline = false;
                _lastReceiveTime = DateTime.Now;

                // 启动独立接收线程
                StartReceiveThread();

                // 通知状态变化
                PortStateChanged?.Invoke(true);
                LogReceived?.Invoke($"[{portName}] 打开成功，波特率：{baudRate}", Color.LimeGreen);
                return true;
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 打开串口失败：{ex.Message}", Color.Orange);
                return false;
            }
        }

        /// <summary>
        /// 关闭串口（自动停止接收线程）
        /// </summary>
        public void Close()
        {
            try
            {
                StopReceiveThread();

                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                    PortStateChanged?.Invoke(false);
                }
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 关闭串口失败：{ex.Message}", Color.Orange);
            }
        }
        #endregion

        #region 发送数据
        /// <summary>
        /// 发送字节数组（异步非阻塞）
        /// </summary>
        public void Send(byte[] data)
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    LogReceived?.Invoke("[错误] 串口未打开，无法发送", Color.Orange);
                    return;
                }
                string sendStr = Encoding.ASCII.GetString(data);
                LogReceived?.Invoke($"[发送] {sendStr}", Color.Cyan);
                _serialPort.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[错误] 发送数据失败：{ex.Message}", Color.Orange);
                Close();
            }
        }

        /// <summary>
        /// 发送ASCII字符串
        /// </summary>
        public void SendString(string str)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(str);
            Send(bytes);
        }
        #endregion

        #region 独立线程接收（核心高性能）
        /// <summary>
        /// 启动高优先级接收线程
        /// </summary>
        private void StartReceiveThread()
        {
            StopReceiveThread();

            _isReceiving = true;
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
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
        /// 独立接收循环（实时性极强，不阻塞UI）
        /// 规则：未联机 → 20秒无数据自动断开；已联机 → 不触发超时
        /// </summary>
        private void ReceiveLoop()
        {
            _lastReceiveTime = DateTime.Now;

            while (_isReceiving && _serialPort.IsOpen)
            {
                try
                {
                    // 无数据时判断超时
                    if (_serialPort.BytesToRead == 0)
                    {
                        // 未联机状态下 20s 无数据则断开
                        if (!_isOnline && (DateTime.Now - _lastReceiveTime).TotalSeconds > ReceiveTimeoutSeconds)
                        {
                            LogReceived?.Invoke("[超时] 20秒未接收数据，已关闭串口", Color.Orange);
                            Close();
                            break;
                        }

                        Thread.Sleep(1);
                        continue;
                    }

                    // 读取所有缓存数据
                    _lastReceiveTime = DateTime.Now;
                    byte[] buffer = new byte[_serialPort.BytesToRead];
                    int readLen = _serialPort.Read(buffer, 0, buffer.Length);

                    if (readLen > 0)
                    {
                        string recvStr = Encoding.ASCII.GetString(buffer, 0, readLen);
                        LogReceived?.Invoke($"[接收] {recvStr}", Color.Blue);
                        DataReceived?.Invoke(buffer);
                    }
                }
                catch (TimeoutException)
                {
                    // 正常读取超时，忽略
                }
                catch (Exception ex)
                {
                    if (_isReceiving)
                        LogReceived?.Invoke($"[接收异常] {ex.Message}", Color.Orange);
                    break;
                }
            }
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 获取系统可用串口列表
        /// </summary>
        public static string[] GetPortNames() => SerialPort.GetPortNames();
        #endregion

        #region 释放资源
        public void Dispose()
        {
            Close();
            _serialPort?.Dispose();
        }
        #endregion
    }
}