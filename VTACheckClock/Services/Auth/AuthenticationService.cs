using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.Services.Auth
{
    public class AuthenticationService(HttpClient httpClient) : IAuthenticationService
    {
        private readonly MainSettings _mainSettings = RegAccess.GetMainSettings() ?? new MainSettings();
        private string? _currentToken;

        public bool IsAuthenticated => !string.IsNullOrEmpty(_currentToken);
        public string? CurrentToken => _currentToken;
        private string API_URL => $"{_mainSettings.SignalRHubUrl}";

        public async Task<string> RegisterDeviceAsync(string deviceId)
        {
            // Generar un API_KEY único y seguro
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string apiKey = GenerateSecureApiKey(deviceId, timestamp);

            var response = await httpClient.PostAsync($"{API_URL}/api/auth/register", JsonContent.Create(new { deviceId, apiKey, timestamp }));

            if (!response.IsSuccessStatusCode)
                throw new AuthenticationException("Failed to register device");

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            _currentToken = result?.Token;

            // Guardar el API_KEY en la configuración
            RegAccess.SetRegValue("signalRApiKey", apiKey);

            return _currentToken ?? throw new AuthenticationException("Invalid token received");
        }

        public async Task<string> LoginAsync(string deviceId, string apiKey)
        {
            var response = await httpClient.PostAsync($"{API_URL}/api/auth/login?deviceId={deviceId}&apiKey={apiKey}", null);

            if (!response.IsSuccessStatusCode)
                throw new AuthenticationException("Failed to authenticate device");

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            _currentToken = result?.Token;

            return _currentToken ?? throw new AuthenticationException("Invalid token received");
        }

        public async Task<string> RefreshTokenAsync(string deviceId, string apiKey)
        {
            var response = await httpClient.PostAsync($"{API_URL}/api/auth/refresh?deviceId={deviceId}&apiKey={apiKey}", null);
            if (!response.IsSuccessStatusCode)
                throw new AuthenticationException("Failed to refresh token");

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            _currentToken = result?.Token;

            return _currentToken ?? throw new AuthenticationException("Invalid token received");
        }

        /// <summary>
        /// Genera un API_KEY seguro usando una combinación de:
        /// <para>1) UUID del dispositivo</para>
        /// <para>2) Timestamp</para>
        /// <para>3) Una clave secreta del servidor</para>
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        private static string GenerateSecureApiKey(string deviceId, string timestamp)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes(GlobalVars.SERVER_SECRET_KEY)
            );

            // Combinar deviceId y timestamp
            var dataToHash = $"{deviceId}:{timestamp}";
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));

            // Convertir a Base64URL (Base64 seguro para URLs)
            return Convert.ToBase64String(hashBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        private record TokenResponse(string Token);
    }
}
