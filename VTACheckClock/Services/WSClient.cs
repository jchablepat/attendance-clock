using Avalonia.Threading;
using Newtonsoft.Json;
using NLog;
using PusherClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;
using VTACheckClock.Views;

namespace VTACheckClock.Services
{
    class WSClient
    {
        private static Pusher? _Pusher;
        private static Channel? _TimeClockChannel;
        //private static GenericPresenceChannel<Office> _TimeClockChannel;
        private static Dictionary<string, int>? _Settings;
        private string? OFFICE_ID;
        readonly Logger log = LogManager.GetLogger("app_logger"); //.LogManager.GetCurrentClassLogger();
        //MemoryTarget memoryTarget = (NLog.Targets.MemoryTarget)LogManager.Configuration.FindTargetByName("logViewer");
        public MainSettings? m_settings;
        public ClockSettings? c_settings;
        //public static WSServer? WSServer = new();
        public event EventHandler<string>? PunchReceived;
        private const double ReconnectThreshold = 60000; // 1 minute in milliseconds
        private static double DisconnectedElapsed = 0; // 1 minute in milliseconds
        private static DateTime? _OldDisconnectTime = DateTime.UtcNow;
        private static DateTime? _NewDisconnectTime;
        private static bool ForceReconnecting;
        private readonly WSServer _WSServer = new();
        /// <summary>
        /// Cola de mensajes pendientes por enviar.
        /// </summary>
        private readonly ConcurrentQueue<PunchRecord> PendingMessages = new();
        private readonly ConcurrentQueue<AdminErrorAlert> PendingAdminAlerts = new();

        public WSClient()
        {
            LoadSettings();
        }

        private static void LoadSettings()
        {
            Dictionary<string, int>? settings = new() {
                { "awaitTime", 30 },
                { "reconnectDelay", 30 },
                { "maxAwaitTime", 3600 }
            };
#if DEBUG
            settings["reconnectDelay"] = 20;
            settings["maxAwaitTime"] = 300;
#endif
            _Settings = settings;
        }

        public async Task Connect()
        {
            m_settings = RegAccess.GetMainSettings() ?? new MainSettings();
            c_settings = RegAccess.GetClockSettings() ?? new ClockSettings();

            if (m_settings.Websocket_enabled) {
                await InitPusher();
                //await Task.Delay(3000); // We wait 1-3 secs to avoid server saturation problems
            } else {
                log.Warn("El Servidor WebSocket se encuentra actualmente desactivado.");
            }
        }

        public async Task Disconect()
        {
            try {
                if (_Pusher != null) {
                    await _Pusher.DisconnectAsync().ConfigureAwait(false);
                    // Unbind all event listeners from the channel
                    _TimeClockChannel?.UnbindAll();
                    // Remove all channel subscriptions
                    await _Pusher.UnsubscribeAllAsync().ConfigureAwait(false);
                    ReportClientStatus("Offline");
                }
            } catch (Exception e) {
                log.Error(new Exception(), $"Pusher error ocurred, can't disconnect: {e.InnerException}");
            }
        }

        public async Task ReloadConnection()
        {
            try {
                ForceReconnecting = true;
                log.Warn("####### WSClient Restarting Pusher #######");
                await Disconect();
                await Connect();
            }
            catch (Exception e) {
                log.Error(e.Message);
            }

        }

