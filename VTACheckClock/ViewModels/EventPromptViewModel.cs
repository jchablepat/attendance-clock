using ReactiveUI;
using System;

namespace VTACheckClock.ViewModels
{
    class EventPromptViewModel : ViewModelBase
    {
        private readonly TimeSpan shift_max;
        private string _message = "Han transcurrido más de xxx horas con xxx minutos desde su entrada.";
        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }
        /// <summary>
        /// Muestra un modal emergente, solicitando el tipo de evento que estas tratando de registrar (ENTRADA o SALIDA). 
        /// </summary>
        /// <param name="shiftMax"></param>
        public EventPromptViewModel(TimeSpan shiftMax)
        {
            if(shiftMax != TimeSpan.Zero) {
                shift_max = shiftMax;
                int max_hrs = shift_max.Hours;
                int max_mins = shift_max.Minutes;
                string shift_hrs = max_hrs.ToString();
                string shift_mins = max_mins.ToString();
                string hrs_plural1 = (max_hrs == 1) ? string.Empty : "n";
                string hrs_plural2 = (max_hrs == 1) ? string.Empty : "s";
                string min_plural = (max_mins == 1) ? string.Empty : "s";
                string show_mins = (max_mins > 0) ? "con " + shift_mins + " minuto" + min_plural + " " : string.Empty;

                Message = "Ha" + hrs_plural1 + " transcurrido más de " + shift_hrs + " hora" + hrs_plural2 + " " + show_mins + "desde su entrada.";
            } else {
                Message = "Evento fuera de horario.";
            }
        }
    }
}
