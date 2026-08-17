namespace NextLevelAgentClient.Core.Services
{
    // NOTA: POST /auth/customer/login e POST /machines/:uuid/sessions/start-for-customer
    // ainda NÃO existem no backend (só machines/payments/webhooks/time-packages/hourly-rates
    // estão implementados até agora). Estes DTOs seguem o contrato descrito em
    // next-level-gerenciador-planejamento.md (seção 6), mas os nomes de campo exatos
    // podem precisar de ajuste quando o backend implementar de fato.

    /// <summary>
    /// Resposta esperada de POST /auth/customer/login: token do cliente + seus dados/saldo.
    /// </summary>
    public sealed record CustomerLoginResult(string AccessToken, CustomerInfo Customer);

    public sealed record CustomerInfo(string Id, string Name, string Username, int BalanceMinutes, string? LoyaltyTier, bool MustChangePassword);

    /// <summary>
    /// Resposta esperada de POST /machines/:uuid/sessions/start-for-customer: o backend decide
    /// sozinho se consome assinatura ou saldo, e devolve quanto tempo foi efetivamente alocado.
    /// </summary>
    public sealed record StartSessionResult(string SessionId, int AllocatedSeconds, string Source);

    /// <summary>
    /// Resposta esperada de POST /auth/customer/change-password (senha temporária -> definitiva).
    /// AccessToken vem nulo se o backend simplesmente mantiver o token de login já emitido válido.
    /// </summary>
    public sealed record ChangePasswordResult(bool Ok, string? AccessToken);
}
