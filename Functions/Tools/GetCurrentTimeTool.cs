using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace clubmanager_booking.Functions.Tools
{
    public class GetCurrentTimeTool : ITool
    {
        public string Name => "get_current_time";
        
        public string Description => "Get the current date and time";
        
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["timezone"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Timezone (optional, defaults to UTC)"
                }
            }
        };
        
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var timezone = parameters.TryGetValue("timezone", out var tz) ? tz?.ToString() : "UTC";
            
            var currentTime = timezone?.ToLower() == "utc" 
                ? DateTime.UtcNow 
                : DateTime.Now;
                
            return await Task.FromResult(currentTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
} 