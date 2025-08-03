using System.Collections.Generic;

namespace clubmanager_booking.Functions.Models
{
    public class PromptResponse
    {
        public string Response { get; set; } = string.Empty;
        
        public string? SessionId { get; set; }
        
        public bool Success { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public List<ToolCall>? ToolCalls { get; set; }
        
        public Dictionary<string, object>? Metadata { get; set; }
    }
    
    public class ToolCall
    {
        public string ToolName { get; set; } = string.Empty;
        
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        public string? Result { get; set; }
    }
} 