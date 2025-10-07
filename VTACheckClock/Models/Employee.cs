using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace VTACheckClock.Models
{
    class Employee
    {
       private static int nextIndex = 0;
       private static readonly Dictionary<DateTime, int> EventCountByDate = [];

       public int Index { get; set; } 
       public int EmpNo { get; set; }
       public int EmpID { get; set; }
       public string? FullName { get; set; }
       public string? EventTime { get; set; }
       public string? EventType { get; set; }

       public Employee(int EmpID, string? FullName, string? EventTime, string? EventType, bool assignIndex = true) {
          this.EmpID = EmpID;
          this.FullName = FullName;
          this.EventTime = EventTime;
          this.EventType = EventType;

          if (assignIndex)
          {
            Index = nextIndex;
            nextIndex++;
            UpdateEventCount();
          }
        }

        private void UpdateEventCount()
        {
            try {
                _ = DateTime.TryParse(EventTime, out DateTime EventDateTime);
                DateTime eventDate = EventDateTime.Date;

                if (!EventCountByDate.TryGetValue(eventDate, out int value))
                {
                    EventCountByDate[eventDate] = 1;
                } else {
                    EventCountByDate[eventDate] = ++value;
                }

                EmpNo = EventCountByDate[eventDate];
                //Debug.WriteLine(EventTime);

            } catch (Exception) {}
        }

        public static Employee? FromJson(string json)
        {
            PunchRecord? employee = JsonConvert.DeserializeObject<PunchRecord>(json);
            if (employee != null) {
                var EventTime = employee.EventTime != null ? DateTime.Parse(employee.EventTime).ToString("dd/MM/yyyy HH:mm"): DateTime.MinValue.ToString("dd/MM/yyyy HH:mm");

                return new Employee(employee.IdEmployee, employee.EmployeeFullName, EventTime, employee.EventName);
            }

            return null;
        }

        public static void ResetAndReloadData()
        {
            nextIndex = 0;
            EventCountByDate.Clear();
        }
    }
}
