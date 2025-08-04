using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Newtonsoft.Json;
using BookingsApi.Services;

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
                
                // Use the shared court availability service
                var courtAvailabilityService = new CourtAvailabilityService();
                var courtsData = await courtAvailabilityService.GetCourtAvailabilityAsync(date);
                
                // Format the data for the AI to understand
                var formattedData = new
                { 
                    Date = date,
                    Courts = courtsData.Courts.Select(court => new
                    {
                        Name = court.ColumnHeading,
                        CourtNumber = court.ColumnHeading.Replace("Court ", ""),
                        Cells = court.Cells.Select(cell => new
                        {
                            TimeSlot = cell.TimeSlot,
                            Status = cell.CssClass,
                            Player = cell.ToolTip,
                            IsBooked = !string.IsNullOrEmpty(cell.ToolTip) && cell.ToolTip != "Available",
                            Court = cell.Court
                        }).ToList()
                    }).ToList()
                };
                 
                return JsonConvert.SerializeObject(formattedData);
            }
            catch (Exception ex)
            {
                return $"Error checking court availability: {ex.Message}";
            }
        }
    }
}