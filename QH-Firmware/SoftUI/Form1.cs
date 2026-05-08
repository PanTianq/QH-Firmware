using QH_Firmware.Other_UI;
using QH_Firmware.SoftUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace QH_Firmware
{
    public partial class Form1 : Form
    {
        #region 全局对象
        /// <summary>串口通信</summary>
        public readonly SerialCommunication serialComm;

        /// <summary>协议加载</summary>
        public readonly ProtocolLoading protocolLoader;

        /// <summary>日志输出</summary>
        public readonly LogOutput _logOutput;

        /// <summary>固件升级</summary>
        private Floading _floading;

        /// <summary>自动握手定时器</summary>
        private AutoSendTimer _handshakeTimer;

        /// <summary>默认波特率</summary>
        private static readonly int[] DefaultBaudRates = { 9600, 115200 };

        /// <summary>防重复点击</summary>
        private bool _isPortOperating;

        /// <summary>版本号</summary>
        public string version = "_V1.0";

        /// <summary>握手状态</summary>
        public bool _isHandshakeSuccess;

        /// <summary> 高级设置窗口正在写入标志</summary>
        public bool IsAdvancedWriting { get; set; } = false;
        /// <summary>设备信息字典</summary>
        public Dictionary<string, string> DeviceInfo { get; set; } = new Dictionary<string, string>();

        /// <summary>等待接收设备信息</summary>
        private bool _waitingForDeviceInfo;
        #endregion

        #region 构造函数
        public Form1()
        {
            InitializeComponent();
            this.Text = "QH Firmware 固件烧录工具" + version;

            // 初始禁用按钮
            loadFileButton.Enabled = false;
            burnButton.Enabled = false;
            advancedSettingButton.Enabled = false;
            resetbutton.Enabled = false;

            // 初始化核心组件
            serialComm = new SerialCommunication();
            protocolLoader = new ProtocolLoading();
            _logOutput = new LogOutput(richTextBox1);

            // 绑定事件
            serialComm.LogReceived += (msg, color) => _logOutput.Append(msg, color);
            serialComm.PortStateChanged += UpdatePortButtonState;
            protocolLoader.LogReceived += (msg, color) => _logOutput.Append(msg, color);

            // 协议加载完成
            protocolLoader.ProtocolLoaded += (fileName, desc) =>
            {
                toolStripStatusLabel1.Text = $"协议：{fileName}";
                openPortButton.Enabled = true;
            };

            // 初始化协议交互
            protocolInteraction();

            // 设备信息表格右键菜单
            InformationParsing.InitGridContextMenu(dataGridView1, () => serialComm.IsOpen, RefreshDeviceInfo);

            // 初始化升级
            _floading = new Floading(serialComm, protocolLoader, _logOutput);

            // 进度条绑定
            _floading.OnProgressChanged += progress =>
            {
                if (progressBar1.InvokeRequired)
                    progressBar1.Invoke(new Action(() => progressBar1.Value = progress));
                else
                    progressBar1.Value = progress;
            };

            Load += Form1_Load;
        }
        #endregion

        #region 窗体加载 & 关闭
        private void Form1_Load(object sender, EventArgs e)
        {
            InitializeBaudRateComboBox();
            InitializeRegion();
            GetSerialPorts();
            InitializeRecentFilesMenu();
            progressBar1.Value = 0;

            bool protocolLoaded = false;
            protocolLoader.LoadRecentFiles();

            // 自动加载最近协议
            if (protocolLoader.RecentFiles.Count > 0)
            {
                string lastFile = protocolLoader.RecentFiles[0];
                if (File.Exists(lastFile) && protocolLoader.LoadProtocolFileSilent(lastFile))
                {
                    toolStripStatusLabel1.Text = $"协议：{Path.GetFileName(lastFile)}";
                    openPortButton.Enabled = true;
                    protocolLoaded = true;
                    UpdateRecentFilesMenu();
                }
            }

            if (protocolLoaded)
                _logOutput.Append($"协议加载成功 | 版本：{protocolLoader.CurrentConfig.protocol_version}", Color.LimeGreen);
            else
                _logOutput.Append("请加载协议文件(文件-加载协议)", Color.Orange);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            serialComm.Close();
            serialComm.Dispose();
            base.OnFormClosing(e);
        }
        #endregion

        #region 串口列表 & 波特率
        private void GetSerialPorts()
        {
            try
            {
                string selected = comboBox1.Text;
                string[] ports = SerialCommunication.GetPortNames();

                if (IsComboBoxDifferent(comboBox1, ports))
                {
                    comboBox1.BeginUpdate();
                    comboBox1.Items.Clear();
                    comboBox1.Items.AddRange(ports);
                    comboBox1.EndUpdate();
                    comboBox1.SelectedItem = comboBox1.Items.Contains(selected) ? selected : (comboBox1.Items.Count > 0 ? comboBox1.Items[0] : null);
                }
            }
            catch { }
        }

        private bool IsComboBoxDifferent(ComboBox comboBox, string[] items)
        {
            if (comboBox.Items.Count != items.Length) return true;
            for (int i = 0; i < items.Length; i++)
                if (i >= comboBox.Items.Count || comboBox.Items[i].ToString() != items[i])
                    return true;
            return false;
        }

        private void RefreshButton_Click(object sender, EventArgs e) => GetSerialPorts();

        private void InitializeBaudRateComboBox()
        {
            comboBox2.Items.Clear();
            foreach (int rate in DefaultBaudRates)
                comboBox2.Items.Add(rate.ToString());
            comboBox2.SelectedItem = "115200";
        }
        #endregion

        #region 打开 / 关闭串口
        private void openPortButton_Click(object sender, EventArgs e)
        {
            if (_isPortOperating) return;
            try
            {
                _isPortOperating = true;
                openPortButton.Enabled = false;

                if (!serialComm.IsOpen)
                {
                    int baudRate = int.Parse(comboBox2.Text);
                    serialComm.Open(comboBox1.Text, baudRate);
                    toolStripStatusLabel1.Text = $"{comboBox1.Text}@{comboBox2.Text} | 协议: {Path.GetFileName(protocolLoader.CurrentFilePath)}";

                    dataGridView1.Rows.Clear();
                    DeviceInfo.Clear();
                    richTextBox1.Clear();

                    string deviceModel = textBox1.Text.Trim();
                    string firmwareRegion = comboBox3.SelectedIndex.ToString();
                    _handshakeTimer.StartHandshake(deviceModel, firmwareRegion);
                }
                else
                {
                    serialComm.Close();
                }
            }
            finally
            {
                _isPortOperating = false;
                openPortButton.Enabled = true;
            }
        }

        /// <summary>统一刷新控件状态</summary>
        private void UpdatePortButtonState(bool isOpen)
        {
            if (openPortButton.InvokeRequired)
            {
                openPortButton.Invoke(new Action<bool>(UpdatePortButtonState), isOpen);
                return;
            }

            openPortButton.Text = isOpen ? "关闭串口" : "打开串口";
            openPortButton.BackColor = isOpen ? Color.Crimson : Color.Green;

            // 串口配置
            comboBox1.Enabled = !isOpen;
            comboBox2.Enabled = !isOpen;
            refreshButton.Enabled = !isOpen;

            // 设备配置
            textBox1.Enabled = !isOpen;
            comboBox3.Enabled = !isOpen;

            // 功能按钮：串口打开 + 握手成功 才可用
            loadFileButton.Enabled = isOpen && _isHandshakeSuccess;
            burnButton.Enabled = isOpen && _isHandshakeSuccess;
            advancedSettingButton.Enabled = isOpen && _isHandshakeSuccess;
            resetbutton.Enabled = isOpen && _isHandshakeSuccess;

            // 关闭时重置状态
            if (!isOpen)
            {
                _handshakeTimer?.Stop();
                _waitingForDeviceInfo = false;
                _isHandshakeSuccess = false;
                progressBar1.Value = 0;
                fileNameLabel.Text = "文件名：";
                toolStripStatusLabel1.Text = "就绪";
            }
        }
        #endregion

        #region 协议文件 & 最近文件
        private void LoadProtocolMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog od = new OpenFileDialog())
            {
                od.Filter = "JSON协议文件|*.json|所有文件|*.*";
                od.Title = "选择协议文件";
                if (od.ShowDialog() == DialogResult.OK)
                {
                    protocolLoader.LoadProtocolFile(od.FileName);
                    UpdateRecentFilesMenu();
                }
            }
        }

        private void ClearHistoryItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定清除最近文件记录？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                protocolLoader.ClearAllHistory();
                protocolLoader.CurrentConfig = null;
                protocolLoader.CurrentFilePath = string.Empty;
                UpdateRecentFilesMenu();

                _logOutput.Append("请加载协议文件(文件-加载协议)", Color.Orange);

                toolStripStatusLabel1.Text = "就绪";
                openPortButton.Enabled = false;
            }
        }

        private void RecentFileItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is string path)
                protocolLoader.LoadProtocolFile(path);
        }

        private void InitializeRecentFilesMenu()
        {
            recentFilesToolStripMenuItem1.DropDownItems.Clear();
            UpdateRecentFilesMenu();
        }

        private void UpdateRecentFilesMenu()
        {
            recentFilesToolStripMenuItem1.DropDownItems.Clear();
            if (protocolLoader.RecentFiles.Count == 0)
            {
                recentFilesToolStripMenuItem1.DropDownItems.Add(new ToolStripMenuItem("(无)") { Enabled = false });
                return;
            }

            foreach (var file in protocolLoader.RecentFiles)
            {
                if (!File.Exists(file)) continue;
                string name = Path.GetFileName(file);
                ToolStripMenuItem item = new ToolStripMenuItem($"{name}  {file}") { Tag = file, ToolTipText = file };
                item.Click += RecentFileItem_Click;
                recentFilesToolStripMenuItem1.DropDownItems.Add(item);
            }

            recentFilesToolStripMenuItem1.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem clear = new ToolStripMenuItem("清除历史记录");
            clear.Click += ClearHistoryItem_Click;
            recentFilesToolStripMenuItem1.DropDownItems.Add(clear);
        }
        #endregion

        #region 设备型号 & 区域保存
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.textBox1Value = textBox1.Text;
            Properties.Settings.Default.Save();
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.comboBox3Index = comboBox3.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void InitializeRegion()
        {
            if (string.IsNullOrEmpty(textBox1.Text))
                textBox1.Text = "QH4011";

            if (!string.IsNullOrEmpty(Properties.Settings.Default.textBox1Value))
                textBox1.Text = Properties.Settings.Default.textBox1Value;

            if (comboBox3.Items.Count > 1)
                comboBox3.SelectedIndex = 1;

            if (Properties.Settings.Default.comboBox3Index >= 0)
                comboBox3.SelectedIndex = Properties.Settings.Default.comboBox3Index;
        }
        #endregion

        #region 协议交互流程（握手 + 解析设备信息）
        private void protocolInteraction()
        {
            _handshakeTimer = new AutoSendTimer(serialComm, protocolLoader, _logOutput);

            serialComm.DataReceived += (buffer) =>
            {
                // 如果高级窗口正在写入，就跳过固件升级处理
                if (IsAdvancedWriting)
                    return;

                _floading.HandleUpgradeResponse(buffer);
                string recvStr = Encoding.ASCII.GetString(buffer).Trim();

                // 握手验证
                bool isHandshakeOk = _handshakeTimer.CheckHandshakeAck(recvStr);
                if (isHandshakeOk)
                {
                    _isHandshakeSuccess = true;
                    _waitingForDeviceInfo = true;
                    Invoke((MethodInvoker)delegate { _handshakeTimer.SwitchToGetInfoMode(); });
                }

                if (!_waitingForDeviceInfo) return;

                // 解析设备信息
                var deviceDict = InformationParsing.ParseDeviceFrame(recvStr, out string logMsg);
                if (!string.IsNullOrEmpty(logMsg) && !logMsg.Contains("未找到帧头"))
                    _logOutput.Append(logMsg, Color.Orange);

                if (deviceDict == null) return;

                // 解析成功
                DeviceInfo = deviceDict;
                serialComm.IsOnline = true;
                _handshakeTimer.Stop();
                _waitingForDeviceInfo = false;

                Invoke((MethodInvoker)delegate
                {
                    ShowDeviceInfoToGrid();
                    _logOutput.Append("设备信息解析完成", Color.LimeGreen);
                    UpdatePortButtonState(serialComm.IsOpen);
                });
            };
        }
        #endregion

        #region 设备信息显示
        private void ShowDeviceInfoToGrid()
        {
            dataGridView1.Columns.Clear();
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Add("Key", "名称");
            dataGridView1.Columns.Add("Value", "值");
            dataGridView1.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.ColumnHeadersVisible = false;
            dataGridView1.RowHeadersVisible = false;

            var sorted = new List<string> { "PM", "PN", "CM", "CN", "BV", "ID", "AV", "ABT", "PFN", "PBT" };
            foreach (var k in sorted)
            {
                string name = InformationParsing.GetChineseName(k);
                string val = DeviceInfo.ContainsKey(k) ? DeviceInfo[k] : "N/A";
                dataGridView1.Rows.Add(name, val);
            }
        }

        private void RefreshDeviceInfo()
        {
            if (!_isHandshakeSuccess)
            {
                _logOutput.Append("请先完成设备握手", Color.Orange);
                return;
            }
            if (!serialComm.IsOpen)
            {
                _logOutput.Append("请先打开串口", Color.Orange);
                return;
            }

            try
            {
                dataGridView1.Rows.Clear();
                DeviceInfo.Clear();
                serialComm.SendString(protocolLoader.Cmd_GetInfo);
                _waitingForDeviceInfo = true;
                _logOutput.Append("正在刷新设备信息", Color.LimeGreen);
            }
            catch (Exception ex)
            {
                _logOutput.Append("发送失败：" + ex.Message, Color.Orange);
            }
        }
        #endregion

        #region 高级设置 & 重启
        private void advancedSettingButton_Click(object sender, EventArgs e)
        {
            password pwdForm = new password();
            pwdForm.StartPosition = FormStartPosition.CenterParent;
            if (pwdForm.ShowDialog() == DialogResult.OK)
            {
                Advanced advanced = new Advanced(this);
                advanced.StartPosition = FormStartPosition.CenterParent;
                advanced.ShowDialog();
            }
        }

        private void resetbutton_Click(object sender, EventArgs e)
        {
            Reset frm = new Reset(serialComm, _logOutput, protocolLoader);
            frm.StartPosition = FormStartPosition.CenterParent;
            frm.ShowDialog();
        }
        #endregion

        #region 退出 & 关于
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定退出？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // 安全关闭串口
                serialComm.Close();
                // 安全关闭窗体（不会卡死）
                this.Close();
            }
        }

        private void aboutSoftwareToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About frm = new About();
            frm.AppVersion = version;
            frm.StartPosition = FormStartPosition.CenterParent;
            frm.ShowDialog();
        }
        #endregion

        #region 固件升级
        private void loadFileButton_Click(object sender, EventArgs e)
        {
            progressBar1.Value = 0;
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "固件文件 (*.bin;*.hex)|*.bin;*.hex";
                ofd.Title = "选择固件";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = ofd.FileName;
                    string area = comboBox3.Text.Trim();
                    string deviceModel = textBox1.Text.Trim();
                    _floading.LoadFirmware(filePath, area, deviceModel);
                    fileNameLabel.Text = $"文件名：{_floading.FileName}";
                }
            }
        }

        private void burnButton_Click(object sender, EventArgs e)
        {
            if (_floading.FirmwareData == null)
            {
                MessageBox.Show("请先加载固件！");
                return;
            }
            if (!serialComm.IsOpen)
            {
                MessageBox.Show("请先打开串口！");
                return;
            }
            _floading.StartUpgrade();
        }
        #endregion
    }
}