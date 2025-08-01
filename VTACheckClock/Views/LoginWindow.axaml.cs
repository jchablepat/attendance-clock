using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using NLog;
using Prism.Events;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;
using VTACheckClock.ViewModels;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.Views
{
    /*public*/ partial class LoginWindow : ReactiveWindow<LoginViewModel> /*Window*/
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        public LoginWindow()
        {
            InitializeComponent();
            this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(model =>
            {
                Close();
            })));

            this.WhenActivated(d => d(ViewModel!.OkCommand.Subscribe(async(_isLogged) =>
            {
                if (_isLogged) {
                    Close();
                    var vm = new ClockSettingsViewModel();
                    var dialog = new ClockSettingsWindow {
                        DataContext = vm,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
                    {
                        var result = await dialog.ShowDialog<bool>(mainWindow);
                        if(result) {
                            await ShowMessage("Reiniciando aplicación", "La configuración de la aplicación ha sido cambiada, por lo que se reiniciará ahora.");
                            RestartApplication();
                        }
                    }
                }
            })));
            this.WhenActivated(d => d(ViewModel!.ShowSettingsDialog.RegisterHandler(ShowSettingsDialogAsync)));
            
            txtUser.KeyUp += OnTextInput;

            Activated += (sender, e) => {
                txtUser.Focus();
            };
    }

        private void OnTextInput(object? sender, KeyEventArgs e)
        {
            e.Handled = !CommonValids.ValidaTecla((char)e.Key, 0);
        }

        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            
        }

        private void TriggerEnter_OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(txtUser.Text) && !string.IsNullOrEmpty(txtPwd.Text)) {
                btnLogin.Command?.Execute(null);
            }
        }
        
        private async Task ShowSettingsDialogAsync(IInteractionContext<ClockSettingsViewModel, bool> interaction)
        {
            var dialog = new ClockSettingsWindow {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
        }

        private static void RestartApplication()
        {
            //Obtener una instancia de la interfaz IClassicDesktopStyleApplicationLifetime,
            //que representa el ciclo de vida de la aplicación de estilo de escritorio clásico.
            try {
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                lifetime?.Restart();
                //lifetime.Shutdown();
            }
            catch (Exception ex) {
                log.Warn("Error ocurred while restarting aplication: " + ex.Message);
            }
        }
    }
}
