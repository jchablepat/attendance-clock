using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using DPUruNet;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using VTACheckClock.Helpers;
using VTACheckClock.Models;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;
using VTACheckClock.ViewModels;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.Views
{
    /*public*/
    partial class MainWindow : ReactiveWindow<MainWindowViewModel> /*Window*/
    {
        private List<Fmd>? las_fmds;
        private List<FMDItem>? fmd_collection;

        public MainWindow()
        {
            InitializeComponent();

            this.WhenActivated(d => d(ViewModel!.ShowLoginDialog.RegisterHandler(DoShowDialogAsync)));
            //this.WhenActivated(d => d(ViewModel!.ShowPwdPunchDialog.RegisterHandler(ShowPwdPunchDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowLoggerDialog.RegisterHandler(ShowLoggerDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowAttendanceRptDialog.RegisterHandler(ShowAttendanceRptDialogAsync)));
            GetFMDs();

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

        private void SetParentWindow()
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

        private async Task DoShowDialogAsync(InteractionContext<LoginViewModel, bool> interaction)
        {
            var dialog = new LoginWindow {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
        }

        private async Task ShowPwdPunchDialogAsync(InteractionContext<PwdPunchViewModel, int> interaction)
        {
            var dialog = new PwdPunchWindow
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<int>(this);
            interaction.SetOutput(result);
        }

        private async Task ShowLoggerDialogAsync(InteractionContext<WebsocketLoggerViewModel, bool> interaction)
        {
            var dialog = new WebsocketLoggerWindow
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
        }

        private async Task ShowAttendanceRptDialogAsync(InteractionContext<AttendanceViewModel, bool> interaction)
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
                try {
                    var frmPassPunc = new PwdPunchViewModel(fmd_collection);
                    var dialog = new PwdPunchWindow {
                        DataContext = frmPassPunc
                    };

                    int found_idx = await dialog.ShowDialog<int>(this);
                    if(found_idx != -1) {
                        ((MainWindowViewModel?)DataContext).PwdPunchIndex = found_idx;
                    }
                } catch (Exception ex) {
                    await ShowMessage("Error en la ventana de Empleado", ex.Message);
                    Debug.WriteLine(ex);
                }
            } else if (e.Key == Key.Escape) {
                await LogOut();
            } else if (e.Key == Key.LeftAlt) {
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
                int found_idx = fmd_collection.FindIndex(f => f.empid == window.EmployeeId);

                //var parentViewModel = new MainWindowViewModel("");
                //DataContext = parentViewModel;
                ((MainWindowViewModel?)DataContext).PwdPunchIndex = found_idx;
            }
        }

        /// <summary>
        /// Recupera las huellas dactilares de los empleados asignados a la oficina actual.
        /// </summary>
        private bool GetFMDs()
        {
            DataTable? emp_dt;
            int el_idx = 0;
            las_fmds = null;
            fmd_collection = null;
            las_fmds = new List<Fmd>();
            fmd_collection = new List<FMDItem>();

            if (!GlobalVars.BeOffline)
            {
                emp_dt = CommonProcs.GetOfficeFMDs((new ScantRequest { Question = (GlobalVars.clockSettings.clock_office.ToString()) }));

                if (emp_dt == null) {
                    emp_dt = CommonObjs.VoidFMDs;
                } else {
                    GlobalVars.AppCache.SaveEmployees(emp_dt);
                }
            }
            else
            {
                emp_dt = GlobalVars.AppCache.RetrieveEmployees();
            }

            if(emp_dt.Columns.Contains("Error")) {
                return false;
            }

            foreach (DataRow dr in emp_dt.Rows)
            {
                las_fmds.Add(new Fmd(SimpleAES.ToHexBytes(dr["FingerFMD"].ToString() ?? ""), GlobalVars.FMDFormat, GlobalVars.FMDVersion));

                var item = new FMDItem {
                    idx = el_idx,
                    offid = int.Parse(dr["OffID"].ToString() ?? "0"),
                    empid = int.Parse(dr["EmpID"].ToString() ?? "0"),
                    fingid = int.Parse(dr["FingerID"].ToString() ?? "0"),
                    empnum = dr["EmpNum"].ToString(),
                    empnom = dr["EmpName"].ToString(),
                    emppass = dr["EmpPass"].ToString(),
                    fmd = SimpleAES.ToHexBytes(dr["FingerFMD"].ToString()!)
                };

                fmd_collection.Add(item);

                el_idx++;
            }

            if (las_fmds.Count <= 0) las_fmds.Add(new Fmd((new byte[124]), GlobalVars.FMDFormat, GlobalVars.FMDVersion));

            if (((emp_dt == null) || (emp_dt.Rows.Count <= 0)) && GlobalVars.StartingUp) {
                return false;
            }
            else
            {
                return true;
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
                Debug.WriteLine(exc);
            }
        }

        public void addNotice(Notice? notice)
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
