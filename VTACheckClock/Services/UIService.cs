using Avalonia.Threading;
using NAudio.Wave;
using NLog;
using System;
using System.IO;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.Services
{
    public class UIService
    {
        private readonly Logger log = LogManager.GetLogger("app_logger");
        private WaveOutEvent? waveOutEvent;

        // Sonidos asociados a los tipos de evento
        private static readonly string[] PunchSounds = ["Unknown", "Entry", "Exit", "Error"];

        /// <summary>
        /// Carga y reproduce el sonido indicado, de acuerdo al tipo de evento.
        /// </summary>
        /// <param name="beepType">Tipo de sonido a reproducir.</param>
        public async Task PlayBeep(int beepType = 0)
        {
            try {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StopSound();

                    var beepFile = Path.Combine(GlobalVars.AppWorkPath, "Assets", "Audio", $"Punch{PunchSounds[beepType]}.wav");
                    waveOutEvent = new WaveOutEvent();
                    waveOutEvent.Init(new WaveFileReader(beepFile));
                    waveOutEvent.Play();
                });
            }
            catch (Exception ex) {
                log.Warn($"Error al reproducir sonido: {ex.Message}");
            }
        }

        /// <summary>
        /// Detiene la reproducción actual si ya se está reproduciendo.
        /// </summary>
        public void StopSound()
        {
            if (waveOutEvent != null) {
                if (waveOutEvent.PlaybackState == PlaybackState.Playing) {
                    waveOutEvent.Stop();
                }

                waveOutEvent.Dispose();
                waveOutEvent = null;
            }
        }

        /// <summary>
        /// Construye los textos de las etiquetas de información según la acción y datos recibidos.
        /// <para> Metodo que devuelve una tupla con los textos para las etiquetas de información.</para>
        /// </summary>
        public (string name, string eventText) BuildInfoLabels(int action, string? emp_num, string? emp_nom, PunchLine? punch)
        {
            switch (action)
            {
                case 6:
                    return ($"{emp_num} - {emp_nom}", "Esperando huella...");
                case 5:
                    return (string.Empty, "Esperando huella...");
                case 4:
                    return ("Procesando...", "Espere...");
                case 3:
                    return ("No se encontró su huella", "Por favor, inténtelo de nuevo.");
                case 2:
                    var allowed_time = CommonProcs.ParamInt(3);
                    string secs_str = (allowed_time == 1) ? "segundo." : ($"{allowed_time} segundos.");
                    return ("Registro duplicado", $"Sólo puede generar un registro cada {secs_str}");
                case 1:
                    if (punch != null) {
                        return ($"{emp_num} - {emp_nom}", $"{CommonObjs.EvTypes[punch.Punchevent]} registrada a las {punch.Punchtime:HH:mm:ss} horas.");
                    }
                    return (string.Empty, string.Empty);
                case 0:
                default:
                    return (string.Empty, string.Empty);
            }
        }
    }
}