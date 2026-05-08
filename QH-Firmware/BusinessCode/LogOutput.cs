using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QH_Firmware
{
    /// <summary>
    /// 日志输出管理类
    /// 功能：统一处理日志显示、带时间戳、跨线程安全、清空、导出、右键菜单
    /// </summary>
    public class LogOutput
    {
        /// <summary>
        /// 界面日志显示控件（富文本框）
        /// </summary>
        private readonly RichTextBox _rtbLog;

        /// <summary>
        /// 构造函数：绑定日志 RichTextBox 并初始化右键菜单
        /// </summary>
        /// <param name="rtb">界面上的日志控件</param>
        public LogOutput(RichTextBox rtb)
        {
            _rtbLog = rtb ?? throw new ArgumentNullException(nameof(rtb));
            InitLogRightMenu();
        }

        /// <summary>
        /// 追加带颜色的日志（自动添加时间戳）
        /// </summary>
        /// <param name="msg">日志内容</param>
        /// <param name="color">文字颜色</param>
        public void Append(string msg, Color color)
        {
            // 跨线程安全调用 UI 控件
            if (_rtbLog.InvokeRequired)
            {
                _rtbLog.BeginInvoke(new Action(() => Append(msg, color)));
                return;
            }

            // 控件不可见时不输出
            if (!_rtbLog.Visible)
                return;

            // 追加日志（末尾追加、设置颜色、自动滚动）
            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionColor = color;
            _rtbLog.AppendText($"{DateTime.Now:HH:mm:ss.fff} {msg}\r\n");
            _rtbLog.SelectionColor = _rtbLog.ForeColor;
            _rtbLog.ScrollToCaret();
        }

        /// <summary>
        /// 清空所有日志
        /// </summary>
        public void Clear()
        {
            if (_rtbLog.InvokeRequired)
            {
                _rtbLog.BeginInvoke(new Action(Clear));
                return;
            }

            _rtbLog.Clear();
        }

        /// <summary>
        /// 导出日志到 TXT 文件
        /// </summary>
        public void Export()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "日志文件(*.txt)|*.txt|所有文件|*.*";
                sfd.FileName = $"日志_{DateTime.Now:yyyyMMddHHmmss}.txt";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, _rtbLog.Text);
                    Append($"日志已导出：{sfd.FileName}", Color.LimeGreen);
                }
            }
        }

        /// <summary>
        /// 初始化右键菜单：清空日志 + 导出日志
        /// </summary>
        private void InitLogRightMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem itemClear = new ToolStripMenuItem("清空日志");
            itemClear.Click += (s, e) => Clear();

            ToolStripMenuItem itemExport = new ToolStripMenuItem("导出日志");
            itemExport.Click += (s, e) => Export();

            menu.Items.Add(itemClear);
            menu.Items.Add(itemExport);
            _rtbLog.ContextMenuStrip = menu;
        }
    }
}