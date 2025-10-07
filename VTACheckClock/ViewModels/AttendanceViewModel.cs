using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using VTACheckClock.Models;
using VTACheckClock.Services;

namespace VTACheckClock.ViewModels
{
    class AttendanceViewModel : ViewModelBase
    {
        private DateTimeOffset _startDate, _endDate;
        private ObservableCollection<AttendanceRecord> _attendances = [];
        public ObservableCollection<OfficeData> Offices { get; } = [];
        private int _selOffice = -1;
        private readonly ClockSettings? c_settings;

        public AttendanceViewModel()
        {
            c_settings = RegAccess.GetClockSettings() ?? new ClockSettings();

            // Inicializar fechas
            StartDate = DateTime.Now.AddMonths(-1);
            EndDate = DateTime.Now;

            // Inicializar colección de asistencias
            Attendances = [];
            Offices = [];
            GetOffices();

            // Configurar comando para generar reporte
            //GenerateReportCommand = ReactiveCommand.Create(GenerateReport);
            CancelCommand = ReactiveCommand.Create(() => { });
        }

        public DateTimeOffset StartDate
        {
            get => _startDate;
            set => this.RaiseAndSetIfChanged(ref _startDate, value);
        }

        public DateTimeOffset EndDate
        {
            get => _endDate;
            set => this.RaiseAndSetIfChanged(ref _endDate, value);
        }

        public ObservableCollection<AttendanceRecord> Attendances
        {
            get => _attendances;
            set => this.RaiseAndSetIfChanged(ref _attendances, value);
        }

        public int SelectedOffice
        {
            get => _selOffice;
            set => this.RaiseAndSetIfChanged(ref _selOffice, value);
        }

        //public ICommand GenerateReportCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        private void GetOffices()
        {
            var offices = CommonProcs.GetOffices(new ScantRequest { Question = "0" });

            Offices.Clear();

            foreach (OfficeData off in offices)
            {
                Offices.Add(new OfficeData() {
                    Offid = off.Offid,
                    Offname = off.Offname,
                    Offdesc = off.Offdesc
                });
            }

            SelectedOffice = Offices.Count > 0 ? offices.FindIndex(o => o.Offid == c_settings.clock_office) : -1;
        }
    }
}
