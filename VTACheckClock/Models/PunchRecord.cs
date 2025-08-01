namespace VTACheckClock.Models
{
    public class PunchRecord
    {
        public int IdEmployee { get; set; }
        public string? EmployeeFullName { get; set; }
        public int IdEvent { get; set; }
        public string? EventName { get; set; }
        public string? EventTime { get; set; }
        public string? InternalEventTime { get; set; }
    }
}
