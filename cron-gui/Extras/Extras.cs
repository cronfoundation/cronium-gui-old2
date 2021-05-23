using Neo.IO.Json;
using Neo.Network.RPC;
using Neo.Plugins;
using Neo.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Neo.Extras
{
    class Extras
    {
        private static readonly string AppDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static readonly char   ec = Path.DirectorySeparatorChar;
        private static readonly string nl = Environment.NewLine;
        static string _extrasPath = AppDir + $"{ec}extras.json";
        static string _extrasLog = AppDir + $"{ec}extras.log";
        private static ExtrasConfig _config;
        private static MainForm _FM;

        internal static void InitializeExtras(MainForm fMain)
        {
            _FM = fMain;
            LoadExtrasConfig();

            if (Plugin.Plugins.Count > 0)
            {
                LogExtras($"Loaded plugins: { Plugin.Plugins.Select(x => $"{x.Name}: {x.Version}").Tie(", ") }");

                var plugin = Plugin.Plugins.Where(x => x.Name == "RpcSystemAssetTrackerPlugin").SingleOrDefault();
                if(plugin != null)
                {
                    var rpcSAT = plugin as IRpcPlugin;
                    if (rpcSAT == null)
                        return;
                   /* try
                    {
                        var jo = rpcSAT.OnProcess(null, "cron_send",
                            new JArray {
                            "000100000000000000000000000000000000000000000000000000000000f000",
                            "AJYoooT6vupz4rLdoys1YSkes4LThbGud5",
                            10.0,
                            "CRONIUM" });
                           LogExtras(jo.AsString());
                    }
                    catch (RpcException ex)
                    {
                        LogExtras($"Exception sending assets: {ex.HResult}, {ex.Message}");
                    }
                    */
                }
            }
        }

        private static void LoadExtrasConfig()
        {
            try
            {
                LogExtras($"Loading config {_extrasPath}");

                _config = JsonConvert.DeserializeObject<ExtrasConfig>(File.ReadAllText(_extrasPath));
                if (_config == null) throw new Exception("Config is null");

                LogExtras("Create or load 1000 single-addy wallets");
                LogExtras("Use utxo tracking as contract call");
                LogExtras("CREATE CRON EXTRA GUI with UTXO indexer and and not using Wallets/indexers");

                // TODO: create extra tabs in GUI, depending on conf

            }
            catch (Exception ex)
            {
                try
                {
                    LogExtras($"Unable to load config {_extrasPath} : {ex.Message}");
                    _config = new ExtrasConfig();
                    SaveExtrasConfig();
                }
                catch (Exception ex1) { }
            }
        }

        private static void SaveExtrasConfig()
        {
            try
            {
                LogExtras($"Saving config {_extrasPath}");
                File.WriteAllText(_extrasPath, JsonConvert.SerializeObject(_config));
            }
            catch (Exception ex)
            {
                try
                {
                    LogExtras($"Unable to save config {_extrasPath} : {ex.Message}");
                }
                catch (Exception ex1) { }
            }
        }

        private static void LogExtras(string v)
        {
            File.AppendAllText(_extrasLog, $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] {v}{nl}");
        }
    }

    public static class _EXT_EXTRAS_
    {
        public static string Tie<T>(this IEnumerable<T> E, String separator)
            => String.Join(separator, E);

       /* public static T TryGetEnum<T>(this JObject obj, T defaultValue = default(T), bool ignoreCase = false)
        {
            Type enumType = typeof(T);
            object value;
            try
            {
                value = Convert.ChangeType(obj.AsEnum<T>(), enumType.GetEnumUnderlyingType());
            }
            catch (OverflowException)
            {
                return defaultValue;
            }
            object result = Enum.ToObject(enumType, value);
            return Enum.IsDefined(enumType, result) ? (T)result : defaultValue;
        }*/
    }
}
