// ArcaPay - Plugin Oxide/uMod para Rust
// Coloque este arquivo em: server/oxide/plugins/ArcaPay.cs

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ArcaPay", "ArcaPay", "1.0.0")]
    [Description("Entrega automatica de produtos da ArcaPay")]
    public class ArcaPay : CovalencePlugin
    {
        #region Configuration

        private class PluginConfig
        {
            [JsonProperty("Token")]
            public string Token { get; set; } = "SEU_TOKEN_AQUI";

            [JsonProperty("API URL")]
            public string ApiUrl { get; set; } = "https://arcapay.org/api/v1/fivem";

            [JsonProperty("Poll Interval (seconds)")]
            public int PollInterval { get; set; } = 10;

            [JsonProperty("Identifier Type (steam/name)")]
            public string IdentifierType { get; set; } = "steam";

            [JsonProperty("Debug")]
            public bool Debug { get; set; } = false;
        }

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks

        private Timer pollTimer;

        private void OnServerInitialized()
        {
            pollTimer = timer.Every(config.PollInterval, () => PollCommands());
            Puts($"ArcaPay iniciado! Polling a cada {config.PollInterval}s");
        }

        private void Unload()
        {
            pollTimer?.Destroy();
        }

        #endregion

        #region Polling

        private void PollCommands()
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + config.Token,
                ["Accept"] = "application/json"
            };

            webrequest.Enqueue(
                config.ApiUrl + "/pending-commands",
                null,
                (code, response) =>
                {
                    if (code != 200 || string.IsNullOrEmpty(response))
                    {
                        if (config.Debug) Puts($"Polling: HTTP {code}");
                        return;
                    }

                    var commands = JsonConvert.DeserializeObject<List<PendingCommand>>(response);
                    if (commands == null || commands.Count == 0) return;

                    Puts($"[ArcaPay] {commands.Count} comando(s) pendente(s)");

                    foreach (var cmd in commands)
                    {
                        var result = ExecuteCommand(cmd.command);
                        Puts($"[ArcaPay] CMD #{cmd.id} [{(result.Success ? "OK" : "FAIL")}]: {cmd.command} -> {result.Message}");
                        ReportCommand(cmd.id, result.Success, result.Message);
                    }
                },
                this,
                RequestMethod.GET,
                headers,
                10f
            );
        }

        private void ReportCommand(string id, bool success, string message)
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + config.Token,
                ["Content-Type"] = "application/json",
                ["Accept"] = "application/json"
            };

            var body = JsonConvert.SerializeObject(new
            {
                id = id,
                success = success,
                error = success ? null : message,
                response = new { message = message }
            });

            webrequest.Enqueue(
                config.ApiUrl + "/report-command",
                body,
                (code, resp) =>
                {
                    if (config.Debug) Puts($"Report #{id}: HTTP {code}");
                },
                this,
                RequestMethod.POST,
                headers,
                10f
            );
        }

        #endregion

        #region Command Execution

        private class CommandResult
        {
            public bool Success;
            public string Message;
        }

        private CommandResult ExecuteCommand(string commandText)
        {
            try
            {
                var parts = commandText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    // Comando simples sem identifier — executa direto
                    server.Command(commandText);
                    return new CommandResult { Success = true, Message = "Executado: " + commandText };
                }

                var action = parts[0].ToLower();
                var identifier = parts[1];

                // Encontra o jogador
                var player = FindPlayer(identifier);

                // Substitui placeholders
                if (player != null)
                {
                    commandText = commandText.Replace("{player}", player.Name);
                    commandText = commandText.Replace("{steamid}", player.Id);
                }

                // Handlers especificos
                switch (action)
                {
                    case "addmoney":
                    case "givemoney":
                        if (player == null) return new CommandResult { Success = false, Message = "Jogador offline" };
                        var amount = parts.Length >= 3 ? parts[2] : "0";
                        // Usa Economics plugin se disponivel
                        if (Economics != null)
                        {
                            Economics.Call("Deposit", player.Id, double.Parse(amount));
                            return new CommandResult { Success = true, Message = $"Adicionado ${amount} via Economics" };
                        }
                        // Fallback: comando de console
                        server.Command(commandText);
                        return new CommandResult { Success = true, Message = "Executado via console" };

                    case "giveitem":
                        if (player == null) return new CommandResult { Success = false, Message = "Jogador offline" };
                        var itemName = parts.Length >= 3 ? parts[2] : "";
                        var qty = parts.Length >= 4 ? int.Parse(parts[3]) : 1;
                        var basePlayer = player.Object as BasePlayer;
                        if (basePlayer != null)
                        {
                            var itemDef = ItemManager.FindItemDefinition(itemName);
                            if (itemDef != null)
                            {
                                var item = ItemManager.Create(itemDef, qty);
                                if (item != null)
                                {
                                    basePlayer.GiveItem(item);
                                    return new CommandResult { Success = true, Message = $"Item {itemName} x{qty} entregue" };
                                }
                            }
                            return new CommandResult { Success = false, Message = $"Item '{itemName}' nao encontrado" };
                        }
                        return new CommandResult { Success = false, Message = "BasePlayer nao encontrado" };

                    case "addgroup":
                        if (player == null) return new CommandResult { Success = false, Message = "Jogador offline" };
                        var group = parts.Length >= 3 ? parts[2] : "";
                        permission.AddUserGroup(player.Id, group);
                        return new CommandResult { Success = true, Message = $"Grupo '{group}' adicionado" };

                    case "grantperm":
                        if (player == null) return new CommandResult { Success = false, Message = "Jogador offline" };
                        var perm = parts.Length >= 3 ? parts[2] : "";
                        permission.GrantUserPermission(player.Id, perm, null);
                        return new CommandResult { Success = true, Message = $"Permissao '{perm}' concedida" };

                    default:
                        // Comando generico — executa no console do servidor
                        server.Command(commandText);
                        return new CommandResult { Success = true, Message = "Executado via console: " + commandText };
                }
            }
            catch (Exception ex)
            {
                return new CommandResult { Success = false, Message = ex.Message };
            }
        }

        private IPlayer FindPlayer(string identifier)
        {
            if (config.IdentifierType == "steam")
            {
                // Busca por Steam ID
                var p = covalence.Players.FindPlayerById(identifier);
                if (p != null && p.IsConnected) return p;
            }

            // Busca por nome
            foreach (var p in covalence.Players.Connected)
            {
                if (p.Name.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                    return p;
                if (p.Id == identifier)
                    return p;
            }

            return null;
        }

        // Plugin references
        [PluginReference] private Plugin Economics;

        #endregion

        #region Chat Commands

        [Command("arcapay")]
        private void CmdArcaPay(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { player.Reply("Sem permissao."); return; }

            if (args.Length == 0)
            {
                player.Reply("[ArcaPay] Comandos: status, poll, reload");
                return;
            }

            switch (args[0].ToLower())
            {
                case "status":
                    player.Reply($"[ArcaPay] API: {config.ApiUrl}");
                    player.Reply($"[ArcaPay] Token: {config.Token.Substring(0, Math.Min(8, config.Token.Length))}...");
                    player.Reply($"[ArcaPay] Polling: {config.PollInterval}s");
                    break;
                case "poll":
                    player.Reply("[ArcaPay] Polling manual...");
                    PollCommands();
                    break;
                case "reload":
                    LoadConfig();
                    player.Reply("[ArcaPay] Config recarregada.");
                    break;
            }
        }

        #endregion

        #region Models

        private class PendingCommand
        {
            public string id;
            public string command;
            public string name;
            public string order_id;
        }

        #endregion
    }
}