        private async Task InitPusher()
        {
            try {
                string? host = m_settings?.Websocket_host ?? string.Empty;
                string? port = m_settings?.Websocket_port ?? string.Empty;
                string? appKey = m_settings?.Pusher_key ?? string.Empty;
                string? pusher_cluster = m_settings?.Pusher_cluster ?? "mt1";
                OFFICE_ID = (c_settings?.clock_office).ToString();

                string? uri_host = host.Replace("https://", "").Replace("http://", "");
                string? fullHost = $"{uri_host}:{port}";

                if (!string.IsNullOrEmpty(port) && !string.IsNullOrEmpty(appKey) && !string.IsNullOrEmpty(pusher_cluster)) {
                    log.Info("####### WSClient Initing pusher #######");
                    log.Info($"Connecting to {fullHost} with Id Office: {OFFICE_ID}.");

                    // Create Pusher client ready to subscribe to public, private and presence channels
                    ///////////Use "Authorizer" parameter, to connect only private and presence channels.
                    //log.Info($"Authenticating client from url {host}/broadcasting/auth");
                    var seconds = GlobalVars.BeOffline ? 60 : 120;

                    _Pusher = new Pusher(appKey, new PusherOptions {
                        Cluster = pusher_cluster,
                        Encrypted = true,
                        ClientTimeout = TimeSpan.FromSeconds(seconds)
                    });
                    ///************** Add event handlers **************/
                    _Pusher.ConnectionStateChanged += Pusher_ConnectionStateChanged;
                    _Pusher.Error += Pusher_Error;
                    _Pusher.Connected += Pusher_Connected;
                    _Pusher.Disconnected += Pusher_Disconnected;
                    _Pusher.Subscribed += SubscribedHandler;

                    try {
                        /************** Subscribe to Public Channel before connect **********/
                        //_TimeClockChannel = await _Pusher.SubscribeAsync("my-channel").ConfigureAwait(false);

                        /************** Subscribe to Public Channel before connect **********/
                        _TimeClockChannel = await _Pusher.SubscribeAsync($"checkclock-offices.{OFFICE_ID}").ConfigureAwait(false);
                        /************** Subscribe to Presence Channel **********************/
                        /////////////// Important!!! Presence channels always start with prefix 'presence-', following of {channel_name}.{ID}'
                        //_TimeClockChannel = await _Pusher.SubscribePresenceAsync<Office>($"presence-checkclock-offices.{OFFICE_ID}").ConfigureAwait(false);

                        // Connect
                        await _Pusher.ConnectAsync().ConfigureAwait(false);

                    } catch (Exception error) {
                        // Handle error
                        log.Error(new Exception(), "Error attempting to connect Pusher: " + error);
                    }
                } else {
                    log.Warn("Missing parameters to connect with WebSocket Server, verify configuration...");
                }
            }
            catch (Exception ex)
            {
                log.Error(new Exception(), "" + ex);
            }
        }

        public async void Pusher_ConnectionStateChanged(object sender, ConnectionState state)
        {
            //if (!RetryReconnection()) {
            //    return;
            //}

            switch(state)
            {
                case ConnectionState.Disconnected:
                    if (!_TimeClockChannel.IsSubscribed)
                    {
                        string? eventName = m_settings?.Event_name ?? string.Empty;
                        log.Info("The channel unsubscribed from the " + '"' + eventName + '"' + " event.");
                    }

                    log.Warn("Pusher Service is disconnected with ID: " + ((Pusher)sender).SocketID + " and thread: " + Environment.CurrentManagedThreadId);
                    break;
                case ConnectionState.WaitingToReconnect:
                    log.Info("Please wait, re-trying connection... with thread: " + Environment.CurrentManagedThreadId);
                    break;
                case ConnectionState.Connected:
                    log.Info("Pusher Service is connected with ID: " + ((Pusher)sender).SocketID);
                    ForceReconnecting = false;

                    await SendPendingMessagesAsync();
                    break;
                default:
                    log.Info("Connection state is '" + state.ToString() + "' from thread: " + Environment.CurrentManagedThreadId);
                    break;
            }
        }

        public void Pusher_Error(object? sender, PusherException? error)
        {
            if ((int)error.PusherCode < 5000)
            {
                //if(error.PusherCode == ErrorCodes.ConnectionNotAuthorizedWithinTimeout && !RetryReconnection()) {
                //    return;
                //}

                // Error received from Pusher cluster, use PusherCode to filter.
                log.Warn("Pusher error ocurred: " + error.Message);
            }
            else
            {
                if (error is ChannelUnauthorizedException unauthorizedAccess)
                {
                    // Private and Presence channel failed authorization with Forbidden (403)
                    log.Error(new Exception(), "Private and Presence channel failed authorization with Forbidden: " + unauthorizedAccess.Message);
                }
                else if (error is ChannelAuthorizationFailureException httpError)
                {
                    // Authorization endpoint returned an HTTP error other than Forbidden (403)
                    log.Error(new Exception(), "Authorization endpoint returned an HTTP error other than Forbidden: " + httpError.Message);
                }
                else if (error is OperationTimeoutException timeoutError)
                {
                    // A client operation has timed-out. Governed by PusherOptions.ClientTimeout
                    log.Error(new Exception(), "A client operation has timed-out: " + timeoutError.Message);
                }
                else if (error is ChannelDecryptionException decryptionError)
                {
                    // Failed to decrypt the data for a private encrypted channel
                    log.Error(new Exception(), "Failed to decrypt the data for a private encrypted channel: " + error.ToString());
                    log.Warn("ChannelDecryptionException => " + decryptionError.Message);
                }
                else
                {
                    // Handle other errors
                    //log.Error(new Exception(), "Pusher error ocurred: " + error.ToString());
                    log.Warn("Pusher error ocurred: " + error.Message);
                    //readyEvent.WaitOne(TimeSpan.FromSeconds(10));
                }
            }
        }

