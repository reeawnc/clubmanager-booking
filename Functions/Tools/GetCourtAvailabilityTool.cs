using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using Courts;
using Microsoft.AspNetCore.Mvc;

namespace clubmanager_booking.Functions.Tools
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
                
                // Create a mock HTTP request to pass to the Courts function
                var mockRequest = CreateMockHttpRequest(date);
                
                // Create a mock logger
                var mockLogger = new MockLogger();
                
                // Call the Courts function directly
                var result = await Courts.Courts.Run(mockRequest, mockLogger);
                
                if (result is OkObjectResult okResult)
                {
                    return okResult.Value?.ToString() ?? "No court data available";
                }
                else if (result is BadRequestObjectResult badResult)
                {
                    return $"Error getting court availability: {badResult.Value}";
                }
                else
                {
                    return "Unexpected response from court availability service";
                }
            }
            catch (Exception ex)
            {
                return $"Error checking court availability: {ex.Message}";
            }
        }
        
        private HttpRequest CreateMockHttpRequest(string date)
        {
            var context = new DefaultHttpContext();
            var request = context.Request;
            
            // Set query parameters
            request.QueryString = new QueryString($"?date={Uri.EscapeDataString(date)}");
            
            // Set empty body
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            request.ContentType = "application/json";
            
            return request;
        }
    }
    
    // Simple mock logger for the Courts function
    public class MockLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // Do nothing - we don't need logging for tool calls
        }
    }
}