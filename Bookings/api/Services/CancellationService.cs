using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BookingsApi.Services;
using BookingsApi.Helpers;

namespace BookingsApi.Services
{
    public class CancellationService
    {
        private readonly ClubManagerLoginService _loginService;

        public CancellationService()
        {
            _loginService = new ClubManagerLoginService();
        }

        public async Task<string> CancelCourtAsync(long bookingId)
        {
            var client = await _loginService.GetAuthenticatedClientAsync();
            // Ensure fresh authenticated session before performing cancellation
            await new LoginHelper4().GetLoggedInRequestAsync(client);
            var parameters = new Dictionary<string, string>
            {
                { "siteCallback", "CourtCallback" },
                { "action", "CancelBooking" }
            };

            var baseUrl = "https://clubmanager365.com/Club/ActionHandler.ashx";
            var url = UrlQueryHelper.BuildUrl(baseUrl, parameters);
            url = url + $"&{{\"BookingID\":{bookingId}}}";
            var uri = new Uri(url);

            var response = await client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}


