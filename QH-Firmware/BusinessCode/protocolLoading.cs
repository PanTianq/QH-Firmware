using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace QH_Firmware
{
    /// <summary>
    /// 协议加载管理类
    /// 功能：加载 JSON 协议、解析指令、管理最近文件、提供事件通知界面
    /// </summary>
    public class ProtocolLoading
    {
        #region 公共属性
        /// <summary>当前加载的协议配置</summary>
        public ProtocolConfig CurrentConfig { get; set; }

        /// <summary>当前协议文件路径</summary>
        public string CurrentFilePath { get; set; } = string.Empty;

        /// <summary>最近打开的协议文件列表</summary>
        public List<string> RecentFiles { get; } = new List<string>();

        /// <summary>最大历史文件数量</summary>
        public const int MAX_RECENT_FILES = 5;
        #endregion

        #region 事件
        /// <summary>协议加载完成事件</summary>
        public event Action<string, string> ProtocolLoaded;

        /// <summary>日志输出事件</summary>
        public event Action<string, Color> LogReceived;
        #endregion

        #region 私有路径
        /// <summary>历史文件存储目录</summary>
        private readonly string _historyFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FirmwareTool");

        /// <summary>历史文件完整路径</summary>
        private string HistoryFilePath => Path.Combine(_historyFolder, "recentFiles.txt");
        #endregion

        #region 协议指令变量（自动解析赋值）
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
        #endregion

        #region 加载协议
        /// <summary>
        /// 正常加载协议文件（带日志输出）
        /// </summary>
        public bool LoadProtocolFile(string filePath)
        {
            // 文件不存在
            if (!File.Exists(filePath))
            {
                LogReceived?.Invoke($"协议文件不存在：{filePath}", Color.Orange);
                return false;
            }

            // 非 JSON 文件
            if (!Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                LogReceived?.Invoke("请选择 .json 协议文件", Color.Orange);
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                ProtocolConfig config = JsonConvert.DeserializeObject<ProtocolConfig>(json);

                // 校验协议有效性
                if (config == null || string.IsNullOrWhiteSpace(config.protocol_version) || config.commands == null)
                {
                    LogReceived?.Invoke("协议JSON格式错误：缺少必填字段", Color.Orange);
                    return false;
                }

                // 赋值并加载
                CurrentConfig = config;
                CurrentFilePath = filePath;
                AddToRecentFiles(filePath);
                ParseProtocolToVariables();

                ProtocolLoaded?.Invoke(Path.GetFileName(filePath), config.description);
                LogReceived?.Invoke($"协议加载成功 | 版本：{config.protocol_version}", Color.LimeGreen);
                return true;
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"加载协议失败：{ex.Message}", Color.Orange);
                return false;
            }
        }

        /// <summary>
        /// 静默加载协议（程序启动时使用，无日志）
        /// </summary>
        public bool LoadProtocolFileSilent(string filePath)
        {
            try
            {
                if (!File.Exists(filePath) || !Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    return false;

                string json = File.ReadAllText(filePath);
                ProtocolConfig config = JsonConvert.DeserializeObject<ProtocolConfig>(json);

                if (config == null || string.IsNullOrWhiteSpace(config.protocol_version) || config.commands == null)
                    return false;

                CurrentConfig = config;
                CurrentFilePath = filePath;
                AddToRecentFiles(filePath);
                ParseProtocolToVariables();
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region 最近文件管理
        /// <summary>
        /// 加载本地保存的历史文件
        /// </summary>
        public void LoadRecentFiles()
        {
            try
            {
                if (!File.Exists(HistoryFilePath)) return;

                var lines = File.ReadAllLines(HistoryFilePath);
                RecentFiles.Clear();
                RecentFiles.AddRange(lines.Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x)));
            }
            catch { }
        }

        /// <summary>
        /// 添加文件到最近列表（去重、置顶、限制数量）
        /// </summary>
        private void AddToRecentFiles(string filePath)
        {
            RecentFiles.RemoveAll(x => x.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            RecentFiles.Insert(0, filePath);

            // 限制最大数量
            if (RecentFiles.Count > MAX_RECENT_FILES)
                RecentFiles.RemoveAt(MAX_RECENT_FILES);

            SaveRecentFiles();
        }

        /// <summary>
        /// 保存历史文件到本地
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
        /// 清空所有历史记录，并提示用户重新加载协议
        /// </summary>
        public void ClearAllHistory()
        {
            RecentFiles.Clear();
            SaveRecentFiles();

            try
            {
                string oldFile = Path.Combine(_historyFolder, "lastProtocol.txt");
                if (File.Exists(oldFile))
                    File.Delete(oldFile);
            }
            catch { }
        }
        #endregion

        #region 协议解析到变量
        /// <summary>
        /// 将协议指令解析到类变量，方便外部直接调用
        /// </summary>
        public void ParseProtocolToVariables()
        {
            // 清空旧数据
            Cmd_Handshake = Ack_Handshake = string.Empty;
            Interval_Handshake = 0;

            Cmd_GetInfo = Ack_GetInfo = string.Empty;
            Interval_GetInfo = 0;

            Cmd_SetInfo = Ack_SetInfo = string.Empty;
            Interval_SetInfo = 0;

            Cmd_FirmwareHeader = Ack_FirmwareHeader = string.Empty;
            Interval_FirmwareHeader = 0;

            Cmd_Reboot = Ack_Reboot = string.Empty;
            Interval_Reboot = 0;

            if (CurrentConfig?.commands == null) return;

            // 按指令名称匹配赋值
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

            LogReceived?.Invoke("协议指令解析完成", Color.LimeGreen);
        }
        #endregion
    }

    #region 协议实体类
    /// <summary>
    /// 协议根配置
    /// </summary>
    public class ProtocolConfig
    {
        public string protocol_version { get; set; }
        public string description { get; set; }
        public string interval_unit { get; set; }
        public string zero_interval_meaning { get; set; }
        public List<ProtocolCommand> commands { get; set; }
        public Dictionary<string, string> variables { get; set; }
    }

    /// <summary>
    /// 单条协议指令
    /// </summary>
    public class ProtocolCommand
    {
        public string name { get; set; }
        public string command { get; set; }
        public int interval { get; set; }
        public string confirmation { get; set; }
    }
    #endregion
}