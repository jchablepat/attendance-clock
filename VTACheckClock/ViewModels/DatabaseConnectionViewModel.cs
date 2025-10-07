using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.ViewModels
{
    class DatabaseConnectionViewModel : ViewModelBase
    {
        private readonly IFilePickerService _filePickerService;
        string _server = "", _database = "", _dbUser = "", _dbPass = "", _filePath = "";
        private bool _isBusy;
        public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
        public ReactiveCommand<Unit, Unit> ReadFileCommand { get; }
        public IReactiveCommand SaveSettingsCommand { get; }
        public string? Server
        {
            get => _server;
            set
            {
                if (string.IsNullOrEmpty(value)) {
                    this.RaiseAndSetIfChanged(ref _server, "");
                    throw new DataValidationException("Campo requerido");
                } else {
                    this.RaiseAndSetIfChanged(ref _server, value);
                }
            }
        }

        public string? Database
        {
            get => _database;
            set
            {
                if (string.IsNullOrEmpty(value)) {
                    this.RaiseAndSetIfChanged(ref _database, "");
                    throw new DataValidationException("Campo requerido");
                } else {
                    this.RaiseAndSetIfChanged(ref _database, value);
                }
            }
        }

        public string? DBUser
        {
            get => _dbUser;
            set
            {
                if (string.IsNullOrEmpty(value)) {
                    this.RaiseAndSetIfChanged(ref _dbUser, "");
                    throw new DataValidationException("Campo requerido");
                } else {
                    this.RaiseAndSetIfChanged(ref _dbUser, value);
                }
            }
        }

        public string? DBPassword
        {
            get => _dbPass;
            set
            {
                if (string.IsNullOrEmpty(value)) {
                    this.RaiseAndSetIfChanged(ref _dbPass, "");
                    throw new DataValidationException("Campo requerido");
                } else {
                    this.RaiseAndSetIfChanged(ref _dbPass, value);
                }
            }
        }

        public string? FilePath
        {
            get => _filePath;
            set => this.RaiseAndSetIfChanged(ref _filePath!, value);
        }
        public bool IsBusy
        {
            get => _isBusy;
            set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        public DatabaseConnectionViewModel(IFilePickerService filePickerService)
        {
            _filePickerService = filePickerService;

            IObservable<bool>? okEnabled = this.WhenAnyValue(
                x => x.Server, x => x.Database, x => x.DBUser, x => x.DBPassword,
                (server, db, user, pass) =>
                    !string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(db) &&
                    !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass)
                )
                .DistinctUntilChanged();

            IObservable<bool>? hasFile = this.WhenAnyValue(x => x.FilePath, (filepath) => !string.IsNullOrWhiteSpace(filepath));

            BrowseCommand = ReactiveCommand.CreateFromTask(BrowseAsync);
            ReadFileCommand = ReactiveCommand.CreateFromTask(SaveDBConnection, hasFile);
            SaveSettingsCommand = ReactiveCommand.CreateFromTask(SaveSettings, okEnabled);
        }

        private async Task SaveDBConnection()
        {
            if (string.IsNullOrEmpty(FilePath) && !File.Exists(FilePath)) {
                await ShowMessage("Error", "No se ha seleccionado un archivo válido.");
                return;
            }

            IsBusy = true;
            try {
                await Task.Run(async () =>
                {
                    // Leer la información del archivo
                    var la_conn = await CommonProcs.GetDBConn(FilePath);
                    var result = await CommonProcs.SaveConnectionSettings(la_conn);

                    await Dispatcher.UIThread.InvokeAsync(async () => {
                        IsBusy = false;

                        if (result)
                        {
                            await ShowMessage("Servicio Web configurado", "La configuración de conexión al Servicio Web se ha guardado exitosamente.", height: 130);
                            
                            var parentWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.OwnedWindows?.Where(x => x.Name == "wdw_dbConnect").LastOrDefault();
                            parentWindow?.Close(true);
                        }
                        else
                        {
                            await ShowMessage("Fallo al guardar la configuración", "Ocurrió un problema al guardar la configuración del Servicio Web. Favor de contactar al administrador del sistema.", height: 150);
                        }
                    });
                });

            } finally {
                IsBusy = false;
            }
        }

        private async Task<bool> SaveSettings()
        {
            MainSettings db_settings = new() {
                Db_server = Server,
                Db_name = Database,
                Db_user = DBUser,
                Db_pass = DBPassword
            };

            if (DBAccess.DBConnection.TestConnection(db_settings) && RegAccess.SetDBConSettings(db_settings)) {
                await ShowMessage("Servicio Web configurado", "La configuración de conexión al Servicio Web se ha guardado exitosamente.");
                var openedWindows = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows.ToList();
                openedWindows?.LastOrDefault()?.Close(true);
                
                return true;
            } else {
                await ShowMessage("Fallo al guardar la configuración", "Ocurrió un problema al guardar la configuración del Servicio Web. Favor de contactar al administrador del sistema.");
                return false;
            }
        }

        private async Task BrowseAsync()
        {
            var filePath = await _filePickerService.OpenFilePickerAsync();
            if (filePath != null)
            {
                FilePath = filePath.Path.LocalPath;
            }
        }
    }
}
