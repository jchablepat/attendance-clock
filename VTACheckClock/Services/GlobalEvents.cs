using Microsoft.AspNetCore.SignalR.Client;
using System;

namespace VTACheckClock.Services
{
    public class GlobalEvents
    {
        public static event Action<HubConnectionState, object>? HubStatusChanged;
        public static void NotifyStatusChange(HubConnectionState connection, object status)
        {
            HubStatusChanged?.Invoke(connection, status);
        }
    }
}
