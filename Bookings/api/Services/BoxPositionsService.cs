using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BookingsApi.Services;
using BookingsApi.Helpers;

namespace BookingsApi.Services
{
    public class BoxPositionsService
    {
        private readonly ClubManagerLoginService _loginService;
        private const string BaseAddress = "https://clubmanager365.com/ActionHandler.ashx";

        public BoxPositionsService()
        {
            _loginService = new ClubManagerLoginService();
        }

        public async Task<string> GetBoxPositionsAsync(string groupId)
        {
            var client = await _loginService.GetAuthenticatedClientAsync();
            // Ensure fresh authenticated session (retains cookies across calls)
            await new LoginHelper4().GetLoggedInRequestAsync(client);

            var param = new Dictionary<string, string>
            {
                { "siteCallback", "BoxLeagueCallback" },
                { "action", "GetBoxLeaguePositions" },
                { string.Empty, $"{{'GroupID':'{groupId}'}}" }
            };

            var url = UrlQueryHelper.BuildUrl(BaseAddress, param);
            var response = await client.GetAsync(new Uri(url));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}


