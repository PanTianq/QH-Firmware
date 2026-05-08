using System;
using System.Windows.Forms;

namespace QH_Firmware.SoftUI
{
    /// <summary>
    /// 关于窗口（显示软件名称、版本、版权信息）
    /// </summary>
    public partial class About : Form
    {
        /// <summary>
        /// 外部传入的软件版本号
        /// </summary>
        public string AppVersion { get; set; }

        public About()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗口加载时显示软件名称 + 版本号
        /// </summary>
        private void About_Load(object sender, EventArgs e)
        {
            // 空值保护：防止版本号为空导致显示异常
            string version = string.IsNullOrEmpty(AppVersion) ? "" : AppVersion;

            // 设置显示文本
            label3.Text = "QH Firmware 固件烧录工具 " + version;
        }
    }
}