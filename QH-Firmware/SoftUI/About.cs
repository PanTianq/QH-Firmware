using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QH_Firmware.SoftUI
{
    public partial class About : Form
    {
        public string AppVersion { get; set; }
        public About()
        {
            InitializeComponent();
        }

        private void About_Load(object sender, EventArgs e)
        {
            label3.Text =  "QH Firmware 固件烧录工具" + AppVersion;


        }
    }
}
