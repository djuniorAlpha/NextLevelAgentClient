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

        /// <summary>
        /// Lists the active closed time packages configured by the admin (e.g. "1 hora - R$5,00").
        /// </summary>
        Task<IReadOnlyList<TimePackageDto>> GetTimePackagesAsync();

        /// <summary>
        /// Lists the active running-hour rates configured by the admin (free-form time purchase).
        /// </summary>
        Task<IReadOnlyList<HourlyRateDto>> GetHourlyRatesAsync();

        /// <summary>
        /// Creates a real Pix charge for either a closed time package or a running-hour rate.
        /// Exactly one of <paramref name="timePackageId"/>/<paramref name="hourlyRateId"/> must be set.
        /// </summary>
        Task<PixPaymentResult> CreatePixPaymentAsync(string computerUuid, string apiKey, string? timePackageId, string? hourlyRateId, int minutes);

        /// <summary>
        /// Fallback poll for a payment's current status, in case the WebSocket notification is missed.
        /// </summary>
        Task<PixPaymentStatus> GetPaymentStatusAsync(string apiKey, string paymentId);
    }
}
