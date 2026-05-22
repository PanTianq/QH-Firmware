using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QH_Firmware
{
    /// <summary>
    /// 固件升级核心逻辑类
    /// 功能：文件加载、分包发送、超时检测、应答处理、进度更新
    /// </summary>
    public class Floading
    {
        // 固件分包发送的单包大小（1024字节）
        private const int PACKET_SIZE = 1024;

        // 固件文件二进制数据
        public byte[] FirmwareData { get; private set; }

        // 固件文件名
        public string FileName { get; private set; }

        // 固件文件总长度
        public int FileSize => FirmwareData?.Length ?? 0;

        // 固件总包数（根据总长度自动计算）
        public int TotalPackets
        {
            get
            {
                if (FirmwareData == null || FirmwareData.Length == 0)
                    return 0;

                return (FirmwareData.Length + PACKET_SIZE - 1) / PACKET_SIZE;
            }
        }

        // CRC16-CCITT 校验值
        public ushort CRC16Value { get; private set; }

        // 串口通信对象
        private readonly SerialCommunication _serialComm;

        // 协议解析对象
        private readonly ProtocolLoading _protocolLoader;

        // 日志输出
        private readonly LogOutput _logOutput;

        // 当前正在发送的包索引
        private int _currentPacketIndex;

        // 当前升级的区域（应用程序/参数区）
        private string _currentArea;

        // 升级进度变化事件（通知UI更新进度条）
        public event Action<int> OnProgressChanged;

        // 设备是否准备就绪（收到就绪应答后为true）
        private bool _deviceReadyResponded;

        // 升级应答超时时间（20秒）
        private readonly int _ackTimeout = SerialCommunication.GLOBAL_COMMAND_ACK_TIMEOUT;

        // 最后一次发送数据的时间
        private DateTime _lastSendTime;

        // 是否已经发生超时错误
        private bool _isTimeoutError;

        // 超时检测任务的取消令牌（防止多任务重复打印）
        private CancellationTokenSource _cts;

        /// <summary>
        /// 构造函数
        /// </summary>
        public Floading(SerialCommunication serialComm, ProtocolLoading protocolLoader, LogOutput logOutput)
        {
            _serialComm = serialComm;
            _protocolLoader = protocolLoader;
            _logOutput = logOutput;
            _deviceReadyResponded = false;
            _isTimeoutError = false;
        }

        /// <summary>
        /// 开始等待设备应答
        /// 每次发送数据后必须调用
        /// </summary>
        private void StartWaitAck()
        {
            if (_isTimeoutError)
                return;

            StopWaitAck();
            _lastSendTime = DateTime.Now;
            _cts = new CancellationTokenSource();
            Task.Run(() => WaitAckTimeoutCheck(_cts.Token));
        }

        /// <summary>
        /// 停止等待应答
        /// </summary>
        private void StopWaitAck()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// 超时检测循环
        /// </summary>
        private async void WaitAckTimeoutCheck(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && !_isTimeoutError)
                {
                    await Task.Delay(500, token);

                    if ((DateTime.Now - _lastSendTime).TotalSeconds > _ackTimeout)
                    {
                        _isTimeoutError = true;
                        StopWaitAck();
                        _logOutput.Append($"[超时] 设备未应答，超时 {_ackTimeout} 秒！已自动关闭串口", Color.Orange);
                        _serialComm.Close();
                        return;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 根据操作类型和升级区域，返回对应的日志文本
        /// </summary>
        private string GetLogTip(string tipType)
        {
            string baseTip = "";
            if (_currentArea == "应用程序(R1)")
            {
                switch (tipType)
                {
                    case "LoadSuccess":
                        baseTip = $"固件加载成功：{FileName} | 大小：{FileSize} 字节 | 总包：{TotalPackets} | CRC16-CCITT: 0x{CRC16Value:X4}";
                        break;
                    case "BurnSuccess":
                        baseTip = "固件烧写成功！";
                        break;
                    case "LoadFail":
                        baseTip = "固件加载失败";
                        break;
                    default:
                        baseTip = "操作成功";
                        break;
                }
            }
            else if (_currentArea == "程序参数")
            {
                switch (tipType)
                {
                    case "LoadSuccess":
                        baseTip = $"参数文件加载成功：{FileName} | 大小：{FileSize} 字节 | 总包：{TotalPackets} | CRC16-CCITT: 0x{CRC16Value:X4}";
                        break;
                    case "BurnSuccess":
                        baseTip = "参数写入成功！";
                        break;
                    case "LoadFail":
                        baseTip = "参数文件加载失败";
                        break;
                    default:
                        baseTip = "操作成功";
                        break;
                }
            }
            else
            {
                switch (tipType)
                {
                    case "LoadSuccess":
                        baseTip = $"文件加载成功：{FileName} | 大小：{FileSize} 字节 | 总包：{TotalPackets} | CRC16-CCITT: 0x{CRC16Value:X4} ";
                        break;
                    case "BurnSuccess":
                        baseTip = "文件写入成功！";
                        break;
                    case "LoadFail":
                        baseTip = "文件加载失败";
                        break;
                    default:
                        baseTip = "操作成功";
                        break;
                }
            }
            return baseTip;
        }

        /// <summary>
        /// 加载固件/参数文件
        /// </summary>
        public bool LoadFirmware(string filePath, string area, string deviceModel)
        {
            try
            {
                _currentArea = area;
                _isTimeoutError = false;

                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".bin" && ext != ".hex")
                {
                    _logOutput.Append("仅支持 .bin / .hex 文件", Color.Orange);
                    return false;
                }

                string fileName = Path.GetFileName(filePath);
                if (area == "应用程序(R1)" && !string.IsNullOrWhiteSpace(deviceModel) && !fileName.Contains(deviceModel.Trim()))
                {
                    _logOutput.Append($"应用程序固件必须包含型号：{deviceModel.Trim()}", Color.Orange);
                    return false;
                }

                FirmwareData = File.ReadAllBytes(filePath);
                FileName = fileName;

                // ====================== 这里换成 CRC16-CCITT ======================
                CRC16Value = CRC16.CalculateCCITT(FirmwareData);

                _deviceReadyResponded = false;

                _logOutput.Append(GetLogTip("LoadSuccess"), Color.LimeGreen);
                SendDeviceReadyCommand();
                return true;
            }
            catch
            {
                _logOutput.Append(GetLogTip("LoadFail"), Color.Orange);
                return false;
            }
        }

        /// <summary>
        /// 发送设备就绪指令（通知设备准备升级）
        /// </summary>
        public void SendDeviceReadyCommand()
        {
            try
            {
                string prefix = _protocolLoader.Cmd_FirmwareHeader.Split('{')[0].Trim();
                // 发送 CRC16（4位16进制）
                string sendCmd = $"{prefix} {TotalPackets} 0x{CRC16Value:X4}";
                _serialComm.SendString(sendCmd);
                StartWaitAck();
            }
            catch
            {
                _logOutput.Append("设备未就绪", Color.Orange);
            }
        }

        /// <summary>
        /// 开始升级流程
        /// </summary>
        public void StartUpgrade()
        {
            if (_isTimeoutError || !_serialComm.IsOpen || FirmwareData == null)
                return;

            if (!_deviceReadyResponded)
            {
                _logOutput.Append("设备未就绪", Color.Orange);
                return;
            }

            _currentPacketIndex = 0;
            OnProgressChanged?.Invoke(0);
            SendNextPacket();
        }

        /// <summary>
        /// 发送下一包数据
        /// </summary>
        private void SendNextPacket()
        {
            if (_isTimeoutError || !_serialComm.IsOpen)
                return;

            if (_currentPacketIndex >= TotalPackets)
            {
                OnProgressChanged?.Invoke(100);
                SendUpdateInformCommand();
                return;
            }

            int start = _currentPacketIndex * PACKET_SIZE;
            int dataLen = Math.Min(PACKET_SIZE, FirmwareData.Length - start);
            byte[] firmwarePacket = new byte[dataLen];
            Array.Copy(FirmwareData, start, firmwarePacket, 0, dataLen);

            string loadhead = _protocolLoader.Cmd_FirmwareHeader.Split('{')[0].Trim();
            string header = $"{loadhead} {TotalPackets} {_currentPacketIndex + 1} {dataLen} ";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            byte[] sendData = new byte[headerBytes.Length + firmwarePacket.Length];
            Array.Copy(headerBytes, sendData, headerBytes.Length);
            Array.Copy(firmwarePacket, 0, sendData, headerBytes.Length, firmwarePacket.Length);

            _serialComm.Send(sendData);
            StartWaitAck();

            // ------------------------------
            // 【新增】发包进度日志（青色）
            // ------------------------------
            int pkgNo = _currentPacketIndex + 1;
            int progress = (int)(pkgNo * 100.0 / TotalPackets);
            _logOutput.Append($"[发包] 第 {pkgNo}/{TotalPackets} 包 | 大小：{dataLen} 字节 | 进度 {progress}%", Color.Cyan);

            OnProgressChanged?.Invoke(progress);
        }

        /// <summary>
        /// 处理设备返回的升级应答
        /// </summary>
        public void HandleUpgradeResponse(byte[] buffer)
        {
            if (_isTimeoutError || !_serialComm.IsOpen)
                return;

            string recv = Encoding.ASCII.GetString(buffer).Trim();
            string ackInform = _protocolLoader.Ack_SetInfo.Trim();

            if (recv.Contains(ackInform))
            {
                StopWaitAck();
                _logOutput.Append(GetLogTip("BurnSuccess"), Color.LimeGreen);
                return;
            }

            string clean = recv.Replace(" ", "");

            if (!_deviceReadyResponded)
            {
                string ackReady = _protocolLoader.Ack_FirmwareHeader.Split('{')[0].Trim().Replace(" ", "");
                string expectReady = $"{ackReady}{TotalPackets}0";

                if (clean.Contains(expectReady))
                {
                    _deviceReadyResponded = true;
                    _logOutput.Append("设备准备就绪，请点击[固化文件]", Color.LimeGreen);
                }
                return;
            }

            if (_deviceReadyResponded)
            {
                int pkgNo = _currentPacketIndex + 1;
                string ack = _protocolLoader.Ack_FirmwareHeader.Split('{')[0].Trim().Replace(" ", "");
                string expected = $"{ack}{TotalPackets}{pkgNo}ok";

                if (clean.Contains(expected))
                {
                    _currentPacketIndex++;
                    SendNextPacket();
                }
            }
        }

        /// <summary>
        /// 清空所有状态
        /// </summary>
        public void Clear()
        {
            FirmwareData = null;
            FileName = string.Empty;
            CRC16Value = 0;
            _currentPacketIndex = 0;
            _deviceReadyResponded = false;
            _isTimeoutError = false;
            StopWaitAck();
        }

        /// <summary>
        /// 发送更新信息
        /// </summary>
        private void SendUpdateInformCommand()
        {
            if (_isTimeoutError || !_serialComm.IsOpen || string.IsNullOrEmpty(_currentArea))
                return;

            try
            {
                string cmdHead = _protocolLoader.Cmd_SetInfo.Split('{')[0].Trim();
                string cmd;

                if (_currentArea == "应用程序(R1)")
                {
                    cmd = $"{cmdHead} {{#DEV_INFO:ABT={DateTime.Now:yyyy-MM-dd HH:mm:ss};}}";
                }
                else if (_currentArea == "程序参数")
                {
                    cmd = $"{cmdHead} {{#DEV_INFO:PFN={FileName};}}";
                }
                else
                {
                    cmd = $"{cmdHead} {{#DEV_INFO:ABT={DateTime.Now:yyyy-MM-dd HH:mm:ss};}}";
                }
                _serialComm.SendString(cmd);
                StartWaitAck();
            }
            catch
            {
                _logOutput.Append("发送更新信息失败", Color.Orange);
            }
        }
    }
}