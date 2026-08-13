namespace NextLevelAgentClient.Core.Services
{
    /// <summary>
    /// Resposta do backend ao criar uma cobrança Pix (POST /machines/:uuid/payments/pix).
    /// </summary>
    public sealed record PixPaymentResult(string PaymentId, string? QrCodeBase64, string? QrCodeText, int AmountCents, string Status);

    /// <summary>
    /// Recorte do Payment do backend usado só pra checar status (GET /payments/:id),
    /// como fallback caso o WebSocket falhe.
    /// </summary>
    public sealed record PixPaymentStatus(string Id, string Status);
}
