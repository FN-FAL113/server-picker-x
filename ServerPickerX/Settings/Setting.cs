using System.Threading.Tasks;

namespace ServerPickerX.Settings
{
    public abstract class Setting
    {
        // Override this method on derived or implementing classes 
        public abstract Task<Setting> LoadSettingsAsync();

        public abstract Task<bool> SaveSettingsAsync();
    }
}
