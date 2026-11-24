using Eventora_Project.Models;
using Eventora_Project.Repositories.Results;

namespace Eventora_Project.Repositories.Interfaces
{
    public interface IEventRepository
    {
        Task<List<Event>> GetAllForAdminAsync();

        Task<List<Event>> GetForSocietyHeadAsync(string societyHeadId);

        Task<List<Event>> GetApprovedAsync(string? search, int categoryId);

        Task<Event?> GetApprovedByIdAsync(int id);

        Task<Event?> GetOwnedByIdAsync(int id, string societyHeadId);

        Task AddAsync(Event entity);

        Task<EventStatusUpdateResult> UpdateStatusAsync(
            int id,
            string expectedStatus,
            string newStatus);

        Task<EventEditResult> UpdateOwnedEventAndRecalculateSeatsAsync(
            Event model,
            int newCapacity,
            string societyHeadId);

        Task<EventDeleteResult> DeleteWithRegistrationsAsync(int id);

        Task<EventDeleteResult> DeleteOwnedWithRegistrationsAsync(int id, string societyHeadId);
    }
}
