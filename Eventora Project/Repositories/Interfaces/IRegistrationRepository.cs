using Eventora_Project.Models;
using Eventora_Project.Repositories.Results;

namespace Eventora_Project.Repositories.Interfaces
{
    public interface IRegistrationRepository
    {
        Task<List<Registration>> GetByStudentWithEventAsync(string studentId);

        Task<bool> ExistsForStudentAsync(int eventId, string studentId);

        Task<Registration?> GetByIdAndStudentAsync(int registrationId, string studentId);

        Task<RegistrationOutcome> TryRegisterWithSeatDecrementAsync(
            int eventId,
            string studentId,
            DateTime registeredAt);

        Task<CancellationOutcome> CancelOwnedWithSeatReturnAsync(
            int registrationId,
            string studentId);
    }
}
