using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VTACheckClock.Helpers;
using VTACheckClock.ViewModels;

namespace VTACheckClock.Views
{
    /*public*/
    partial class ClockSettingsWindow : ReactiveWindow<ClockSettingsViewModel> /*Window*/
    {
        public ClockSettingsWindow()
        {
            InitializeComponent();
            this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(model => {
                Close();
                Messenger.Send("ToggleOverlay", false);
            })));

            txtFTPPort.KeyDown += OnTextInput;
            txtWSPort.KeyDown += OnTextInput;

            TitleBar.Cursor = new Cursor(StandardCursorType.DragMove);
            TitleBar.PointerPressed += (i, e) => {
                if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) {
                    BeginMoveDrag(e);
                }
            };

            btnSaveSet.Click += (sender, e) => {
                ((ClockSettingsViewModel?)DataContext).txtPathTmp = txtPathTmp;
                ((ClockSettingsViewModel?)DataContext).txtFTPServ = txtFTPServ;
                ((ClockSettingsViewModel?)DataContext).txtFTPPort = txtFTPPort;
                ((ClockSettingsViewModel?)DataContext).txtFTPUsr = txtFTPUsr;
                ((ClockSettingsViewModel?)DataContext).txtFTPPass = txtFTPPass;
                ((ClockSettingsViewModel?)DataContext).txtDBServer = txtDBServer;
                ((ClockSettingsViewModel?)DataContext).txtDBName = txtDBName;
                ((ClockSettingsViewModel?)DataContext).txtDBUser = txtDBUser;
                ((ClockSettingsViewModel?)DataContext).txtDBPass = txtDBPass;
                ((ClockSettingsViewModel?)DataContext).cmbOff = cmbOff;
                ((ClockSettingsViewModel?)DataContext).txtClockUsr = txtClockUsr;
                ((ClockSettingsViewModel?)DataContext).txtClockPass = txtClockPass;
                ((ClockSettingsViewModel?)DataContext).txtSignalRHubUrl = txtSignalRHubUrl;
                ((ClockSettingsViewModel?)DataContext).txtSignalRHubName = txtSignalRHubName;
                ((ClockSettingsViewModel?)DataContext).txtSignalRMethodName = txtSignalRMethodName;
            };

            WindowHelper.CenterOnScreen(this);

            Messenger.Send("ToggleOverlay", true);
        }

        private void ClockSettingsWindow_Opened(object? sender, EventArgs e)
        {
            var screens = Screens.All;

            // Verifica si hay más de una pantalla
            if (screens.Count > 1)
            {
                // Obtiene la segunda pantalla
                var secondScreen = screens.ElementAt(1);

                // Calcula la posición para centrar en la segunda pantalla
                double left = secondScreen.Bounds.X + (secondScreen.Bounds.Width - this.Width) / 2;
                double top = secondScreen.Bounds.Y + (secondScreen.Bounds.Height - this.Height) / 2;

                // Establece la posición de la ventana
                this.Position = new PixelPoint((int)left, (int)top);
            }
            else
            {
                // Si solo hay una pantalla, centra en ella
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        public void closing()
        {
            Close(true);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) {
                Close();
                Messenger.Send("ToggleOverlay", false);
            }
        }

        private static void OnTextInput(object? sender, KeyEventArgs e)
        {
            // Verificar si la tecla presionada es numérica
            if (!IsNumericKey(e.Key))
            {
                // Si la tecla presionada no es numérica, cancelar el evento
                e.Handled = true;
            }
        }

        private static bool IsNumericKey(Key key)
        {
            // Verificar si la tecla presionada es numérica
            return key >= Key.D0 && key <= Key.D9 || key >= Key.NumPad0 && key <= Key.NumPad9;
        }

        private async void Button_Click(object? sender, RoutedEventArgs e)
        {

            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = GetTopLevel(this);

            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Seleccionar imagen",
                AllowMultiple = false,
                FileTypeFilter = new FilePickerFileType[] {
                    new("Archivos de imagen") {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif" }
                    }
                }
            });

            if (files.Count >= 1)
            {
                var file = files[0];
                txtLogoPath.Text = file.Path.LocalPath;

                try {
                    string destinationPath = Path.Combine("Assets", file.Name);

                    // Open reading stream from the first file.
                    using var sourceStream = await file.OpenReadAsync();

                    using var destinationStream = File.Create(destinationPath);
                    await sourceStream.CopyToAsync(destinationStream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al copiar el logo: {ex.Message}");
                }
            }
        }
    }   
}
