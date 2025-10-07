using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using VTACheckClock.Helpers;
using VTACheckClock.Models;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;
using VTACheckClock.Views;

namespace VTACheckClock.ViewModels
{
    partial class MainWindowViewModel
    {
        private readonly DispatcherTimer tmrSyncRetry = new();
        private readonly DispatcherTimer tmrCheckNetConnection = new();
        private bool ScheduleTriggered = false;
        private static DateTime LastSyncDate = DateTime.MinValue;
        private static bool RetryingSync = false;
        private readonly TimeSpan normalInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan offlineInterval = TimeSpan.FromSeconds(5);
        private bool lastConnectionState = true;

        /// <summary>
        /// Envía los registros de asistencia almacenados en caché al servidor.
        /// </summary>
        private Task<bool> UploadPunches()
        {
            tmrSyncRetry.IsEnabled = false;

            if (!GlobalVars.BeOffline) {
                try {
                    string[] from_file = GlobalVars.AppCache.GetCachedPunches(2);

                    if (from_file.Length > 0) {
                        ScantRequest la_req = new() {
                            Question = (
                                GlobalVars.clockSettings.clock_office.ToString() + "." +
                                CommonProcs.ZipString(string.Join(GlobalVars.SeparAtor[0].ToString(), from_file))
                            )
                        };

                        if (CommonProcs.SendPunches(la_req)) {
                            GlobalVars.AppCache.PurgeCachedPunches(2);
                            log.Info($"Se sincronizaron correctamente {from_file.Length} registro(s) de empleados.");

                            return Task.FromResult(true);
                        } else {
                            SyncError = "El servidor ha reportado un problema al procesar los registros de asistencia. Favor de comunicarse con el administrador del sistema.";
                            log.Warn("El servidor ha reportado un problema al procesar los registros de asistencia.");

                            return Task.FromResult(false);
                        }
                    }
                    else {
                        return Task.FromResult(true);
                    }
                } catch (Exception exc) {
                    SyncError = "Ha ocurrido un fallo al sincronizar los registros de asistencia. Favor de comunicarse con el administrador del sistema.";
                    log.Error(exc, SyncError);

                    return Task.FromResult(false);
                } finally
                {
                    tmrSyncRetry.IsEnabled = true;
                }
            } else {
                tmrSyncRetry.IsEnabled = true;
                return Task.FromResult(true);
            }
        }

        /// <summary>
        /// Configura los intervalos para los temporizadores de la aplicación.
        /// <para>1) Temporizador para intentos de sincronización de registros.</para>
        /// <para>2) Temporizador para mostrar Avisos.</para>
        /// <para>3) Temporizador para verificar la conexión a Internet.</para>
        /// </summary>
        private void ConfigTimers()
        {
            int retrymins = CommonProcs.ParamInt(11); //Retry interval in minutes

            retrymins = (retrymins < 10) ? 10 : retrymins;
            tmrSyncRetry.Interval = TimeSpan.FromMilliseconds(retrymins * 60 * 1000);
            tmrSyncRetry.Tick += TmrSyncRetry_Tick;
            tmrSyncRetry.Start();

            tmrNotices.Interval = TimeSpan.FromMilliseconds(CommonProcs.ParamInt(9) * 1000);
            tmrNotices.Tick += TmrNotices_Tick;
            tmrNotices.Start();

            tmrCheckNetConnection.Interval = normalInterval;
            tmrCheckNetConnection.Tick += async (sender, e) => await ToggleConnIndicator();
            tmrCheckNetConnection.Start();
        }

        /// <summary>
        /// Inicia o detiene los temporizadores.
        /// </summary>
        /// <param name="startstop">True para iniciar los temporizadores, False para detenerlos.</param>
        private void ToggleTimers(bool startstop = true)
        {
            tmrNotices.IsEnabled = startstop;

            var clock = App.ServiceProvider.GetService<ClockService>() ?? new ClockService();
            clock.ToggleTimers(startstop);
        }

        /// <summary>
        /// Actualiza el estado de la conexion a Internet en la Pantalla del Checador.
        /// </summary>
        /// <returns></returns>
        private async Task ToggleConnIndicator()
        {
            //bool isLocalNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
            var IsOnline = await CommonValids.ValidInternetConnAsync();

            NetworkConStatus = IsOnline ? "Conectado" : "Sin conexión";
            IsNetConnected = IsOnline;
            GlobalVars.BeOffline = !IsOnline;

            // Ajustar el intervalo según el estado de la conexión
            if (IsOnline != lastConnectionState)
            {
                tmrCheckNetConnection.Interval = IsOnline ? normalInterval : offlineInterval;
                lastConnectionState = IsOnline;
            }
        }

