using NLog;
using PusherServer;
using System;
using System.Threading.Tasks;
using VTACheckClock.Models;


namespace VTACheckClock.Services
{
    class WSServer
    {
        public Logger log = LogManager.GetLogger("app_logger");
        private readonly Pusher? _pusher;
        public MainSettings? m_settings;

        public WSServer()
        {
            m_settings = RegAccess.GetMainSettings() ?? new MainSettings();

            string? APP_ID = m_settings.Pusher_app_id, APP_KEY = m_settings.Pusher_key, APP_SECRET = m_settings.Pusher_secret, CLUSTER = m_settings.Pusher_cluster;

            if (!string.IsNullOrEmpty(APP_ID) && !string.IsNullOrEmpty(APP_KEY) && !string.IsNullOrEmpty(APP_SECRET)) {
                var options = new PusherOptions {
                    Cluster = CLUSTER,
                    Encrypted = true
                };

                _pusher = new Pusher(APP_ID, APP_KEY, APP_SECRET, options);
            }
        }

        public async Task TriggerEventAsync(string channel, string eventName, object data)
        {
            try {
                if (_pusher == null) return;

                var result = await _pusher.TriggerAsync(channel, eventName, data);
                //log.Info("Resultado de Evento: " + result.StatusCode);
            } catch(Exception ex) {
                log.Warn("Error triggering Server event: " + ex.Message);
            }
        }
    }
}
