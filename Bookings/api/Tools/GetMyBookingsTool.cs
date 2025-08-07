using System.Collections.Generic;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Tools
{
    public class GetMyBookingsTool : ITool
    {
        public string Name => "get_my_bookings";
        public string Description => "Fetches the current user's bookings from ClubManager";
        public Dictionary<string, object> Parameters => new() { };

        private readonly MyBookingsService _service = new();

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            return await _service.GetMyBookingsRawAsync();
        }
    }
}


