using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services;
using VTACheckClock.Services.Auth;
using VTACheckClock.Services.Libs;
using VTACheckClock.Views;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.ViewModels
{
    /*public*/ class ClockSettingsViewModel : ViewModelBase
    {
        private string? _pathTmp = "", _logo = "", _ftpServe = "", _ftpPort = "", _ftpUser = "", _ftpPass = "",
            _server = "", _database = "", _dbUser = "", _dbPass = "", _clockUsr = "", _clockPass = "",
            _emp_host = "", _wsHost = "", _wsPort = "", _pusherAppId = "", _pusherKey = "", _pusherSecret = "",
            _pusherCluster = "", _eventName = "", _clockUUID = "",
            _seqUrl = "", _seqApiKey = "";

        private string?  _mailServer = "", _mailPort = "", _mailSender = "", _mailPass = "", _mailRecipient = "";
        private bool _mailEnabled = false;

        private int _selOffice = -1, _selTZ = -1;
        private bool _uuid = false, _websocket_enabled = false;
        private readonly ClockSettings? _clockSettings;

        public TextBox? txtPathTmp { get; set; }
        public TextBox? txtFTPServ { get; set; }
        public TextBox? txtFTPPort { get; set; }
        public TextBox? txtFTPUsr { get; set; }
        public TextBox? txtFTPPass { get; set; }
        public TextBox? txtDBServer { get; set; }
        public TextBox? txtDBName { get; set; }
        public TextBox? txtDBUser { get; set; }
        public TextBox? txtDBPass { get; set; }
        public ComboBox? cmbOff { get; set; }
        public TextBox? txtClockUsr { get; set; }
        public TextBox? txtClockPass { get; set; }
        public TextBox? txtSignalRHubUrl { get; set; }
        public TextBox? txtSignalRHubName { get; set; }
        public TextBox? txtSignalRMethodName { get; set; }
        public TextBox? txtSeqUrl { get; set; }
        public TextBox? txtSeqApiKey { get; set; }

        public ClockSettingsViewModel()
        {
            _clockSettings = RegAccess.GetClockSettings() ?? new ClockSettings();
            Offices = [];
            SetDefPathCommand = ReactiveCommand.Create(SetDefPath);
            GenerateUUIDCommand = ReactiveCommand.Create(GenerateUUID);
            OpenFolderBrowserCommand = ReactiveCommand.CreateFromTask(OpenFolderBrowser);
            SaveSettingsCommand = ReactiveCommand.CreateFromTask(SaveSettings);
            CancelCommand = ReactiveCommand.Create(() => { });
            RegisterDeviceCommand = ReactiveCommand.CreateFromTask(RegisterDevice);

            DoLogin();
        }

        public string? PathTmp
        {
            get => _pathTmp;
            set {
                if (string.IsNullOrEmpty(value)) {
                    _pathTmp = "";
                    throw new DataValidationException("Campo requerido");
                } else {
                    this.RaiseAndSetIfChanged(ref _pathTmp, value);
                }
            }
        }

        public string? Logo
        {
            get => _logo;
            set => this.RaiseAndSetIfChanged(ref _logo, value);
        }

        public string? FTPServer
        {
            get => _ftpServe;
            set {
                if (string.IsNullOrEmpty(value))
                {
                    _ftpServe = "";
                    throw new DataValidationException("Campo requerido");
                } else { 
                    this.RaiseAndSetIfChanged(ref _ftpServe, value);
                }

            }
        }

        public string? FTPPort
        {
            get => _ftpPort;
            set {
                if (string.IsNullOrEmpty(value))
                {
                    _ftpPort = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _ftpPort, value);
                }
            }
        }

        public string? FTPUsr
        {
            get => _ftpUser;
            set {
                if (string.IsNullOrEmpty(value))
                {
                    _ftpUser = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _ftpUser, value);
                }
            }
        }

        public string? FTPPass
        {
            get => _ftpPass;
            set {
                if (string.IsNullOrEmpty(value))
                {
                    _ftpPass = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _ftpPass, value);
                }
            }
        }

        public string? DBServer
        {
            get => _server;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _server = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _server, value);
                }
            }
        }

        public string? Database
        {
            get => _database;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _database = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _database, value);
                }
            }
        }

        public string? DBUser
        {
            get => _dbUser;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _dbUser = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _dbUser, value);
                }
            }
        }

        public string? DBPassword
        {
            get => _dbPass;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _dbPass = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _dbPass, value);
                }
            }
        }

        public string? EmployeesHost
        {
            get => _emp_host;
            set => this.RaiseAndSetIfChanged(ref _emp_host, value);
        }

        public string? SeqUrl
        {
            get => _seqUrl;
            set => this.RaiseAndSetIfChanged(ref _seqUrl, value);
        }

        public string? SeqApiKey
        {
            get => _seqApiKey;
            set => this.RaiseAndSetIfChanged(ref _seqApiKey, value);
        }

        public bool WebSocketEnabled
        {
            get => _websocket_enabled;
            set => this.RaiseAndSetIfChanged(ref _websocket_enabled, value);
        }

        public string? WSHost
        {
            get => _wsHost;
            set => this.RaiseAndSetIfChanged(ref _wsHost, value);
        }

        public string? WSPort
        {
            get => _wsPort;
            set => this.RaiseAndSetIfChanged(ref _wsPort, value);
        }

        public string? PusherAppId
        {
            get => _pusherAppId;
            set => this.RaiseAndSetIfChanged(ref _pusherAppId, value);
        }

        public string? PusherKey
        {
            get => _pusherKey;
            set => this.RaiseAndSetIfChanged(ref _pusherKey, value);
        }

        public string? PusherSecret
        {
            get => _pusherSecret;
            set => this.RaiseAndSetIfChanged(ref _pusherSecret, value);
        }

        public string? PusherCluster
        {
            get => _pusherCluster;
            set => this.RaiseAndSetIfChanged(ref _pusherCluster, value);
        }

        public string? EventName
        {
            get => _eventName;
            set => this.RaiseAndSetIfChanged(ref _eventName, value);
        }

        public string? ClockUsr
        {
            get => _clockUsr;
            set {
                if (string.IsNullOrEmpty(value))
                {
                    _clockUsr = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _clockUsr, value);
                }
            }
        }

        public string? ClockPass
        {
            get => _clockPass;
            set {
                if (string.IsNullOrEmpty(value))
                {
                    _clockPass = "";
                    throw new DataValidationException("Campo requerido");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _clockPass, value);
                }
            }
        }

        public bool HasUUID
        {
            get => _uuid;
            set => this.RaiseAndSetIfChanged(ref _uuid, value);
        }

        public string? CLOCK_UUID
        {
            get => _clockUUID;
            set => this.RaiseAndSetIfChanged(ref _clockUUID, value);
        }

        public int SelectedOffice
        {
            get => _selOffice;
            set => this.RaiseAndSetIfChanged(ref _selOffice, value);
        }

        public int SelectedTimeZone
        {
            get => _selTZ;
            set => this.RaiseAndSetIfChanged(ref _selTZ, value);
        }

        public bool MailEnabled
        {
            get => _mailEnabled;
            set => this.RaiseAndSetIfChanged(ref _mailEnabled, value);
        }

        public string? MailServer
        {
            get => _mailServer;
            set => this.RaiseAndSetIfChanged(ref _mailServer, value);
        }

        public string? MailPort
        {
            get => _mailPort;
            set => this.RaiseAndSetIfChanged(ref _mailPort, value);
        }

        public string? MailUser
        {
            get => _mailSender;
            set => this.RaiseAndSetIfChanged(ref _mailSender, value);
        }

        public string? MailPass
        {
            get => _mailPass;
            set => this.RaiseAndSetIfChanged(ref _mailPass, value);
        }

        public string? MailRecipient
        {
            get => _mailRecipient;
            set => this.RaiseAndSetIfChanged(ref _mailRecipient, value);
        }

        private bool _usePusher;
        public bool UsePusher
        {
            get => _usePusher;
            set => this.RaiseAndSetIfChanged(ref _usePusher, value);
        }

        private string _signalRHubUrl = "";
        public string SignalRHubUrl
        {
            get => _signalRHubUrl;
            set
            {
                if (string.IsNullOrEmpty(value) && !UsePusher)
                {
                    _signalRHubUrl = "";
                    throw new DataValidationException("Campo requerido para SignalR");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _signalRHubUrl, value);
                }
            }
        }

        private string _signalRHubName = "";
        public string SignalRHubName
        {
            get => _signalRHubName;
            set
            {
                if (string.IsNullOrEmpty(value) && !UsePusher)
                {
                    _signalRHubName = "";
                    throw new DataValidationException("Campo requerido para SignalR");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _signalRHubName, value);
                }
            }
        }

        private string _signalRMethodName = "";
        public string SignalRMethodName
        {
            get => _signalRMethodName;
            set
            {
                if (string.IsNullOrEmpty(value) && !UsePusher)
                {
                    _signalRMethodName = "";
                    throw new DataValidationException("Campo requerido para SignalR");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _signalRMethodName, value);
                }
            }
        }

        private string _signalRApiKey = "";
        public string SignalRApiKey
        {
            get => _signalRApiKey;
            set
            {
                if (string.IsNullOrEmpty(value) && !UsePusher)
                {
                    _signalRApiKey = "";
                    throw new DataValidationException("Campo requerido para SignalR");
                }
                else
                {
                    this.RaiseAndSetIfChanged(ref _signalRApiKey, value);
                }
            }
        }

        public ObservableCollection<OfficeData> Offices { get; } = [];
        public ObservableCollection<TimeZoneList> TimeZones { get; } = [];
        public ReactiveCommand<Unit, Unit> SetDefPathCommand { get; }
        public ReactiveCommand<Unit, Unit> GenerateUUIDCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenFolderBrowserCommand { get; }
        public IReactiveCommand SaveSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public ReactiveCommand<Unit, Unit> RegisterDeviceCommand { get; }
        #region Inicialización
        private readonly string? ws_url = string.Empty;
        //private bool forceclose;
        readonly bool do_login = true;
        private MainSettings? test_msettings = null;
        private ClockSettings? test_csettings = null;

        public bool setupok = false;
        public MainSettings? m_settings;
        public ClockSettings? c_settings;
        List<IdxObjs>? OfficesList;
        private readonly List<TimeZoneList> TimeZoneList = [];

        /// <summary>
        /// Solicita y valida el inicio de sesión para este formulario.
        /// </summary>
        private void DoLogin()
        {
            int el_privilege = ((!GlobalVars.StartingUp) && (GlobalVars.VTAttModule == 1)) ? GlobalVars.ClockSetPriv : GlobalVars.AdminSetPriv;

            if (do_login && (!CommonValids.InvokeLogin(el_privilege, ws_url)))
            {
                setupok = false;
                //forceclose = true;
                //Close();
            } else {
                //forceclose = false;
                FormInit();
            }
        }

        /// <summary>
        /// Inicializa el entorno del formulario.
        /// </summary>
        private void FormInit()
        {
            //CommonProcs.UniversalToolTip(this);
            //CommonProcs.TagPrivilege(this);
            SetFieldsValue();
        }

        /// <summary>
        /// Rellena los campos del formulario con los valores guardados en la configuración, o con predeterminados, de ser necesario.
        /// </summary>
        private void SetFieldsValue()
        {
            m_settings = RegAccess.GetMainSettings() ?? new MainSettings();

            _server = m_settings.Db_server ?? string.Empty;
            _database = m_settings.Db_name ?? string.Empty;
            _dbUser = m_settings.Db_user ?? string.Empty;
            _dbPass = m_settings.Db_pass ?? string.Empty;

            try {
                PathTmp = m_settings.Path_tmp ?? GlobalVars.DefWorkPath;
                Logo = m_settings.Logo ?? "";
                FTPServer = m_settings.Ftp_url ?? string.Empty;
                FTPPort = m_settings.Ftp_port ?? string.Empty;
                FTPUsr = m_settings.Ftp_user ?? string.Empty;
                FTPPass = m_settings.Ftp_pass ?? string.Empty;
                FTPServer = m_settings.Db_server;
                //Database = m_settings.db_name ?? string.Empty;
                //DBUser = m_settings.db_user ?? string.Empty;
                //DBPassword = m_settings.db_pass ?? string.Empty;
                EmployeesHost = m_settings.Employees_host ?? string.Empty;
                WebSocketEnabled = m_settings.Websocket_enabled;
                WSHost = m_settings.Websocket_host ?? string.Empty;
                WSPort = m_settings.Websocket_port ?? string.Empty;
                PusherAppId = m_settings.Pusher_app_id ?? string.Empty;
                PusherKey = m_settings.Pusher_key ?? string.Empty;
                PusherSecret = m_settings.Pusher_secret ?? string.Empty;
                PusherCluster = m_settings.Pusher_cluster ?? string.Empty;
                EventName = m_settings.Event_name ?? string.Empty;
                MailEnabled = m_settings.MailEnabled;
                MailServer = m_settings.MailServer ?? string.Empty;
                MailPort = m_settings.MailPort ?? string.Empty;
                MailUser = m_settings?.MailUser ?? string.Empty;
                MailPass = m_settings.MailPass ?? string.Empty;
                MailRecipient = m_settings.MailRecipient ?? string.Empty;
                UsePusher = m_settings.UsePusher;
                SignalRHubUrl = m_settings.SignalRHubUrl ?? string.Empty;
                SignalRHubName = m_settings.SignalRHubName ?? string.Empty;
                SignalRMethodName = m_settings.SignalRMethodName ?? string.Empty;
                SignalRApiKey = m_settings.SignalRApiKey ?? string.Empty;
                SeqUrl = m_settings.SeqUrl ?? string.Empty;
                SeqApiKey = m_settings.SeqApiKey ?? string.Empty;
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
            }

            if (GlobalVars.VTAttModule == 1) {
                int sel_office, sel_tz = -1;

                GetOffices();
                GetTimeZones();
                c_settings = RegAccess.GetClockSettings() ?? new ClockSettings();
                EvalUUID();

                try {
                   ClockUsr = c_settings.clock_user ?? string.Empty;
                   ClockPass = c_settings.clock_pass ?? string.Empty;
                   sel_office = OfficesList != null ? OfficesList.ElementAt(OfficesList.FindIndex(o => o.el_id == c_settings.clock_office)).el_idx: -1;
                } catch {
                   sel_office = -1;
                }

                SelectedOffice = sel_office;
                
                if (!string.IsNullOrEmpty(c_settings.clock_timezone) && c_settings.clock_timezone != "-1") {
                    var tz_item = TimeZoneList.FindIndex(x => x.el_id == c_settings.clock_timezone);
                    sel_tz = tz_item != -1 ? TimeZoneList.ElementAt(tz_item).el_idx: tz_item;
                } else {
                    Dispatcher.UIThread.InvokeAsync(async () => {
                        string? remote_tz = await CommonObjs.GetTimeZoneAsync();
                        TimeZoneInfo? timeZone = TimeZoneInfo.FindSystemTimeZoneById(remote_tz);

                        var tz_item_by_id = TimeZoneList.FindIndex(x => x.el_id == timeZone.Id);
                        var tz_item_by_name = TimeZoneList.FindIndex(x => x.el_text == timeZone.DisplayName);

                        sel_tz = tz_item_by_id != -1 ? TimeZoneList.ElementAt(tz_item_by_id).el_idx: (tz_item_by_name != -1 ? TimeZoneList.ElementAt(tz_item_by_name).el_idx : tz_item_by_id);
                        SelectedTimeZone = sel_tz;
                    });
                }
                SelectedTimeZone = sel_tz;

                //grpClock.Visible = T;
                //grpUUID.Visible = T;
                //Height = 500;
            }
        }

        private void GetTimeZones()
        {
            TimeZones.Clear();
            int _counter = 0;
            foreach(TimeZoneInfo z in TimeZoneInfo.GetSystemTimeZones())
            {
                TimeZoneList.Add(new TimeZoneList() {
                    el_idx = _counter,
                    el_text = z.DisplayName,
                    el_id = z.Id
                });

                TimeZones.Add(new TimeZoneList()
                {
                    el_idx = _counter,
                    el_text = z.DisplayName,
                    el_id = z.Id
                });
                //z.BaseUtcOffset + ", " + z.StandardName + ", " + z.DisplayName + ", " + z.DaylightName
                _counter++;
            }
        }

        /// <summary>
        /// Puebla el ComboBox con el listado de las oficinas registradas en el sistema.
        /// </summary>
        private void GetOffices()
        {
            List<OfficeData> las_offices = CommonProcs.GetOffices(new ScantRequest { Question = "0" });

            int iii = 0;
            Offices.Clear();
            OfficesList = null;
            OfficesList = [];

            foreach (OfficeData la_off in las_offices)
            {
                var item = new IdxObjs {
                    el_idx = iii,
                    el_id = la_off.Offid,
                    el_obj = la_off
                };

                OfficesList.Add(item);

                Offices.Add(new OfficeData() {
                    Offid = la_off.Offid,
                    Offname = la_off.Offname,
                    Offdesc = la_off.Offdesc
                }); //la_off.offname + " - " + la_off.offdesc

                iii++;
            }
        }

        /// <summary>
        /// Evalúa si el cliente cuenta con un UUID y decide qué hacer con los controles correspondientes.
        /// </summary>
        private void EvalUUID()
        {
            if (string.IsNullOrWhiteSpace(c_settings.clock_uuid)) {
                HasUUID = false;
                CLOCK_UUID = "UUID no establecido. Para crearlo ahora, haga click en:";
            } else {
                HasUUID = true;
                CLOCK_UUID = c_settings.clock_uuid;
            }
        }
        #endregion

        #region Validaciones

        /// <summary>
        /// Validar que los valores introducidos en los campos del formulario sean correctos.
        /// </summary>
        /// <returns>True si la información introducida en el formulario cumple todas las validaciones.</returns>
        private async Task<bool> SaveSettings()
        {
            if (string.IsNullOrWhiteSpace(PathTmp))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar la ruta de una carpeta de trabajo temporal para el sistema.", 350);
                txtPathTmp.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(FTPServer))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar la URL del Servidor FTP.", 350);
                txtFTPServ.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(FTPPort))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el puerto del Servidor FTP.", 350);
                txtFTPPort.Focus();
                return false;
            }

            if (!int.TryParse(FTPPort, out int el_port))
            {
                await ShowMessage($"Puerto FTP {el_port} inválido", "El puerto de conexión al Servidor FTP debe ser un número entero, favor de rectificar.", 350);

                //    txtFTPPort.Text = string.Empty;
                txtFTPPort.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(FTPUsr))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el usuario del Servidor FTP.", 350);
                txtFTPUsr.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(FTPPass))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar la contraseña del Servidor FTP.", 350);
                txtFTPPass.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(DBServer))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar la dirección IP del Servidor.", 350);
                txtDBServer.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(Database))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el nombre de la base de datos.", 350);
                txtDBName.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(DBUser))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el usuario de la base de datos.", 350);
                txtDBUser.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(DBPassword))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar la contraseña del usuario de la base de datos.", 350);
                txtDBPass.Focus();
                return false;
            }

            if (!DBAccess.DBConnection.ValidDBConnection(DBServer, Database, DBUser, DBPassword))
            {
                await ShowMessage("Error de Conexión", "No fue posible conectarse a la Base de Datos. Verifique los datos del Web Service.", 350);
                txtDBPass.Focus();
                return false;
            }

            if (WebSocketEnabled && !UsePusher && string.IsNullOrWhiteSpace(SignalRHubUrl))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el Hub URL del WebSocket.", 350);
                txtSignalRHubUrl.Focus();
                return false;
            }

            if (WebSocketEnabled && !UsePusher && string.IsNullOrWhiteSpace(SignalRHubName))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el Hub Name del WebSocket.", 350);
                txtSignalRHubName.Focus();
                return false;
            }

            if (WebSocketEnabled && !UsePusher && string.IsNullOrWhiteSpace(SignalRMethodName))
            {
                await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el Method Name del WebSocket.", 350);
                txtSignalRMethodName.Focus();
                return false;
            }

            if (!Path.IsPathRooted(PathTmp))
            {
                await ShowMessage("Ruta temporal inválida", "No se comprende la ruta temporal especificada. Escriba una ruta absoluta (en la forma \"X:\\carpeta\\carpeta\") o selecciónela desde el cuadro de diálogo correspondiente.", 350);
                txtPathTmp.Focus();
                setupok = false;
                return false;
            }

            test_msettings = new MainSettings {
                Path_tmp = PathTmp,
                Logo = Logo,
                Ftp_url = FTPServer,
                Ftp_port = FTPPort,
                Ftp_user = FTPUsr,
                Ftp_pass = FTPPass,
                Db_server = DBServer,
                Db_name = Database,
                Db_user = DBUser,
                Db_pass = DBPassword,
                Employees_host = EmployeesHost,
                Websocket_enabled = WebSocketEnabled,
                Websocket_host = WSHost,
                Websocket_port = WSPort,
                Pusher_app_id = PusherAppId,
                Pusher_key = PusherKey,
                Pusher_secret = PusherSecret,
                Pusher_cluster = PusherCluster,
                Event_name = EventName,
                MailEnabled = MailEnabled,
                MailServer = MailServer,
                MailPort = MailPort,
                MailUser = MailUser,
                MailPass = MailPass,
                MailRecipient = MailRecipient,
                UsePusher = UsePusher,
                SignalRHubUrl = SignalRHubUrl,
                SignalRHubName = SignalRHubName,
                SignalRMethodName = SignalRMethodName,
                SeqUrl = SeqUrl,
                SeqApiKey = SeqApiKey
            };

            // Persistir parámetros de Seq
            test_msettings.SeqUrl = SeqUrl;
            test_msettings.SeqApiKey = SeqApiKey;

            if (GlobalVars.VTAttModule == 1)
            {
                if (SelectedOffice <= -1)
                {
                    await ShowMessage("Se detectaron campos vacíos", "Debe asignar una ubicación a esta instancia del Reloj Checador.", 350);
                    cmbOff.Focus();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ClockUsr))
                {
                    await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar el usuario que utilizará el Reloj Checador para su conexión al Servicio Web.", 350);
                    txtClockUsr.Focus();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ClockPass))
                {
                    await ShowMessage("Se detectaron campos vacíos", "Debe proporcionar la contraseña que utilizará el Reloj Checador para su conexión al Servicio Web.", 350);
                    txtClockPass.Focus();
                    return false;
                }

                int off_id;
                string? tz_id;
                try {
                    var off_found = OfficesList?.ElementAt(SelectedOffice);
                    off_id = off_found != null ? ((OfficeData?)off_found?.el_obj).Offid: 0;
                } catch {
                    off_id = 0;
                }

                try {
                    tz_id = TimeZoneList.ElementAt(SelectedTimeZone).el_id;
                } catch {
                    tz_id = "";
                }

                test_csettings = new ClockSettings {
                    clock_office = off_id,
                    clock_user = ClockUsr,
                    clock_pass = ClockPass,
                    clock_timezone = tz_id
                };
            }

            if (CommonValids.ValidSettings(test_msettings, test_csettings, out int error_code, ws_url))
            {
                if (GlobalVars.StartingUp) {
                    m_settings = test_msettings;
                    c_settings = test_csettings;
                    setupok = true;

                    if (!RegAccess.SetRegSettings(m_settings, c_settings)) {
                        await ShowMessage("Fallo al guardar la configuración", "Ocurrió un problema al guardar las configuraciones. Favor de contactar al administrador del sistema.");
                        return false;
                    }

                    await ShowMessage("Configuración actualizada", "La configuración ha sido almacenada correctamente.\n\n\nLa aplicación se reiniciará ahora.");
                    RestartApp();
                    return true;
                } else {
                    if (RegAccess.SetRegSettings(test_msettings, test_csettings))
                    {
                        await ShowMessage("Configuración guardada", "La configuración ha sido actualizada con éxito.", 350);
                        RestartApp();

                        return true;
                    } else {
                        return false;
                    }
                }
            } else {
                await ShowMessage("Configuración Incorrecta", "No se pudo guardar la información, revise de nuevo.", 350);
                return false;
            }
        }

        private static void RestartApp()
        {
            var windows = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows;
            windows?.Where(x => x.Name == "wdw_clock_settings").FirstOrDefault().Close(true);
            GlobalVars.IsRestart = true;
        }

        #endregion

        #region Manejadores de eventos.
        /// <summary>
        /// Abre el cuadro de diálogo de selección de carpeta y escribe la ruta en el cuadro de texto.
        /// </summary>
        private async Task OpenFolderBrowser()
        {
            Window? _window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).Windows.LastOrDefault();
            var topLevel = TopLevel.GetTopLevel(_window);

            if (topLevel != null)
            {
                var folderOptions = new FolderPickerOpenOptions
                {
                    Title = "Seleccione un Directorio",
                    SuggestedStartLocation = await FolderPickerService.GetStartLocationAsync(topLevel.StorageProvider, PathTmp)
                };

                var result = await topLevel.StorageProvider.OpenFolderPickerAsync(folderOptions);

                if (result.Count > 0)
                {
                    PathTmp = result[0].Path.LocalPath;
                }
            }
        }

        /// <summary>
        /// Escribe la ruta de trabajo predeterminada en el cuadro de texto.
        /// </summary>
        private void SetDefPath()
        {
            PathTmp = GlobalVars.DefWorkPath;
        }

        /// <summary>
        /// Crea un nuevo UUID para este cliente del reloj.
        private void GenerateUUID()
        {
            string? new_giud = Guid.NewGuid().ToString();

            RegAccess.SetRegValue("clock_uuid", new_giud);
            if (GlobalVars.clockSettings != null) GlobalVars.clockSettings.clock_uuid = new_giud;
            if (c_settings != null) c_settings.clock_uuid = new_giud;
            EvalUUID();
        }

        private async Task RegisterDevice()
        {
            try
            {
                var auth = new AuthenticationService(new HttpClient());
                await auth.RegisterDeviceAsync($"checkclock_offices_{_clockSettings.clock_uuid}");


                await ShowMessage("Registro exitoso", "El dispositivo ha sido registrado correctamente.", 350);
                RestartApp();
            }
            catch (Exception ex)
            {
                await ShowMessage("Error", $"Error al registrar el dispositivo: {ex.Message}", 350);
            }
        }
        #endregion
    }
}
