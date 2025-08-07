using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BookingsApi.Services;

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
            using var client = await _loginService.GetAuthenticatedClientAsync();

            // Ensure a session is established by loading the calendar page first (hidden fields are not required here but replicate original flow)
            var _ = await client.GetAsync(new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash"));

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


