using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using VTACheckClock.Models;

namespace VTACheckClock.Services
{
    public class AdminAlertBackgroundQueue(IServiceProvider serviceProvider)
    {
        private readonly Logger _log = LogManager.GetLogger("app_logger");
        private readonly ConcurrentQueue<QueueItem> _queue = new();
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private volatile bool _isProcessing = false; // Volatile significa que puede ser accedido por múltiples hilos simultáneamente y asegura que siempre se lea el valor más reciente
        private const int MaxRetries = 3;

        private record QueueItem(AdminErrorAlert Alert, int Attempts);

        /// <summary>
        /// Adds an administrative error alert to the processing queue.
        /// </summary>
        /// <remarks>The alert is added to the queue with a default priority of 0. The queue is processed
        /// asynchronously.
        /// </remarks>
        /// <param name="alert">The <see cref="AdminErrorAlert"/> instance representing the error alert to be queued. Cannot be null.</param>
        public void Enqueue(AdminErrorAlert alert)
        {
            _queue.Enqueue(new QueueItem(alert, 0));
            _ = ProcessQueueAsync();
        }

        /// <summary>
        /// Processes the queue of administrative alerts asynchronously, dispatching each alert to the appropriate
        /// services.
        /// </summary>
        /// <remarks>This method ensures that alerts in the queue are sent to both real-time and email
        /// notification services.  If an alert fails to dispatch, it will be retried up to a maximum number of
        /// attempts, with a delay between retries. The method is thread-safe and prevents concurrent processing of the
        /// queue.</remarks>
        /// <returns></returns>
        private async Task ProcessQueueAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true; // Note: This is a simple lock mechanism to prevent concurrent processing.

            try
            {
                while (_queue.TryDequeue(out var item))
                {
                    var alert = item.Alert;
                    var alertException = new Exception(alert.Exception ?? alert.Message ?? "Undefined Error");

                    try
                    {
                        // Enviar por tiempo real
                        //var realtime = _serviceProvider.GetRequiredService<IRealtimeService>();
                        //await realtime.SendAdminAlertAsync(alert);

                        // Enviar por email
                        var adminService = _serviceProvider.GetRequiredService<IAdminAlertService>();
                        await adminService.NotifyErrorAsync(alert.Title ?? "Error", alertException, alert);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn(ex, "Fallo al despachar admin alert, reintentando...");
                        // Reintentos limitados
                        var nextAttempts = item.Attempts + 1;
                        if (nextAttempts < MaxRetries)
                        {
                            await Task.Delay(5000);
                            _queue.Enqueue(new QueueItem(alert, nextAttempts));
                        }
                        else
                        {
                            _log.Error("Se alcanzó el máximo de reintentos para admin alert: {0}", alert.Title);
                        }
                    }
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }
}