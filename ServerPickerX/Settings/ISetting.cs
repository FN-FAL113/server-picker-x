using System.Threading.Tasks;

namespace ServerPickerX.Settings
{
    public interface ISetting
    {
        // Override this method on derived or implementing classes 
        public abstract Task LoadSettingsAsync();

        public abstract Task<bool> SaveSettingsAsync();
    }
}
