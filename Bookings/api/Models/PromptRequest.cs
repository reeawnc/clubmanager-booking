using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace BookingsApi.Models
{
    public class PromptRequest
    {
        [Required]
        public string Prompt { get; set; } = string.Empty;
        
        public string? UserId { get; set; }
        
        public string? SessionId { get; set; }
        
        public Dictionary<string, object>? Context { get; set; }
    }
}