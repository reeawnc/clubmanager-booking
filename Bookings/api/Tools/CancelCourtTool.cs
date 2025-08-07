using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Tools
{
    public class CancelCourtTool : ITool
    {
        public string Name => "cancel_court";
        public string Description => "Cancels a court booking given a bookingId (number)";
        public Dictionary<string, object> Parameters => new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["bookingId"] = new Dictionary<string, object>
                {
                    ["type"] = "number",
                    ["description"] = "Booking ID to cancel"
                }
            },
            ["required"] = new[] { "bookingId" }
        };

        private readonly CancellationService _service = new();

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("bookingId", out var idObj))
            {
                throw new ArgumentException("bookingId is required");
            }
            long bookingId = Convert.ToInt64(idObj);
            return await _service.CancelCourtAsync(bookingId);
        }
    }
}


