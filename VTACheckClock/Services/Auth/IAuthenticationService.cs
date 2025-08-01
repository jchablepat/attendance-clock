using System.Threading.Tasks;

namespace VTACheckClock.Services.Auth
{
    public interface IAuthenticationService
    {
        Task<string> RegisterDeviceAsync(string deviceId);
        Task<string> LoginAsync(string deviceId, string apiKey);
        Task<string> RefreshTokenAsync(string deviceId, string apiKey);
        bool IsAuthenticated { get; }
        string? CurrentToken { get; }
    }
}
