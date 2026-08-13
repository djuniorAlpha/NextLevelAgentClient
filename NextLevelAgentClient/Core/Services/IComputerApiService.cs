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

        /// <summary>
        /// Logs a customer in (username/password), returning a token to start a session with.
        /// NOTE: backend endpoint not implemented yet as of 2026-08-13 - see CustomerSession.cs.
        /// </summary>
        Task<CustomerLoginResult> LoginCustomerAsync(string username, string password);

        /// <summary>
        /// Starts a session for the logged-in customer; the backend decides whether it draws
        /// from their subscription or wallet balance.
        /// NOTE: backend endpoint not implemented yet as of 2026-08-13 - see CustomerSession.cs.
        /// </summary>
        Task<StartSessionResult> StartSessionForCustomerAsync(string computerUuid, string apiKey, string customerAccessToken);

        /// <summary>
        /// Sets a definitive password when the customer's current one is temporary
        /// (Customer.mustChangePassword == true).
        /// NOTE: backend endpoint not implemented yet as of 2026-08-13 - see CustomerSession.cs.
        /// </summary>
        Task<ChangePasswordResult> ChangeCustomerPasswordAsync(string customerAccessToken, string newPassword);
    }
}
