using Neo.Properties;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace Neo.UI
{

    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
            label1.Text = $"{Strings.AboutMessage} {Strings.AboutVersion} {Assembly.GetExecutingAssembly().GetName().Version}";
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(linkLabel1.Text);
        }

        private void pictureBox1_Click(object sender, System.EventArgs e)
        {
            Process.Start(linkLabel1.Text);
        }

    }
}
