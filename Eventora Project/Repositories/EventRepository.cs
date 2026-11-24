using Dapper;
using Eventora_Project.Data;
using Eventora_Project.Models;
using Eventora_Project.Repositories.Interfaces;
using Eventora_Project.Repositories.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Eventora_Project.Repositories
{
    public class EventRepository : IEventRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public EventRepository(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<List<Event>> GetAllForAdminAsync()
        {
            return await _context.Events
                .Include(e => e.Category)
                .Include(e => e.SocietyHead)
                .Include(e => e.Registrations)
                .OrderByDescending(e => e.Id)
                .ToListAsync();
        }

        public async Task<List<Event>> GetForSocietyHeadAsync(string societyHeadId)
        {
            return await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Registrations)
                .Where(e => e.SocietyHeadId == societyHeadId)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<List<Event>> GetApprovedAsync(string? search, int categoryId)
        {
            IQueryable<Event> eventsQuery = _context.Events
                .Include(e => e.Category)
                .Where(e => e.Status == "Approved");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                eventsQuery = eventsQuery.Where(e =>
                    e.Title.ToLower().Contains(term) ||
                    e.City.ToLower().Contains(term));
            }

            if (categoryId > 0)
            {
                eventsQuery = eventsQuery.Where(e => e.CategoryId == categoryId);
            }

            return await eventsQuery
                .OrderBy(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<Event?> GetApprovedByIdAsync(int id)
        {
            return await _context.Events
                .AsNoTracking()
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.Id == id && e.Status == "Approved");
        }

        public async Task<Event?> GetOwnedByIdAsync(int id, string societyHeadId)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

            await using var connection = new SqlConnection(connectionString);

            const string sql = @"
SELECT [Id],
       [Title],
       [Description],
       [EventDate],
       [Venue],
       [City],
       [AvailableSeats],
       [TotalCapacity],
       [Status],
       [CategoryId],
       [SocietyHeadId]
FROM [Events]
WHERE [Id] = @Id AND [SocietyHeadId] = @SocietyHeadId;";

            return await connection.QueryFirstOrDefaultAsync<Event>(
                sql,
                new { Id = id, SocietyHeadId = societyHeadId });
        }

        public async Task AddAsync(Event entity)
        {
            _context.Events.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<EventStatusUpdateResult> UpdateStatusAsync(
            int id,
            string expectedStatus,
            string newStatus)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
            {
                return new EventStatusUpdateResult(EventStatusUpdateResultKind.NotFound, null);
            }

            if (ev.Status != expectedStatus)
            {
                return new EventStatusUpdateResult(EventStatusUpdateResultKind.Conflict, ev.Title);
            }

            ev.Status = newStatus;
            await _context.SaveChangesAsync();
            return new EventStatusUpdateResult(EventStatusUpdateResultKind.Updated, ev.Title);
        }

        public async Task<EventEditResult> UpdateOwnedEventAndRecalculateSeatsAsync(
            Event model,
            int newCapacity,
            string societyHeadId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var updated = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE e
SET Title = {model.Title ?? string.Empty},
    Description = {model.Description ?? string.Empty},
    EventDate = {model.EventDate},
    Venue = {model.Venue ?? string.Empty},
    City = {model.City ?? string.Empty},
    CategoryId = {model.CategoryId},
    Status = {"Pending"},
    TotalCapacity = {newCapacity},
    AvailableSeats = {newCapacity} - (
        SELECT COUNT(*) FROM Registrations r
        WHERE r.EventId = e.Id AND r.Status = {"Confirmed"})
FROM Events e
WHERE e.Id = {model.Id} AND e.SocietyHeadId = {societyHeadId}
  AND {newCapacity} >= (
        SELECT COUNT(*) FROM Registrations r
        WHERE r.EventId = e.Id AND r.Status = {"Confirmed"});");

                if (updated == 1)
                {
                    await transaction.CommitAsync();
                    return new EventEditResult(EventEditOutcome.Updated, 0);
                }

                var ownedExists = await _context.Events.AsNoTracking()
                    .AnyAsync(e => e.Id == model.Id && e.SocietyHeadId == societyHeadId);

                var confirmedRegistrations = await _context.Registrations
                    .CountAsync(r => r.EventId == model.Id && r.Status == "Confirmed");

                await transaction.RollbackAsync();

                if (!ownedExists)
                {
                    return new EventEditResult(EventEditOutcome.NotFoundOrNotOwned, confirmedRegistrations);
                }

                if (newCapacity < confirmedRegistrations)
                {
                    return new EventEditResult(EventEditOutcome.CapacityBelowRegistrations, confirmedRegistrations);
                }

                return new EventEditResult(EventEditOutcome.NotFoundOrNotOwned, confirmedRegistrations);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<EventDeleteResult> DeleteWithRegistrationsAsync(int id)
        {
            var ev = await _context.Events
                .Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null)
            {
                return new EventDeleteResult(false, null);
            }

            var title = ev.Title;
            _context.Registrations.RemoveRange(ev.Registrations);
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            return new EventDeleteResult(true, title);
        }

        public async Task<EventDeleteResult> DeleteOwnedWithRegistrationsAsync(int id, string societyHeadId)
        {
            var ev = await _context.Events
                .Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Id == id && e.SocietyHeadId == societyHeadId);

            if (ev == null)
            {
                return new EventDeleteResult(false, null);
            }

            var title = ev.Title;
            _context.Registrations.RemoveRange(ev.Registrations);
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            return new EventDeleteResult(true, title);
        }
    }
}