        public void Pusher_Connected(object sender)
        {
            if (_NewDisconnectTime.HasValue) _OldDisconnectTime = _NewDisconnectTime;
        }

        public void Pusher_Disconnected(object sender)
        {
            _NewDisconnectTime = DateTime.UtcNow;

            // Si es la primera vez que se desconecta le asignamos el mismo horario.
            //if (!_OldDisconnectTime.HasValue) _OldDisconnectTime = DateTime.UtcNow;
            TimeSpan? difference = _NewDisconnectTime - _OldDisconnectTime;
            DisconnectedElapsed = difference?.TotalSeconds ?? 0;

            //Debug.WriteLine("_OldDisconnectTime => " + _OldDisconnectTime + ", _NewDisconnectTime => " + _NewDisconnectTime + ": "+ DisconnectedElapsed + " segundos");
        }

        private static bool RetryReconnection()
        {
            var retry = ForceReconnecting; // || DisconnectedElapsed == 0 || DisconnectedElapsed >= 60;
            return retry;
        }

        private void SubscribedHandler(object? sender, Channel? channel)
        {
            try {
                //if (!RetryReconnection()) return;

                //if (channel is GenericPresenceChannel<Office>) {
                    log.Info("Binding channel events.......");
                    string? eventName = m_settings?.Event_name ?? string.Empty; //VtSoftware\\VtEmployees\\Events\\CheckClockOfficeOnline

                    if (!string.IsNullOrEmpty(eventName)) {
                        channel.Bind(eventName, ChannelListener);
                    } else {
                        log.Warn("Hey, no events configured!");
                    }

                    ReportClientStatus("Online");
                    //readyEvent.Set();
                //}
            } catch(Exception exc) {
                log.Info("SubscribedHandlerError: " + exc.Message);
            }
        }

        private void ChannelListener(PusherEvent data)
        {
            try {
                log.Info($"Message received from Channel '{data.ChannelName}' and Event '{ data.EventName}': { data.Data }");
                
                OnMessageReceived(data.Data);
                Message? jsonConvert = JsonConvert.DeserializeObject<Message>(data.Data);
                //dynamic? punch_event = JsonConvert.DeserializeObject(data.Data);

                if (jsonConvert?.Data != null && jsonConvert.Data != "")
                {
                    Notice? notice = JsonConvert.DeserializeObject<Notice>(jsonConvert.Data);
                    //dynamic notice = JsonConvert.DeserializeObject(jsonConvert.Data);
                    Dispatcher.UIThread.InvokeAsync(async () => {
                        await Task.Delay(200);
                        new MainWindow().AddNotice(new Notice {
                            id = Convert.ToInt32(notice.id),
                            caption = notice.caption,
                            body = notice.body,
                            image = notice.image
                        });
                    });
                }
            } catch (Exception exc) {
                log.Info("ChannelListenerError: " + exc.Message);
            }
        }

        protected virtual void OnMessageReceived(string message)
        {
            PunchReceived?.Invoke(this, message);
        }

        private void ReportClientStatus(string status)
        {
            log.Info(status);
        }

