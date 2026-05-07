using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace QH_Firmware
{
    public class Floading
    {
        private const int PACKET_SIZE = 1024;
        public byte[] FirmwareData { get; private set; }
        public string FileName { get; private set; }
        public int FileSize => FirmwareData?.Length ?? 0;
        public int TotalPackets
        {
            get
            {
                if (FirmwareData == null || FirmwareData.Length == 0) return 0;
                return (FirmwareData.Length + PACKET_SIZE - 1) / PACKET_SIZE;
            }
        }
        public uint Checksum { get; private set; }

        private readonly SerialCommunication _serialComm;
        private readonly ProtocolLoading _protocolLoader;
        private readonly LogOutput _logOutput;
        private int _currentPacketIndex;

        public event Action<int> OnProgressChanged;

        // 状态标记：是否收到设备【准备就绪应答】
        private bool _deviceReadyResponded;

        public Floading(SerialCommunication serialComm, ProtocolLoading protocolLoader, LogOutput logOutput)
        {
            _serialComm = serialComm;
            _protocolLoader = protocolLoader;
            _logOutput = logOutput;
            _deviceReadyResponded = false;
        }

        // ======================
        /// 加载固件（会自动发送【设备准备就绪】）
        // ======================
        public bool LoadFirmware(string filePath, string area, string deviceModel)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".bin" && ext != ".hex")
                {
                    _logOutput.Append("仅支持 .bin / .hex 文件", Color.Orange);
                    return false;
                }

                string fileName = Path.GetFileName(filePath);
                if (area == "应用程序(R1)" && !string.IsNullOrWhiteSpace(deviceModel))
                {
                    string model = deviceModel.Trim();
                    if (!fileName.Contains(model))
                    {
                        _logOutput.Append($"R1 固件必须包含型号：{model}", Color.Orange);
                        return false;
                    }
                }

                FirmwareData = File.ReadAllBytes(filePath);
                FileName = fileName;
                Checksum = CalculateChecksum(FirmwareData);
                _deviceReadyResponded = false;

                _logOutput.Append($"固件加载成功：{FileName} | 大小：{FileSize} 字节 | 总包：{TotalPackets}", Color.LimeGreen);

                // ======================
                // 自动发送【设备准备就绪】
                // ======================
                SendDeviceReadyCommand();

                return true;
            }
            catch
            {
                _logOutput.Append("固件加载失败", Color.Orange);
                return false;
            }
        }

        // ======================
        // 发送：设备准备就绪（升级开始帧）
        // ======================
        public void SendDeviceReadyCommand()
        {
            try
            {
                string prefix = _protocolLoader.Cmd_FirmwareHeader.Split('{')[0].Trim();
                string sendCmd = $"{prefix} {TotalPackets} 0x{Checksum:X8}";
                _serialComm.SendString(sendCmd);
            }
            catch
            {
                _logOutput.Append("设备未就绪", Color.Orange);
            }
        }

        // ======================
        // 开始烧录
        // ======================
        public void StartUpgrade()
        {
            if (FirmwareData == null)
            {
                _logOutput.Append("请先加载固件", Color.Orange);
                return;
            }
            if (!_deviceReadyResponded)
            {
                _logOutput.Append("等待设备准备就绪...", System.Drawing.Color.Orange);
                return;
            }
            _currentPacketIndex = 0;
            OnProgressChanged?.Invoke(0);
            SendNextPacket();
        }

        // ======================
        // 发送下一包
        // ======================
        private void SendNextPacket()
        {
            if (_currentPacketIndex >= TotalPackets)
            {
                _logOutput.Append("固件上传全部完成！", Color.LimeGreen);
                OnProgressChanged?.Invoke(100);
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

            int progress = (int)((_currentPacketIndex + 1) * 100.0 / TotalPackets);
            OnProgressChanged?.Invoke(progress);
        }

        // ======================
        // 处理升级应答
        // ======================
        public void HandleUpgradeResponse(byte[] buffer)
        {
            string recv = Encoding.ASCII.GetString(buffer).Trim();
            string clean = recv.Replace(" ", "");
            if (!_deviceReadyResponded)
            {
                string ackReady = _protocolLoader.Ack_FirmwareHeader.Split('{')[0].Trim().Replace(" ", "");
                string expectReady = $"{ackReady}{TotalPackets}0";

                if (clean.Contains(expectReady))
                {
                    _deviceReadyResponded = true;
                    _logOutput.Append("设备准备就绪，等待上传...", System.Drawing.Color.LimeGreen);
                    return;
                }
            }
            if (_deviceReadyResponded)
            {
                int pkgNo = _currentPacketIndex + 1;
                int pkgLen = (_currentPacketIndex == TotalPackets - 1)
                    ? FirmwareData.Length - _currentPacketIndex * PACKET_SIZE
                    : PACKET_SIZE;

                string ack = _protocolLoader.Ack_FirmwareHeader.Split('{')[0].Trim().Replace(" ", ""); ;
                string expected = $"{ack}{TotalPackets}{pkgNo}ok";

                if (clean.Contains(expected))
                {
                    _currentPacketIndex++;
                    SendNextPacket();
                }
            }
        }

        private uint CalculateChecksum(byte[] data)
        {
            uint sum = 0;
            foreach (byte b in data) sum += b;
            return sum;
        }

        public void Clear()
        {
            FirmwareData = null;
            FileName = string.Empty;
            Checksum = 0;
            _currentPacketIndex = 0;
            _deviceReadyResponded = false;
        }
    }
}