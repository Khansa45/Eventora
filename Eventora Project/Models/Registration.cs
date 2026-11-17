namespace Eventora_Project.Models
{
    public class Registration
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public Event? Event { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public DateTime RegisteredAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Confirmed";
    }
}