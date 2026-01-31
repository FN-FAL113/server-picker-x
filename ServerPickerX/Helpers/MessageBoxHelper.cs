using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using ServerPickerX.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ServerPickerX.Helpers
{
    public class MessageBoxHelper
    {
        public static async Task ShowMessageBox(string title, string text, Icon icon = Icon.Info)
        {
            var customMbsParams = new MessageBoxCustomParams
            {
                ContentTitle = title,
                ContentMessage = text,
                ButtonDefinitions = [
                        new() { Name = "Ok", },
                    ],
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                ShowInCenter = true,
                CanResize = false,
                Icon = title == "Error" ? Icon.Error : icon,
                Topmost = true,
            };

            var box = MessageBoxManager.GetMessageBoxCustom(customMbsParams);

            await box.ShowWindowDialogAsync(MainWindow.Instance);
        }

        public static async Task ShowMessageBoxWithLink(string title, string text, string url, Icon icon = Icon.Info)
        {
            var customMbsParams = new MessageBoxCustomParams
            {
                ContentTitle = title,
                ContentMessage = text,
                ButtonDefinitions = [
                        new() { Name = "Ok", },
                        new() { Name = "Cancel", },
                    ],
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                ShowInCenter = true,
                CanResize = false,
                Icon = title == "Error" ? Icon.Error : icon,
                Topmost = true,
            };

            var box = MessageBoxManager.GetMessageBoxCustom(customMbsParams);

            var result = await box.ShowWindowDialogAsync(MainWindow.Instance);

            if (result != "Ok")
            {
                return;
            }

            ProcessHelper.createProcessFromUrl(url);
        }
    }
}
