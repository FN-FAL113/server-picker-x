using ServerPickerX.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;

namespace ServerPickerX.Services.Servers
{
    public class ConfiguredServerDataService : GenericService
    {
        private readonly ServerDefinition _definition;

        public ConfiguredServerDataService(
            ServerDefinition definition,
            ILoggerService logger,
            IMessageBoxService messageBoxService,
            HttpClient httpClient
        ) : base(logger, messageBoxService, httpClient)
        {
            _definition = definition;
        }

        protected override string ResponseUrl => string.Format(_definition.ResponseUrlTemplate, _definition.AppId);

        protected override string ServiceDisplayName => _definition.DisplayName ?? _definition.Id;

        protected override bool IsServerAccepted(string serverDescription)
        {
            if (_definition.KeywordFilterMode?.ToLower() == "include")
            {
                return _definition.Keywords.Any(k => serverDescription.Contains(k));
            }

            if (_definition.KeywordFilterMode?.ToLower() == "exclude")
            {
                return !_definition.Keywords.Any(k => serverDescription.Contains(k));
            }

            return true;
        }

        public override List<string> GetClusterKeywords()
        {
            return _definition.ClusterKeywords ?? new List<string>();
        }
    }
}
