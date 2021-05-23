using Neo.Extras;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Properties;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Neo.UI
{
    internal partial class InvokeContractDialog : Form
    {
        private InvocationTransaction tx;
        private JObject abi;
        private UInt160 script_hash;
        private ContractParameter[] parameters;
        private ContractParameter[] parameters_abi;

        private static readonly Fixed8 net_fee = Fixed8.FromDecimal(0.001m);

        public InvokeContractDialog(InvocationTransaction tx = null)
        {
            InitializeComponent();
            txtFeeNetwork.Validating += TxtFeeNetwork_Validating;
            this.tx = tx;
            txtFeeNetwork.Text = net_fee.ToString();
            if (tx != null)
            {
                tabControl1.SelectedTab = tabPage2;
                textBox6.Text = tx.Script.ToHexString();
            }
        }

        public InvocationTransaction GetTransaction(Fixed8 fee, UInt160 Change_Address = null)
        {
            if (tx.Size > 1024)
            {
                Fixed8 sumFee = Fixed8.FromDecimal(tx.Size * 0.00001m) + Fixed8.FromDecimal(0.001m);
                if (fee < sumFee)
                {
                    fee = sumFee;
                }
            }

            if (Helper.CostRemind(tx.Gas.Ceiling(), fee))
            {
                InvocationTransaction result = Program.CurrentWallet.MakeTransaction(new InvocationTransaction
                {
                    Version = tx.Version,
                    Script = tx.Script,
                    Gas = tx.Gas,
                    Attributes = tx.Attributes,
                    Outputs = tx.Outputs
                }, change_address: Change_Address, fee: fee);
                return result;
            }
            else
            {
                return null;
            }
        }

        public InvokeContractDialog(UInt160 scriptHash)
        {
            InitializeComponent();

            txtFeeNetwork.Validating += TxtFeeNetwork_Validating;

            tabControl1.SelectedTab = tabPage1;


            textBox1.Text = scriptHash.ToString();

            button1_Click(button1, new EventArgs { });

        }

        private void TxtFeeNetwork_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ParseMinFee())
            {
                errorProvider1.SetError(txtFeeNetwork, "It's not ");

            }
            else

                errorProvider1.Clear();
        }


        Fixed8 _minFee = net_fee;

        public InvocationTransaction GetTransaction()
        {
            Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : Fixed8.Zero;

            ParseMinFee();

            if (fee < _minFee)
                fee = _minFee;

            return Program.CurrentWallet.MakeTransaction(new InvocationTransaction
            {
                Version = tx.Version,
                Script = tx.Script,
                Gas = tx.Gas,
                Attributes = tx.Attributes,
                Inputs = tx.Inputs,
                Outputs = tx.Outputs
            }, fee: fee);
        }

        private bool ParseMinFee()
        {
            try
            {
                _minFee
                    = Fixed8.FromDecimal(
                        decimal.Parse(txtFeeNetwork.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception ex)
            {
            }
            return false;
        }

        public InvocationTransaction GetTransaction(UInt160 change_address, Fixed8 fee)
        {
            return Program.CurrentWallet.MakeTransaction(new InvocationTransaction
            {
                Version = tx.Version,
                Script = tx.Script,
                Gas = tx.Gas,
                Attributes = tx.Attributes,
                Inputs = tx.Inputs,
                Outputs = tx.Outputs
            }, change_address: change_address, fee: fee);
        }

        private void UpdateParameters()
        {
            parameters = new[]
            {
                new ContractParameter
                {
                    Type = ContractParameterType.String,
                    Value = comboBox1.SelectedItem
                },
                new ContractParameter
                {
                    Type = ContractParameterType.Array,
                    Value = parameters_abi
                }
            };
        }

        private void UpdateScript()
        {
            if (parameters.Any(p => p.Value == null)) return;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(script_hash, parameters);
                textBox6.Text = sb.ToArray().ToHexString();
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            button1.Enabled = UInt160.TryParse(textBox1.Text, out _);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            script_hash = UInt160.Parse(textBox1.Text);
            ContractState contract = Blockchain.Singleton.Store.GetContracts().TryGet(script_hash);
            if (contract == null) return;
            parameters = contract.ParameterList.Select(p => new ContractParameter(p)).ToArray();
            textBox2.Text = contract.Name;
            textBox3.Text = contract.CodeVersion;
            textBox4.Text = contract.Author;
            textBox5.Text = string.Join(", ", contract.ParameterList);
            button2.Enabled = parameters.Length > 0;
            UpdateScript();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (ParametersEditor dialog = new ParametersEditor(parameters))
            {
                dialog.ShowDialog();
            }
            UpdateScript();
        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {
            button3.Enabled = false;
            button5.Enabled = textBox6.TextLength > 0;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            byte[] script;
            try
            {
                script = textBox6.Text.Trim().HexToBytes();
            }
            catch (FormatException ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            if (tx == null) tx = new InvocationTransaction();
            tx.Version = 1;
            tx.Script = script;
            if (tx.Attributes == null) tx.Attributes = new TransactionAttribute[0];
            if (tx.Inputs == null) tx.Inputs = new CoinReference[0];
            if (tx.Outputs == null) tx.Outputs = new TransactionOutput[0];
            if (tx.Witnesses == null) tx.Witnesses = new Witness[0];
            using (ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx, testMode: true))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"VM State: {engine.State}");
                sb.AppendLine($"Cron Consumed: {engine.GasConsumed}");
                sb.AppendLine($"Evaluation Stack: {new JArray(engine.ResultStack.Select(p => p.ToParameter().ToJson()))}");
                textBox7.Text = sb.ToString();
                if (!engine.State.HasFlag(VMState.FAULT))
                {
                    tx.Gas = engine.GasConsumed - Fixed8.FromDecimal(10);
                    if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
                    tx.Gas = tx.Gas.Ceiling();
                    Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : tx.Gas;
                    label7.Text = fee + " CRON";
                    button3.Enabled = true;
                }
                else
                {
                    button3.Enabled = true;
                    // *GEORGE* <george@cryptopass.team>: In test mode 
                    // SC execution could be failed because no storage 
                    // presented or some VM conditions wern't met

                    // MessageBox.Show(Strings.ExecutionFailed);

                }
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            textBox6.Text = File.ReadAllBytes(openFileDialog1.FileName).ToHexString();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (openFileDialog2.ShowDialog() != DialogResult.OK) return;
            abi = JObject.Parse(File.ReadAllText(openFileDialog2.FileName));
            script_hash = UInt160.Parse(abi["hash"].AsString());
            textBox8.Text = script_hash.ToString();
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(((JArray)abi["functions"]).Select(p => p["name"].AsString()).Where(p => p != abi["entrypoint"].AsString()).ToArray());
            textBox9.Clear();
            button8.Enabled = false;
        }

        public UInt160 ScriptHash => script_hash;

        private void button8_Click(object sender, EventArgs e)
        {
            using (ParametersEditor dialog = new ParametersEditor(parameters_abi))
            {
                dialog.ShowDialog();
            }
            UpdateParameters();
            UpdateScript();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(comboBox1.SelectedItem is string method)) return;
            JArray functions = (JArray)abi["functions"];
            JObject function = functions.First(p => p["name"].AsString() == method);
            JArray _params = (JArray)function["parameters"];
            parameters_abi = _params.Select(p => new ContractParameter(p["type"].TryGetEnum<ContractParameterType>())).ToArray();
            textBox9.Text = string.Join(", ", _params.Select(p => p["name"].AsString()));
            button8.Enabled = parameters_abi.Length > 0;
            UpdateParameters();
            UpdateScript();
        }
    }
}
