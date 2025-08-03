using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

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
                
                // Call the actual Courts function to get real data
                using (var httpClient = new HttpClient())
                {
                    // Use the Static Web Apps URL since both functions are in the same environment
                    var functionUrl = "https://lemon-cliff-0ffa36b03.1.azurestaticapps.net/api/Courts";
                    
                    // Add date as query parameter
                    var url = $"{functionUrl}?date={Uri.EscapeDataString(date)}";
                    
                    // Add function key if available
                    var functionKey = Environment.GetEnvironmentVariable("COURTS_FUNCTION_KEY");
                    if (!string.IsNullOrEmpty(functionKey))
                    {
                        httpClient.DefaultRequestHeaders.Add("x-functions-key", functionKey);
                    }
                    
                    var response = await httpClient.GetAsync(url);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        return $"Error calling Courts function: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";
                    } 
                    
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    
                    // Parse the response from the Courts function
                    var courtsData = JsonConvert.DeserializeObject<BookingsApi.Models.RootObject>(jsonResponse);
                    
                    // Format the data for the AI to understand
                    var formattedData = new
                    {
                        Date = date,
                        Courts = courtsData.Courts.Select(court => new
                        {
                            Name = court.ColumnHeading,
                            Cells = court.Cells.Select(cell => new
                            {
                                TimeSlot = cell.TimeSlot,
                                Status = cell.CssClass,
                                Player = cell.ToolTip,
                                IsBooked = !string.IsNullOrEmpty(cell.ToolTip) && cell.ToolTip != "Available"
                            }).ToList()
                        }).ToList()
                    };
                     
                    return JsonConvert.SerializeObject(formattedData);
                }
            }
            catch (Exception ex)
            {
                return $"Error checking court availability: {ex.Message}";
            }
        }
    }
}