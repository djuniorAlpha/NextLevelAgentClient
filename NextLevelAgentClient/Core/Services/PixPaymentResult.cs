namespace NextLevelAgentClient.Core.Services
{
    /// <summary>
    /// Resposta do backend ao criar uma cobrança Pix (POST /machines/:uuid/payments/pix).
    /// </summary>
    public sealed record PixPaymentResult(string PaymentId, string? QrCodeBase64, string? QrCodeText, int AmountCents, string Status);

    /// <summary>
    /// Token de saldo restante gerado pelo backend quando um pagamento Pix é aprovado
    /// (sobra de tempo não consumido pode ser resgatada depois em qualquer máquina).
    /// </summary>
    public sealed record PixTokenInfo(string Code, System.DateTimeOffset ExpiresAt, int RemainingSeconds);

    /// <summary>
    /// Sessão que o backend abriu automaticamente na máquina pagante assim que o Pix foi aprovado.
    /// </summary>
    public sealed record PaymentSessionInfo(string Id);

    /// <summary>
    /// Recorte do Payment do backend usado só pra checar status (GET /payments/:id),
    /// como fallback caso o WebSocket falhe. <see cref="PixToken"/> e <see cref="Session"/> só vêm
    /// preenchidos depois que o pagamento é aprovado.
    /// </summary>
    public sealed record PixPaymentStatus(string Id, string Status, PixTokenInfo? PixToken, PaymentSessionInfo? Session);

    /// <summary>
    /// Resposta do backend ao resgatar um código Pix nesta máquina
    /// (POST /machines/:uuid/sessions/start-with-token).
    /// </summary>
    public sealed record RedeemTokenResult(string SessionId, int AllocatedSeconds, string Source, string TokenCode);
}
