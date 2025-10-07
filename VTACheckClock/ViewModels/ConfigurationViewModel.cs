using Avalonia.Threading;
using NLog;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.ViewModels
{
    class ConfigurationViewModel : ViewModelBase
    {
        private string _title = "Configuración", _message = "Cargando y validando datos, espere...";
        private bool _isConfigured = false, _isLoading = true;
        private int _nextStep = 0;
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        public ConfigurationViewModel()
        {
            Dispatcher.UIThread.InvokeAsync(ValidateConfigurations);
            ApplyCommand = ReactiveCommand.Create(() => "All configured successfully, initializing MainWindow!");
            OkCommand = ReactiveCommand.CreateFromTask(async() => {
                if (IsConfigured || NextStep == 0) NextStep++;
                await ValidateConfigurations();
            });
        }

        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        public int NextStep
        {
            get => _nextStep;
            set => this.RaiseAndSetIfChanged(ref _nextStep, value);
        }

        public bool IsConfigured
        {
            get => _isConfigured;
            set => this.RaiseAndSetIfChanged(ref _isConfigured, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public ReactiveCommand<Unit, string> ApplyCommand { get; }
        public ICommand OkCommand { get; }
        public Interaction<DatabaseConnectionViewModel, bool> ShowDBDialog { get; } = new();
        public Interaction<LoginViewModel, bool> ShowLoginDialog { get; } = new();

        static readonly Mutex mutex = new(true, "VTA_Clock");

        /// <summary>
        /// Punto de entrada principal de la aplicación.
        /// </summary>
        private async Task ValidateConfigurations()
        {
            try {
                GlobalVars.TimeZone = RegAccess.GetRegValue("clock_timezone");
                GlobalVars.RunningTime.Start();
                GlobalVars.StartTime = DateTime.Now;
                GlobalVars.VTAttModule = 1;
                string error_str = string.Empty;
                await IsAppAlreadyRunningAsync();

                if (!mutex.WaitOne(TimeSpan.Zero, true)) {
                    Title = "Programa ya ejecutándose";
                    Message = "La aplicación ya se está ejecutando en este equipo, favor de rectificar.";
                    //Thread.Sleep(2000);
                    //if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is { } mainWindow) {
                    //    (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                    //}
                    return;
                }

            //if (GlobalVars.DoReinstall)  {
            //    CommonValids.ReinstallMe(true);
            //}
            Reentrada:
                if (!CommonValids.ValidStartup(out error_str)) {
                    IsLoading = false;
                    if (GlobalVars.BeOffline) {
                        Title = "Sin conexión a Internet.";
                        Message = error_str;
                    } 
                    else {
                        if (NextStep == 0) {
                            Title = "Configuración errónea o incompleta";
                            Message = !string.IsNullOrEmpty(error_str) ? error_str: "No se ha llevado a cabo la configuración inicial de la aplicación o la misma se encuentra incompleta o dañada.\n\nA continuación, se le solicitará la información faltante, por lo que antes de continuar deberá tenerla a la mano o comunicarse con el administrador del sistema.\n\n¿Desea continuar?";
                        }

                        if (!IsConfigured && NextStep == 1) {
                            var db = new DatabaseConnectionViewModel(new FilePickerService());
                            await ShowDBDialog.Handle(db);
                        }

                        if (NextStep == 2)
                        {
                            Title = "Claves de administrador requeridas";
                            Message = "Deberá iniciar sesión como administrador para completar la configuración siguiente, ¿desea continuar?";
                        }

                        if (NextStep > 2)
                        {
                            var frmPassPunc = new LoginViewModel();
                            await ShowLoginDialog.Handle(frmPassPunc);
                        }
                    }
                } else {
                    GlobalVars.StartingUp = false;
                    GlobalVars.IsRestart = false;

                    UrUClass.LoadCurrentReader();
                    //if (!UrUClass.OpenReader())
                    //{
                    //    Title = "No se pudo inicializar el Lector de Huellas.";
                    //    Message = "No se ha encontrado ningún lector de huella dactilar o no se ha podido tener acceso al mismo.\n\nPruebe una de las siguientes opciones:\n\n1. Rectifique que el lector se encuentra debidamente conectado al equipo; deberá ver una luz azul en el lector que así lo indica.\n2. Asegúrese que los controladores necesarios han sido correctamente instalados.\n3. Conecte y desconecte el lector o conéctelo a un puerto USB diferente.\n4. Reinicie el equipo.\n\nSi el problema persiste, póngase en contacto con el administrador del sistema.";
                    //    return;
                    //}

                    if (CommonProcs.RetrieveParams()) {
                        try {
                            ApplyCommand.Execute().Subscribe();
                        } catch {}

                        if (GlobalVars.IsRestart) {
                            goto Reentrada;
                        }
                    } else {
                        await ShowMessage("Configuración incorrecta", "No se pudieron recuperar los parámetros de la aplicación. Asegúrese de contar con una conexión activa a Internet y reintente.");
                    }
                }
            } catch (Exception exc) {
                await ShowMessage("Configuración incorrecta", "Ocurrio un error al configurar: " + exc.Message);
            }
            mutex.ReleaseMutex();
        }

        /// <summary>
        /// Verifica si hay más de una instancia del proceso actualmente en ejecución con el mismo nombre que el proceso actual.
        /// </summary>
        /// <returns>
        /// Si se encuentran más de una instancia, se considera que la aplicación ya está en ejecución y el método devuelve true.
        /// En caso contrario, si solo hay una instancia del proceso, se considera que la aplicación no está en ejecución y el método devuelve false
        /// </returns>
        public static Task<bool> IsAppAlreadyRunningAsync()
        {
            try {
                string processName = Process.GetCurrentProcess().ProcessName;
                Process[] processes = Process.GetProcessesByName(processName);

                // Verificar si se encontraron más de una instancia del proceso con el mismo nombre
                if (processes.Length > 1) {
                    // La aplicación ya está en ejecución
                    // Finalizar la tarea (proceso) actual
                    Process.GetCurrentProcess().Kill();
                    return Task.FromResult(true);
                } else {
                    // La aplicación no está en ejecución
                    return Task.FromResult(false);
                }
            } catch(Exception ex) {
                log.Error(new Exception(), $"Error al cerrar la instancia de la Aplicación: {ex.InnerException}");
                return Task.FromResult(false);
            }
        }
    }
}
