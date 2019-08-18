using System;
using System.Windows.Forms;

namespace drawbridge
{
    public partial class PortSelect : Form
    {
        public int PortNum;

        public PortSelect()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.PortNum = Int32.Parse(numPort.Value.ToString());
            this.Dispose();
        }
    }
}
