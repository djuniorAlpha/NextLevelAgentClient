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
        public async Task<bool> IsComputerRegisteredAsync(string macAddress)
        {
            await Task.Delay(1500);

            return false;
        }

        public async Task<string> RegisterComputerAsync(string macAddress, string hostname, string ipAddress)
        {
            await Task.Delay(2000);

            return Guid.NewGuid().ToString();
        }

        public async Task<bool> SendHeartbeatAsync(string computerUuid, string currentStatus)
        {
            // Simulate network latency for the heartbeat ping
            await Task.Delay(200);

            // Console/Debug write to verify background execution without interrupting UI
            Debug.WriteLine($"[HEARTBEAT] Signal sent safely for Machine {computerUuid}. Status: {currentStatus} at {DateTime.Now}");

            return true;
        }
    }
}
