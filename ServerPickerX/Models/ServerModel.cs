using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ServerPickerX.Models
{
    // ObservableObject base class requries a partial class to  
    // generate boiler plate code in another file with same class name
    public partial class ServerModel: ObservableObject
    {
        public string Flag { get; set; } = "";

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        [ObservableProperty]
        public string? ping;

        [ObservableProperty]
        public string? status;

        public Collection<RelayModel> RelayModels { get; set; } = [];
    }
}
