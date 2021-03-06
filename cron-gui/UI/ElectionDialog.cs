using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Linq;
using System.Windows.Forms;

namespace Neo.UI
{
    public partial class ElectionDialog : Form
    {
        public ElectionDialog()
        {
            InitializeComponent();
        }

        public StateTransaction GetTransaction()
        {
            ECPoint pubkey = (ECPoint)comboBox1.SelectedItem;
            return Program.CurrentWallet.MakeTransaction(new StateTransaction
            {
                Version = 0,
                Descriptors = new[]
                {
                    new StateDescriptor
                    {
                        Type = StateType.Validator,
                        Key = pubkey.ToArray(),
                        Field = "Registered",
                        Value = BitConverter.GetBytes(true)
                    }
                }
            });
        }

        private void ElectionDialog_Load(object sender, EventArgs e)
        {
            var a = Program.CurrentWallet.GetAccounts();
            var y = a
                .Where(p => !p.WatchOnly && p.Contract.Script.IsStandardContract());

            var t =  
                y.Select(p => p?.GetKey()?.PublicKey).Where(c => c != null);

            comboBox1.Items.AddRange(t.ToArray());
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex >= 0)
            {
                button1.Enabled = true;
                ECPoint pubkey = (ECPoint)comboBox1.SelectedItem;
                StateTransaction tx = new StateTransaction
                {
                    Version = 0,
                    Descriptors = new[]
                    {
                        new StateDescriptor
                        {
                            Type = StateType.Validator,
                            Key = pubkey.ToArray(),
                            Field = "Registered",
                            Value = BitConverter.GetBytes(true)
                        }
                    }
                };
                label3.Text = $"{tx.SystemFee} cron";
            }
        }
    }
}
