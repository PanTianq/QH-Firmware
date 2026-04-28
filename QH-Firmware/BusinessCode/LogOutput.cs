using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// 日志输出管理类
// 功能：统一处理日志显示、清空、导出、右键菜单
namespace QH_Firmware
{
    public class LogOutput
    {
        // 绑定界面上的日志富文本框控件
        private readonly RichTextBox _rtbLog;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="rtb">传入日志显示的RichTextBox控件</param>
        public LogOutput(RichTextBox rtb)
        {
            _rtbLog = rtb;         // 绑定控件
            InitLogRightMenu();    // 初始化右键菜单
        }

        /// <summary>
        /// 追加带颜色的日志（自动带时间戳）
        /// </summary>
        /// <param name="msg">日志内容</param>
        /// <param name="color">文字颜色</param>
        public void Append(string msg, Color color)
        {
            //空值防护
            if (_rtbLog == null) throw new ArgumentNullException(nameof(_rtbLog));
            // 控件不可见时不输出
            if (!_rtbLog.Visible) return;
            // 跨线程访问UI时，使用Invoke封送回主线程
            if (_rtbLog.InvokeRequired)
            {
                _rtbLog.BeginInvoke(new Action(() => Append(msg, color)));
                return;
            }

            // 在文本末尾追加内容
            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionColor = color;                     // 设置颜色
            _rtbLog.AppendText($"{DateTime.Now:HH:mm:ss.fff} {msg}\r\n");
            _rtbLog.SelectionColor = _rtbLog.ForeColor;         // 恢复默认颜色
            _rtbLog.ScrollToCaret();                            // 自动滚动到底部
        }

        /// <summary>
        /// 清空所有日志
        /// </summary>
        public void Clear()
        {
            // 跨线程处理
            if (_rtbLog.InvokeRequired)
            {
                _rtbLog.BeginInvoke(new Action(Clear));
                return;
            }
            _rtbLog.Clear();
        }

        /// <summary>
        /// 导出日志到txt文件
        /// </summary>
        public void Export()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "日志文件(*.txt)|*.txt|所有文件|*.*";
                sfd.FileName = $"日志_{DateTime.Now:yyyyMMddHHmmss}.txt"; // 默认文件名

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, _rtbLog.Text); // 写入文件
                    Append($"日志已导出至：{sfd.FileName}", Color.LimeGreen); // 输出提示日志
                }
            }
        }

        /// <summary>
        /// 初始化日志控件的右键菜单
        /// 包含：清空日志、导出日志
        /// </summary>
        private void InitLogRightMenu()
        {
            var menu = new ContextMenuStrip();

            // 创建【清空日志】菜单项
            var itemClear = new ToolStripMenuItem("清空日志");
            itemClear.Click += (s, e) => Clear();

            // 创建【导出日志】菜单项
            var itemExport = new ToolStripMenuItem("导出日志");
            itemExport.Click += (s, e) => Export();

            // 添加到菜单
            menu.Items.Add(itemClear);
            menu.Items.Add(itemExport);

            // 绑定到RichTextBox
            _rtbLog.ContextMenuStrip = menu;
        }
    }
}