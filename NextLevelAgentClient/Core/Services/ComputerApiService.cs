using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
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

        public async Task<PixPaymentResult> CreatePixPaymentAsync(string computerUuid, string apiKey, string? timePackageId, string? hourlyRateId, int minutes)
        {
            object payload;
            if (timePackageId != null)
                payload = new { timePackageId };
            else
                payload = new { hourlyRateId, minutes };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"machines/{computerUuid}/payments/pix")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Add("X-Api-Key", apiKey);

            using HttpResponseMessage response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            PixPaymentResult? result = await response.Content.ReadFromJsonAsync<PixPaymentResult>(JsonOptions);
            return result ?? throw new InvalidOperationException("Resposta vazia do backend ao criar cobrança Pix.");
        }

        public async Task<PixPaymentStatus> GetPaymentStatusAsync(string apiKey, string paymentId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"payments/{paymentId}");
            request.Headers.Add("X-Api-Key", apiKey);

            using HttpResponseMessage response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            PixPaymentStatus? status = await response.Content.ReadFromJsonAsync<PixPaymentStatus>(JsonOptions);
            return status ?? throw new InvalidOperationException("Resposta vazia do backend ao consultar pagamento.");
        }

        public async Task<RedeemTokenResult> RedeemPixTokenAsync(string computerUuid, string apiKey, string code)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"machines/{computerUuid}/sessions/start-with-token")
            {
                Content = JsonContent.Create(new { code }, options: JsonOptions)
            };
            request.Headers.Add("X-Api-Key", apiKey);

            using HttpResponseMessage response = await _http.SendAsync(request);
            await EnsureSuccessOrThrowFriendlyAsync(response);
            RedeemTokenResult? result = await response.Content.ReadFromJsonAsync<RedeemTokenResult>(JsonOptions);
            return result ?? throw new InvalidOperationException("Resposta vazia do backend ao resgatar código.");
        }

        public async Task<CustomerLoginResult> LoginCustomerAsync(string username, string password)
        {
            var payload = new { username, password };
            using HttpResponseMessage response = await _http.PostAsJsonAsync("auth/customer/login", payload, JsonOptions);
            await EnsureSuccessOrThrowFriendlyAsync(response);
            CustomerLoginResult? result = await response.Content.ReadFromJsonAsync<CustomerLoginResult>(JsonOptions);
            return result ?? throw new InvalidOperationException("Resposta vazia do backend ao fazer login.");
        }

        public async Task<StartSessionResult> StartSessionForCustomerAsync(string computerUuid, string apiKey, string customerAccessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"machines/{computerUuid}/sessions/start-for-customer");
            request.Headers.Add("X-Api-Key", apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerAccessToken);

            using HttpResponseMessage response = await _http.SendAsync(request);
            await EnsureSuccessOrThrowFriendlyAsync(response);
            StartSessionResult? result = await response.Content.ReadFromJsonAsync<StartSessionResult>(JsonOptions);
            return result ?? throw new InvalidOperationException("Resposta vazia do backend ao iniciar sessão.");
        }

        public async Task<ChangePasswordResult> ChangeCustomerPasswordAsync(string customerAccessToken, string newPassword)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/customer/change-password")
            {
                Content = JsonContent.Create(new { newPassword }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerAccessToken);

            using HttpResponseMessage response = await _http.SendAsync(request);
            await EnsureSuccessOrThrowFriendlyAsync(response);
            ChangePasswordResult? result = await response.Content.ReadFromJsonAsync<ChangePasswordResult>(JsonOptions);
            return result ?? new ChangePasswordResult(true, null);
        }

        public async Task<bool> EndSessionAsync(string computerUuid, string apiKey, string sessionId, int consumedSeconds)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"machines/{computerUuid}/sessions/{sessionId}/end")
                {
                    Content = JsonContent.Create(new { consumedSeconds }, options: JsonOptions)
                };
                request.Headers.Add("X-Api-Key", apiKey);

                using HttpResponseMessage response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Falha ao reportar fim de sessão: {ex.Message}");
                return false;
            }
        }

        // O NestJS devolve { message, error, statusCode } em erros - lê isso em vez de deixar
        // o EnsureSuccessStatusCode() jogar o texto genérico do .NET ("Response status code...").
        private static async Task EnsureSuccessOrThrowFriendlyAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            string? message = null;
            try
            {
                NestErrorBody? body = await response.Content.ReadFromJsonAsync<NestErrorBody>(JsonOptions);
                message = body?.Message;
            }
            catch
            {
                // Corpo não veio no formato esperado; usa a mensagem genérica abaixo.
            }

            throw new InvalidOperationException(message ?? $"Erro inesperado do servidor ({(int)response.StatusCode}).");
        }

        private sealed record NestErrorBody(string? Message);
    }
}
