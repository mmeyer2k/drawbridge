using Mono.Nat;
using System;
using System.Threading;
using System.Windows.Forms;

namespace drawbridge
{
    public partial class Form1 : Form
    {
        public INatDevice Router;

        public Form1()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text = Registry.Get("ApiKey");
            textBox1_TextChanged(null, null);
        }

        private void button1_ClickAsync(object sender, EventArgs e)
        {
            button1.Enabled = false;

            string ApiKey = textBox1.Text;

            string Key = Registry.Get("Key");

            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                PingRequest Ping = new PingRequest();

                //try
                //{
                    await Ping.SendAsync(this.Router, ApiKey, Key);
                //}
                //catch (Exception exc)
                //{
                    //MessageBox.Show(exc.ToString());
                //}

                if (Ping.LifeTime > 0)
                {
                    Registry.Set("ApiKey", ApiKey);

                    this.Invoke(new MethodInvoker(delegate
                    {
                        this.Close();
                    }));
                }

                button1.Invoke(new MethodInvoker(delegate
                {
                    button1.Text = "INVALID!";
                }));

                Thread.Sleep(1000);

                button1.Invoke(new MethodInvoker(delegate
                {
                    Thread.Sleep(2000);
                    button1.Enabled = true;
                    button1.Text = "Submit";
                }));

            }).Start();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.Text = textBox1.Text.Trim();
            button1.Enabled = textBox1.Text.Length == 22;
        }
    }
}
