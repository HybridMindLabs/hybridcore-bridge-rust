using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("HybridCore Bridge", "HybridMind Labs", "1.0.0")]
    [Description("Connects a Rust server to HybridCore — polls for queued commands (vote/store/giveaway rewards, bans) and executes them in-game.")]
    internal class HybridCoreBridge : CovalencePlugin
    {
        private const string Prefix = "[HybridCore] ";
        private ConfigData config;

        // ── Config ───────────────────────────────────────────────

        private class ConfigData
        {
            [JsonProperty("Site base URL (no trailing slash)")]
            public string BaseUrl = "https://your-community.com";

            [JsonProperty("Bridge token (hcb_...)")]
            public string BridgeToken = "none";

            [JsonProperty("Poll interval (seconds)")]
            public float PollInterval = 5f;

            [JsonProperty("Debug logging")]
            public bool Debug = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>() ?? new ConfigData();
            }
            catch
            {
                PrintWarning(Prefix + "Config was invalid; recreating defaults.");
                config = new ConfigData();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private bool Configured =>
            !string.IsNullOrEmpty(config.BridgeToken) && config.BridgeToken.StartsWith("hcb_");

        // ── Lifecycle ────────────────────────────────────────────

        private void Init()
        {
            permission.RegisterPermission("hybridcore.bridge.admin", this);

            if (!Configured)
            {
                PrintWarning(Prefix + "Set a valid bridge token (hcb_...) in oxide/config/HybridCoreBridge.json");
                return;
            }

            var interval = Math.Max(2f, config.PollInterval);
            timer.Every(interval, Poll);
            Log(Prefix + $"Polling {config.BaseUrl} every {interval}s.");
        }

        [Command("hcbridge.poll"), Permission("hybridcore.bridge.admin")]
        private void CmdForcePoll(IPlayer player, string command, string[] args)
        {
            if (!Configured)
            {
                player.Reply(Prefix + "Not configured — set a bridge token first.");
                return;
            }

            player.Reply(Prefix + "Polling...");
            Poll();
        }

        // ── Poll / ack ───────────────────────────────────────────

        private void Poll()
        {
            if (!Configured) return;

            var url = config.BaseUrl.TrimEnd('/') + "/api/bridge/poll";

            webrequest.Enqueue(url, string.Empty, (code, response) =>
            {
                if (code == 401)
                {
                    PrintError(Prefix + "Bridge token rejected (401). Check the config.");
                    return;
                }
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    if (config.Debug) Log(Prefix + "Poll HTTP " + code);
                    return;
                }

                HandlePoll(response);
            }, this, RequestMethod.POST, JsonHeaders());
        }

        private void HandlePoll(string response)
        {
            PollResponse data;
            try
            {
                data = JsonConvert.DeserializeObject<PollResponse>(response);
            }
            catch (Exception e)
            {
                PrintError(Prefix + "Bad JSON in poll response: " + e.Message);
                return;
            }

            if (data?.Commands == null || data.Commands.Count == 0) return;

            var ids = new List<int>();
            foreach (var item in data.Commands)
            {
                if (item.Id <= 0 || string.IsNullOrEmpty(item.Command)) continue;

                server.Command(item.Command);
                ids.Add(item.Id);

                if (config.Debug) Log(Prefix + $"Exec #{item.Id}: {item.Command}");
            }

            if (ids.Count > 0)
            {
                Ack(ids);
                Log(Prefix + $"Executed {ids.Count} command(s).");
            }
        }

        private void Ack(List<int> ids)
        {
            var url = config.BaseUrl.TrimEnd('/') + "/api/bridge/ack";
            var body = JsonConvert.SerializeObject(new { ids });

            webrequest.Enqueue(url, body, (code, response) =>
            {
                if (config.Debug) Log(Prefix + "Ack HTTP " + code);
            }, this, RequestMethod.POST, JsonHeaders());
        }

        private Dictionary<string, string> JsonHeaders() => new Dictionary<string, string>
        {
            { "Authorization", "Bearer " + config.BridgeToken },
            { "Content-Type", "application/json" },
            { "Accept", "application/json" },
            { "X-Requested-With", "XMLHttpRequest" },
        };

        // ── DTOs ─────────────────────────────────────────────────

        private class PollResponse
        {
            [JsonProperty("commands")] public List<CommandItem> Commands { get; set; }
        }

        private class CommandItem
        {
            [JsonProperty("id")] public int Id { get; set; }
            [JsonProperty("command")] public string Command { get; set; }
        }
    }
}
