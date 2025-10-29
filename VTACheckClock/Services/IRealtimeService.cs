using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using VTACheckClock.Models;

namespace VTACheckClock.Services
{
    public interface IRealtimeService
    {
        event EventHandler<string>? MessageReceived;
        event EventHandler<HubConnectionState>? ConnectionStateChanged;
        Task InitializeAsync();
        Task ReloadAsync();
        Task SendMessageAsync(PunchLine new_punch, FMDItem? emp = null);
        Task SendAdminAlertAsync(AdminErrorAlert alert);
        bool IsConnected { get; }
    }
}