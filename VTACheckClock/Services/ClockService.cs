using Avalonia.Threading;
using NLog;
using System;
using System.Threading.Tasks;
using VTACheckClock.Services.Libs;
using static VTACheckClock.Services.CommonObjs;

namespace VTACheckClock.Services
{
    public class ClockService
    {
        private readonly Logger log = LogManager.GetLogger("app_logger");
        private readonly DispatcherTimer tmrClock = new();
        private readonly DispatcherTimer tmrCachedTime = new();
        private readonly DispatcherTimer tmrNtpRefresh = new();

        /// <summary>
        /// Gets the current clock time data. This property provides access to the current time information, including the current time and its string representation.
        /// </summary>
        public ClockTimeData ClockTimeData { get; private set; } = new();
        private int failedAttempts = 0;

        public event Action<ClockTimeData>? OnTick;

        public async Task InitializeAsync()
        {
            var data = await GetTimeNow();
            GlobalVars.CachedTime = data.CurrentTime;

            ClockTimeData = new ClockTimeData {
                CurrentTime = DateTime.Now,
                TimeString = DateTime.Now.ToString("HH:mm:ss")
            };

            StartTimers();
        }

        private void StartTimers()
        {
            tmrCachedTime.Interval = TimeSpan.FromSeconds(1);
            tmrCachedTime.Tick += (_, __) =>
            {
                GlobalVars.CachedTime = GlobalVars.CachedTime.AddSeconds(1);
            };
            tmrCachedTime.Start();

            tmrClock.Interval = TimeSpan.FromSeconds(1);
            tmrClock.Tick += (_, __) =>
            {
                ClockTimeData = new ClockTimeData {
                    CurrentTime = DateTime.Now,
                    TimeString = DateTime.Now.ToString("HH:mm:ss")
                };

                OnTick?.Invoke(ClockTimeData);
            };
            tmrClock.Start();

            tmrNtpRefresh.Interval = TimeSpan.FromMinutes(15);
            tmrNtpRefresh.Tick += async (_, __) => await UpdateNetworkTimeAsync();
            tmrNtpRefresh.Start();
        }

        public void ToggleTimers(bool start) {
            tmrClock.IsEnabled = start;
            tmrCachedTime.IsEnabled = start;
            tmrNtpRefresh.IsEnabled = start;
        }

        public async Task UpdateNetworkTimeAsync()
        {
            var networkTime = await GetDateTime();
            if (networkTime == DateTime.MinValue)
            {
                log.Warn("No se pudo obtener la hora del servidor NTP. Manteniendo la última hora calculada.");
                failedAttempts++;
                if (failedAttempts > 12)
                {
                    log.Warn("Advertencia: No se ha podido sincronizar la hora en más de 1 hora.");
                }
                return;
            }

            failedAttempts = 0;
            if (Math.Abs((networkTime - GlobalVars.CachedTime).TotalSeconds) > 5 || GlobalVars.CachedTime == DateTime.MinValue)
            {
                log.Info("Actualizando la hora de la caché a la hora del servidor NTP. La diferencia de tiempo detectada es mayor a 5 segundos.");
                GlobalVars.CachedTime = networkTime;
            }
        }
    }
}