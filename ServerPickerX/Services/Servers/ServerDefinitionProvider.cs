using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ServerPickerX.Services.Servers
{
    public class ServerDefinitionProvider
    {
        private const string DefinitionsFileName = "ServerDefinitions.json";
        private readonly ServerDefinitionsFile _definitions;
        public ServerDefinitionProvider()
        {
            string path = Path.Combine(Environment.CurrentDirectory, DefinitionsFileName);
            EnsureDefinitionsFileExists(path);
            string json = File.ReadAllText(path);

            ServerDefinitionsFile? doc = JsonSerializer.Deserialize<ServerDefinitionsFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _definitions = doc ?? new ServerDefinitionsFile();
        }

        public IReadOnlyList<ServerDefinition> GetDefinitions() => _definitions.Servers.AsReadOnly();

        public IReadOnlyList<string> GetGameModes()
        {
            return _definitions.Servers
                .Select(definition => definition.GameMode)
                .Where(gameMode => !string.IsNullOrWhiteSpace(gameMode))
                .ToList()
                .AsReadOnly();
        }

        public ServerDefinition? GetDefinitionByGameMode(string gameMode)
        {
            return _definitions.Servers.FirstOrDefault(definition =>
                definition.GameMode.Equals(gameMode, StringComparison.OrdinalIgnoreCase));
        }

        public string GetRevisionKeyByGameMode(string gameMode)
        {
            ServerDefinition? definition = GetDefinitionByGameMode(gameMode);

            if (definition == null)
            {
                throw new InvalidOperationException($"Unsupported game mode: {gameMode}");
            }

            return definition.AppId.ToString();
        }

        public IReadOnlyList<string> GetGameModesByRevisionKey(string revisionKey)
        {
            return _definitions.Servers
                .Where(definition => definition.AppId.ToString() == revisionKey)
                .Select(definition => definition.GameMode)
                .Where(gameMode => !string.IsNullOrWhiteSpace(gameMode))
                .ToList()
                .AsReadOnly();
        }
        
        private static void EnsureDefinitionsFileExists(string path)
        {
            if (File.Exists(path))
            {
                return;
            }

            ServerDefinitionsFile defaults = CreateDefaultDefinitions();
            string json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }

        private static ServerDefinitionsFile CreateDefaultDefinitions()
        {
            return new ServerDefinitionsFile
            {
                Servers =
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
                ]
            };
        }

        private class ServerDefinitionsFile
        {
            public List<ServerDefinition> Servers { get; set; } = new();
        }
    }
}
