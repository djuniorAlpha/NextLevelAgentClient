using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelAgentClient.Core.Services
{
    public interface IComputerApiService
    {
        /// <summary>
        /// Retrieves this computer's existing registration (including its assigned machine number),
        /// or null if it isn't registered in the backend yet.
        /// </summary>
        Task<MachineRegistration?> GetRegistrationAsync(string macAddress);

        /// <summary>
        /// Registers this hardware instance in the backend pool, which assigns it a machine number.
        /// </summary>
        Task<MachineRegistration> RegisterComputerAsync(string macAddress, string hostname, string ipAddress);

        /// <summary>
        /// Sends a periodic status signal to the backend to maintain the machine connection live.
        /// Requires the API key issued to this machine at registration time.
        /// </summary>
        Task<bool> SendHeartbeatAsync(string computerUuid, string apiKey, string currentStatus);
    }
}
