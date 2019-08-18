using System;
using System.Windows.Forms;

namespace drawbridge
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (textBox2.Text.Length < 30)
            {
                MessageBox.Show("Encryption keys must be at least 30 characters!");

                return;
            }

            Registry.Set("Key", textBox2.Text);

            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            textBox2.Text = StaticHelpers.RandomString(32);
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            if (Registry.Has("Key"))
            {
                textBox2.Text = Registry.Get("Key");
            }
        }
    }
}
