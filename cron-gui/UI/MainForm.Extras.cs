using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.UI
{

    public class SmartContractAutoDeploy
    {
        public string AvmPath { get; set; }
        public string ParamList { get; set; } = "0710";
        public string RetValType { get; set; } = "F0";
        public bool HasStorage { get; set; } = true;
        public bool HasDynamicInvoke { get; set; } = false;
        public bool Payable { get; set; } = false;
        public string Author { get; set; } = "Unknown";
        public string EMail { get; set; } = "unknown@email.local";
        public string Description { get; set; } = "Unknown smart contract";
    }
    // private static readonly Fixed8 net_fee = Fixed8.FromDecimal(1m);

    public class TxRegistration
    {
        static Dictionary<UInt160, TxInfoHolder> _mapSC = new Dictionary<UInt160, TxInfoHolder>();

        internal static void Register(UInt160 sh, Transaction txSrc, InvocationTransaction txInvoke)
        {
            _mapSC[sh] =
                new TxInfoHolder { ScriptHash = sh, TxSrc = txSrc, TxInvoke = txInvoke };
        }

        internal static TxInfoHolder GetByTxInvokeHash(UInt256 hash)
        {
            return _mapSC.Where(x => x.Value.TxInvoke.Hash.Equals(hash)).SingleOrDefault().Value;
        }
    }

    public class TxInfoHolder
    {
        public UInt160 ScriptHash { get; set; }
        public InvocationTransaction TxInvoke { get; set; }
        public Transaction TxSrc { get; set; }
        public object Extra { get; set; }
    }

    class XPersistence : Plugin, IPersistencePlugin
    {

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> aeList)
        {

            if (aeList.Count > 0)
            {
                foreach (var v in aeList)
                {
                    var g = TxRegistration.GetByTxInvokeHash(v.Transaction.Hash);
                    if (g != null)
                        g.Extra = JsonConvert.SerializeObject(v, Formatting.Indented);
                    
                    /*
                    if (v.ExecutionResults?[0].VMState == VM.VMState.HALT
                        && v.Transaction.Type == TransactionType.InvocationTransaction)
                    {
                        // TODO: catch successful txn Invocation here
                    } //*/
                }
            }
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return false;
        }

        public override void Configure()
        {
        }

        public void OnCommit(Snapshot snapshot)
        {
        }

    }

    internal partial class MainForm
    {
        /// *GEORGE* <george@cryptopass.team>
        /// Here was some experimental SC auto-deployment code. 
        /// It has been removed as unused. 
        /// May be I'll put it back here later.

        // To catch an invocation result
        XPersistence _persistence = null;

        void AfterLoad()
        {
            _persistence = new XPersistence();
            Extras.Extras.InitializeExtras(this);
        }

        private void ShowTxnInfoEx(Transaction tx)
        {
            using (PropertyGridForm pgf = new PropertyGridForm())
            {
                pgf.Text = tx.Hash.ToString() + " txn properties";

                Transaction txB = null;
                try { txB = Blockchain.Singleton.GetTransaction(tx.Hash); } catch { }

                pgf.SetReadonly(new
                {
                    TxInBlockchain = JsonConvert.SerializeObject(txB, Formatting.Indented),

                    // *GEORGE* 
                    // TODO: encrypt/decrypt data in attributes.
                    // Each txn must have an individual private key for decrypting and blockchain should not to store it.

                    Attributes = JsonConvert.SerializeObject(
                        txB.Attributes.Select(x => new { x.Usage, Data = Encoding.UTF8.GetString(x.Data) }).ToList()),

                    TxLocal = JsonConvert.SerializeObject(tx, Formatting.Indented),
                    Obj = TxRegistration.GetByTxInvokeHash(tx.Hash)
                });

                pgf.ShowDialog(this);
            }
        }
    }
}
 
