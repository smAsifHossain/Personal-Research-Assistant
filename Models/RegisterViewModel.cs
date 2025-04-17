using System.ComponentModel.DataAnnotations;

namespace PersonalResearchAssistantV5.Models
{
    public class RegisterViewModel
    {
        [Required]
        public required string Name { get; set; }

        [Required]
        [Phone]
        [Display(Name = "Contact Number")]
        public required string ContactNumber { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public required string Password { get; set; }

        [Required]
        [Display(Name = "User Type")]
        public required string UserType { get; set; } // "Admin" or "User"
    }
}
