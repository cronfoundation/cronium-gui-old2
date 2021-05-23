using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Linq;
using System.Windows.Forms;

namespace Neo.UI
{
    internal partial class ImportCustomContractDialog : Form
    {
        public Contract GetContract()
        {
            ContractParameterType[] parameterList = textBox1.Text.HexToBytes().Select(p => (ContractParameterType)p).ToArray();
            byte[] redeemScript = textBox2.Text.HexToBytes();
            return Contract.Create(parameterList, redeemScript);
        }

        public KeyPair GetKey()
        {
            if (textBox3.TextLength == 0) return null;
            byte[] privateKey;
            try
            {
                privateKey = Wallet.GetPrivateKeyFromWIF(textBox3.Text);
            }
            catch (FormatException)
            {
                privateKey = textBox3.Text.HexToBytes();
            }
            return new KeyPair(privateKey);
        }

        public ImportCustomContractDialog()
        {
            InitializeComponent();
        }

        private void Input_Changed(object sender, EventArgs e)
        {
            button1.Enabled = textBox1.TextLength > 0 && textBox2.TextLength > 0;
        }

        private void buttonImpAVM_Click(object sender, EventArgs e)
        {
            OpenFileDialog of = new OpenFileDialog();
            of.Filter = "AVM files (*.avm)|*.avm|All files (*.*)|*.*";

            if (of.ShowDialog() != DialogResult.OK)
                return;

            byte[] bytes = System.IO.File.ReadAllBytes(of.FileName);

            string str = "";
            for (int i = 0; i < bytes.Length; i++)
                str += (bytes[i].ToString("x2"));

            //  MessageBox.Show(str);

            textBox2.Text = str;
        }
    }
}
