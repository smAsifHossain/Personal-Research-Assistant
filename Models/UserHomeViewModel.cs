using System.Collections.Generic;

namespace PersonalResearchAssistantV5.Models
{
    public class UserHomeViewModel
    {
        public required ApplicationUser User { get; set; }
        public required List<UserPdf> UploadedPdfs { get; set; }
    }
}
