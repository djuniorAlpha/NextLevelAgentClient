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
        /// Validates if the computer is already registered in the backend cloud database.
        /// </summary>
        Task<bool> IsComputerRegisteredAsync(string macAddress);

        /// <summary>
        /// Registers this hardware instance in the backend pool.
        /// </summary>
        Task<string> RegisterComputerAsync(string macAddress, string hostname, string ipAddress);

        /// <summary>
        /// Sends a periodic status signal to the backend to maintain the machine connection live.
        /// </summary>
        Task<bool> SendHeartbeatAsync(string computerUuid, string currentStatus);
    }
}
