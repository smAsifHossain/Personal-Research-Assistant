using System.Collections.Generic;

namespace PersonalResearchAssistantV5.Models
{
    public class AdminHomeViewModel
    {
        public required List<ApplicationUser> PendingUsers { get; set; }
        public required List<ApplicationUser> ApprovedUsers { get; set; }
        public required List<UserPdf> UploadedPdfs { get; set; }
    }
}
