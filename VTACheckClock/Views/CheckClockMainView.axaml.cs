using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.ApplicationLifetimes;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Platform;
using VTACheckClock.ViewModels;
using System;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace VTACheckClock.Views
{
    public partial class CheckClockMainView : UserControl
    {
        public CheckClockMainView()
        {
            InitializeComponent();
            //this.FindControl<DataGrid>("dgAttsList").PropertyChanged += dgAttsListPropertyChanged;
            var lsbAttsList = this.FindControl<ListBox>("lsbAttsList");

            this.Get<Button>("Dialog").Click += async delegate
            {
                //var dialog = ShowEvtPrompt();
                var dialog = new EventPromptWindow
                {
                    DataContext = new EventPromptViewModel(TimeSpan.MinValue),
                    ShowInTaskbar = false
                };

                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
                {
                    var _result = await dialog.ShowDialog<string>(mainWindow);
                    Debug.WriteLine(_result);
                }
            };
        }

        public static Window ShowEvtPrompt()
        {
            Button btnEvtExit, btnEvtEnter;

            var window = new Window {
                ExtendClientAreaToDecorationsHint = true,
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome,
                ExtendClientAreaTitleBarHeightHint = -1,
                Background = Brushes.WhiteSmoke,
                Height = 220,
                Width = 500,
                Content = new StackPanel {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10),
                    Spacing = 4,
                    Children = {
                        new TextBlock { 
                            Text = "Han transcurrido más de xxx horas con xxx minutos desde su entrada.",
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 20,
                            FontWeight = FontWeight.Bold,
                            Foreground =  Brushes.Firebrick,
                            Margin = new Thickness(10)
                        },
                        new TextBlock {
                            Text = "¿Está registrando su SALIDA o una nueva ENTRADA?",
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 20,
                            FontWeight = FontWeight.Bold,
                            Foreground =  Brushes.DarkBlue,
                            Margin = new Thickness(10)
                        },
                        new DockPanel() {
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children = {
                                (btnEvtExit = new Button {
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Background = Brushes.Khaki,
                                    Content = "Salida",
                                    Height = 40,
                                    IsDefault = true
                                }),
                                (btnEvtEnter = new Button {
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Background = Brushes.DarkSeaGreen,
                                    Content = "Entrada",
                                    Height = 40,
                                    IsDefault = false,
                                })
                            }
                        }
                    }
                },
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            btnEvtExit.Click += (_, __) => window.Close(2);
            btnEvtEnter.Click += (_, __) => window.Close(1);

            return window;
        }

        private void dgAttsListPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Items")
            {
                //Thread.Sleep(10000);
                //dgAttsList.SelectedIndex = 100; // the index you want to select
                //ScrollToBottom();
            }
        }

        public void ScrollToBottom()
        {
            //var lastItem = dgAttsList.Items.OfType<object>().LastOrDefault();
            //lsbAttsList.ScrollIntoView(100);
        }

        public void ScrollToBottom2(int index)
        {
            //lsbAttsList.ScrollIntoView(index);
        }

        private void LsbAttsList_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {

        }

        private void LsbAttsList_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            
        }
    }
}
