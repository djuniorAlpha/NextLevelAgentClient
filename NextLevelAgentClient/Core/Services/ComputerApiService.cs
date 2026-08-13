using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NextLevelAgentClient.Core.Services
{
    /// <summary>
    /// Implementação real de <see cref="IComputerApiService"/>, que fala com o backend
    /// NestJS (NextLevelManager) descrito em next-level-gerenciador-planejamento.md.
    /// </summary>
    public sealed class ComputerApiService : IComputerApiService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public ComputerApiService(string backendBaseUrl)
        {
            string baseUrl = backendBaseUrl.EndsWith('/') ? backendBaseUrl : backendBaseUrl + "/";
            _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        public async Task<MachineRegistration?> GetRegistrationAsync(string macAddress)
        {
            using HttpResponseMessage response = await _http.GetAsync($"machines/registration?mac={Uri.EscapeDataString(macAddress)}");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MachineRegistration>(JsonOptions);
        }

        public async Task<MachineRegistration> RegisterComputerAsync(string macAddress, string hostname, string ipAddress)
        {
            var payload = new { macAddress, hostname, ipAddress };
            using HttpResponseMessage response = await _http.PostAsJsonAsync("machines", payload, JsonOptions);

            response.EnsureSuccessStatusCode();
            MachineRegistration? registration = await response.Content.ReadFromJsonAsync<MachineRegistration>(JsonOptions);
            return registration ?? throw new InvalidOperationException("Resposta vazia do backend ao registrar a máquina.");
        }

        public async Task<bool> SendHeartbeatAsync(string computerUuid, string apiKey, string currentStatus)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"machines/{computerUuid}/heartbeat")
                {
                    Content = JsonContent.Create(new { currentStatus }, options: JsonOptions)
                };
                request.Headers.Add("X-Api-Key", apiKey);

                using HttpResponseMessage response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Heartbeat falhou: {ex.Message}");
                return false;
            }
        }

        public async Task<IReadOnlyList<TimePackageDto>> GetTimePackagesAsync()
        {
            using HttpResponseMessage response = await _http.GetAsync("time-packages");
            response.EnsureSuccessStatusCode();
            List<TimePackageDto>? packages = await response.Content.ReadFromJsonAsync<List<TimePackageDto>>(JsonOptions);
            return packages ?? [];
        }

        public async Task<IReadOnlyList<HourlyRateDto>> GetHourlyRatesAsync()
        {
            using HttpResponseMessage response = await _http.GetAsync("hourly-rates");
            response.EnsureSuccessStatusCode();
            List<HourlyRateDto>? rates = await response.Content.ReadFromJsonAsync<List<HourlyRateDto>>(JsonOptions);
            return rates ?? [];
        }
    }
}
