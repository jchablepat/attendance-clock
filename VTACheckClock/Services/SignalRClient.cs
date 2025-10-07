using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Auth;


namespace VTACheckClock.Services
{
    public class SignalRClient : IAsyncDisposable
    {
        private readonly Logger _log = LogManager.GetLogger("app_logger");
        private HubConnection? _hubConnection;
        private readonly string? _deviceId;
        private readonly ConcurrentQueue<PunchRecord> _pendingMessages = new();
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
                _log.Error(ex, "Error obteniendo el token de acceso");
                throw;
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
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
                            TimeSpan.FromSeconds(30)
                        ])
                        .Build();

                    // Configurar event handlers
                    ConfigureHubEvents();

                    // Iniciar conexión
                    await ConnectAsync();
                }
                else
                {
                    _log.Warn("No se puede conectar al servidor SignalR, la configuraci�n no es v�lida.");
                }
            }
            catch
            {
                _log.Warn("Error initializing SignalR connection.");
                throw;
            }
        }

        private void ConfigureHubEvents()
        {
            if (_hubConnection == null) return;

            _hubConnection!.Reconnecting += error =>
            {
                _log.Warn($"Attempting to reconnect: {error?.Message}");
                ConnectionStateChanged?.Invoke(this, HubConnectionState.Reconnecting);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += async connectionId =>
            {
                var timeSinceLastConnection = DateTime.Now - LastConnection;
                _log.Info($"Reconnected with connection ID: {connectionId}. Time since last connection: {timeSinceLastConnection.TotalMinutes:F2} minutes");

                ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);
                await RegisterDevice();
                await ProcessPendingMessages();

                LastConnection = DateTime.Now;
            };

            _hubConnection.Closed += error =>
            {
                _log.Warn($"Connection closed: {error?.Message}");
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
                _log.Warn("Error connecting to SignalR hub: " + ex.Message);
                throw;
            }
        }

        private async Task RegisterDevice()
        {
            try
            {
                await _hubConnection!.InvokeAsync("RegisterDevice", _deviceId, _clockSettings.clock_office.ToString());
                _log.Info($"Device registered: {_deviceId}");

                ConnectionStateChanged?.Invoke(this, HubConnectionState.Connected);
            }
            catch (HubException ex)
            {
                _log.Error(ex, "Error espec�fico del hub al registrar el dispositivo");
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
                    _log.Error(ex, "Error sending punch");
                    _pendingMessages.Enqueue(punch);
                }
            }
            else
            {
                _log.Warn("Connection not available, queueing message");
                _pendingMessages.Enqueue(punch);
            }
        }

        /// <summary>
        /// Reenv�a los mensajes en cola, cuando la conexi�n se restablece.
        /// <para>1. Introduce un contador de reintentos para evitar el ciclo infinito</para>
        /// <para>2. Utiliza una cola temporal para manejar los mensajes durante los reintentos</para>
        /// <para>3. Agrega un delay entre reintentos para no saturar el sistema</para>
        /// <para>4. Mejora los mensajes de log para mostrar el progreso</para>
        /// <para>5. Si se alcanza el m�ximo de reintentos, devuelve los mensajes a la cola principal para intentarlo en la pr�xima reconexi�n</para>
        /// </summary>
        /// <returns></returns>
        private async Task ProcessPendingMessages()
        {
            const int maxRetries = 3; // Número máximo de reintentos
            int currentRetry = 0;
            var tempQueue = new ConcurrentQueue<PunchRecord>();

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

        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null; // Importante: limpiar la referencia
            }
        }

        public async ValueTask DisposeAsync() => await DisconnectAsync();

        public async Task ReloadConnectionAsync()
        {
            await DisconnectAsync();
            await InitializeAsync();
        }

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    }
}