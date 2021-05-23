using Neo.Ledger;

namespace Neo.Models
{
    public class AccountAssetBalance
    {
        public AssetState Asset { get; set; }
        public Fixed8 Balance { get; set; }
    }
}
