using System;
using System.Collections.Generic;

namespace VTACheckClock.Models
{
    public class AttendanceRecord
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public Dictionary<string, AttendanceTimeInfo> DailyStatus { get; set; } = [];
    }

    public class AttendanceTimeInfo
    {
        public bool IsNonWorkingDate { get; set; }
        public string BenefitAlias { get; set; } = string.Empty;
        public string? Event { get; set; }
        public TimeSpan EventTime { get; set; }
    }
}
