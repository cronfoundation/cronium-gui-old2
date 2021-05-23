using Neo.Ledger;
using Neo.Models;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Collections.Generic;
using System.Linq;

namespace Neo.UI
{
    partial class MainForm
    {
        private Dictionary<UInt160, AccountBalance> GetUserBalances(Snapshot snapshot)
        {
            var coins = Program.CurrentWallet?.GetCoins()
                .Where(x => !x.State.HasFlag(CoinState.Spent))
                .ToList();

            if (coins == null || !coins.Any())
            {
                return new Dictionary<UInt160, AccountBalance>();
            }

            var result = new Dictionary<UInt160, AccountBalance>();

            var balances = coins.GroupBy(p => p.Output.ScriptHash)
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var balance in balances)
            {
                var items = balance.Value.GroupBy(x => x.Output.AssetId, (k, g) => new
                {
                    Asset = snapshot.Assets.TryGet(k),
                    Value = g.Sum(x => x.Output.Value),
                }).ToList();

                result.Add(balance.Key, new AccountBalance
                {
                    Account = balance.Key,
                    Balances = items.Select(x => new AccountAssetBalance { Asset = x.Asset, Balance = x.Value }).ToList()
                });
            }

            return result;
        }

        private IEnumerable<string> GetUserAssetNames()
        {
            yield return "CRONIUM";
            yield return "CRON";

            var baseAssets = new[] { AssetType.GoverningToken, AssetType.UtilityToken };

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                var coins = Program.CurrentWallet?.GetCoins().Where(x => !x.State.HasFlag(CoinState.Spent)).ToList();

                if (coins == null || !coins.Any())
                {
                    yield break;
                }

                var assets = coins.GroupBy(x => x.Output.AssetId, (k, g) => snapshot.Assets.TryGet(k))
                    .ToDictionary(x => x.AssetId);

                foreach (var asset in assets)
                {
                    if (asset.Value == null || baseAssets.Any(x => x == asset.Value.AssetType))
                    {
                        continue;
                    }

                    yield return asset.Value.GetName();
                }
            }
        }
    }
}
