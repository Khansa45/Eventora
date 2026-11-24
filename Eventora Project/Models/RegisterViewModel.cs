using System.ComponentModel.DataAnnotations;

namespace Eventora_Project.Models
{
    public class RegisterViewModel
    {
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string _studentId = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName
        {
            get => _fullName;
            set => _fullName = value?.Trim() ?? string.Empty;
        }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        [Display(Name = "Email Address")]
        public string Email
        {
            get => _email;
            set => _email = value?.Trim() ?? string.Empty;
        }

        [Required]
        [Display(Name = "Student ID")]
        public string StudentId
        {
            get => _studentId;
            set => _studentId = value?.Trim() ?? string.Empty;
        }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
