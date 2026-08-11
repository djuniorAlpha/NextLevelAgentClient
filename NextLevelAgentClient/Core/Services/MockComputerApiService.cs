using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelAgentClient.Core.Services
{
    public class MockComputerApiService : IComputerApiService
    {
        private readonly string _backendBaseUrl;

        public MockComputerApiService(string backendBaseUrl)
        {
            _backendBaseUrl = backendBaseUrl;
        }

        public async Task<MachineRegistration?> GetRegistrationAsync(string macAddress)
        {
            await Task.Delay(1500);

            // Sem backend real ainda: simula a chamada e que a máquina nunca foi registrada antes.
            Debug.WriteLine($"[MOCK] GET {_backendBaseUrl}/machines?mac={macAddress}");
            return null;
        }

        public async Task<MachineRegistration> RegisterComputerAsync(string macAddress, string hostname, string ipAddress)
        {
            await Task.Delay(2000);

            // Sem backend real ainda: simula a chamada e o número que seria atribuído pelo painel de gestão.
            Debug.WriteLine($"[MOCK] POST {_backendBaseUrl}/machines");
            int simulatedMachineNumber = Random.Shared.Next(1, 100);
            return new MachineRegistration(Guid.NewGuid().ToString(), simulatedMachineNumber);
        }

        public async Task<bool> SendHeartbeatAsync(string computerUuid, string currentStatus)
        {
            // Simulate network latency for the heartbeat ping
            await Task.Delay(200);

            // Console/Debug write to verify background execution without interrupting UI
            Debug.WriteLine($"[MOCK] POST {_backendBaseUrl}/machines/{computerUuid}/heartbeat. Status: {currentStatus} at {DateTime.Now}");

            return true;
        }
    }
}
