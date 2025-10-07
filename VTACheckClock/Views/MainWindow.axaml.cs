using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia.Enums;
using NLog;
using ReactiveUI;
using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VTACheckClock.Helpers;
using VTACheckClock.Models;
using VTACheckClock.Services.Audit;
using VTACheckClock.Services.Libs;
using VTACheckClock.ViewModels;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.Views
{
    /*public*/
    partial class MainWindow : ReactiveWindow<MainWindowViewModel> /*Window*/
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");
        private readonly TimeChangeAuditService _timeAuditService;

        public MainWindow()
        {
            InitializeComponent();

            this.WhenActivated(d => d(ViewModel!.ShowLoginDialog.RegisterHandler(DoShowDialogAsync)));
            //this.WhenActivated(d => d(ViewModel!.ShowPwdPunchDialog.RegisterHandler(ShowPwdPunchDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowLoggerDialog.RegisterHandler(ShowLoggerDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowAttendanceRptDialog.RegisterHandler(ShowAttendanceRptDialogAsync)));

            Activated += OnActivated;

            // Registra el manejador de eventos aquí o agrega el evento 'KeyDown' en la ventana del archivo .axaml.
            KeyDown += OnKeyDown;

            Closed += (sender, args) => {
                UrUClass.CancelCaptureAndCloseReader();
            };

            //ClientSizeProperty.Changed.Subscribe(size => {
            //    lblTimer.FontSize = CalculateNewFontSize();
            //    Debug.WriteLine(lblTimer.Bounds.Width);
            //});

            //HideWindowBorders();
            WindowHelper.CenterOnScreen(this);

            dgAttsList.SelectionChanged += DgAttsList_SelectionChanged;
            Messenger.MessageReceived += Messenger_MessageReceived;
            _timeAuditService = App.ServiceProvider.GetRequiredService<TimeChangeAuditService>();
            _timeAuditService.Initialize();
        }

        private void Messenger_MessageReceived(object? sender, Messenger.MessageEventArgs e)
        {
            OverlayPanel.IsVisible = (bool) e.Data;
        }

        private void DgAttsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            dgAttsList.ScrollIntoView(dgAttsList.SelectedItem, null);
        }

        private void OnActivated(object? sender, EventArgs e)
        {
            WindowState = WindowState.FullScreen;

            MinWidth = 800;
            MinHeight = 597;
        }

        private static void SetParentWindow()
        {
            // Obtiene el servicio "IMouseDevice" de Avalonia.
            //var mouse = AvaloniaLocator.Current.GetService<IMouseDevice>();

            // Obtiene la posición actual del mouse.
            //Point mousePosition = mouse.GetPosition(null);

            // Obtiene las coordenadas X e Y del mouse.
            //int mouseX = (int)mousePosition.X;
            //int mouseY = (int)mousePosition.Y;

            // Obtiene la pantalla en la que se encuentra la posición del mouse.
            //Screen screen = Screens.ScreenFromPoint(new PixelPoint(mouseX, mouseY));

            // Establece la posición de la ventana en la pantalla en la que se encuentra el mouse.
            //WindowStartupLocation = WindowStartupLocation.Manual;
            //Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
        }

        private async Task DoShowDialogAsync(IInteractionContext<LoginViewModel, bool> interaction)
        {
            OverlayPanel.IsVisible = true;

            var dialog = new LoginWindow {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);

            OverlayPanel.IsVisible = false;
        }

        private async Task ShowPwdPunchDialogAsync(IInteractionContext<PwdPunchViewModel, int> interaction)
        {
            var dialog = new PwdPunchWindow
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<int>(this);
            interaction.SetOutput(result);
        }

        private async Task ShowLoggerDialogAsync(IInteractionContext<WebsocketLoggerViewModel, bool> interaction)
        {
            OverlayPanel.IsVisible = true;

            var dialog = new WebsocketLoggerWindow
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);

            OverlayPanel.IsVisible = false;
        }

        private async Task ShowAttendanceRptDialogAsync(IInteractionContext<AttendanceViewModel, bool> interaction)
        {
            var dialog = new AttendanceWindow() {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            if(GlobalVars.IsRestart || GlobalVars.ForceExit) {
                e.Cancel = false;
            } else {
                await LogOut();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timeAuditService?.Dispose();
            base.OnClosed(e);
        }

        /// <summary>
        /// Intercepta el evento de presionar y soltar una tecla y decide la acción a realizar.
        /// <para>
        /// - F11: Alterna el modo de pantalla completa.
        /// </para>
        /// <para>
        /// - Shift + F12: Invoca el cuadro de diálogo para efectuar el registro de asistencia con clave.
        /// </para>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.T && e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                // Si se presiono Ctrl + T
                e.Handled = true;
                //await TestAssistance();
            } else if(e.Key == Key.F11) {
                ToggleFullScreen();
            } else if(e.Key == Key.F12) {
                //try {
                //    var frmPassPunc = new PwdPunchViewModel(fmd_collection);
                //    var dialog = new PwdPunchWindow {
                //        DataContext = frmPassPunc
                //    };

                //    OverlayPanel.IsVisible = true;
                //    int found_idx = await dialog.ShowDialog<int>(this);
                //    if(found_idx != -1) {
                //        ((MainWindowViewModel?)DataContext).PwdPunchIndex = found_idx;
                //    }
                //    OverlayPanel.IsVisible = false;
                //}
                //catch (Exception ex) {
                //    await ShowMessage("Error en la ventana de Empleado", ex.Message);
                //    Debug.WriteLine(ex);
                //}
            } else if (e.Key == Key.Escape) {
                OverlayPanel.IsVisible = true;
                await LogOut();
                OverlayPanel.IsVisible = false;
            }
            else if (e.Key == Key.LeftAlt) {
                e.Handled = true;
            }

            Debug.WriteLine($"An KeyDown event has been handled, this is the Key: {e.Key}");
        }

        public async Task TestAssistance()
        {
            var window = new EmployeeCheckWindow {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            await window.ShowDialog(this);

            if (window.SelectedAction != null) {
                var emp_dt = GlobalVars.AppCache.RetrieveEmployees();
                int found_idx = (int)(emp_dt?.AsEnumerable()
                    .Select((row, index) => new { Row = row, Index = index })
                    .Where(x => x.Row.Field<string>("EmpID") == window.EmployeeId.ToString())
                    .Select(x => x.Index).FirstOrDefault(-1))!;

                //var parentViewModel = new MainWindowViewModel("");
                //DataContext = parentViewModel;
                ((MainWindowViewModel?)DataContext).PwdPunchIndex = found_idx;
            }
        }

        public async Task LogOut(bool forceexit = false)
        {
            try {
                if (!forceexit) {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
                    {
                        var _result = await ShowPrompt("¿Salir?", "¿Confirma que desea salir de la aplicación?");
                        if (_result == ButtonResult.Yes) {
                            ((MainWindowViewModel?)DataContext).ShowLoader = true;
                            //(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                        }
                    }
                } else {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }) {
                        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                    }
                }
            } catch(Exception exc) {
                log.Warn("Error ocurred while logging out: " + exc.Message);
            }
        }

        public void AddNotice(Notice? notice)
        {
            ((MainWindowViewModel?)DataContext).NewNotice = notice;
        }

        /// <summary>
        /// Set a borderless window by code.
        /// </summary>
        private void HideWindowBorders()
        {
            // A borderless window is also possible in Avalonia, by setting these properties on the window directly:
            //ExtendClientAreaToDecorationsHint = "True"
            //ExtendClientAreaChromeHints = "NoChrome"
            //ExtendClientAreaTitleBarHeightHint = "-1"
            //SystemDecorations = "None"

            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = -1;
        }

        /// <summary>
        /// Alterna la ejecución del formulario en modo de pantalla completa.
        /// </summary>
        private void ToggleFullScreen()
        {
            if (WindowState == WindowState.Maximized) {
                WindowState = WindowState.FullScreen;
                SystemDecorations = SystemDecorations.None;
            }
            else {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                WindowState = WindowState.Maximized;
                SystemDecorations = SystemDecorations.Full;
            }
        }

        private double CalculateNewFontSize()
        {
            var el_size = lblTimer.Bounds.Width / 4 - 14;
            el_size = (el_size < 1) ? 1 : el_size;

            return el_size; //txtBlockTimer.FontSize;
        }

        private void Button_Click(object? sender, RoutedEventArgs e)
        {
            txtSearchEmployeePunch.Text = string.Empty;
            txtSearchEmployeePunch.Focus();
        }
    }
}
