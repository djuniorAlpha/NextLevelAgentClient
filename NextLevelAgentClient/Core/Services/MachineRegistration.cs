namespace NextLevelAgentClient.Core.Services
{
    /// <summary>
    /// Dados de registro da máquina retornados pelo backend, incluindo o número
    /// atribuído a ela no painel de gestão.
    /// </summary>
    public sealed record MachineRegistration(string ComputerUuid, int MachineNumber);
}
