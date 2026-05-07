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
        private string _currentArea;

        public event Action<int> OnProgressChanged;
        private bool _deviceReadyResponded;

        public Floading(SerialCommunication serialComm, ProtocolLoading protocolLoader, LogOutput logOutput)
        {
            _serialComm = serialComm;
            _protocolLoader = protocolLoader;
            _logOutput = logOutput;
            _deviceReadyResponded = false;
        }

        // ================================
        // 统一获取区域日志提示
        // ================================
        private string GetLogTip(string tipType)
        {
            string baseTip = "";

            if (_currentArea == "应用程序(R1)")
            {
                switch (tipType)
                {
                    case "LoadSuccess":
                        baseTip = $"固件加载成功：{FileName} | 大小：{FileSize} 字节 | 总包：{TotalPackets}";
                        break;
                    case "UploadComplete":
                        baseTip = "固件上传全部完成！";
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
                        baseTip = $"参数文件加载成功：{FileName} | 大小：{FileSize} 字节 | 总包：{TotalPackets}";
                        break;
                    case "UploadComplete":
                        baseTip = "参数文件上传全部完成！";
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
                        baseTip = $"文件加载成功：{FileName} | 大小：{FileSize} 字节 | 总包：{TotalPackets}";
                        break;
                    case "UploadComplete":
                        baseTip = "文件上传全部完成！";
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

        public bool LoadFirmware(string filePath, string area, string deviceModel)
        {
            try
            {
                _currentArea = area;
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".bin" && ext != ".hex")
                {
                    _logOutput.Append("仅支持 .bin / .hex 文件", Color.Orange);
                    return false;
                }

                string fileName = Path.GetFileName(filePath);

                // 只有应用程序(R1) 校验型号
                if (area == "应用程序(R1)" && !string.IsNullOrWhiteSpace(deviceModel))
                {
                    string model = deviceModel.Trim();
                    if (!fileName.Contains(model))
                    {
                        _logOutput.Append($"应用程序(R1)固件必须包含型号：{model}", Color.Orange);
                        return false;
                    }
                }

                // ==============================================
                // 所有区域统一：读取文件 + 计算校验和
                // ==============================================
                FirmwareData = File.ReadAllBytes(filePath);
                FileName = fileName;
                Checksum = CalculateChecksum(FirmwareData);
                _deviceReadyResponded = false;

                _logOutput.Append(GetLogTip("LoadSuccess"), Color.LimeGreen);

                // ==============================================
                // 所有区域统一：发送设备准备就绪指令
                // ==============================================
                SendDeviceReadyCommand();

                return true;
            }
            catch
            {
                _logOutput.Append(GetLogTip("LoadFail"), Color.Orange);
                return false;
            }
        }

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

        public void StartUpgrade()
        {
            if (FirmwareData == null)
            {
                _logOutput.Append("请先加载文件", Color.Orange);
                return;
            }
            if (!_deviceReadyResponded)
            {
                _logOutput.Append("等待设备准备就绪...", Color.Orange);
                return;
            }
            _currentPacketIndex = 0;
            OnProgressChanged?.Invoke(0);
            SendNextPacket();
        }

        private void SendNextPacket()
        {
            if (_currentPacketIndex >= TotalPackets)
            {
                _logOutput.Append(GetLogTip("UploadComplete"), Color.LimeGreen);
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

            int progress = (int)((_currentPacketIndex + 1) * 100.0 / TotalPackets);
            OnProgressChanged?.Invoke(progress);
        }

        public void HandleUpgradeResponse(byte[] buffer)
        {
            string recv = Encoding.ASCII.GetString(buffer).Trim();
            string ack_inform = _protocolLoader.Ack_SetInfo.Trim();

            if (recv.Contains(ack_inform))
            {
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
                    _logOutput.Append("设备准备就绪，等待上传...", Color.LimeGreen);
                    return;
                }
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

        private void SendUpdateInformCommand()
        {
            try
            {
                string cmd = "";
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string cmd_head = _protocolLoader.Cmd_SetInfo.Split('{')[0].Trim().Replace(" ", "");

                if (_currentArea == "应用程序(R1)")
                {
                    cmd = $"{cmd_head} {{#DEV_INFO:ABT={now};}}";
                }
                else if (_currentArea == "程序参数")
                {
                    cmd = $"{cmd_head} {{#DEV_INFO:PFN={FileName};}}";
                }
                else
                {
                    // 其他区域默认发送时间
                    cmd = cmd = $"{cmd_head} {{#DEV_INFO:FF={now};}}"; 
                }

                if (!string.IsNullOrEmpty(cmd))
                {
                    _serialComm.SendString(cmd);
                }
            }
            catch
            {
                _logOutput.Append("发送更新信息失败", Color.Orange);
            }
        }
    }
}