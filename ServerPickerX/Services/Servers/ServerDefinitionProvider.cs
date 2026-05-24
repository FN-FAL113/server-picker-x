using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ServerPickerX.Services.Servers
{
    public class ServerDefinitionProvider
    {
        private static readonly ServerDefinitionsFile _definitions = new();
        static ServerDefinitionProvider()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "ServerDefinitions.json");
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<ServerDefinitionsFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _definitions.Servers = doc?.Servers ?? new List<ServerDefinition>();
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

        private class ServerDefinitionsFile
            {
                public List<ServerDefinition> Servers { get; set; } = new();
            }
    }
}
