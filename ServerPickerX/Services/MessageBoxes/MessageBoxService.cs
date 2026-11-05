using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using ServerPickerX.Helpers;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.Processes;
using ServerPickerX.Views;
using System;
using System.Threading.Tasks;

namespace ServerPickerX.Services.MessageBoxes
{
    public class MessageBoxService : IMessageBoxService
    {
        private readonly IProcessService _processService;
        private readonly ILoggerService _logger;

        public MessageBoxService(ILoggerService logger, IProcessService processService)
        {
            _logger = logger;
            _processService = processService;
        }

        private static async Task<string> ShowDialogAndWaitForCloseAsync(IMsBox<string> box)
        {
            string result = await box.ShowWindowDialogAsync(MainWindow.Instance!);

            // The dialog result is set before its window is destroyed and callers would resume inline,
            // on the same call stack as the button click. Yield so the window finishes closing first,
            // long running work in a caller can otherwise deadlock with the Avalonia render thread.
            await Task.Yield();

            return result;
        }

        public async Task ShowMessageBoxAsync(string title, string text, Icon icon = Icon.Info)
        {
            try
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

                await ShowDialogAndWaitForCloseAsync(box);
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to show message box", ex.Message);
            }
        }

        public async Task<bool> ShowMessageBoxConfirmationAsync(string title, string text, Icon icon = Icon.Info)
        {
            try
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

                var result = await ShowDialogAndWaitForCloseAsync(box);

                return result == "Ok";
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to show message box confirmation", ex.Message);
                return false;
            }
        }

        public async Task ShowMessageBoxWithLinkAsync(string title, string text, string url, Icon icon = Icon.Info)
        {
            try
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

                var result = await ShowDialogAndWaitForCloseAsync(box);

                if (result != "Ok")
                {
                    return;
                }

                await _processService.OpenUrl(url);
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to show message box with hyperlink", ex.Message);
            }
        }
    }
}