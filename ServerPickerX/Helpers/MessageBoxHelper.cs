using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ServerPickerX.Helpers
{
    public class MessageBoxHelper
    {
        public static async Task ShowMessageBox(string title, string text, ButtonEnum buttonEnum = ButtonEnum.Ok)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, text, buttonEnum);

            await box.ShowAsync();
        }
    }
}
