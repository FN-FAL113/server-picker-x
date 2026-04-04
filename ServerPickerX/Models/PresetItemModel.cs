using CommunityToolkit.Mvvm.ComponentModel;

namespace ServerPickerX.Models
{
    public class PresetItemModel : ObservableObject
    {
        public ServerPresetModel Preset { get; }

        public string Name
        {
            get => Preset.Name;
            set
            {
                if (Preset.Name == value)
                {
                    return;
                }

                Preset.Name = value;
                OnPropertyChanged();
            }
        }

        public PresetItemModel(ServerPresetModel preset)
        {
            Preset = preset;
        }
    }
}
