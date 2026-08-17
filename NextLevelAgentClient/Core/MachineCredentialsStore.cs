using Microsoft.Win32;

namespace NextLevelAgentClient.Core
{
    /// <summary>
    /// Credenciais desta estação emitidas pelo backend no registro (POST /machines).
    /// A ApiKey só é retornada nesse momento, então precisa ser guardada localmente
    /// para autenticar as chamadas seguintes (ex.: heartbeat).
    /// </summary>
    public sealed record MachineCredentials(string ComputerUuid, string ApiKey, int MachineNumber, string MacAddress);

    /// <summary>
    /// Persiste as credenciais da máquina no registro do Windows (HKCU), no mesmo
    /// estilo usado por <see cref="RegistryManager"/>.
    /// </summary>
    public static class MachineCredentialsStore
    {
        private const string REGISTRY_PATH = @"Software\NextLevelAgentClient\Machine";

        public static MachineCredentials? Load()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH);
                if (key == null) return null;

                string? computerUuid = key.GetValue("ComputerUuid") as string;
                string? apiKey = key.GetValue("ApiKey") as string;
                string? macAddress = key.GetValue("MacAddress") as string;
                object? machineNumberValue = key.GetValue("MachineNumber");

                if (string.IsNullOrEmpty(computerUuid) || string.IsNullOrEmpty(apiKey) ||
                    string.IsNullOrEmpty(macAddress) || machineNumberValue is not int machineNumber)
                {
                    return null;
                }

                return new MachineCredentials(computerUuid, apiKey, machineNumber, macAddress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao ler credenciais da máquina no registro: {ex.Message}");
                return null;
            }
        }

        public static void Save(MachineCredentials credentials)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH);
                key.SetValue("ComputerUuid", credentials.ComputerUuid, RegistryValueKind.String);
                key.SetValue("ApiKey", credentials.ApiKey, RegistryValueKind.String);
                key.SetValue("MacAddress", credentials.MacAddress, RegistryValueKind.String);
                key.SetValue("MachineNumber", credentials.MachineNumber, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar credenciais da máquina no registro: {ex.Message}");
            }
        }
    }
}
