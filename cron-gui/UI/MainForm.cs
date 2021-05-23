using Akka.Actor;
using Neo.Cryptography;
using Neo.IO;
using Neo.IO.Actors;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Properties;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using Neo.Wallets.SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml.Linq;
using Settings = Neo.Properties.Settings;
using VMArray = Neo.VM.Types.Array;
using CommandLine;
using System.Globalization;
using Newtonsoft.Json;
using System.Text;
using Neo.IO.Caching;
using Neo.Models;

namespace Neo.UI
{
    internal partial class MainForm : Form
    {
        private static readonly UInt160 RecycleScriptHash = new[] { (byte)OpCode.PUSHT }.ToScriptHash();
        private bool balance_changed = false;
        private bool check_nep5_balance = false;
        private DateTime persistence_time = DateTime.MinValue;
        private IActorRef actor;
        private WalletIndexer indexer;
        private string _strTransfer = ""; // TODO: load from config or state json

        public MainForm(XDocument xdoc = null)
        {
            InitializeComponent();

            toolStripProgressBar1.Maximum = (int)Blockchain.SecondsPerBlock;

            if (xdoc != null)
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                Version latest = Version.Parse(xdoc.Element("update").Attribute("latest").Value);
                if (version < latest)
                {
                    toolStripStatusLabel3.Tag = xdoc;
                    toolStripStatusLabel3.Text += $": {latest}";
                    toolStripStatusLabel3.Visible = true;
                }
            }
        }

        private void AddAccount(WalletAccount account, bool selected = false)
        {
            ListViewItem item = listView1.Items[account.Address];
            if (item != null)
            {
                if (!account.WatchOnly && ((WalletAccount)item.Tag).WatchOnly)
                {
                    listView1.Items.Remove(item);
                    item = null;
                }
            }
            if (item == null)
            {
                string groupName = account.WatchOnly ? "watchOnlyGroup" : account.Contract.Script.IsSignatureContract() ? "standardContractGroup" : "nonstandardContractGroup";

                var columns = new[]
                {
                    new ListViewItem.ListViewSubItem
                    {
                        Name = "address",
                        Text = account.Address
                    }
                };

                var customAssets = GetUserAssetNames();

                var tokenColumns = customAssets.Select(x => new ListViewItem.ListViewSubItem { Name = x }).ToList();

                columns = columns.Concat(tokenColumns).ToArray();

                item = listView1.Items.Add(new ListViewItem(columns, -1, listView1.Groups[groupName])
                {
                    Name = account.Address,
                    Tag = account
                });
            }
            item.Selected = selected;
        }

