using Microsoft.AspNetCore.Identity;

namespace PersonalResearchAssistantV5.Models
{
    public class ApplicationUser : IdentityUser
    {
        public required string Name { get; set; }
        public required string ContactNumber { get; set; }
        public required string UserType { get; set; } // "Admin" or "User"
    }
}
