using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Reactive;
using VTACheckClock.Models;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.ViewModels
{
    public class PunchSyncPreviewViewModel : ViewModelBase
    {
        public ObservableCollection<PunchRecord> Punches { get; } = [];

        public ReactiveCommand<PunchRecord, Unit> DeletePunchCommand { get; }
        public ReactiveCommand<Unit, bool> SyncNowCommand { get; }
        public ReactiveCommand<Unit, bool> CancelCommand { get; }

        public int Total => Punches.Count;

        public PunchSyncPreviewViewModel()
        {
            LoadPunches();

            DeletePunchCommand = ReactiveCommand.Create<PunchRecord>(DeletePunch);
            SyncNowCommand = ReactiveCommand.Create(() => true);
            CancelCommand = ReactiveCommand.Create(() => false);
        }

        private void LoadPunches()
        {
            try
            {
                var cached = GlobalVars.AppCache.GetCachedPunches(2);
                if (cached == null || cached.Length == 0) return;

                var empDt = GlobalVars.AppCache.RetrieveEmployees();
                Dictionary<string, string>? empMap = null;

                if (empDt != null)
                {
                    empMap = [];
                    foreach (DataRow r in empDt.Rows)
                    {
                        var id = r.Field<string>("EmpID");
                        var name = r.Field<string>("EmpName") ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        // Tolerar duplicados: mantener el primero, o actualizar si el existente está vacío
                        if (empMap.TryGetValue(id, out var existingName))
                        {
                            if (string.IsNullOrWhiteSpace(existingName) && !string.IsNullOrWhiteSpace(name))
                            {
                                empMap[id] = name;
                            }
                        }
                        else
                        {
                            empMap[id] = name;
                        }
                    }
                }

                foreach (var line in cached)
                {
                    var parts = line.Split('|');
                    if (parts.Length < 3) continue;

                    _ = int.TryParse(parts[0], out var empId);
                    _ = int.TryParse(parts[1], out var evtId);
                    var evtName = (evtId >= 0 && evtId < CommonObjs.EvTypes.Length) ? CommonObjs.EvTypes[evtId] : CommonObjs.EvTypes[0];
                    var empName = empMap != null && empMap.TryGetValue(parts[0], out var nm) ? nm : string.Empty;

                    Punches.Add(new PunchRecord
                    {
                        IdEmployee = empId,
                        EmployeeFullName = empName,
                        IdEvent = evtId,
                        EventName = evtName,
                        EventTime = parts.Length > 2 ? parts[2] : string.Empty,
                        InternalEventTime = parts.Length > 3 ? parts[3] : string.Empty
                    });
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
                // Silencio: la vista se mostrará vacía ante cualquier error
            }
        }

        private void DeletePunch(PunchRecord? record)
        {
            if (record == null) return;
            var serialized = string.Join("|", [
                record.IdEmployee.ToString(),
                record.IdEvent.ToString(),
                record.EventTime ?? string.Empty,
                record.InternalEventTime ?? string.Empty
            ]);

            if (GlobalVars.AppCache.RemoveCachedPunch(serialized))
            {
                Punches.Remove(record);
            }
        }
    }
}