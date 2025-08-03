using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace BookingsApi.Tools
{
    public class GetCourtAvailabilityTool : ITool
    {
        public string Name => "get_court_availability";
        
        public string Description => "Get tennis/sports court availability and booking information for a specific date. Use this when users ask about courts, court bookings, court availability, or what courts are free.";
        
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["date"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Date to check court availability (format: 'dd MMM yy' like '01 Jan 25'). Optional - defaults to today if not provided."
                }
            }
        };
        
        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // Get date parameter, default to today if not provided
                var date = parameters.TryGetValue("date", out var dateValue) 
                    ? dateValue?.ToString() 
                    : DateTime.Now.ToString("dd MMM yy");
                
                // For now, return mock court data
                // TODO: Replace with actual court availability logic
                var mockData = new
                {
                    Courts = new[]
                    {
                        new {
                            Name = "Court 1",
                            Bookings = new[]
                            {
                                new { Time = "09:00-10:00", Player = "John Smith" },
                                new { Time = "14:00-15:00", Player = "Training" }
                            },
                            Available = new[]
                            {
                                "10:00-14:00",
                                "15:00-18:00"
                            }
                        },
                        new {
                            Name = "Court 2", 
                            Bookings = new[]
                            {
                                new { Time = "10:00-11:00", Player = "Sarah Johnson" },
                                new { Time = "16:00-17:00", Player = "Mike Wilson" }
                            },
                            Available = new[]
                            {
                                "09:00-10:00",
                                "11:00-16:00",
                                "17:00-18:00"
                            }
                        }
                    },
                    Date = date
                };
                
                return await Task.FromResult(System.Text.Json.JsonSerializer.Serialize(mockData));
            }
            catch (Exception ex)
            {
                return $"Error checking court availability: {ex.Message}";
            }
        }
    }
}