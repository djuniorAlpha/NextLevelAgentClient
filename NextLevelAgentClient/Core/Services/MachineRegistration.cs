namespace NextLevelAgentClient.Core.Services
{
    /// <summary>
    /// Dados de registro da máquina retornados pelo backend, incluindo o número
    /// atribuído a ela no painel de gestão. ApiKey só vem preenchida na resposta
    /// do registro inicial (POST /machines) - o backend não a devolve depois.
    /// </summary>
    public sealed record MachineRegistration(string ComputerUuid, int MachineNumber, string? ApiKey = null);
}