        private void AddTransaction(Transaction tx, uint? height, uint time)
        {
            int? confirmations = (int)Blockchain.Singleton.Height - (int?)height + 1;
            if (confirmations <= 0) confirmations = null;
            string confirmations_str = confirmations?.ToString() ?? Strings.Unconfirmed;
            string txid = tx.Hash.ToString();
            if (listView3.Items.ContainsKey(txid))
            {
                listView3.Items[txid].Tag = height;
                listView3.Items[txid].SubItems["confirmations"].Text = confirmations_str;
            }
            else
            {
                listView3.Items.Insert(0, new ListViewItem(new[]
                {
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "time",
                                Text = time.ToDateTime().ToString(),
                                Tag = tx
                            },
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "hash",
                                Text = txid
                            },
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "confirmations",
                                Text = confirmations_str
                            },
                            //add transaction type to list by phinx
                            new ListViewItem.ListViewSubItem
                            {
                                Name = "txtype",
                                Text = tx.Type.ToString()
                            }
                            //end

                        }, -1)
                {
                    Name = txid,
                    Tag = height
                });
            }
        }

        private void Blockchain_PersistCompleted(Blockchain.PersistCompleted e)
        {
            if (IsDisposed) return;

            persistence_time = DateTime.UtcNow;
            if (Program.CurrentWallet != null)
            {
                check_nep5_balance = true;
                if (Program.CurrentWallet.GetCoins().Any(p => !p.State.HasFlag(CoinState.Spent) && p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)) == true)
                    balance_changed = true;
            }

            BeginInvoke(new Action(RefreshConfirmations));
        }

        private void ChangeWallet(Wallet wallet)
        {
            if (Program.CurrentWallet != null)
            {
                Program.CurrentWallet.WalletTransaction -= CurrentWallet_WalletTransaction;
                if (Program.CurrentWallet is IDisposable disposable)
                    disposable.Dispose();
            }
            Program.CurrentWallet = wallet;
            listView3.Items.Clear();
            if (Program.CurrentWallet != null)
            {
                using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
                    foreach (var i in Program.CurrentWallet.GetTransactions().Select(p => snapshot.Transactions.TryGet(p)).Where(p => p.Transaction != null).Select(p => new
                    {
                        p.Transaction,
                        p.BlockIndex,
                        Time = snapshot.GetHeader(p.BlockIndex).Timestamp
                    }).OrderBy(p => p.Time))
                    {
                        AddTransaction(i.Transaction, i.BlockIndex, i.Time);
                    }
                Program.CurrentWallet.WalletTransaction += CurrentWallet_WalletTransaction;
            }
            修改密码CToolStripMenuItem.Enabled = Program.CurrentWallet is UserWallet;
            交易TToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            提取小蚁币CToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            signDataToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            requestCertificateToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            注册资产RToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            资产分发IToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            deployContractToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            invokeContractToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            选举EToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            创建新地址NToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            导入私钥IToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            创建智能合约SToolStripMenuItem.Enabled = Program.CurrentWallet != null;
            listView1.Items.Clear();

            if (Program.CurrentWallet != null)
            {
                this.SetColumnHeader();

                foreach (WalletAccount account in Program.CurrentWallet.GetAccounts().ToArray())
                {
                    AddAccount(account);
                }
            }

            balance_changed = true;
            check_nep5_balance = true;
        }

        private void SetColumnHeader()
        {
            var assets = this.GetUserAssetNames().ToList();

            foreach (var asset in assets.Where(x => !this.IsColumnExists(x)))
            {
                listView1.Columns.Add(new ColumnHeader() { Text = asset, Width = 120 });
            }
        }

        private bool IsColumnExists(string columnName)
        {
            foreach (ColumnHeader column in listView1.Columns)
            {
                if (column.Text == columnName)
                {
                    return true;
                }
            }

            return false;
        }

        private void CurrentWallet_WalletTransaction(object sender, WalletTransactionEventArgs e)
        {
            balance_changed = true;
            BeginInvoke(new Action<Transaction, uint?, uint>(AddTransaction), e.Transaction, e.Height, e.Time);
        }

        private WalletIndexer GetIndexer()
        {
            if (indexer is null)
                indexer = new WalletIndexer(Settings.Default.Paths.Index);
            return indexer;
        }

        private void RefreshConfirmations()
        {
            try
            {
                foreach (ListViewItem item in listView3.Items)
                {
                    uint? height = item.Tag as uint?;
                    int? confirmations = (int)Blockchain.Singleton.Height - (int?)height + 1;
                    if (confirmations <= 0) confirmations = null;
                    item.SubItems["confirmations"].Text = confirmations?.ToString() ?? Strings.Unconfirmed;
                }
            }
            catch(Exception ex)
            {

            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {             
            actor = Program.NeoSystem.ActorSystem.ActorOf(EventWrapper<Blockchain.PersistCompleted>.Props(Blockchain_PersistCompleted));
            Program.NeoSystem.StartNode(Settings.Default.P2P.Port, Settings.Default.P2P.WsPort);

            Program._parsed.WithParsed(x =>
            {
                if(!string.IsNullOrEmpty(x.Password) && !string.IsNullOrEmpty(x.Wallet))
                 OpenNep6Wallet( x.Password, x.Wallet );
            });

            AfterLoad();
            // Blockchain.ApplicationExecuted; IPersistencePlugin
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (actor != null)
                Program.NeoSystem.ActorSystem.Stop(actor);
            ChangeWallet(null);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            uint walletHeight = 0;

            if (Program.CurrentWallet != null)
            {
                walletHeight = (Program.CurrentWallet.WalletHeight > 0) ? Program.CurrentWallet.WalletHeight - 1 : 0;
            }

            lbl_height.Text = $"{walletHeight}/{Blockchain.Singleton.Height}/{Blockchain.Singleton.HeaderHeight}";

            lbl_count_node.Text = LocalNode.Singleton.ConnectedCount.ToString();
            TimeSpan persistence_span = DateTime.UtcNow - persistence_time;
            if (persistence_span < TimeSpan.Zero) persistence_span = TimeSpan.Zero;
            if (persistence_span > Blockchain.TimePerBlock)
            {
                toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                toolStripProgressBar1.Value = persistence_span.Seconds;
                toolStripProgressBar1.Style = ProgressBarStyle.Blocks;
            }

            if(LocalNode.Singleton.ConnectedCount == 0)
            {
                toolStripStatusLabel5.Image = Resources.conn0;
            }
            else if (LocalNode.Singleton.ConnectedCount == 1)
            {
                toolStripStatusLabel5.Image = Resources.conn1;
            }
            else if (LocalNode.Singleton.ConnectedCount > 1)
                toolStripStatusLabel5.Image = Resources.connN;

            if (Program.CurrentWallet != null)
            {
                if (Program.CurrentWallet.WalletHeight <= Blockchain.Singleton.Height + 1)
                {
                    if (balance_changed)
                        using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
                        {
                            var coins = Program.CurrentWallet?.GetCoins().Where(p => !p.State.HasFlag(CoinState.Spent)).ToList();

                            Fixed8 bonus_available = snapshot.CalculateBonus(Program.CurrentWallet.GetUnclaimedCoins().Select(p => p.Reference));
                            Fixed8 bonus_unavailable = snapshot.CalculateBonus(coins.Where(p => p.State.HasFlag(CoinState.Confirmed) && p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)).Select(p => p.Reference), snapshot.Height + 1);
                            Fixed8 bonus = bonus_available + bonus_unavailable;

                            var assets = coins.GroupBy(p => p.Output.AssetId, (k, g) => new
                            {
                                Asset = snapshot.Assets.TryGet(k),
                                Value = g.Sum(p => p.Output.Value),
                                Claim = k.Equals(Blockchain.UtilityToken.Hash) ? bonus : Fixed8.Zero
                            }).ToDictionary(p => p.Asset.AssetId);

                            if (bonus != Fixed8.Zero && !assets.ContainsKey(Blockchain.UtilityToken.Hash))
                            {
                                assets[Blockchain.UtilityToken.Hash] = new
                                {
                                    Asset = snapshot.Assets.TryGet(Blockchain.UtilityToken.Hash),
                                    Value = Fixed8.Zero,
                                    Claim = bonus
                                };
                            }

                            // Fill Tab1 and account balances including governing, utility, token assets

                            var userBalances = this.GetUserBalances(snapshot);

                            foreach (ListViewItem item in listView1.Items)
                            {
                                var addressScriptHash = item.Name.ToScriptHash();

                                if (!userBalances.TryGetValue(addressScriptHash, out var addressBalances))
                                {
                                    continue;
                                }

                                foreach (var balance in addressBalances.Balances)
                                {
                                    try
                                    {
                                        item.SubItems[balance.Asset.GetName()].Text = balance.Balance.ToString();
                                    }
                                    catch
                                    { 
                                    }
                                }
                            }

                            // Fill Tab2 and asset balances

                            foreach (AssetState asset in listView2.Items.OfType<ListViewItem>().Select(p => p.Tag as AssetState).Where(p => p != null).ToArray())
                            {
                                if (!assets.ContainsKey(asset.AssetId))
                                {
                                    listView2.Items.RemoveByKey(asset.AssetId.ToString());
                                }
                            }

                            foreach (var asset in assets.Values)
                            {
                                string value_text = asset.Value.ToString() + (asset.Asset.AssetId.Equals(Blockchain.UtilityToken.Hash) ? $"+({asset.Claim})" : "");
                                if (listView2.Items.ContainsKey(asset.Asset.AssetId.ToString()))
                                {
                                    listView2.Items[asset.Asset.AssetId.ToString()].SubItems["value"].Text = value_text;
                                }
                                else
                                {
                                    string asset_name = asset.Asset.AssetType == AssetType.GoverningToken ? "CRONIUM" :
                                                        asset.Asset.AssetType == AssetType.UtilityToken ? "CRON" :
                                                        asset.Asset.GetName();
                                    listView2.Items.Add(new ListViewItem(new[]
                                    {
                                        new ListViewItem.ListViewSubItem
                                        {
                                            Name = "name",
                                            Text = asset_name
                                        },
                                        new ListViewItem.ListViewSubItem
                                        {
                                            Name = "type",
                                            Text = asset.Asset.AssetType.ToString()
                                        },
                                        new ListViewItem.ListViewSubItem
                                        {
                                            Name = "value",
                                            Text = value_text
                                        },
                                        new ListViewItem.ListViewSubItem
                                        {
                                            ForeColor = Color.Gray,
                                            Name = "issuer",
                                            Text = $"{Strings.UnknownIssuer}[{asset.Asset.Owner}]"
                                        }
                                    }, -1, listView2.Groups["unchecked"])
                                    {
                                        Name = asset.Asset.AssetId.ToString(),
                                        Tag = asset.Asset,
                                        UseItemStyleForSubItems = false
                                    });
                                }
                            }
                            balance_changed = false;
                        }
                    foreach (ListViewItem item in listView2.Groups["unchecked"].Items.OfType<ListViewItem>().ToArray())
                    {
                        ListViewItem.ListViewSubItem subitem = item.SubItems["issuer"];
                        AssetState asset = (AssetState)item.Tag;
                        CertificateQueryResult result;
                        if (asset.AssetType == AssetType.GoverningToken || asset.AssetType == AssetType.UtilityToken)
                        {
                            result = new CertificateQueryResult { Type = CertificateQueryResultType.System };
                        }
                        else
                        {
                           /// result = CertificateQueryService.Query(asset.Owner);
                           result = new CertificateQueryResult() {Type=CertificateQueryResultType.Missing };
                        }
                        using (result)
                        {
                            subitem.Tag = result.Type;
                            switch (result.Type)
                            {
                                case CertificateQueryResultType.Querying:
                                case CertificateQueryResultType.QueryFailed:
                                    break;
                                case CertificateQueryResultType.System:
                                    subitem.ForeColor = Color.Green;
                                    subitem.Text = Strings.SystemIssuer;
                                    break;
                                case CertificateQueryResultType.Invalid:
                                    subitem.ForeColor = Color.Red;
                                    subitem.Text = $"[{Strings.InvalidCertificate}][{asset.Owner}]";
                                    break;
                                case CertificateQueryResultType.Expired:
                                    subitem.ForeColor = Color.Yellow;
                                    subitem.Text = $"[{Strings.ExpiredCertificate}]{result.Certificate.Subject}[{asset.Owner}]";
                                    break;
                                case CertificateQueryResultType.Good:
                                    subitem.ForeColor = Color.Black;
                                    subitem.Text = $"{result.Certificate.Subject}[{asset.Owner}]";
                                    break;
                            }
                            switch (result.Type)
                            {
                                case CertificateQueryResultType.System:
                                case CertificateQueryResultType.Missing:
                                case CertificateQueryResultType.Invalid:
                                case CertificateQueryResultType.Expired:
                                case CertificateQueryResultType.Good:
                                    item.Group = listView2.Groups["checked"];
                                    break;
                            }
                        }
                    }
                }
                if (check_nep5_balance && persistence_span > TimeSpan.FromSeconds(2))
                {
                    UInt160[] addresses = Program.CurrentWallet.GetAccounts().Select(p => p.ScriptHash).ToArray();
                    foreach (string s in Settings.Default.NEP5Watched)
                    {
                        UInt160 script_hash = UInt160.Parse(s);
                        byte[] script;
                        using (ScriptBuilder sb = new ScriptBuilder())
                        {
                            foreach (UInt160 address in addresses)
                                sb.EmitAppCall(script_hash, "balanceOf", address);
                            sb.Emit(OpCode.DEPTH, OpCode.PACK);
                            sb.EmitAppCall(script_hash, "decimals");
                            sb.EmitAppCall(script_hash, "name");
                            script = sb.ToArray();
                        }
                        using (ApplicationEngine engine = ApplicationEngine.Run(script))
                        {
                            if (engine.State.HasFlag(VMState.FAULT)) continue;
                            string name = engine.ResultStack.Pop().GetString();
                            byte decimals = (byte)engine.ResultStack.Pop().GetBigInteger();
                            BigInteger amount = ((VMArray)engine.ResultStack.Pop()).Aggregate(BigInteger.Zero, (x, y) => x + y.GetBigInteger());
                            if (amount == 0)
                            {
                                listView2.Items.RemoveByKey(script_hash.ToString());
                                continue;
                            }
                            BigDecimal balance = new BigDecimal(amount, decimals);
                            string value_text = balance.ToString();
                            if (listView2.Items.ContainsKey(script_hash.ToString()))
                            {
                                listView2.Items[script_hash.ToString()].SubItems["value"].Text = value_text;
                            }
                            else
                            {
                                listView2.Items.Add(new ListViewItem(new[]
                                {
                                    new ListViewItem.ListViewSubItem
                                    {
                                        Name = "name",
                                        Text = name
                                    },
                                    new ListViewItem.ListViewSubItem
                                    {
                                        Name = "type",
                                        Text = "NEP-5"
                                    },
                                    new ListViewItem.ListViewSubItem
                                    {
                                        Name = "value",
                                        Text = value_text
                                    },
                                    new ListViewItem.ListViewSubItem
                                    {
                                        ForeColor = Color.Gray,
                                        Name = "issuer",
                                        Text = $"ScriptHash:{script_hash}"
                                    }
                                }, -1, listView2.Groups["checked"])
                                {
                                    Name = script_hash.ToString(),
                                    UseItemStyleForSubItems = false
                                });
                            }
                        }
                    }
                    check_nep5_balance = false;
                }
            }

           ///   TryToDeploy();
        }        

        private void 创建钱包数据库NToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CreateWalletDialog dialog = new CreateWalletDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                NEP6Wallet wallet = new NEP6Wallet(GetIndexer(), dialog.WalletPath);
                wallet.Unlock(dialog.Password);
                wallet.CreateAccount();
                wallet.Save();
                ChangeWallet(wallet);
                Settings.Default.LastWalletPath = dialog.WalletPath;
                Settings.Default.Save();
            }
        }

        private void 打开钱包数据库OToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenWalletDialog dialog = new OpenWalletDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                string path = dialog.WalletPath;
                Wallet wallet;
                if (Path.GetExtension(path) == ".db3")
                {
                    if (MessageBox.Show(Strings.MigrateWalletMessage, Strings.MigrateWalletCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                    {
                        string path_old = path;
                        path = Path.ChangeExtension(path_old, ".json");
                        NEP6Wallet nep6wallet;
                        try
                        {
                            nep6wallet = NEP6Wallet.Migrate(GetIndexer(), path, path_old, dialog.Password);
                        }
                        catch (CryptographicException)
                        {
                            MessageBox.Show(Strings.PasswordIncorrect);
                            return;
                        }
                        nep6wallet.Save();
                        nep6wallet.Unlock(dialog.Password);
                        wallet = nep6wallet;
                        MessageBox.Show($"{Strings.MigrateWalletSucceedMessage}\n{path}");
                    }
                    else
                    {
                        try
                        {
                            wallet = UserWallet.Open(GetIndexer(), path, dialog.Password);
                        }
                        catch (CryptographicException)
                        {
                            MessageBox.Show(Strings.PasswordIncorrect);
                            return;
                        }
                    }
                }
                else
                {
                    NEP6Wallet nep6wallet = new NEP6Wallet(GetIndexer(), path);
                    try
                    {
                        nep6wallet.Unlock(dialog.Password);
                    }
                    catch (CryptographicException)
                    {
                        MessageBox.Show(Strings.PasswordIncorrect);
                        return;
                    }
                    wallet = nep6wallet;
                }
                ChangeWallet(wallet);
                Settings.Default.LastWalletPath = path;
                Settings.Default.Save();
            }
        }

        private void CToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ChangePasswordDialog dialog = new ChangePasswordDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                if (((UserWallet)Program.CurrentWallet).ChangePassword(dialog.OldPassword, dialog.NewPassword))
                    MessageBox.Show(Strings.ChangePasswordSuccessful);
                else
                    MessageBox.Show(Strings.PasswordIncorrect);
            }
        }

        private void RToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView2.Items.Clear();
            listView3.Items.Clear();
            GetIndexer().RebuildIndex();
        }

        private void XToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void TToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Transaction tx;
            UInt160 change_address;
            Fixed8 fee;
            using (TransferDialog dialog = new TransferDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                tx = dialog.GetTransaction();
                change_address = dialog.ChangeAddress;
                fee = dialog.Fee;
            }
            if (tx is InvocationTransaction itx)
            {
                using (InvokeContractDialog dialog = new InvokeContractDialog(itx))
                {
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    tx = dialog.GetTransaction(change_address, fee);
                    if (dialog.ScriptHash != default(UInt160))
                        TxRegistration.Register(dialog.ScriptHash, null, tx as InvocationTransaction);
                }
            }
            Helper.SignAndShowInformation(tx);
        }

        private void TToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (TradeForm form = new TradeForm())
            {
                form.ShowDialog();
            }
        }

        private void SToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SigningTxDialog dialog = new SigningTxDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void ClaimCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Helper.Show<ClaimForm>();
        }

        private void requestCertificateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CertificateRequestWizard wizard = new UI.CertificateRequestWizard())
            {
                wizard.ShowDialog();
            }
        }

        private void ClaimRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InvocationTransaction txS, txI;
            using (AssetRegisterDialog dialog = new AssetRegisterDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                txS = dialog.GetTransaction();
            }
            using (InvokeContractDialog dialog = new InvokeContractDialog(txS))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                txI = dialog.GetTransaction();
                if (dialog.ScriptHash != default(UInt160))
                    TxRegistration.Register(dialog.ScriptHash, txS, txI);
            }

            Helper.SignAndShowInformation(txI);
        }

        /// 资产分发 [Zīchǎn fēnfā]
        /// 
        private void IssueToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            using (IssueDialog dialog = new IssueDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    Helper.SignAndShowInformation(dialog.GetTransaction());
                }
                catch
                {
                    return;
                }
            }
        }

        private void deployContractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InvocationTransaction txI, txD;
            UInt160 sh = UInt160.Zero;
            using (DeployContractDialog dialog = new DeployContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                sh = dialog.ScriptHash;
                txD = dialog.GetTransaction();                
            }
            using (InvokeContractDialog dialog = new InvokeContractDialog(txD))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                txI = dialog.GetTransaction();
            }

            TxRegistration.Register(sh, txD, txI);

            Helper.SignAndShowInformation(txI);
        }

        private void invokeContractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (InvokeContractDialog dialog = new InvokeContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                var tx = dialog.GetTransaction();
                if (dialog.ScriptHash != default(UInt160))
                    TxRegistration.Register(dialog.ScriptHash, null, tx);
                Helper.SignAndShowInformation(tx);
            }
        }

        private void 选举EToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ElectionDialog dialog = new ElectionDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    Helper.SignAndShowInformation(dialog.GetTransaction());
                }
                catch
                {
                    return;
                }
            }
        }

        private void signDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SigningDialog dialog = new SigningDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OptionsDialog dialog = new OptionsDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void WToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://cronfoundation.org/");
        }

        private void DevTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Helper.Show<DeveloperToolsForm>();
        }

        private void AntSharesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // MessageBox.Show($"{Strings.AboutMessage} {Strings.AboutVersion}{Assembly.GetExecutingAssembly().GetName().Version}", Strings.About);
            using (AboutForm f = new AboutForm())
            {
                f.ShowDialog(this);
            }

        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            var sel = listView1.SelectedIndices.Count == 1;
            var wa = sel ? ((WalletAccount)listView1.SelectedItems[0].Tag) : null;
            var bSigCont = wa == null? true : wa.Contract.Script.IsSignatureContract();

            RVToolStripMenuItem.Enabled =
                sel &&
                !wa.WatchOnly && bSigCont;
            viewContractToolStripMenuItem.Enabled =
                sel &&
                !wa.WatchOnly;
            voteToolStripMenuItem.Enabled =
                sel &&
                !wa.WatchOnly &&
                !string.IsNullOrEmpty(listView1.SelectedItems[0].SubItems["CRONIUM"].Text) &&
                decimal.Parse(listView1.SelectedItems[0].SubItems["CRONIUM"].Text,
                NumberStyles.Any, CultureInfo.InvariantCulture) > 0;

            FCToolStripMenuItem.Enabled = listView1.SelectedIndices.Count == 1;

            FDToolStripMenuItem.Enabled = listView1.SelectedIndices.Count > 0;

            if(sel && wa != null)
             invokeToolStripMenuItem.Visible = ! bSigCont;
        }

        private void WNToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.SelectedIndices.Clear();
            WalletAccount account = Program.CurrentWallet.CreateAccount();
            AddAccount(account, true);
            if (Program.CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
        }

        private void importWIFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ImportPrivateKeyDialog dialog = new ImportPrivateKeyDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                listView1.SelectedIndices.Clear();
                foreach (string wif in dialog.PrivKeyStrings)
                {
                    WalletAccount account;
                    try
                    {
                        account = Program.CurrentWallet.Import(wif);
                    }
                    catch (FormatException ex)
                    {
                        continue;
                    }
                    AddAccount(account, true);
                }
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
            }
        }

        private void importFromHexToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ImportPrivateKeyDialog dialog = new ImportPrivateKeyDialog("HEX Private Key:"))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                listView1.SelectedIndices.Clear();
                foreach (string hex in dialog.PrivKeyStrings)
                {
                    WalletAccount account;
                    try
                    {
                        account = Program.CurrentWallet.CreateAccount(hex.HexToBytes());
                    }
                    catch (FormatException ex)
                    {
                        continue;
                    }
                    AddAccount(account, true);
                }
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
            }
        }

        private void importCertificateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SelectCertificateDialog dialog = new SelectCertificateDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                listView1.SelectedIndices.Clear();
                WalletAccount account = Program.CurrentWallet.Import(dialog.SelectedCertificate);
                AddAccount(account, true);
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
            }
        }

        private void importWatchOnlyAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string text = InputBox.Show(Strings.Address, Strings.ImportWatchOnlyAddress);
            if (string.IsNullOrEmpty(text)) return;
            using (StringReader reader = new StringReader(text))
            {
                while (true)
                {
                    string address = reader.ReadLine();
                    if (address == null) break;
                    address = address.Trim();
                    if (string.IsNullOrEmpty(address)) continue;
                    UInt160 scriptHash;
                    try
                    {
                        scriptHash = address.ToScriptHash();
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    WalletAccount account = Program.CurrentWallet.CreateAccount(scriptHash);
                    AddAccount(account, true);
                }
            }
            if (Program.CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
        }

        private void KMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CreateMultiSigContractDialog dialog = new CreateMultiSigContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Contract contract = dialog.GetContract();
                if (contract == null)
                {
                    MessageBox.Show(Strings.AddContractFailedMessage);
                    return;
                }
                WalletAccount account = Program.CurrentWallet.CreateAccount(contract, dialog.GetKey());
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
                listView1.SelectedIndices.Clear();
                AddAccount(account, true);
            }
        }

        private void lockToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (CreateLockAccountDialog dialog = new CreateLockAccountDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Contract contract = dialog.GetContract();
                if (contract == null)
                {
                    MessageBox.Show(Strings.AddContractFailedMessage);
                    return;
                }
                WalletAccount account = Program.CurrentWallet.CreateAccount(contract, dialog.GetKey());
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
                listView1.SelectedIndices.Clear();
                AddAccount(account, true);
            }
        }

        private void ImportDialogCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ImportCustomContractDialog dialog = new ImportCustomContractDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Contract contract = dialog.GetContract();
                WalletAccount account = Program.CurrentWallet.CreateAccount(contract, dialog.GetKey());
                if (Program.CurrentWallet is NEP6Wallet wallet)
                    wallet.Save();
                listView1.SelectedIndices.Clear();
                AddAccount(account, true);
            }
        }

        private void RVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WalletAccount account = (WalletAccount)listView1.SelectedItems[0].Tag;
            using (ViewPrivateKeyDialog dialog = new ViewPrivateKeyDialog(account))
            {
                dialog.ShowDialog();
            }
        }

        private void viewContractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WalletAccount account = (WalletAccount)listView1.SelectedItems[0].Tag;
            using (ViewContractDialog dialog = new ViewContractDialog(account.Contract))
            {
                dialog.ShowDialog();
            }
        }

        private void voteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WalletAccount account = (WalletAccount)listView1.SelectedItems[0].Tag;
            using (VotingDialog dialog = new VotingDialog(account.ScriptHash))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    Helper.SignAndShowInformation(dialog.GetTransaction());
                }
                catch
                {
                    return;
                }
            }
        }

        private void FCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(listView1.SelectedItems[0].Text);
            }
            catch (ExternalException) { }
        }

        private void FDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(Strings.DeleteAddressConfirmationMessage, Strings.DeleteAddressConfirmationCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            WalletAccount[] accounts = listView1.SelectedItems.OfType<ListViewItem>().Select(p => (WalletAccount)p.Tag).ToArray();
            foreach (WalletAccount account in accounts)
            {
                listView1.Items.RemoveByKey(account.Address);
                Program.CurrentWallet.DeleteAccount(account.ScriptHash);
            }
            if (Program.CurrentWallet is NEP6Wallet wallet)
                wallet.Save();
            balance_changed = true;
            check_nep5_balance = true;
        }

        private void contextMenuStrip2_Opening(object sender, CancelEventArgs e)
        {
            viewCertificateToolStripMenuItem.Enabled = listView2.SelectedIndices.Count == 1;
            if (viewCertificateToolStripMenuItem.Enabled)
            {
                CertificateQueryResultType? type = (CertificateQueryResultType?)listView2.SelectedItems[0].SubItems["issuer"].Tag;
                viewCertificateToolStripMenuItem.Enabled = type == CertificateQueryResultType.Good || type == CertificateQueryResultType.Expired || type == CertificateQueryResultType.Invalid;
            }
            FDToolStripMenuItem1.Enabled = listView2.SelectedIndices.Count > 0;
            if (FDToolStripMenuItem1.Enabled)
            {
                FDToolStripMenuItem1.Enabled = listView2.SelectedItems.OfType<ListViewItem>().Select(p => p.Tag as AssetState).All(p => p == null || (p.AssetType != AssetType.GoverningToken && p.AssetType != AssetType.UtilityToken));
            }
        }

        private void viewCertificateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AssetState asset = (AssetState)listView2.SelectedItems[0].Tag;
            UInt160 hash = Contract.CreateSignatureRedeemScript(asset.Owner).ToScriptHash();
            string address = hash.ToAddress();
            string path = Path.Combine(Settings.Default.Paths.CertCache, $"{address}.cer");
            Process.Start(path);
        }

        private void FDToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count == 0) return;
            var delete = listView2.SelectedItems.OfType<ListViewItem>().Select(p => p.Tag as AssetState).Where(p => p != null).Select(p => new
            {
                Asset = p,
                Value = Program.CurrentWallet.GetAvailable(p.AssetId)
            }).ToArray();
            if (delete.Length == 0) return;
            if (MessageBox.Show($"{Strings.DeleteAssetConfirmationMessage}\n"
                + string.Join("\n", delete.Select(p => $"{Helper.CustomGetAssetName(p.Asset)}:{p.Value}"))
                , Strings.DeleteConfirmation, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            ContractTransaction tx = Program.CurrentWallet.MakeTransaction(new ContractTransaction
            {
                Outputs = delete.Select(p => new TransactionOutput
                {
                    AssetId = p.Asset.AssetId,
                    Value = p.Value,
                    ScriptHash = RecycleScriptHash
                }).ToArray()
            }, fee: Fixed8.Zero);
            try
            {
                Helper.SignAndShowInformation(tx);
            }
            catch
            {
                return;
            }

        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (listView3.SelectedItems.Count == 0) return;
            Clipboard.SetDataObject(listView3.SelectedItems[0].SubItems[1].Text);
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0) return;
            string url = string.Format(Settings.Default.Urls.AddressUrl, listView1.SelectedItems[0].Text);
            Process.Start(url);
        }

        private void listView2_DoubleClick(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count == 0) return;
            string url = string.Format(Settings.Default.Urls.AssetUrl, listView2.SelectedItems[0].Name.Substring(2));
            Process.Start(url);
        }

        private void listView3_DoubleClick(object sender, EventArgs e)
        {
            if (listView3.SelectedIndices.Count == 0) return;
            string url = string.Format(Settings.Default.Urls.TransactionUrl, listView3.SelectedItems[0].Name.Substring(2));
            Process.Start(url);
        }

        private void toolStripStatusLabel3_Click(object sender, EventArgs e)
        {
            using (UpdateDialog dialog = new UpdateDialog((XDocument)toolStripStatusLabel3.Tag))
            {
                dialog.ShowDialog();
            }
        }

        void OpenNep6Wallet(string password, string walletPath)
        {
            NEP6Wallet wallet = new NEP6Wallet(GetIndexer(), walletPath);
            wallet.Unlock(password);
            wallet.Save();
            ChangeWallet(wallet);
            Settings.Default.LastWalletPath = walletPath;
            Settings.Default.Save();
        }

        private void transferToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // MessageBox.Show("Todo - for addy we clicked on: Transfer Dlg and add it to the list of recipients");

            if (listView1.SelectedIndices.Count == 0)
                return;

              string [] L = new  string [listView1.SelectedItems.Count];
            for(int i = 0; i < L.Length; i++)
                L[i] = listView1.SelectedItems[i].Text;
             
            
            Transaction tx;
            UInt160 change_address;
            Fixed8 fee;
            
            var assets = Program.CurrentWallet.FindUnspentCoins()
                .Select(p => p.Output.AssetId).Distinct()
                .Select(x => Helper.CustomAssetDescriptor(x));
            var names = assets.Select(y => y.AssetName);
            string assetNames = string.Join(", ", names);

            if (!(_strTransfer?.Length > 0))
            {
                _strTransfer = $"Examples:{Environment.NewLine}";
                _strTransfer += string.Join(Environment.NewLine, names.Select(x => x.Length + " " + x));
            }

            _strTransfer = InputBox.Show($"Amount to transfer (type {assetNames})", "Enter amount",
                _strTransfer);
            if (string.IsNullOrWhiteSpace(_strTransfer))
                return;

            Dictionary<UIntBase, decimal> D = new Dictionary<UIntBase, decimal>();
            string[] list1 = _strTransfer.Split(new []{ $"{Environment.NewLine}" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var L1 in list1)
            {
                string[] list2 = L1.Split(new[] { '\t', ' '}, 2, StringSplitOptions.RemoveEmptyEntries);
                if (list2.Length != 2)
                    continue;
                double amount = 50000;
                if (!double.TryParse(list2[0], NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                    continue;
                decimal amt = (decimal)amount;
                AssetDescriptor a = assets.Where(x => x.AssetName == list2[1]).SingleOrDefault();
                
                if(a != null && names.Contains(a?.AssetName))
                {
                    if (D.ContainsKey(a.AssetId))
                        D[a.AssetId] = amt + D[a.AssetId];
                    else
                        D[a.AssetId] = amt;
                }
            }

            if (D.Count == 0)
            {
                MessageBox.Show("No assets available for transferring. Make sure you have enough funds");
                return;
            }

            using (TransferDialog dialog = new TransferDialog(L, D))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                tx = dialog.GetTransaction();
                change_address = dialog.ChangeAddress;
                fee = dialog.Fee;
            }
            if (tx is InvocationTransaction itx)
            {
                using (InvokeContractDialog dialog = new InvokeContractDialog(itx))
                {
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    tx = dialog.GetTransaction(change_address, fee);

                    if (dialog.ScriptHash != default(UInt160))
                        TxRegistration.Register(dialog.ScriptHash, null, tx as InvocationTransaction);
                }
            }
            Helper.SignAndShowInformation(tx);
        }

        private void invokeToolStripMenuItem_Click(object sender, EventArgs e)
        {
           
           var wa = (WalletAccount) listView1?.SelectedItems[0]?.Tag;
            if (wa == null)
                return;
           UInt160 sh =  wa.Contract.ScriptHash;
                      
            using (InvokeContractDialog dialog = new InvokeContractDialog(sh))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                var tx = dialog.GetTransaction();
                TxRegistration.Register(sh, null, tx);
                Helper.SignAndShowInformation(tx);
            }
        }

        private void gyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var lvi = listView3?.SelectedItems[0];
            if (lvi == null)
                return;

            Transaction tx = (Transaction)lvi.SubItems[0]?.Tag;
            if (tx == null)
                return;

            ShowTxnInfoEx(tx);

                 
            }


        private void distributeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count == 0) return;
            string id = listView2.SelectedItems[0].Name.Substring(2);
            UInt256.TryParse(id, out UInt256 asset_id);
            AssetState ast = Blockchain.Singleton.GetSnapshot().Assets.TryGet(asset_id);

            using (IssueDialog dialog = new IssueDialog(ast))
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                Helper.SignAndShowInformation(dialog.GetTransaction());
            }
        }

      
    }
}


 
