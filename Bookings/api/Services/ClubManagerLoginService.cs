using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BookingsApi.Helpers;

namespace BookingsApi.Services
{
    public class ClubManagerLoginService
    {
        public async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var cookies = new CookieContainer();
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = cookies
            };

            var client = new HttpClient(handler);

            // Establish session using existing helper; client retains cookies
            _ = await new LoginHelper4().GetLoggedInRequestAsync(client);

            return client;
        }
    }
}