        /// <summary>
        /// Evento TICK del temporizador del reintento de sincronización.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TmrSyncRetry_Tick(object? sender, EventArgs e)
        {
            if(!ScheduleTriggered) {
                RetryingSync = true;
                Dispatcher.UIThread.InvokeAsync(async () => await SyncWithLoader());
            }
        }

        /// <summary>
        /// Evalúa si es hora de iniciar la sincronización automática, según los parámetros de configuración del sistema.
        /// </summary>
        /// <returns>True si la hora actual coincide con la configurada en el parámetro correspondiente.</returns>
        private bool CheckSyncDTime()
        {
            DateTime sync_sched = CommonProcs.ParamDTime(2);
            var currentTime = CurrentClockTime.CurrentTime;

            // Crear un DateTime con la hora de sincronización programada para el día actual
            DateTime targetSyncTime = new(currentTime.Year, currentTime.Month, currentTime.Day, sync_sched.Hour, sync_sched.Minute, 0);

            // Permitir una ventana de +/- 30 segundos para la sincronización
            bool isWithinTolerance = Math.Abs((currentTime - targetSyncTime).TotalSeconds) <= 30;

            return currentTime.Date > LastSyncDate.Date && isWithinTolerance && !RetryingSync;
        }

        /// <summary>
        /// Invoca la sincronización de inicio.
        /// </summary>
        private async Task StarterSync()
        {
            GlobalVars.StartingUp = true;
            Messenger.Send("ToggleOverlay", true);
            await SyncWithLoader();
            Messenger.Send("ToggleOverlay", false);
            GlobalVars.StartingUp = false;
            if (GlobalVars.SyncOnly) KillMe("Cierre después de sincronizar las checadas en el evento FormShown.");
        }

        /// <summary>
        /// Inicia la sincronización del formulario, mostrando el cuadro de carga.
        /// </summary>
        /// <param name="el_msg">Texto (opcional) que sobreescribirá el mensaje predeterminado de la ventana de carga.</param>
        private async Task<bool> SyncWithLoader()
        {
            ToggleTimers(false);

            var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            string mssg = "Sincronizando, por favor espere...";
            var _window = new LoaderWindow() {
                DataContext = new LoaderViewModel(mssg)
            };

            if (MainWindow.Name == "wdw_Main") {
               _ = _window.ShowDialog(MainWindow);
            }

            IsSyncing = true;
            bool sync_resp = await SyncAll();
            IsSyncing = false;

            if (!sync_resp) await MessageBox.ShowMessage("Problema al sincronizar", SyncError, -1, 150);
            HideLoader();

            ToggleTimers(true);

            return sync_resp;
        }

        /// <summary>
        /// Ejecuta todos los procesos de sincronización y actualización del formulario.
        /// </summary>
        private async Task<bool> SyncAll()
        {
            CommonProcs.SetOfflineMode();
            await ToggleConnIndicator();

            bool upload_result;
            //ToggleConnIndicator(!GlobalVars.BeOffline);

            try
            {
                await TakeMyTime();
                upload_result = await UploadPunches();
                await GetNotices();
                upload_result = await GetFMDs();
                await ConsolidateHistory();
                await UpdateHistoryPanel();

                if (upload_result && !GlobalVars.BeOffline) RegAccess.SaveLastSync(GetCurrentClockTime());
            }
            catch (Exception ex)
            {
                SyncError = "Ocurrió un error no especificado durante la sincronización. Favor de contactar al administrador del sistema. " + ex.Message;
                HideLoader();
                upload_result = false;
            }

            if (ScheduleTriggered && upload_result)
            {
                LastSyncDate = GetCurrentClockTime();
                ScheduleTriggered = false;
                log.Info("La sincronización programada ha finalizado correctamente!");
            }
            else if (RetryingSync && upload_result)
            {
                log.Info("El intento de Sincronización finalizó correctamente!");
                RetryingSync = false;
            }

            return upload_result;
        }

        private static void HideLoader() { 
            var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var windows = MainWindow?.OwnedWindows;

            if (windows != null) {
                foreach (var _window in windows) {
                    if (_window.DataContext is LoaderViewModel) {
                        _window.Close();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Abre el cuadro de diálogo de Sincronización Manual.
        /// </summary>
        private void InvokeManualSyncEvt(bool sync = true)
        {
            Dispatcher.UIThread.InvokeAsync(async () => {
                await InvokeManualSync();
                KillMe("Sincronización manual de eventos activado.");
            });
        }

        private async Task InvokeManualSync()
        {
            if (CommonValids.InvokeLogin(GlobalVars.ClockSyncPriv))
            {
                await SyncWithLoader();
                //await Task.Delay(2000);
                GlobalVars.ForceExit = true;
            }
        }
    }
}