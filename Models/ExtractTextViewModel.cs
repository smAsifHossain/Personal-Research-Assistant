using System.Collections.Generic;

namespace PersonalResearchAssistantV5.Models
{
    public class ExtractTextViewModel
    {
        public required string FileName { get; set; }
        public required string ExtractedText { get; set; }
        public required Dictionary<string, int> KeywordFrequency { get; set; }

        // Metadata Fields
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? PublicationDate { get; set; }
    }
}
