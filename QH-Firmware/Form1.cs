using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace QH_Firmware
{
    public partial class Form1 : Form
    {
        private readonly SerialPort serialPort = new SerialPort();
        private static readonly int[] DefaultBaudRates = { 9600, 115200 };
        private string currentProtocolFile = string.Empty;
        private readonly List<string> recentProtocolFiles = new List<string>();
        private const int MAX_RECENT_FILES = 5;

        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;
        }

        #region 窗体生命周期
        private void Form1_Load(object sender, EventArgs e)
        {
            InitializeBaudRateComboBox();
            GetSerialPorts();
            InitializeRecentFilesMenu();
            LoadRecentFiles();

            if (recentProtocolFiles.Count > 0)
            {
                string firstProtocol = recentProtocolFiles[0];
                if (File.Exists(firstProtocol))
                {
                    LoadProtocolFile(firstProtocol);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
            base.OnFormClosing(e);
        }
        #endregion

        #region 串口列表&波特率
        private void GetSerialPorts()
        {
            try
            {
                string selected = comboBox1.Text;
                string[] ports = SerialPort.GetPortNames();

                if (IsComboBoxDifferent(comboBox1, ports))
                {
                    comboBox1.BeginUpdate();
                    comboBox1.Items.Clear();
                    comboBox1.Items.AddRange(ports);
                    comboBox1.EndUpdate();

                    comboBox1.SelectedItem = comboBox1.Items.Contains(selected)
                        ? selected
                        : comboBox1.Items.Count > 0 ? comboBox1.Items[0] : null;
                }
            }
            catch
            {

            }
        }

        private bool IsComboBoxDifferent(System.Windows.Forms.ComboBox comboBox, string[] items)
        {
            if (comboBox.Items.Count != items.Length)
                return true;

            for (int i = 0; i < items.Length; i++)
            {
                if (i >= comboBox.Items.Count || comboBox.Items[i].ToString() != items[i])
                    return true;
            }
            return false;
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            GetSerialPorts();
        }

        private void InitializeBaudRateComboBox()
        {
            comboBox2.Items.Clear();
            foreach (int rate in DefaultBaudRates)
                comboBox2.Items.Add(rate.ToString());
            comboBox2.SelectedItem = "115200";
        }
        #endregion

        #region 串口开关控制
        private bool _isPortOperating;
        private void openPortButton_Click(object sender, EventArgs e)
        {
            if (_isPortOperating) return;
            try
            {
                _isPortOperating = true;
                openPortButton.Enabled = false;

                if (!serialPort.IsOpen)
                    OpenSerialPort();
                else
                    CloseSerialPort();
            }
            finally
            {
                _isPortOperating = false;
                openPortButton.Enabled = true;
            }
        }

        private void OpenSerialPort()
        {
            try
            {
                if (string.IsNullOrEmpty(currentProtocolFile))
                {
                    AppendLog("[错误] 请先加载协议文件再打开串口", Color.Orange);
                    return;
                }
                if (string.IsNullOrEmpty(comboBox1.Text) || string.IsNullOrEmpty(comboBox2.Text))
                {
                    return;
                }

                serialPort.PortName = comboBox1.Text;
                serialPort.BaudRate = int.Parse(comboBox2.Text);
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.One;
                serialPort.Parity = Parity.None;
                serialPort.Handshake = Handshake.None;

                serialPort.Open();
                UpdatePortButtonState(true);
                toolStripStatusLabel1.Text = $"{comboBox1.Text}@{comboBox2.Text} 协议: {Path.GetFileName(currentProtocolFile)}";
                AppendLog("打开串口成功", Color.LimeGreen);
            }
            catch (Exception ex)
            {
                string err = GetErrorMessageInChinese(ex);
                AppendLog($"[错误] 打开串口失败: {err}", Color.Orange);
                UpdatePortButtonState(false);
            }
        }

        private void CloseSerialPort()
        {
            try
            {
                if (serialPort.IsOpen)
                    serialPort.Close();

                UpdatePortButtonState(false);
                toolStripStatusLabel1.Text = "就绪";
                AppendLog("关闭串口成功", Color.LimeGreen);
            }
            catch (Exception ex)
            {
                string err = GetErrorMessageInChinese(ex);
                AppendLog($"[错误] 关闭串口失败: {err}", Color.Orange);
            }
        }

        private void UpdatePortButtonState(bool isOpen)
        {
            if (openPortButton.InvokeRequired)
            {
                openPortButton.Invoke(new Action<bool>(UpdatePortButtonState), isOpen);
                return;
            }

            openPortButton.Text = isOpen ? "关闭串口" : "打开串口";
            openPortButton.BackColor = isOpen
                ? Color.FromArgb(220, 20, 60)
                : Color.FromArgb(34, 139, 34);

            comboBox1.Enabled = !isOpen;
            comboBox2.Enabled = !isOpen;
            refreshButton.Enabled = !isOpen;
        }

        private string GetErrorMessageInChinese(Exception ex)
        {
            string msg = ex.Message.ToLower();
            if (msg.Contains("access") && msg.Contains("denied"))
                return "串口被占用或无权限";
            if (msg.Contains("port") && msg.Contains("not exist"))
                return "串口不存在";
            if (msg.Contains("already open"))
                return "串口已打开";
            if (msg.Contains("timeout"))
                return "操作超时";
            if (msg.Contains("invalid operation"))
                return "无效操作";
            if (ex is UnauthorizedAccessException)
                return "访问被拒绝";
            return ex.Message;
        }
        #endregion

        #region 日志
        private void AppendLog(string message, Color color)
        {
            if (!richTextBox1.Visible) return;

            if (richTextBox1.InvokeRequired)
            {
                richTextBox1.BeginInvoke(new Action<string, Color>(AppendLog), message, color);
                return;
            }

            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.SelectionLength = 0;
            richTextBox1.SelectionColor = color;
            richTextBox1.AppendText($"{DateTime.Now:HH:mm:ss} {message}\r\n");
            richTextBox1.SelectionColor = richTextBox1.ForeColor;
            richTextBox1.ScrollToCaret();
        }
        #endregion

        #region 协议实体类
        public class ProtocolConfig
        {
            public string protocol_version { get; set; }
            public string description { get; set; }
            public string interval_unit { get; set; }
            public string zero_interval_meaning { get; set; }
            public List<ProtocolCommand> commands { get; set; }
            public Dictionary<string, string> variables { get; set; }
        }

        public class ProtocolCommand
        {
            public string name { get; set; }
            public string command { get; set; }
            public int interval { get; set; }
            public string confirmation { get; set; }
        }
        #endregion

        #region 协议文件加载 & 最近文件
        private void 文件FToolStripMenuItem_Click(object sender, EventArgs e) { }

        private void LoadProtocolMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog od = new OpenFileDialog())
            {
                od.Filter = "JSON协议文件|*.json|所有文件|*.*";
                od.FilterIndex = 1;
                od.RestoreDirectory = true;
                od.Title = "选择协议文件";
                if (od.ShowDialog() == DialogResult.OK)
                {
                    LoadProtocolFile(od.FileName);
                }
            }
        }

        private void ClearHistoryItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定清除最近文件记录？", "提示",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                recentProtocolFiles.Clear();
                UpdateRecentFilesMenu();
                SaveRecentFiles();
                AppendLog("已清除历史记录", Color.LimeGreen);
            }
        }

        private void RecentFileItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is string path)
            {
                LoadProtocolFile(path);
            }
        }

        private bool _isLoadingProtocol;
        private void LoadProtocolFile(string filePath)
        {
            if (_isLoadingProtocol) return;
            try
            {
                _isLoadingProtocol = true;

                if (!File.Exists(filePath))
                {
                    AppendLog($"协议文件不存在：{filePath}", Color.Orange);
                    return;
                }

                if (!Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("请选择 .json 协议文件", Color.Orange);
                    return;
                }

                string json = File.ReadAllText(filePath);
                JavaScriptSerializer jss = new JavaScriptSerializer();
                ProtocolConfig config = jss.Deserialize<ProtocolConfig>(json);

                if (config == null)
                {
                    AppendLog("协议JSON格式错误", Color.Orange);
                    return;
                }

                currentProtocolFile = filePath;
                openPortButton.Enabled = true;

                AddToRecentFiles(filePath);
                SaveLastProtocolFile(filePath);

                toolStripStatusLabel1.Text = $"协议：{Path.GetFileName(filePath)}";
                AppendLog($"加载协议成功：{Path.GetFileName(filePath)}", Color.LimeGreen);
                AppendLog($"版本：{config.protocol_version}，描述：{config.description}", Color.LimeGreen);
                Text = $"Firmware - {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                AppendLog($"加载协议失败：{ex.Message}", Color.Orange);
            }
            finally
            {
                _isLoadingProtocol = false;
            }
        }

        private void InitializeRecentFilesMenu()
        {
            recentFilesToolStripMenuItem1.DropDownItems.Clear();
            UpdateRecentFilesMenu();
        }

        private void UpdateRecentFilesMenu()
        {
            recentFilesToolStripMenuItem1.DropDownItems.Clear();
            if (recentProtocolFiles.Count == 0)
            {
                recentFilesToolStripMenuItem1.DropDownItems.Add(new ToolStripMenuItem("(无)") { Enabled = false });
                return;
            }

            foreach (var file in recentProtocolFiles)
            {
                if (!File.Exists(file)) continue;
                string name = Path.GetFileName(file);
                ToolStripMenuItem item = new ToolStripMenuItem($"{name}  {file}")
                {
                    Tag = file,
                    ToolTipText = file
                };
                item.Click += RecentFileItem_Click;
                recentFilesToolStripMenuItem1.DropDownItems.Add(item);
            }

            recentFilesToolStripMenuItem1.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem clear = new ToolStripMenuItem("清除历史记录");
            clear.Click += ClearHistoryItem_Click;
            recentFilesToolStripMenuItem1.DropDownItems.Add(clear);
        }

        private void AddToRecentFiles(string filePath)
        {
            recentProtocolFiles.RemoveAll(x =>
                x.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            recentProtocolFiles.Insert(0, filePath);
            if (recentProtocolFiles.Count > MAX_RECENT_FILES)
                recentProtocolFiles.RemoveAt(MAX_RECENT_FILES);

            UpdateRecentFilesMenu();
            SaveRecentFiles();
        }

        private void LoadRecentFiles()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FirmwareTool");
                string file = Path.Combine(dir, "recentFiles.txt");
                if (File.Exists(file))
                {
                    var lines = File.ReadAllLines(file);
                    recentProtocolFiles.AddRange(lines
                        .Where(x => !string.IsNullOrEmpty(x) && File.Exists(x)));
                }
            }
            catch
            {

            }
            UpdateRecentFilesMenu();
        }

        private void SaveRecentFiles()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FirmwareTool");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "recentFiles.txt");
                File.WriteAllLines(file, recentProtocolFiles);
            }
            catch
            {

            }
        }

        private void SaveLastProtocolFile(string filePath)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FirmwareTool");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "lastProtocol.txt"), filePath);
            }
            catch
            {

            }
        }

        public string LoadLastProtocolFile()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FirmwareTool", "lastProtocol.txt");
                return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
            }
            catch
            {
                return "";
            }
        }
        #endregion
    }
}