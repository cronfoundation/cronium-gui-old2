using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Neo.UI
{
    internal partial class DeployContractDialog : Form
    {
        public DeployContractDialog()
        {
            InitializeComponent();
#if DEBUG
            var t = new SmartContractAutoDeploy();
           //  byte[] script = textBox8.Text.HexToBytes();
             textBox6.Text = t.ParamList;
             textBox7.Text = t.RetValType; 
             checkBox1.Checked = t.HasStorage;
             checkBox2.Checked = t.HasDynamicInvoke;
             checkBox3.Checked =  t.Payable;
             textBox1.Text = GenRndName();
             textBox2.Text = $"1.0.{DateTime.Now.ToTimestamp()}";
             textBox3.Text= "Author " + _rnd.Next(1000, 999999);
             textBox4.Text = $"aaa{_rnd.Next(111,999)}@xxx.yyy.zz";
             textBox5.Text = "Random description";
#endif
        }

       private static Random _rnd = new Random();

        public UInt160 ScriptHash => textBox8.Text.HexToBytes()?.ToScriptHash();

        private string GenRndName()
        {
            var t = new byte[10];
           _rnd.NextBytes(t);
            return "sc"+t.ToHexString();
        }

        public InvocationTransaction GetTransaction()
        {
            byte[] script = textBox8.Text.HexToBytes();
            byte[] parameter_list = textBox6.Text.HexToBytes();
            ContractParameterType return_type = textBox7.Text.HexToBytes().Select(p => (ContractParameterType?)p).FirstOrDefault() ?? ContractParameterType.Void;
            ContractPropertyState properties = ContractPropertyState.NoProperty;
            if (checkBox1.Checked) properties |= ContractPropertyState.HasStorage;
            if (checkBox2.Checked) properties |= ContractPropertyState.HasDynamicInvoke;
            if (checkBox3.Checked) properties |= ContractPropertyState.Payable;
            string name = textBox1.Text;
            string version = textBox2.Text;
            string author = textBox3.Text;
            string email = textBox4.Text;
            string description = textBox5.Text;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall("Neo.Contract.Create", script, parameter_list, return_type, properties, name, version, author, email, description);
                return new InvocationTransaction
                {
                    Script = sb.ToArray()
                };
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            button2.Enabled = textBox1.TextLength > 0
                && textBox2.TextLength > 0
                && textBox3.TextLength > 0
                && textBox4.TextLength > 0
                && textBox5.TextLength > 0
                && textBox8.TextLength > 0;
            try
            {
                textBox9.Text = textBox8.Text.HexToBytes().ToScriptHash().ToString()
                    + $" ({textBox8.Text.HexToBytes()?.Length} bytes) / {textBox8.Text.Length}";
            }
            catch (FormatException)
            {
                textBox9.Text = "";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            textBox8.Text = File.ReadAllBytes(openFileDialog1.FileName).ToHexString();
        }
    }
}
