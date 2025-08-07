using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BookingsApi.Services;
using BookingsApi.Helpers;
using Newtonsoft.Json;

namespace BookingsApi.Services
{
    public class MyBookingsService
    {
        private readonly ClubManagerLoginService _loginService;

        public MyBookingsService()
        {
            _loginService = new ClubManagerLoginService();
        }

        public async Task<string> GetMyBookingsRawAsync()
        {
            var client = await _loginService.GetAuthenticatedClientAsync();

            // Ensure we have a fresh authenticated session for this call
            await new LoginHelper4().GetLoggedInRequestAsync(client);

            var param = new Dictionary<string, string>()
            {
                { "siteCallback", "CourtCallback" },
                { "action", "GetPlayerBookings" },
                { string.Empty, "{}" }
            };

            var url = UrlQueryHelper.BuildUrl("https://clubmanager365.com/Club/ActionHandler.ashx", param);
            var response = await client.GetAsync(new Uri(url));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}


