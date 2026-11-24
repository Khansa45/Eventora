using Eventora_Project.Data;
using Eventora_Project.Models;
using Eventora_Project.Repositories.Interfaces;
using Eventora_Project.Repositories.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Eventora_Project.Repositories
{
    public class RegistrationRepository : IRegistrationRepository
    {
        private readonly ApplicationDbContext _context;

        public RegistrationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Registration>> GetByStudentWithEventAsync(string studentId)
        {
            return await _context.Registrations
                .Include(r => r.Event)
                    .ThenInclude(e => e!.Category)
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.RegisteredAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsForStudentAsync(int eventId, string studentId)
        {
            return await _context.Registrations
                .AnyAsync(r => r.EventId == eventId && r.StudentId == studentId);
        }

        public async Task<Registration?> GetByIdAndStudentAsync(int registrationId, string studentId)
        {
            return await _context.Registrations.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == registrationId && r.StudentId == studentId);
        }

        public async Task<RegistrationOutcome> TryRegisterWithSeatDecrementAsync(
            int eventId,
            string studentId,
            DateTime registeredAt)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var seatsTaken = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Events SET AvailableSeats = AvailableSeats - 1 WHERE Id = {eventId} AND Status = {"Approved"} AND AvailableSeats > 0");

                if (seatsTaken != 1)
                {
                    await transaction.RollbackAsync();
                    var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
                    if (ev == null)
                    {
                        return RegistrationOutcome.NotFound;
                    }

                    return ev.Status != "Approved"
                        ? RegistrationOutcome.NotApproved
                        : RegistrationOutcome.NoSeats;
                }

                _context.Registrations.Add(new Registration
                {
                    EventId = eventId,
                    StudentId = studentId,
                    RegisteredAt = registeredAt,
                    Status = "Confirmed"
                });

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueRegistrationViolation(ex))
                {
                    await transaction.RollbackAsync();
                    return RegistrationOutcome.Duplicate;
                }

                await transaction.CommitAsync();
                return RegistrationOutcome.Success;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CancellationOutcome> CancelOwnedWithSeatReturnAsync(
            int registrationId,
            string studentId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var registration = await _context.Registrations.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == registrationId && r.StudentId == studentId);

                if (registration == null)
                {
                    await transaction.RollbackAsync();
                    return CancellationOutcome.RegistrationNotFound;
                }

                var seatsReturned = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Events SET AvailableSeats = AvailableSeats + 1 WHERE Id = {registration.EventId} AND AvailableSeats < TotalCapacity");

                if (seatsReturned != 1)
                {
                    await transaction.RollbackAsync();
                    return CancellationOutcome.SeatRestoreFailed;
                }

                var deleted = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM Registrations WHERE Id = {registrationId} AND StudentId = {studentId}");

                if (deleted != 1)
                {
                    await transaction.RollbackAsync();
                    return CancellationOutcome.RegistrationNotFound;
                }

                await transaction.CommitAsync();
                return CancellationOutcome.Success;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static bool IsUniqueRegistrationViolation(DbUpdateException exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    (sqlException.Number == 2601 || sqlException.Number == 2627) &&
                    sqlException.Message.Contains("Registrations", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
