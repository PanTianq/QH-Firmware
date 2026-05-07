using System;
using System.IO;
//固件加载和升级核心类：负责读取固件文件、计算校验、分包、发送升级命令、监听设备回复等
namespace QH_Firmware
{
    public class Floading
    {
        private const int PACKET_SIZE = 1024;

        public byte[] FirmwareData { get; private set; }
        public string FileName { get; private set; }
        public int FileSize => FirmwareData?.Length ?? 0;
        public int TotalPackets => FirmwareData == null ? 0 : (FirmwareData.Length + PACKET_SIZE - 1) / PACKET_SIZE;
        public uint Checksum { get; private set; }

        private readonly SerialCommunication _serialComm;
        private readonly ProtocolLoading _protocolLoader;
        private readonly LogOutput _logOutput;

        // 监听标志，防止重复注册事件
        private bool _isListeningResponse = false;

        public Floading(SerialCommunication serialComm, ProtocolLoading protocolLoader, LogOutput logOutput)
        {
            _serialComm = serialComm;
            _protocolLoader = protocolLoader;
            _logOutput = logOutput;
        }

        public bool LoadAndStartUpgrade(string filePath, string area, string deviceModel, bool isHandshakeSuccess)
        {
            try
            {
                bool loadSuccess = LoadFirmware(filePath, area, deviceModel);
                if (!loadSuccess)
                {
                    _logOutput.Append("固件加载失败：文件格式或文件名不匹配", System.Drawing.Color.Orange);
                    return false;
                }

                if (!isHandshakeSuccess)
                {
                    _logOutput.Append("请先完成设备握手，再升级固件", System.Drawing.Color.Orange);
                    return false;
                }

                if (!_serialComm.IsOpen)
                {
                    _logOutput.Append("请先打开串口", System.Drawing.Color.Orange);
                    return false;
                }

                string prefix = _protocolLoader.Cmd_FirmwareHeader.Split('{')[0].Trim();
                string sendCmd = $"{prefix} {TotalPackets} 0x{Checksum:X8}";
                _serialComm.SendString(sendCmd);

                _logOutput.Append($"固件加载成功,大小：{FileSize} 字节 | 总包：{TotalPackets} | 校验：0x{Checksum:X8}", System.Drawing.Color.LimeGreen);

                // 监听硬件回复（只注册一次）
                if (!_isListeningResponse)
                {
                    _serialComm.DataReceived += ListenWriteResponse;
                    _isListeningResponse = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logOutput.Append($"固件加载异常：{ex.Message}", System.Drawing.Color.Orange);
                return false;
            }
        }

        public bool LoadFirmware(string filePath, string area, string deviceModel)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".bin" && ext != ".hex")
                {
                    _logOutput.Append("不支持此文件类型，仅支持 .bin / .hex", System.Drawing.Color.Orange);
                    return false;
                }

                string fileName = Path.GetFileName(filePath);
                if (area == "应用程序(R1)" && !string.IsNullOrWhiteSpace(deviceModel))
                {
                    string model = deviceModel.Trim();
                    if (!fileName.Contains(model))
                    {
                        _logOutput.Append($"文件校验失败：R1 区域固件必须包含型号「{model}」", System.Drawing.Color.Orange);
                        return false;
                    }
                }

                FirmwareData = File.ReadAllBytes(filePath);
                FileName = fileName;
                Checksum = CalculateChecksum(FirmwareData);

                return true;
            }
            catch (Exception ex)
            {
                _logOutput.Append($"读取文件失败：{ex.Message}", System.Drawing.Color.Orange);
                return false;
            }
        }

        /// <summary>
        /// 监听硬件写入回复：update load {总包号} {0} ok
        /// update load 22 0 ok
        /// </summary>
        private void ListenWriteResponse(byte[] buffer)
        {
            string recv = System.Text.Encoding.ASCII.GetString(buffer).Trim();
            string prefix = _protocolLoader.Ack_FirmwareHeader.Split('{')[0].Trim();
            string ack = $"{prefix} {TotalPackets} 0 ok";

            if (recv.Contains(ack))
            {
                // 收到正确回应，移除监听
                _serialComm.DataReceived -= ListenWriteResponse;
                _isListeningResponse = false;

                _logOutput.Append("设备已就绪", System.Drawing.Color.LimeGreen);
            }
            else
            {
                _logOutput.Append("设备未就绪，请重新加载一遍文件", System.Drawing.Color.Orange);
            }
        }

        public byte[] GetPacket(int index)
        {
            if (FirmwareData == null || index < 0 || index >= TotalPackets)
                return null;

            int start = index * PACKET_SIZE;
            int len = Math.Min(PACKET_SIZE, FirmwareData.Length - start);
            byte[] packet = new byte[len];
            Array.Copy(FirmwareData, start, packet, 0, len);
            return packet;
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

            // 防止残留监听
            if (_isListeningResponse)
            {
                _serialComm.DataReceived -= ListenWriteResponse;
                _isListeningResponse = false;
            }
        }
    }
}