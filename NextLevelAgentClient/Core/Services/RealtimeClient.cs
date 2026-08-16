using System.Text.Json.Serialization;
using SocketIOClient;

namespace NextLevelAgentClient.Core.Services
{
    /// <summary>
    /// Conexão WebSocket (Socket.IO) com o namespace /realtime do backend, autenticada
    /// pela apiKey desta máquina. Usada para receber payment.confirmed em tempo real,
    /// evitando depender só do polling de status.
    /// </summary>
    public sealed class RealtimeClient : IAsyncDisposable
    {
        private readonly SocketIO _socket;

        public event Action<string, string?, string?>? OnPaymentConfirmed;

        /// <summary>
        /// Disparado quando o admin manda uma ação remota pelo painel: "lock", "unlock" ou "shutdown".
        /// </summary>
        public event Action<string>? OnForceAction;

        public RealtimeClient(string backendBaseUrl, string apiKey)
        {
            string baseUrl = backendBaseUrl.EndsWith('/') ? backendBaseUrl[..^1] : backendBaseUrl;

            _socket = new SocketIO(new Uri($"{baseUrl}/realtime"), new SocketIOOptions
            {
                Auth = new { apiKey },
                Reconnection = true,
            });

            _socket.On("payment.confirmed", ctx =>
            {
                PaymentConfirmedPayload? payload = ctx.GetValue<PaymentConfirmedPayload>(0);
                if (payload != null)
                    OnPaymentConfirmed?.Invoke(payload.PaymentId, payload.TokenCode, payload.SessionId);
                return Task.CompletedTask;
            });

            _socket.On("machine.force-action", ctx =>
            {
                ForceActionPayload? payload = ctx.GetValue<ForceActionPayload>(0);
                if (payload != null)
                    OnForceAction?.Invoke(payload.Action);
                return Task.CompletedTask;
            });
        }

        public Task ConnectAsync() => _socket.ConnectAsync();

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _socket.DisconnectAsync();
            }
            catch
            {
                // Best-effort: a conexão pode já estar fechada ou nunca ter sido estabelecida.
            }
            _socket.Dispose();
        }

        private sealed record PaymentConfirmedPayload(
            [property: JsonPropertyName("paymentId")] string PaymentId,
            [property: JsonPropertyName("tokenCode")] string? TokenCode,
            [property: JsonPropertyName("sessionId")] string? SessionId);

        private sealed record ForceActionPayload([property: JsonPropertyName("action")] string Action);
    }
}
