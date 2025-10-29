using System;

namespace VTACheckClock.Models
{
    /// <summary>
    /// Payload para alertas administrativas en tiempo real.
    /// </summary>
    public class AdminErrorAlert
    {
        public string? Title { get; set; }
        public string? OfficeId { get; set; }
        public string? DeviceUUID { get; set; }
        public string? Severity { get; set; } // Error, Fatal
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string? Context { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}