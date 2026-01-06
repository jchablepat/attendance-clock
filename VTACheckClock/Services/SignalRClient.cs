using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Auth;
using VTACheckClock.Services.Libs;


namespace VTACheckClock.Services
{
    public class SignalRClient : IAsyncDisposable
    {
        private readonly Logger _log = LogManager.GetLogger("app_logger");
        private HubConnection? _hubConnection;
        private readonly string? _deviceId;
        private readonly ConcurrentQueue<PunchRecord> _pendingMessages = new();
        private readonly ConcurrentQueue<AdminErrorAlert> _pendingAdminAlerts = new();
        private readonly MainSettings? _mainSettings;
        private readonly ClockSettings? _clockSettings;

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<HubConnectionState>? ConnectionStateChanged;
        private readonly IAuthenticationService? _authService;
        private readonly string? _apiKey;
        private string? _currentToken;
        private DateTime LastConnection { get; set; } = DateTime.MinValue;

        public SignalRClient(IAuthenticationService authService)
        {
            _authService = authService;
            _mainSettings = RegAccess.GetMainSettings() ?? new MainSettings();
            _clockSettings = RegAccess.GetClockSettings() ?? new ClockSettings();
            _deviceId = $"checkclock_offices_{_clockSettings.clock_uuid}";
            _apiKey = _mainSettings.SignalRApiKey ?? "";
        }

