using System;
using System.Collections.Generic;
using System.Text;

namespace QH_Firmware
{
    /// <summary>
    /// 设备信息协议解析工具类（独立完整版）
    /// </summary>
    public static class InformationParsing
    {
        // 协议固定帧格式
        private const string FrameHeader = "#DEV_INFO:";
        private const char FrameTail = ';';
        private const char KVSeparator = '&';
        private const char KeyValueSeparator = '=';

        /// <summary>
        /// 外部只需要调用这一个方法：解析一整段接收字符串
        /// </summary>
        public static Dictionary<string, string> ParseDeviceFrame(string recvStr, out string logMsg)
        {
            logMsg = string.Empty;
            try
            {
                // 1. 先处理外层大括号，提取 {} 内的内容
                int startBrace = recvStr.IndexOf('{');
                int endBrace = recvStr.IndexOf('}');
                if (startBrace >= 0 && endBrace > startBrace)
                {
                    // 只保留 {} 里面的部分
                    recvStr = recvStr.Substring(startBrace + 1, endBrace - startBrace - 1).Trim();
                }

                // 2. 再提取 #DEV_INFO: ... ; 中间内容
                string data = ExtractValidData(recvStr, out string err);
                if (string.IsNullOrEmpty(data))
                {
                    logMsg = err;
                    return null;
                }

                // 3. 解析键值对
                var dict = ParseKeyValueData(data, out string invalidInfo);
                if (!string.IsNullOrEmpty(invalidInfo))
                    logMsg = invalidInfo;

                // 4. 必选字段检查
                var missing = CheckRequiredFields(dict);
                if (missing.Count > 0)
                    logMsg += $" 缺失字段：{string.Join("、", missing)}";

                return dict;
            }
            catch (Exception ex)
            {
                logMsg = $"解析异常：{ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// 提取 #DEV_INFO: ... ; 中间内容
        /// </summary>
        private static string ExtractValidData(string raw, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            int head = raw.IndexOf(FrameHeader, StringComparison.Ordinal);
            if (head < 0)
            {
                error = "未找到帧头 #DEV_INFO:";
                return null;
            }

            int tail = raw.IndexOf(FrameTail, head + FrameHeader.Length);
            if (tail < 0)
            {
                error = "未找到帧尾 ;";
                return null;
            }

            return raw.Substring(head + FrameHeader.Length, tail - head - FrameHeader.Length).Trim();
        }

        /// <summary>
        /// 解析 key=value&key=value
        /// </summary>
        private static Dictionary<string, string> ParseKeyValueData(string data, out string invalidInfo)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            invalidInfo = string.Empty;
            var invalidList = new List<string>();

            if (string.IsNullOrWhiteSpace(data))
                return dict;

            var pairs = data.Split(new[] { KVSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in pairs)
            {
                string pair = p.Trim();
                if (string.IsNullOrEmpty(pair)) continue;

                int eq = pair.IndexOf(KeyValueSeparator);
                if (eq <= 0 || eq >= pair.Length - 1)
                {
                    invalidList.Add(pair);
                    continue;
                }

                string key = pair.Substring(0, eq).Trim().ToUpper();
                string val = pair.Substring(eq + 1).Trim();
                if (string.IsNullOrEmpty(val)) val = "N/A";

                if (!dict.ContainsKey(key))
                    dict[key] = val;
            }

            if (invalidList.Count > 0)
                invalidInfo = $"无效字段：{string.Join("、", invalidList)}";

            return dict;
        }

        /// <summary>
        /// 必选字段检查
        /// </summary>
        private static List<string> CheckRequiredFields(Dictionary<string, string> dict)
        {
            var required = new List<string> { "PM", "PN", "CM", "CN", "BV", "ID", "AV", "ABT", "PFN", "PBT" };
            var missing = new List<string>();
            foreach (var k in required)
                if (!dict.ContainsKey(k)) missing.Add(k);
            return missing;
        }

        /// <summary>
        /// 中文名称映射
        /// </summary>
        public static string GetChineseName(string key)
        {
            switch (key?.ToUpper()?.Trim())
            {
                case "PM":
                    return "产品型号";
                case "PN":
                    return "产品编号";
                case "CM":
                    return "电路型号";
                case "CN":
                    return "电路编号";
                case "BV":
                    return "Bootloader版本";
                case "ID":
                    return "主板唯一ID";
                case "AV":
                    return "应用程序版本";
                case "ABT":
                    return "应用程序烧写时间";
                case "PFN":
                    return "参数文件名";
                case "PBT":
                    return "参数文件生成时间";
                default:
                    return key; // 其他扩展键名原样显示
            }
        }
    }
}