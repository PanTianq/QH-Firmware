using System;
using System.Collections.Generic;
using System.Linq;

namespace QH_Firmware
{
    public static class InformationParsing
    {
        /// <summary>
        /// 从 { ... } 中提取设备数据
        /// </summary>
        public static string ExtractDeviceDataFromFrame(string response)
        {
            try
            {
                int start = response.IndexOf('{');
                int end = response.IndexOf('}');
                if (start >= 0 && end > start)
                {
                    return response.Substring(start + 1, end - start - 1).Trim();
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// 解析键值对：key1=value1&key2=value2
        /// </summary>
        public static Dictionary<string, string> ParseDeviceInfo(string data)
        {
            var dict = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(data))
                return dict;

            // 第一步：按 & 分割，再手动过滤空项（替代 RemoveEmptyEntries）
            string[] pairs = data.Split('&');
            foreach (var pair in pairs)
            {
                if (string.IsNullOrWhiteSpace(pair))
                    continue;

                // 第二步：按 = 分割，再手动过滤空项
                string[] kv = pair.Split('=');
                if (kv.Length != 2)
                    continue;

                string key = kv[0].Trim();
                string value = kv[1].Trim();

                if (!dict.ContainsKey(key))
                    dict[key] = value;
            }

            return dict;
        }
    }
}