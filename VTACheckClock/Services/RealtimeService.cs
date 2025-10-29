using Microsoft.AspNetCore.SignalR.Client;
using NLog;
using System;
using System.Threading.Tasks;
using VTACheckClock.Models;

namespace VTACheckClock.Services
{
    public class RealtimeService : IRealtimeService
    {
        private readonly Logger _log = LogManager.GetLogger("app_logger");
        private readonly MainSettings _mainSettings;
        private readonly SignalRClient _signalRClient;
        private WSClient? _wsClient;

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<HubConnectionState>? ConnectionStateChanged;

        public RealtimeService(SignalRClient signalRClient)
        {
            _signalRClient = signalRClient;
            _mainSettings = RegAccess.GetMainSettings() ?? new MainSettings();

            // Puente de eventos desde SignalR
            _signalRClient.ConnectionStateChanged += (s, state) => ConnectionStateChanged?.Invoke(this, state);
            _signalRClient.MessageReceived += (s, msg) => MessageReceived?.Invoke(this, msg);
        }

        public async Task InitializeAsync()
        {
            try
            {
                if (_mainSettings.UsePusher)
                {
                    _wsClient = new WSClient();
                    _wsClient.PunchReceived += (s, msg) => MessageReceived?.Invoke(this, msg);
                    await _wsClient.Connect();
                }
                else
                {
                    await _signalRClient.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error al inicializar RealtimeService");
                throw;
            }
        }

        public async Task ReloadAsync()
        {
            try
            {
                if (_mainSettings.UsePusher)
                {
                    _wsClient ??= new WSClient();
                    await _wsClient.ReloadConnection();
                }
                else
                {
                    await _signalRClient.ReloadConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error al recargar conexión en RealtimeService");
            }
        }

        public bool IsConnected
        {
            get
            {
                if (_mainSettings.UsePusher)
                {
                    // Reflejar estado real de Pusher/WSClient
                    return WSClient.IsPusherConnected();
                }
                return _signalRClient.IsConnected;
            }
        }

        public async Task SendMessageAsync(PunchLine new_punch, FMDItem? emp = null)
        {
            try
            {
                if (_mainSettings.UsePusher)
                {
                    _wsClient ??= new WSClient();
                    _wsClient.StorePunch(new_punch, emp);
                }
                else
                {
                    await _signalRClient.SendMessageAsync(new_punch, emp);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error enviando punch desde RealtimeService");
            }
        }

        public async Task SendAdminAlertAsync(AdminErrorAlert alert)
        {
            try
            {
                if (_mainSettings.UsePusher)
                {
                    _wsClient ??= new WSClient();
                    _wsClient.StoreAdminAlert(alert);
                }
                else
                {
                    await _signalRClient.SendAdminAlertAsync(alert);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error enviando admin alert desde RealtimeService");
            }
        }
    }
}