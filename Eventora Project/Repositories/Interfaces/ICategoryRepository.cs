using Eventora_Project.Models;

namespace Eventora_Project.Repositories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllAsync();

        Task<List<Category>> GetOrderedByNameAsync();

        Task<bool> ExistsAsync(int id);
    }
}
