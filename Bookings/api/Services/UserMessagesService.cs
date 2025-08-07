using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Services
{
    public class UserMessagesService
    {
        private readonly ClubManagerLoginService _loginService;

        public UserMessagesService()
        {
            _loginService = new ClubManagerLoginService();
        }

        public async Task<string> GetUserHasMessagesAsync()
        {
            using var client = await _loginService.GetAuthenticatedClientAsync();
            var param = new Dictionary<string, string>
            {
                { "siteCallback", "MemberCallback" },
                { "action", "GetUserHasMessages" },
                { string.Empty, "{}" }
            };

            var url = UrlQueryHelper.BuildUrl("https://clubmanager365.com/Club/ActionHandler.ashx", param);
            var response = await client.GetAsync(new Uri(url));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetUserMessagesAsync(bool markAsRead = false, bool showExpired = false, bool showRead = true)
        {
            using var client = await _loginService.GetAuthenticatedClientAsync();
            var baseUrl = "https://clubmanager365.com/ActionHandler.ashx";

            var parameters = new Dictionary<string, string>
            {
                { "siteCallback", "MemberCallback" },
                { "action", "GetUserMessages" },
                { string.Empty, "{}" }
            };

            var url = UrlQueryHelper.BuildUrl(baseUrl, parameters);
            // append the JSON query segment
            url += $"&{{%22MarkAsRead%22:{markAsRead.ToString().ToLower()},%22ShowExpired%22:{showExpired.ToString().ToLower()},%22ShowRead%22:{showRead.ToString().ToLower()}}}";

            var response = await client.GetAsync(new Uri(url));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetSentUserMessagesAsync()
        {
            using var client = await _loginService.GetAuthenticatedClientAsync();
            var baseUrl = "https://clubmanager365.com/ActionHandler.ashx";
            var parameters = new Dictionary<string, string>
            {
                { "siteCallback", "MemberCallback" },
                { "action", "GetSentUserMessages" },
                { string.Empty, "{}" }
            };

            var url = UrlQueryHelper.BuildUrl(baseUrl, parameters);
            var response = await client.GetAsync(new Uri(url));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}


