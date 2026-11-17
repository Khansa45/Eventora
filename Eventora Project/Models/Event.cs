using System.ComponentModel.DataAnnotations;

namespace Eventora_Project.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        public string Venue { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        [Required, Range(1, 5000)]
        public int AvailableSeats { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public string SocietyHeadId { get; set; } = string.Empty;
        public ApplicationUser? SocietyHead { get; set; }

        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    }
}