using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace QH_Firmware
{
    /// <summary>
    /// 设备信息协议解析工具类
    /// 功能：解析 #DEV_INFO: 格式的设备上报信息，自动提取键值对、校验字段、显示中文名称
    /// </summary>
    public static class InformationParsing
    {
        #region 协议常量定义
        // 协议帧头
        private const string FrameHeader = "#DEV_INFO:";
        // 协议帧尾
        private const char FrameTail = ';';
        // 键值对之间分隔符 &
        private const char KVSeparator = '&';
        // 键与值分隔符 =
        private const char KeyValueSeparator = '=';
        #endregion

        #region 外部调用入口
        /// <summary>
        /// 外部统一调用：解析一整段设备信息字符串
        /// </summary>
        /// <param name="recvStr">接收到的原始字符串</param>
        /// <param name="logMsg">解析日志/错误信息</param>
        /// <returns>解析后的键值对字典</returns>
        public static Dictionary<string, string> ParseDeviceFrame(string recvStr, out string logMsg)
        {
            logMsg = string.Empty;
            try
            {
                // 1. 提取 {} 内部内容（兼容带大括号的帧格式）
                if (TryExtractBraceContent(ref recvStr))
                {
                    recvStr = recvStr.Trim();
                }

                // 2. 提取 #DEV_INFO: 和 ; 之间的有效数据
                string validData = ExtractValidData(recvStr, out string parseError);
                if (string.IsNullOrEmpty(validData))
                {
                    logMsg = parseError;
                    return null;
                }

                // 3. 解析 key=value 键值对
                var deviceDict = ParseKeyValueData(validData, out string invalidFields);
                logMsg = invalidFields;

                // 4. 检查必选字段是否缺失
                List<string> missingFields = CheckRequiredFields(deviceDict);
                if (missingFields.Count > 0)
                {
                    logMsg += (string.IsNullOrEmpty(logMsg) ? "" : " ") + $"缺失字段：{string.Join("、", missingFields)}";
                }

                return deviceDict;
            }
            catch (Exception ex)
            {
                logMsg = $"解析异常：{ex.Message}";
                return null;
            }
        }
        #endregion

        #region 内部解析工具
        /// <summary>
        /// 尝试提取 { } 内部的内容
        /// </summary>
        private static bool TryExtractBraceContent(ref string recvStr)
        {
            int start = recvStr.IndexOf('{');
            int end = recvStr.IndexOf('}');

            if (start >= 0 && end > start)
            {
                recvStr = recvStr.Substring(start + 1, end - start - 1).Trim();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 提取帧头与帧尾之间的有效数据
        /// </summary>
        private static string ExtractValidData(string rawData, out string errorMsg)
        {
            errorMsg = string.Empty;

            if (string.IsNullOrWhiteSpace(rawData))
            {
                errorMsg = "接收数据为空";
                return null;
            }

            // 查找帧头
            int headerIndex = rawData.IndexOf(FrameHeader, StringComparison.Ordinal);
            if (headerIndex < 0)
            {
                errorMsg = "未找到帧头：#DEV_INFO:";
                return null;
            }

            // 查找帧尾
            int tailIndex = rawData.IndexOf(FrameTail, headerIndex + FrameHeader.Length);
            if (tailIndex < 0)
            {
                errorMsg = "未找到帧尾：;";
                return null;
            }

            // 截取有效数据
            return rawData.Substring(
                headerIndex + FrameHeader.Length,
                tailIndex - headerIndex - FrameHeader.Length
            ).Trim();
        }

        /// <summary>
        /// 解析 key=value&key=value 格式数据
        /// </summary>
        private static Dictionary<string, string> ParseKeyValueData(string data, out string invalidInfo)
        {
            Dictionary<string, string> keyValueDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            invalidInfo = string.Empty;
            List<string> invalidList = new List<string>();

            if (string.IsNullOrWhiteSpace(data))
                return keyValueDict;

            // 拆分键值对
            string[] pairs = data.Split(new[] { KVSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string pair in pairs)
            {
                string currentPair = pair.Trim();
                if (string.IsNullOrEmpty(currentPair))
                    continue;

                // 拆分 key 和 value
                int equalIndex = currentPair.IndexOf(KeyValueSeparator);
                if (equalIndex <= 0 || equalIndex >= currentPair.Length - 1)
                {
                    invalidList.Add(currentPair);
                    continue;
                }

                string key = currentPair.Substring(0, equalIndex).Trim().ToUpper();
                string value = currentPair.Substring(equalIndex + 1).Trim();
                value = string.IsNullOrEmpty(value) ? "N/A" : value;

                // 避免重复键
                if (!keyValueDict.ContainsKey(key))
                    keyValueDict[key] = value;
            }

            // 无效字段提示
            if (invalidList.Count > 0)
                invalidInfo = $"无效字段：{string.Join("、", invalidList)}";

            return keyValueDict;
        }

        /// <summary>
        /// 检查必选字段是否完整
        /// </summary>
        private static List<string> CheckRequiredFields(Dictionary<string, string> dict)
        {
            List<string> requiredFields = new List<string>
            {
                "PM", "PN", "CM", "CN", "BV", "ID", "AV", "ABT", "PFN", "PBT"
            };

            List<string> missing = new List<string>();
            foreach (string field in requiredFields)
            {
                if (!dict.ContainsKey(field))
                    missing.Add(field);
            }

            return missing;
        }
        #endregion

        #region 中文名称映射
        /// <summary>
        /// 将设备字段英文缩写映射为中文名称
        /// </summary>
        public static string GetChineseName(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            switch (key.ToUpper().Trim())
            {
                case "PM": return "产品型号";
                case "PN": return "产品编号";
                case "CM": return "电路型号";
                case "CN": return "电路编号";
                case "BV": return "Bootloader版本";
                case "ID": return "主板唯一ID";
                case "AV": return "应用程序版本";
                case "ABT": return "应用程序烧写时间";
                case "PFN": return "参数文件名";
                case "PBT": return "参数文件生成时间";
                default: return key;
            }
        }
        #endregion

        #region DataGridView 右键菜单
        /// <summary>
        /// 为设备信息表格初始化右键刷新菜单
        /// </summary>
        public static void InitGridContextMenu(
            DataGridView dgv,
            Func<bool> isSerialOpen,
            Action refreshAction)
        {
            if (dgv == null) return;

            ContextMenuStrip menuStrip = new ContextMenuStrip();
            ToolStripMenuItem refreshItem = new ToolStripMenuItem("刷新设备信息");

            refreshItem.Click += (s, e) =>
            {
                // 串口未打开则提示
                if (isSerialOpen == null || !isSerialOpen())
                {
                    MessageBox.Show("请先打开串口", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 清空并刷新
                dgv.Rows.Clear();
                refreshAction?.Invoke();
            };

            menuStrip.Items.Add(refreshItem);
            dgv.ContextMenuStrip = menuStrip;
        }
        #endregion
    }
}