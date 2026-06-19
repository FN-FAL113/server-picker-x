using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ServerPickerX.Services.Servers
{
    public class ServerDefinitionProvider
    {
        private readonly List<ServerDefinition> _serverDefinitions = [];
        private readonly JsonSerializerOptions _jsonDeserializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
        };
        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
        };

        [RequiresUnreferencedCode(message: "Deserialization involves reflection which may be trimmed away.")]
        public ServerDefinitionProvider(ILoggerService _loggerService)
        {
            var path = "";
            try
            {
                path = Path.Combine(AppContext.BaseDirectory, "ServerDefinitions.json");

                if (!File.Exists(path)) 
                {
                    var defaults = CreateDefaultServerDefinitions();
                    string serializedJson = JsonSerializer.Serialize(defaults, _jsonSerializerOptions);

                    File.WriteAllText(path, serializedJson);
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogErrorAsync(ex.Message);

                throw;
            }

            var json = File.ReadAllText(path);

            var serverDefinitions = JsonSerializer.Deserialize<List<ServerDefinition>>(json, _jsonDeserializerOptions);

            _serverDefinitions = serverDefinitions ?? [];
        }

        public IReadOnlyList<ServerDefinition> GetDefinitions() => _serverDefinitions.AsReadOnly();

        public IReadOnlyList<string> GetGameModes()
        {
            return _serverDefinitions
                .Select(definition => definition.GameMode)
                .Where(gameMode => !string.IsNullOrWhiteSpace(gameMode))
                .ToList()
                .AsReadOnly();
        }

        public ServerDefinition? GetServerDefinitionByGameMode(string gameMode)
        {
            return _serverDefinitions
                .FirstOrDefault(
                    definition => definition.GameMode.Equals(gameMode, StringComparison.OrdinalIgnoreCase)
                );
        }

        public string GetAppIdByGameMode(string gameMode)
        {
            ServerDefinition? definition = GetServerDefinitionByGameMode(gameMode);

            if (definition == null)
            {
                throw new InvalidOperationException($"Unsupported game mode: {gameMode}");
            }

            return definition.AppId.ToString();
        }

        public IReadOnlyList<string> GetGameModesByAppId(string appId)
        {
            return _serverDefinitions
                .Where(definition => definition.AppId.ToString() == appId)
                .Select(definition => definition.GameMode)
                .Where(gameMode => !string.IsNullOrWhiteSpace(gameMode))
                .ToList()
                .AsReadOnly();
        }

        private List<ServerDefinition> CreateDefaultServerDefinitions()
        {
            return
                [
                    new ServerDefinition
                    {
                        GameMode = "Counter Strike 2",
                        Id = "cs2",
                        DisplayName = "CS2",
                        AppId = 730,
                        KeywordFilterMode = "exclude",
                        Keywords = ["China"],
                        ClusterKeywords = ["Hong Kong", "Sweden", "India", "Netherlands"]
                    },
                    new ServerDefinition
                    {
                        GameMode = "Counter Strike 2 (Perfect World)",
                        Id = "cs2_perfect_world",
                        DisplayName = "CS2 Perfect World",
                        AppId = 730,
                        KeywordFilterMode = "include",
                        Keywords = ["China"],
                        ClusterKeywords = ["Tencent", "Alibaba", "Perfect World"]
                    },
                    new ServerDefinition
                    {
                        GameMode = "Deadlock",
                        Id = "deadlock",
                        DisplayName = "Deadlock",
                        AppId = 1422450,
                        KeywordFilterMode = "none",
                        Keywords = [],
                        ClusterKeywords = ["China", "Hong Kong", "Sweden", "India", "Netherlands"]
                    },
                    new ServerDefinition
                    {
                        GameMode = "Marathon",
                        Id = "marathon",
                        DisplayName = "Marathon",
                        AppId = 3065800,
                        KeywordFilterMode = "none",
                        Keywords = [],
                        ClusterKeywords = ["Hong Kong", "Sweden", "India", "Netherlands"]
                    }
                ];
        }
    }
}
