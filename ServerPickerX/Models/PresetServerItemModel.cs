using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ServerPickerX.Models
{
    public partial class PresetServerItemModel : ObservableObject
    {
        private static readonly Dictionary<string, string> FlagSortKeyCache = new(StringComparer.OrdinalIgnoreCase);

        public ServerModel ServerModel { get; }

        public string Key { get; }

        public string Flag => ServerModel.Flag;

        public string FlagSortKey { get; }

        public string Name => ServerModel.Name;

        public string Description => ServerModel.Description;

        [ObservableProperty]
        private bool isBlocked;

        public PresetServerItemModel(ServerModel serverModel, string key, bool isBlocked)
        {
            ServerModel = serverModel;
            Key = key;
            IsBlocked = isBlocked;
            FlagSortKey = GetFlagSortKey(serverModel.Flag);
        }

        private static string GetFlagSortKey(string flagPath)
        {
            if (string.IsNullOrWhiteSpace(flagPath))
            {
                return string.Empty;
            }

            if (FlagSortKeyCache.TryGetValue(flagPath, out string? cachedValue))
            {
                return cachedValue;
            }

            string assetUri = $"avares://ServerPickerX{flagPath}";

            try
            {
                using var stream = AssetLoader.Open(new Uri(assetUri));
                byte[] hash = SHA256.HashData(stream);
                string flagSortKey = Convert.ToHexString(hash);
                FlagSortKeyCache[flagPath] = flagSortKey;

                return flagSortKey;
            }
            catch
            {
                FlagSortKeyCache[flagPath] = flagPath;

                return flagPath;
            }
        }
    }
}
