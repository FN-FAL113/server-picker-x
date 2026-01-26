using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ServerPickerX.Models
{
    // ObservableObject base class requires a partial class type to  
    // generate boiler plate code for common MVVM implementations
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
