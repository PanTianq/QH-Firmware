using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace QH_Firmware
{
    /// <summary>
    /// 主窗体：UI交互层，只处理界面显示和事件调用
    /// </summary>
    public partial class Form1 : Form
    {
        // 串口通信操作类（已独立封装）
        private readonly SerialCommunication serialComm = new SerialCommunication();
        // 协议加载与解析类（已独立封装）
        private readonly ProtocolLoading protocolLoader = new ProtocolLoading();
        // 日志输出类（已独立封装）
        private readonly LogOutput _logOutput;
        // 系统默认支持的波特率列表
        private static readonly int[] DefaultBaudRates = { 9600, 115200 };
        // 防止串口操作重复点击
        private bool _isPortOperating;
        // 工具版本号
        private string version = "_V1.0";
        private AutoSendTimer _handshakeTimer;
        // 握手是否成功
        private bool _isHandshakeSuccess;
        // 设备信息（键值对）
        public Dictionary<string, string> DeviceInfo { get; set; } = new Dictionary<string, string>();
        public Form1()
        {
            InitializeComponent();
            // 固定窗口标题 + 版本号
            this.Text = "QH Firmware 固件烧录工具" + version;
            // 初始化日志组件，绑定日志显示控件
            _logOutput = new LogOutput(richTextBox1);
            // 窗体加载事件绑定
            Load += Form1_Load;
            // 串口日志输出 → 转发到日志组件
            serialComm.LogReceived += (msg, color) => _logOutput.Append(msg, color);
            // 串口状态变化 → 更新按钮状态
            serialComm.PortStateChanged += UpdatePortButtonState;
            // 协议加载日志 → 转发到日志组件
            protocolLoader.LogReceived += (msg, color) => _logOutput.Append(msg, color);
            

            // 协议加载完成 → 更新状态栏并启用串口按钮
            protocolLoader.ProtocolLoaded += (fileName, desc) =>
            {
                toolStripStatusLabel1.Text = $"协议：{fileName}";
                openPortButton.Enabled = true;
            };
            _isHandshakeSuccess = false;
            protocolInteraction();
        }
        #region 窗体事件
        /// <summary>
        /// 窗体加载时初始化：串口、波特率、最近文件、协议自动加载
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            // 初始化波特率下拉框
            InitializeBaudRateComboBox();
            // 初始化设备区域
            InitializeRegion();
            // 获取可用串口列表
            GetSerialPorts();

            // 初始化最近协议文件菜单
            InitializeRecentFilesMenu();
            // 协议是否加载成功标记
            bool protocolLoaded = false;
            // 加载最近使用过的协议文件记录
            protocolLoader.LoadRecentFiles();
            // 如果有最近文件，则尝试自动加载第一个
            if (protocolLoader.RecentFiles.Count > 0)
            {
                string lastFile = protocolLoader.RecentFiles[0];
                if (File.Exists(lastFile))
                {
                    // 静默加载（不输出日志）
                    if (protocolLoader.LoadProtocolFileSilent(lastFile))
                    {
                        toolStripStatusLabel1.Text = $"协议：{Path.GetFileName(lastFile)}";
                        openPortButton.Enabled = true;
                        protocolLoaded = true;
                        UpdateRecentFilesMenu();
                    }
                }
            }
            // 根据加载结果输出日志
            if (protocolLoaded)
            {
                _logOutput.Append($"协议加载成功，协议版本：{protocolLoader.CurrentConfig.protocol_version}", Color.LimeGreen);
            }
            else
            {
                _logOutput.Append("请加载协议文件(文件-加载协议)", Color.Orange);
                openPortButton.Enabled = false;
            }
        }
        /// <summary>
        /// 窗体关闭时：释放串口资源
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            serialComm.Close();
            serialComm.Dispose();
            base.OnFormClosing(e);
        }
        #endregion

        #region 串口列表&波特率
        /// <summary>
        /// 获取系统所有可用串口
        /// </summary>
        private void GetSerialPorts()
        {
            try
            {
                string selected = comboBox1.Text;
                string[] ports = SerialCommunication.GetPortNames();
                // 只有串口发生变化时才刷新，避免闪烁
                if (IsComboBoxDifferent(comboBox1, ports))
                {
                    comboBox1.BeginUpdate();
                    comboBox1.Items.Clear();
                    comboBox1.Items.AddRange(ports);
                    comboBox1.EndUpdate();
                    // 保持原来选中的串口（如果存在）
                    comboBox1.SelectedItem = comboBox1.Items.Contains(selected)
                        ? selected
                        : comboBox1.Items.Count > 0 ? comboBox1.Items[0] : null;
                }
            }
            catch { }
        }
        /// <summary>
        /// 判断下拉框内容是否与新列表不同（用于优化刷新）
        /// </summary>
        private bool IsComboBoxDifferent(ComboBox comboBox, string[] items)
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
        /// <summary>
        /// 刷新串口按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, EventArgs e)
        {
            GetSerialPorts();
        }
        /// <summary>
        /// 初始化波特率下拉框默认值
        /// </summary>
        private void InitializeBaudRateComboBox()
        {
            comboBox2.Items.Clear();
            foreach (int rate in DefaultBaudRates)
                comboBox2.Items.Add(rate.ToString());
            // 默认选中 115200
            comboBox2.SelectedItem = "115200";
        }
        #endregion

        #region 串口开关控制
        /// <summary>
        /// 打开/关闭串口按钮点击事件
        /// </summary>
        private void openPortButton_Click(object sender, EventArgs e)
        {
            // 防止重复点击
            if (_isPortOperating) return;
            try
            {
                _isPortOperating = true;
                openPortButton.Enabled = false;

                if (!serialComm.IsOpen)
                {
                    int baudRate = int.Parse(comboBox2.Text);
                    serialComm.Open(comboBox1.Text, baudRate);
                    toolStripStatusLabel1.Text = $"{comboBox1.Text}@{comboBox2.Text} 协议: {Path.GetFileName(protocolLoader.CurrentFilePath)}";
                    // 从UI控件获取值，传入定时器
                    string deviceModel = textBox1.Text.Trim();
                    string firmwareRegion = comboBox3.SelectedIndex.ToString(); // 直接用索引作为数字
                    _handshakeTimer.StartHandshake(deviceModel, firmwareRegion);
                }
                else
                {
                    // 打开串口
                    serialComm.Close();
                    _handshakeTimer.Stop();
                    toolStripStatusLabel1.Text = "就绪";
                }
            }
            finally
            {
                _isPortOperating = false;
                openPortButton.Enabled = true;
            }
        }
        /// <summary>
        /// 根据串口状态更新按钮文字、颜色、控件可用性
        /// </summary>
        private void UpdatePortButtonState(bool isOpen)
        {
            // 跨线程调用UI必须用 Invoke
            if (openPortButton.InvokeRequired)
            {
                openPortButton.Invoke(new Action<bool>(UpdatePortButtonState), isOpen);
                return;
            }
            // 更新按钮显示
            openPortButton.Text = isOpen ? "关闭串口" : "打开串口";
            openPortButton.BackColor = isOpen
                ? Color.FromArgb(220, 20, 60)// 红色：已打开
                : Color.FromArgb(34, 139, 34); // 绿色：已关闭
            // 打开串口后禁止修改串口号和波特率
            comboBox1.Enabled = !isOpen;
            comboBox2.Enabled = !isOpen;
            refreshButton.Enabled = !isOpen;
        }
        #endregion

        #region 协议文件加载 & 最近文件菜单
        /// <summary>
        /// 文件菜单点击（空实现）
        /// </summary>
        private void 文件FToolStripMenuItem_Click(object sender, EventArgs e) { }
        /// <summary>
        /// 手动加载协议文件
        /// </summary>
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
                    protocolLoader.LoadProtocolFile(od.FileName);
                    UpdateRecentFilesMenu();
                }
            }
        }
        /// <summary>
        /// 清除所有协议历史记录（同时清空当前加载的协议）
        /// </summary>
        private void ClearHistoryItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定清除最近文件记录？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // 清除历史记录文件
                protocolLoader.ClearAllHistory();
                // 清空当前加载的协议
                protocolLoader.CurrentConfig = null;
                protocolLoader.CurrentFilePath = string.Empty;
                // 更新界面
                UpdateRecentFilesMenu();
                toolStripStatusLabel1.Text = "就绪";
                openPortButton.Enabled = false;
            }
        }
        /// <summary>
        /// 点击最近文件列表，快速加载协议
        /// </summary>
        private void RecentFileItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is string path)
            {
                protocolLoader.LoadProtocolFile(path);
            }
        }
        /// <summary>
        /// 初始化最近文件菜单
        /// </summary>
        private void InitializeRecentFilesMenu()
        {
            recentFilesToolStripMenuItem1.DropDownItems.Clear();
            UpdateRecentFilesMenu();
        }
        /// <summary>
        /// 更新最近文件菜单显示
        /// </summary>
        private void UpdateRecentFilesMenu()
        {
            recentFilesToolStripMenuItem1.DropDownItems.Clear();
            // 无历史记录时显示灰色“无”
            if (protocolLoader.RecentFiles.Count == 0)
            {
                recentFilesToolStripMenuItem1.DropDownItems.Add(new ToolStripMenuItem("(无)") { Enabled = false });
                return;
            }
            // 添加所有最近文件
            foreach (var file in protocolLoader.RecentFiles)
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
            // 添加分隔线 + 清除历史按钮
            recentFilesToolStripMenuItem1.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem clear = new ToolStripMenuItem("清除历史记录");
            clear.Click += ClearHistoryItem_Click;
            recentFilesToolStripMenuItem1.DropDownItems.Add(clear);
        }
        #endregion

        #region 设备区域
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            // 保存值
            Properties.Settings.Default.textBox1Value = textBox1.Text;
            Properties.Settings.Default.Save();
        }
        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 保存选中索引
            Properties.Settings.Default.comboBox3Index = comboBox3.SelectedIndex;
            Properties.Settings.Default.Save();
        }
        private void InitializeRegion()
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                textBox1.Text = "QH4011";
            }
            // 读取上次保存的值
            if (Properties.Settings.Default.textBox1Value != "")
            {
                textBox1.Text = Properties.Settings.Default.textBox1Value;
            }

            // 2. comboBox3 默认选中第 2 项（索引 1）
            if (comboBox3.Items.Count > 1)
            {
                comboBox3.SelectedIndex = 1;
            }
            // 读取上次保存的值
            if (Properties.Settings.Default.comboBox3Index >= 0)
            {
                comboBox3.SelectedIndex = Properties.Settings.Default.comboBox3Index;
            }
        }
        #endregion

        #region 协议交互流程
        //协议交互流程：握手 → 获取设备信息 → 显示到界面
        private void protocolInteraction()
        {
            // 初始化自动发送
            _handshakeTimer = new AutoSendTimer(serialComm, protocolLoader, _logOutput);
            // 接收数据 → 验证握手
            serialComm.DataReceived += (buffer) =>
            {
                string recvStr = Encoding.UTF8.GetString(buffer).Trim();

                // --------------------- 握手 ---------------------
                if (_handshakeTimer.CheckHandshakeAck(recvStr))
                {
                    Invoke((MethodInvoker)delegate {
                        _handshakeTimer.SwitchToGetInfoMode();
                    });
                }

                // --------------------- 设备信息解析 ---------------------
                // 从协议模板中提取固定的前后缀
                string ackTemplate = protocolLoader.Ack_GetInfo;

                // 找到第一个 { 前面的部分（固定开头）
                int startIndex = ackTemplate.IndexOf('{');
                string ackStart = ackTemplate.Substring(0, startIndex).Trim();

                // 找到最后一个 } 后面的部分（固定结尾）
                int endIndex = ackTemplate.LastIndexOf('}');
                string ackEnd = ackTemplate.Substring(endIndex + 1).Trim();

                if (recvStr.StartsWith(ackStart) && recvStr.EndsWith(ackEnd))
                {
                    serialComm.IsOnline = true;    //  联机成功，关闭超时判断
                    // 停止自动发送
                    _handshakeTimer.Stop();

                    // 解析数据
                    string data = InformationParsing.ExtractDeviceDataFromFrame(recvStr);
                    DeviceInfo = InformationParsing.ParseDeviceInfo(data);

                    // 显示到界面
                    Invoke((MethodInvoker)delegate {
                        ShowDeviceInfoToGrid();
                        _logOutput.Append("设备信息解析完成", Color.LimeGreen);
                    });
                }
            };
        }
        #endregion

        #region 设备信息显示
        /// <summary>
        /// 将设备信息显示到 dataGridView1
        /// </summary>
        private void ShowDeviceInfoToGrid()
        {
            dataGridView1.Columns.Clear();
            dataGridView1.Rows.Clear();

            // 添加两列，并设置均分+居中
            dataGridView1.Columns.Add("Key", "参数名称");
            dataGridView1.Columns.Add("Value", "参数值");

            // 两列按比例均分
            dataGridView1.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // 设置文字居中（表头+单元格）
            dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // 隐藏列标题行
            dataGridView1.ColumnHeadersVisible = false;
            // 绑定数据
            foreach (var item in DeviceInfo)
            {
                string chineseKey = GetChineseKeyName(item.Key);
                dataGridView1.Rows.Add(chineseKey, item.Value);
            }

            // 去掉行号列
            dataGridView1.RowHeadersVisible = false;
        }

        /// <summary>
        /// 把英文键名翻译成中文
        /// </summary>
        private string GetChineseKeyName(string key)
        {
            switch (key)
            {
                case "ProductModel":
                    return "产品型号";
                case "BootloaderVer":
                    return "Bootloader版本";
                case "MainboardID":
                    return "主板ID";
                case "AppVersion":
                    return "应用程序版本";
                case "AppBurnTime":
                    return "应用程序烧写时间";
                case "ParamFileName":
                    return "参数文件名";
                case "ParamBurnTime":
                    return "参数文件烧写时间";
                default:
                    return key; // 其他键名原样显示
            }
        }
        #endregion
    }
}