        /// <summary>
        /// Envia el nuevo registro de asistencia del empleado asociado a una oficina a traves del canal conectado en el servidor de WebSocket, para que se registre en tiempo real en la base de datos.
        /// </summary>
        /// <returns></returns>
        public bool StorePunch(PunchLine new_punch, FMDItem? emp = null)
        {
            try {
                string? host = m_settings?.Employees_host ?? string.Empty;
                /*
                if (!string.IsNullOrEmpty(host)) {
                    log.Info("Sending employee assistance data with ID: " + new_punch.punchemp);

                    var punch_data = new PunchRecord() {
                        punchemp = new_punch.punchemp,
                        punchevent = new_punch.punchevent,
                        punchtime = new_punch.punchtime.ToString("yyyy/MM/dd HH:mm:ss"),
                        punchinternaltime = new_punch.punchinternaltime.ToString("yyyy/MM/dd HH:mm:ss"),
                        offid = !string.IsNullOrEmpty(idOffice) ? Convert.ToInt32(idOffice) : Convert.ToInt32(OFFICE_ID)
                    };

                    var payload = new {
                        clientId = !string.IsNullOrEmpty(idOffice) ? idOffice : OFFICE_ID,
                        punch_data = punch_data
                    };

                    Dispatcher.UIThread.InvokeAsync(async () => {
                        await ExecuteApiRest(host + "/api/v1/employees/punch-record", MethodHttp.POST, punch_data);
                    });
                }
                */

                Dispatcher.UIThread.InvokeAsync(async () => {
                    var punch = new PunchRecord {
                        IdEmployee = new_punch.Punchemp, 
                        EmployeeFullName = emp.empnom,
                        EventTime = new_punch.Punchtime.ToString("yyyy/MM/dd HH:mm:ss"),
                        InternalEventTime = new_punch.Punchinternaltime.ToString("yyyy/MM/dd HH:mm:ss"),
                        IdEvent = new_punch.Punchevent, EventName = CommonObjs.EvTypes[new_punch.Punchevent]
                    };

                    await SendMessageAsync(punch);
                });

                return true;
            }
            catch (Exception ex) {
               log.Error(new Exception(), "Error while triggering employee assistance: " + ex);
                return false;
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
        private async Task SendPendingMessagesAsync()
        {
            const int maxRetries = 3; // Número máximo de reintentos
            int currentRetry = 0;
            var tempQueue = new ConcurrentQueue<PunchRecord>();
            var tempAdminQueue = new ConcurrentQueue<AdminErrorAlert>();

            while (PendingMessages.TryDequeue(out PunchRecord? pendingPunch)) {
                if (!IsPusherConnected())
                {
                    // Si no hay conexión, guardamos el mensaje en una cola temporal
                    tempQueue.Enqueue(pendingPunch);
                    log.Warn($"Reintento de reenvio de mensajes {currentRetry + 1}/{maxRetries}: Conexión no disponible, almacenando mensaje...");
                    
                    currentRetry++;
                    if (currentRetry >= maxRetries)
                    {
                        log.Error("Se alcanzó el límite máximo de reintentos. Los mensajes quedarán pendientes hasta la próxima reconexión.");
                        // Devolvemos todos los mensajes a la cola principal
                        while (tempQueue.TryDequeue(out PunchRecord? temp))
                        {
                            PendingMessages.Enqueue(temp);
                        }

                        break;
                    }
                    
                    // Esperamos antes del siguiente reintento
                    await Task.Delay(5000); // 5 segundos entre reintentos
                    continue;
                }
                
                await SendMessageAsync(pendingPunch);
            }

            // Procesar Admin Alerts pendientes
            currentRetry = 0;
            while (PendingAdminAlerts.TryDequeue(out AdminErrorAlert? pendingAlert))
            {
                if (!IsPusherConnected())
                {
                    tempAdminQueue.Enqueue(pendingAlert);
                    log.Warn($"Reintento admin alerts {currentRetry + 1}/{maxRetries}: Conexión no disponible, almacenando alerta...");

                    currentRetry++;
                    if (currentRetry >= maxRetries)
                    {
                        log.Error("Se alcanzó el límite máximo de reintentos para admin alerts. Quedarán pendientes.");
                        while (tempAdminQueue.TryDequeue(out AdminErrorAlert? temp))
                        {
                            PendingAdminAlerts.Enqueue(temp);
                        }
                        break;
                    }

                    await Task.Delay(5000);
                    continue;
                }

                await SendAdminAlertAsync(pendingAlert);
            }
        }

        /// <summary>
        /// Intenta enviar un mensaje via WebSocket, si la conexión falla, lo almacena en la cola.
        /// </summary>
        /// <param name="punch"></param>
        /// <returns></returns>
        private async Task SendMessageAsync(PunchRecord punch)
        {
            if (IsPusherConnected()) {
                try {
                    //IMPORTANT!!!! The event name for client events must start with 'client-' and are only supported on private(non-encrypted) and presence channels.
                    //_TimeClockChannel.TriggerAsync("client-punch-record", new { message = "Este es un mensaje de Prueba." });

                    await _WSServer.TriggerEventAsync("checkclock-offices." + c_settings?.clock_office, m_settings.Event_name!, punch);
                } catch (Exception ex) {
                    log.Warn("Error enviando mensaje: " + ex.Message);
                    PendingMessages.Enqueue(punch);
                }
            } else {
                log.Warn("Conexión no disponible, almacenando mensaje...");
                PendingMessages.Enqueue(punch);
            }
        }

        public void StoreAdminAlert(AdminErrorAlert alert)
        {
            PendingAdminAlerts.Enqueue(alert);
            _ = SendAdminAlertAsync(alert); // fire-and-forget: intentará enviar si hay conexión
        }

        private async Task SendAdminAlertAsync(AdminErrorAlert alert)
        {
            if (IsPusherConnected())
            {
                try
                {
                    string channel = $"checkclock-admin"; // canal administrativo global (puede filtrarse por office en payload)
                    string eventName = m_settings?.Event_name ?? "admin-alert"; // reutilizamos Event_name si existe otro método
                    await _WSServer.TriggerEventAsync(channel, eventName, alert);
                }
                catch (Exception ex)
                {
                    log.Warn("Error enviando admin alert: " + ex.Message);
                    PendingAdminAlerts.Enqueue(alert);
                }
            }
            else
            {
                log.Warn("Conexión no disponible, almacenando admin alert...");
                PendingAdminAlerts.Enqueue(alert);
            }
        }

        public static bool IsPusherConnected()
        {
            bool IsConnected = _Pusher != null && _Pusher.State == ConnectionState.Connected;
            bool IsSubscribed = _TimeClockChannel?.IsSubscribed ?? false;
            return IsConnected && IsSubscribed;
        }

        #region Consume API REST
        public class Reply
        {
            public string? StatusCode { get; set; }
            public object? Data { get; set; }
        }

        public enum MethodHttp
        {
            GET,
            POST,
            PUT,
            DELETE
        }

        private static HttpMethod CreateHttpMethod(MethodHttp method)
        {
            return method switch {
                MethodHttp.GET => HttpMethod.Get,
                MethodHttp.POST => HttpMethod.Post,
                MethodHttp.PUT => HttpMethod.Put,
                MethodHttp.DELETE => HttpMethod.Delete,
                _ => throw new NotImplementedException("Not implemented http method"),
            };
        }

        /// <summary>
        /// Consume API REST URI with dynamically http methods.
        /// </summary>
        /// <typeparam name="T">Generics <T> allows at runtime to assign a type dynamically.</typeparam>
        /// <param name="url">API REST URI.</param>
        /// <param name="method">HTTP request method or HTTP verbs.</param>
        /// <param name="objectRequest"></param>
        /// <returns></returns>
        public async Task<Reply> ExecuteApiRest<T>(string url, MethodHttp method, T objectRequest)
        {
            //For testing purpose use this url ====> https://jsonplaceholder.typicode.com/posts and create Model
            Reply? oReply = new();
            try {
                using HttpClient? client = new();
                client.DefaultRequestHeaders.Accept.Clear();
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SharedAccessSignature", "VTEWGGJHG-2312");

                var payload = JsonConvert.SerializeObject(objectRequest);
                var bytecontent = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
                bytecontent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                //Si es get o delete no le mandamos content
                HttpContent? c = new StringContent(payload, Encoding.UTF8, "application/json");
                HttpRequestMessage? request = new() {
                    Method = CreateHttpMethod(method),
                    RequestUri = new Uri(url),
                    Content = method != MethodHttp.GET && method != MethodHttp.DELETE ? bytecontent : null
                };

                using HttpResponseMessage? res = await client.SendAsync(request);
                using HttpContent? content = res.Content;
                if (res.IsSuccessStatusCode) {
                    string? data = await content.ReadAsStringAsync();
                    if (data != null)
                        oReply.Data = JsonConvert.DeserializeObject<T>(data);
                } else {
                    log.Info("Error al registrar evento: " + res.ReasonPhrase);
                }

                oReply.StatusCode = res.StatusCode.ToString();
            } catch (WebException ex) {
                oReply.StatusCode = "ServerError";
                if (ex.Response is HttpWebResponse response)
                    oReply.StatusCode = response.StatusCode.ToString();
            } catch (Exception ex) {
                oReply.StatusCode = "AppError";
                log.Error(new Exception(), "Error when sending Employee event from API REST<"+ url + ">: " + ex);
            }
            return oReply;
        }
        #endregion
    }
}