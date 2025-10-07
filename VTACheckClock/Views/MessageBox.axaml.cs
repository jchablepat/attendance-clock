using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Interactivity;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;

namespace VTACheckClock.Views
{
    public partial class MessageBox : Window
    {
        public enum MessageBoxButtons {
            Ok,
            OkCancel,
            YesNo,
            YesNoCancel
        }

        public enum MessageBoxResult
        {
            Ok,
            Cancel,
            Yes,
            No
        }

        public MessageBox()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public static Task<MessageBoxResult>? Show(Window? parent, string title, string text, MessageBoxButtons buttons)
        {
            try {
                Bitmap bitmap = new(AssetLoader.Open(
                     new Uri($"avares://{Assembly.GetExecutingAssembly().GetName().Name}/Assets/question.png"))
                );

                var msgbox = new MessageBox() {
                    Icon = new WindowIcon(bitmap),
                    Title = title,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    Background = Brushes.GhostWhite
                };

                msgbox.FindControl<TextBlock>("Text").Text = text;
                var buttonPanel = msgbox.FindControl<StackPanel>("Buttons");

                var res = MessageBoxResult.Ok;

                void AddButton(string caption, MessageBoxResult r, bool def = false) {
                    var btn = new Button { 
                        Content = caption,
                        Width = 100,
                        Background = Brushes.LightGray
                    };

                    btn.Click += (sender, e) => {
                        res = r;
                        msgbox.Close();
                    };

                    buttonPanel.Children.Add(btn);
                    if (def)
                        res = r;
                }

                if (buttons == MessageBoxButtons.Ok || buttons == MessageBoxButtons.OkCancel)
                    AddButton("Ok", MessageBoxResult.Ok, true);
                if (buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel)
                {
                    AddButton("Yes", MessageBoxResult.Yes);
                    AddButton("No", MessageBoxResult.No, true);
                }

                if (buttons == MessageBoxButtons.OkCancel || buttons == MessageBoxButtons.YesNoCancel)
                    AddButton("Cancel", MessageBoxResult.Cancel, true);


                var tcs = new TaskCompletionSource<MessageBoxResult>();
                msgbox.Closed += delegate { tcs.TrySetResult(res); };
                //if (parent != null) {
                //    msgbox.ShowDialog(parent);
                //}
                //else msgbox.Show();
                var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                var windows = MainWindow?.OwnedWindows;
                if (windows?.Count > 0) {
                   msgbox.ShowDialog(windows[0]);
                } else {
                   msgbox.ShowDialog(MainWindow!);
                }
                return tcs.Task;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// <para>Muestra un mensaje en una ventana nueva.</para>
        /// <para>Default:</para>
        /// Width = 500, Height = 120
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns>ButtonResult</returns>
        public static async Task ShowMessage(/*Window parent = null,*/ string title = "", string message = "", int width = -1, int height = -1, SizeToContent sizeToContent = SizeToContent.Manual)
        {
            try {
                var msgParams = new MessageBoxStandardParams
                {
                    CanResize = false,
                    ContentTitle = title,
                    ContentMessage = message,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    SizeToContent = sizeToContent,
                    Width = width != -1 ? width : 500,
                    Topmost = true,
                    ButtonDefinitions = ButtonEnum.Ok,
                    Icon = MsBox.Avalonia.Enums.Icon.Info
                };

                if (height != -1) {
                    msgParams.Height = height;
                } else {
                    msgParams.SizeToContent = SizeToContent.Height;
                }

                var alert = MessageBoxManager.GetMessageBoxStandard(msgParams);
                //if (parent != null) {
                //    parent.IsVisible = true;
                //    await alert.ShowDialog(parent);
                //} else {
                //    await alert.Show();
                //}
                var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                var windows = MainWindow?.OwnedWindows;
                if (windows?.Count > 0 && windows[0].IsVisible) {
                    await alert.ShowWindowDialogAsync(windows[0]);
                } else {
                    await alert.ShowWindowDialogAsync(MainWindow!);
                }
            } catch(Exception exc) {
                Debug.WriteLine(exc);
            }
        }
    
        public static async Task<ButtonResult> ShowPrompt(string title, string message)
        {
            Bitmap bitmap = new(AssetLoader.Open(
                 new Uri($"avares://{Assembly.GetExecutingAssembly().GetName().Name}/Assets/question.png"))
            );

            var msgParams = new MessageBoxStandardParams {
                CanResize = false,
                ContentTitle = title,
                ContentMessage = message,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SizeToContent = SizeToContent.Manual,
                SystemDecorations = SystemDecorations.Full,
                Width = 350,
                Height = 100,
                Topmost = true,
                ButtonDefinitions = ButtonEnum.YesNo,
                WindowIcon = new WindowIcon(bitmap),
                Icon = MsBox.Avalonia.Enums.Icon.Question
            };

            var prompt = MessageBoxManager.GetMessageBoxStandard(msgParams);
            var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var windows = MainWindow?.OwnedWindows;
            if (windows?.Count > 0) {
               return await prompt.ShowWindowDialogAsync(windows[0]);
            } else {
               return await prompt.ShowWindowDialogAsync(MainWindow!);
            }
        }
    }
}
