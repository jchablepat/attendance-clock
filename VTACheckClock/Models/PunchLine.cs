using System;

namespace VTACheckClock.Models
{
    public class PunchLine
    {
        public int Punchemp { get; set; }
        public int Punchevent { get; set; }
        public DateTime Punchtime { get; set; }
        public DateTime Punchinternaltime { get; set; }
    }
}
