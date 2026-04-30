using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
// 加载协议文件、管理历史记录、解析协议内容到变量，提供事件通知界面更新
namespace QH_Firmware
{
    public class ProtocolLoading
    {
        public ProtocolConfig CurrentConfig { get; set; }
        public string CurrentFilePath { get; set; } = string.Empty;
        public List<string> RecentFiles { get; } = new List<string>();
        public const int MAX_RECENT_FILES = 5;

        public event Action<string, string> ProtocolLoaded;
        public event Action<string, Color> LogReceived;

        // 统一历史文件路径，避免重复拼接
        private readonly string _historyFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FirmwareTool");
        private string HistoryFilePath => Path.Combine(_historyFolder, "recentFiles.txt");

        /// <summary>
        /// 正常加载协议（带日志）
        /// </summary>
        public bool LoadProtocolFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogReceived?.Invoke($"协议文件不存在：{filePath}",Color.Orange);
                return false;
            }

            if (!Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                LogReceived?.Invoke("请选择 .json 协议文件", Color.Orange);
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                ProtocolConfig config = JsonConvert.DeserializeObject<ProtocolConfig>(json);

                if (config == null || string.IsNullOrWhiteSpace(config.protocol_version) || config.commands == null)
                {
                    LogReceived?.Invoke("协议JSON格式错误：缺少必填字段", Color.Orange);
                    return false;
                }

                CurrentConfig = config;
                CurrentFilePath = filePath;
                AddToRecentFiles(filePath);

                ProtocolLoaded?.Invoke(Path.GetFileName(filePath), config.description);
                LogReceived?.Invoke($"协议加载成功，协议版本：{config.protocol_version}", Color.LimeGreen);
                // ==================== 新增：自动解析 ====================
                ParseProtocolToVariables();
                // ========================================================
                return true;
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"加载协议失败：{ex.Message}", Color.Orange);
                return false;
            }
        }

        /// <summary>
        /// 静默加载（启动时用，无日志）
        /// </summary>
        public bool LoadProtocolFileSilent(string filePath)
        {
            try
            {
                if (!File.Exists(filePath) || !Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    return false;

                ProtocolConfig config = JsonConvert.DeserializeObject<ProtocolConfig>(File.ReadAllText(filePath));
                if (config == null || string.IsNullOrWhiteSpace(config.protocol_version) || config.commands == null)
                    return false;

                CurrentConfig = config;
                CurrentFilePath = filePath;
                AddToRecentFiles(filePath);
                ParseProtocolToVariables(); // 新增
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 加载本地历史记录
        /// </summary>
        public void LoadRecentFiles()
        {
            try
            {
                if (!File.Exists(HistoryFilePath)) return;

                var lines = File.ReadAllLines(HistoryFilePath);
                RecentFiles.Clear();
                RecentFiles.AddRange(lines.Where(x => !string.IsNullOrEmpty(x) && File.Exists(x)));
            }
            catch { }
        }

        /// <summary>
        /// 添加文件到历史列表（去重 + 置顶 + 限制数量）
        /// </summary>
        private void AddToRecentFiles(string filePath)
        {
            RecentFiles.RemoveAll(x => x.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            RecentFiles.Insert(0, filePath);

            if (RecentFiles.Count > MAX_RECENT_FILES)
                RecentFiles.RemoveAt(MAX_RECENT_FILES);

            SaveRecentFiles();
        }

        /// <summary>
        /// 保存历史到本地文件
        /// </summary>
        private void SaveRecentFiles()
        {
            try
            {
                Directory.CreateDirectory(_historyFolder);
                File.WriteAllLines(HistoryFilePath, RecentFiles);
            }
            catch { }
        }

        /// <summary>
        /// 清空所有历史（包括旧版残留文件，确保重启不恢复）
        /// </summary>
        public void ClearAllHistory()
        {
            RecentFiles.Clear();
            SaveRecentFiles();

            try
            {
                string oldLastProtocol = Path.Combine(_historyFolder, "lastProtocol.txt");
                if (File.Exists(oldLastProtocol))
                    File.Delete(oldLastProtocol);
            }
            catch { }
        }
        // 握手
        public string Cmd_Handshake { get; private set; }
        public string Ack_Handshake { get; private set; }
        public int Interval_Handshake { get; private set; }

        // 获取设备信息
        public string Cmd_GetInfo { get; private set; }
        public string Ack_GetInfo { get; private set; }
        public int Interval_GetInfo { get; private set; }

        // 写入设备信息
        public string Cmd_SetInfo { get; private set; }
        public string Ack_SetInfo { get; private set; }
        public int Interval_SetInfo { get; private set; }

        // 固件头
        public string Cmd_FirmwareHeader { get; private set; }
        public string Ack_FirmwareHeader { get; private set; }
        public int Interval_FirmwareHeader { get; private set; }

        // 设备软重启
        public string Cmd_Reboot { get; private set; }
        public string Ack_Reboot { get; private set; }
        public int Interval_Reboot { get; private set; }

        // ==================== 按协议解析 → 直接赋值到变量 ====================
        public void ParseProtocolToVariables()
        {
            // 先清空
            Cmd_Handshake = string.Empty;
            Ack_Handshake = string.Empty;
            Interval_Handshake = 0;

            Cmd_GetInfo = string.Empty;
            Ack_GetInfo = string.Empty;
            Interval_GetInfo = 0;

            Cmd_SetInfo = string.Empty;
            Ack_SetInfo = string.Empty;
            Interval_SetInfo = 0;

            Cmd_FirmwareHeader = string.Empty;
            Ack_FirmwareHeader = string.Empty;
            Interval_FirmwareHeader = 0;

            Cmd_Reboot = string.Empty;
            Ack_Reboot = string.Empty;
            Interval_Reboot = 0;

            if (CurrentConfig?.commands == null) return;

            // 按协议名称匹配 → 直接赋值变量
            foreach (var cmd in CurrentConfig.commands)
            {
                switch (cmd.name.Trim())
                {
                    case "握手":
                        Cmd_Handshake = cmd.command;
                        Ack_Handshake = cmd.confirmation;
                        Interval_Handshake = cmd.interval;
                        break;

                    case "获取设备信息":
                        Cmd_GetInfo = cmd.command;
                        Ack_GetInfo = cmd.confirmation;
                        Interval_GetInfo = cmd.interval;
                        break;

                    case "写入设备信息":
                        Cmd_SetInfo = cmd.command;
                        Ack_SetInfo = cmd.confirmation;
                        Interval_SetInfo = cmd.interval;
                        break;

                    case "固件头":
                        Cmd_FirmwareHeader = cmd.command;
                        Ack_FirmwareHeader = cmd.confirmation;
                        Interval_FirmwareHeader = cmd.interval;
                        break;

                    case "设备软重启":
                        Cmd_Reboot = cmd.command;
                        Ack_Reboot = cmd.confirmation;
                        Interval_Reboot = cmd.interval;
                        break;
                }
            }

            LogReceived?.Invoke("协议解析成功", Color.LimeGreen);
        }
        // ==========================================================================
    }

    // 协议配置实体
    public class ProtocolConfig
    {
        public string protocol_version { get; set; }
        public string description { get; set; }
        public string interval_unit { get; set; }
        public string zero_interval_meaning { get; set; }
        public List<ProtocolCommand> commands { get; set; }
        public Dictionary<string, string> variables { get; set; }
    }

    // 单条指令实体
    public class ProtocolCommand
    {
        public string name { get; set; }
        public string command { get; set; }
        public int interval { get; set; }
        public string confirmation { get; set; }
    }
}