        /// <summary>
        /// Obtiene el token de acceso para la autenticación.
        /// <para>1) Obtiene un token inicial para establecer la conexión</para>
        /// <para>2) Automáticamente cuando SignalR detecta que necesita un nuevo token lo renueva</para>
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetAccessTokenAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentToken))
                {
                    // Obtener token inicial
                    _currentToken = await _authService.LoginAsync(_deviceId!, _apiKey!);
                }
                else
                {
                    // Intentar renovar el token
                    _currentToken = await _authService.RefreshTokenAsync(_deviceId!, _apiKey!);
                }
                return _currentToken;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error obteniendo el token de acceso para conectarse al servidor SignalR");
                throw;
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Check network connection
                //if (GlobalVars.BeOffline)
                //{
                //    _log.Warn("No internet connection available. SignalR client will not be initialized.");
                //    return;
                //}

                if (_mainSettings.Websocket_enabled && !_mainSettings.UsePusher && !string.IsNullOrEmpty(_mainSettings.SignalRHubUrl) && !string.IsNullOrEmpty(_mainSettings.SignalRMethodName))
                {
                    string hubUrl = $"{_mainSettings.SignalRHubUrl}/{_mainSettings.SignalRHubName}";

                    // Crear nueva conexión
                    _hubConnection = new HubConnectionBuilder()
                        .WithUrl(hubUrl, options => {
                            options.AccessTokenProvider = async () => await GetAccessTokenAsync();
                        })
                        .WithAutomaticReconnect([ 
                            TimeSpan.FromSeconds(0), 
                            TimeSpan.FromSeconds(2), 
                            TimeSpan.FromSeconds(5), 
                            TimeSpan.FromSeconds(30),
                            TimeSpan.FromMinutes(30),
                            TimeSpan.FromHours(1)
                        ])
                        .Build();

                    // Configurar event handlers
                    ConfigureHubEvents();

                    // Iniciar conexión
                    await ConnectAsync();
                }
                else
                {
                    _log.Warn("Invalid SignalR configuration, skipping initialization.");
                }
            }
            catch(Exception ex)
            {
                _log.Error(ex, "Error initializing SignalR connection.");
                throw;
            }
        }

        private void ConfigureHubEvents()
        {
            if (_hubConnection == null) return;

            _hubConnection!.Reconnecting += error =>
            {
                _log.Warn($"Attempting to reconnect: {error?.Message}");
                // Notificar estado de reconexión
                ConnectionStateChanged?.Invoke(this, HubConnectionState.Reconnecting);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += async connectionId =>
            {
                var timeSinceLastConnection = DateTime.Now - LastConnection;
                _log.Info($"Reconnected with connection ID: {connectionId}. Time since last connection: {timeSinceLastConnection.TotalMinutes:F2} minutes");
                // Notificar estado de reconexión exitosa
                ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);
                await RegisterDevice();
                await ProcessPendingMessages();
                await ProcessPendingAdminAlerts();

                LastConnection = DateTime.Now;
            };

            _hubConnection.Closed += error =>
            {
                _log.Warn($"Connection closed: {error?.Message}");
                // Notificar estado de desconexión
                ConnectionStateChanged?.Invoke(this, HubConnectionState.Disconnected);
                return Task.CompletedTask;
            };

            // Manejador de mensajes entrantes
            _hubConnection.On<string>("DeviceConnected", (deviceId) =>
            {
                _log.Info($"Device connected: {deviceId}");
            });

            _hubConnection.On<string>("DeviceDisconnected", (deviceId) =>
            {
                _log.Info($"Device disconnected: {deviceId}");
            });

            _hubConnection.On<string, PunchRecord>("ReceivePunch", (senderId, punch) =>
            {
                Dispatcher.UIThread.InvokeAsync(() => {
                    _log.Info($"Message received: {JsonSerializer.Serialize(punch)}");

                    MessageReceived?.Invoke(this, JsonSerializer.Serialize(punch));
                });
            });
        }

        private async Task ConnectAsync()
        {
            try
            {
                await _hubConnection!.StartAsync();
                _log.Info("Connected to SignalR hub!");
                await RegisterDevice();

                LastConnection = DateTime.Now;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error connecting to SignalR hub.");
                throw;
            }
        }

        private async Task RegisterDevice()
        {
            try
            {
                await _hubConnection!.InvokeAsync("RegisterDevice", _deviceId, _clockSettings.clock_office.ToString());
                _log.Info($"Device registered: {_deviceId}");
                // Notificar estado de conexión exitosa
                ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);
            }
            catch (HubException ex)
            {
                _log.Error(ex, "Error específico del hub al registrar el dispositivo");
                //throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error registering device");
            }
        }

        public async Task SendMessageAsync(PunchLine new_punch, FMDItem? emp = null)
        {
            var punch = new PunchRecord
            {
                IdEmployee = new_punch.Punchemp,
                EmployeeFullName = emp?.empnom ?? "",
                EventTime = new_punch.Punchtime.ToString("yyyy/MM/dd HH:mm:ss"),
                InternalEventTime = new_punch.Punchinternaltime.ToString("yyyy/MM/dd HH:mm:ss"),
                IdEvent = new_punch.Punchevent,
                EventName = CommonObjs.EvTypes[new_punch.Punchevent]
            };

            await SendPunchAsync(punch);
        }

        public async Task SendPunchAsync(PunchRecord punch)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection.InvokeAsync(_mainSettings.SignalRMethodName!, _deviceId, punch);
                    _log.Info($"Punch sent for employee {punch.IdEmployee}");
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error sending SignalR punch.");
                    _pendingMessages.Enqueue(punch);
                }
            }
            else
            {
                _log.Warn("SignalR Connection not available, queueing message to send later.");
                _pendingMessages.Enqueue(punch);
            }
        }

        /// <summary>
        /// Reenvía los mensajes en cola, cuando la conexión se restablece.
        /// <para>1. Introduce un contador de reintentos para evitar el ciclo infinito</para>
        /// <para>2. Utiliza una cola temporal para manejar los mensajes durante los reintentos</para>
        /// <para>3. Agrega un delay entre reintentos para no saturar el sistema</para>
        /// <para>4. Mejora los mensajes de log para mostrar el progreso</para>
        /// <para>5. Si se alcanza el máximo de reintentos, devuelve los mensajes a la cola principal para intentarlo en la próxima reconexión</para>
        /// </summary>
        /// <returns></returns>
        private async Task ProcessPendingMessages()
        {
            const int maxRetries = 3; // Número máximo de reintentos
            int currentRetry = 0;
            var tempQueue = new ConcurrentQueue<PunchRecord>();

            // El ciclo continúa hasta que no queden mensajes pendientes o se alcance el límite de reintentos
            while (_pendingMessages.TryDequeue(out PunchRecord? punch))
            {
                if (_hubConnection?.State != HubConnectionState.Connected)
                {
                    // Si no hay conexión, guardamos el mensaje en una cola temporal
                    tempQueue.Enqueue(punch);
                    _log.Warn($"Reintento de reenvio de mensajes {currentRetry + 1}/{maxRetries}: Conexión no disponible, almacenando mensaje...");

                    currentRetry++;
                    if (currentRetry >= maxRetries)
                    {
                        _log.Error("Se alcanzó el límite máximo de reintentos. Los mensajes quedarán pendientes hasta la próxima reconexión.");
                        // Devolvemos todos los mensajes a la cola principal
                        while (tempQueue.TryDequeue(out PunchRecord? temp))
                        {
                            _pendingMessages.Enqueue(temp);
                        }

                        break;
                    }

                    // Esperamos antes del siguiente reintento
                    await Task.Delay(5000); // 5 segundos entre reintentos
                    continue;
                }

                await SendPunchAsync(punch);
            }
        }

        public async Task SendAdminAlertAsync(AdminErrorAlert alert)
        {
            var methodName = _mainSettings!.SignalRAdminMethodName;
            if (string.IsNullOrEmpty(methodName))
            {
                //_log.Warn("SignalRAdminMethodName is not configured, cannot send admin alert.");
                return;
            }

            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection!.InvokeAsync(methodName!, _deviceId, alert);
                    _log.Info($"Admin alert sent: {alert.Title} - {alert.Severity}");
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error sending admin alert over SignalR, queueing alert.");
                    _pendingAdminAlerts.Enqueue(alert);
                }
            }
            else
            {
                _log.Warn("SignalR connection not available, queueing admin alert on next reconnection.");
                _pendingAdminAlerts.Enqueue(alert);
            }
        }

        private async Task ProcessPendingAdminAlerts()
        {
            const int maxRetries = 3;
            int currentRetry = 0;
            var tempQueue = new ConcurrentQueue<AdminErrorAlert>();

            while (_pendingAdminAlerts.TryDequeue(out AdminErrorAlert? alert))
            {
                if (_hubConnection?.State != HubConnectionState.Connected)
                {
                    tempQueue.Enqueue(alert);
                    _log.Warn($"Admin alerts retry {currentRetry + 1}/{maxRetries}: SignalR connection unavailable, storing alert...");

                    currentRetry++;
                    if (currentRetry >= maxRetries)
                    {
                        _log.Error("Max retries reached for admin alerts. Alerts will remain pending until next reconnection.");
                        while (tempQueue.TryDequeue(out AdminErrorAlert? temp))
                        {
                            _pendingAdminAlerts.Enqueue(temp);
                        }
                        break;
                    }

                    await Task.Delay(5000);
                    continue;
                }

                await SendAdminAlertAsync(alert);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null; // Importante: limpiar la referencia
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            GC.SuppressFinalize(this);
        }

        public async Task ReloadConnectionAsync()
        {
            await DisconnectAsync();
            await InitializeAsync();
        }

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    }
}