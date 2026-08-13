namespace NextLevelAgentClient.Core.Services
{
    /// <summary>
    /// Pacote de tempo fechado (ex: "1 hora - R$5,00"), configurado pelo admin no painel.
    /// </summary>
    public sealed record TimePackageDto(string Id, string Label, int Minutes, int PriceCents, bool Active);

    /// <summary>
    /// Tarifa por hora corrida (ex: "Tarifa padrão PC - R$8,00/h"), configurada pelo admin no painel.
    /// </summary>
    public sealed record HourlyRateDto(string Id, string Label, int RatePerHourCents, bool Active);
}
