using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalResearchAssistantV5.Models
{
    public class UserPdf
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string FileName { get; set; }

        [Required]
        public required string FilePath { get; set; }

        [Required]
        public required string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
    }
}
