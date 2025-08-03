using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ClubManager.Helpers;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;
using System.Linq;
using clubmanager_booking.Models;

namespace ClubManager
{
    public static class GetMyBookings
    {
        [FunctionName("GetMyBookings")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            CookieContainer cookies = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = cookies,
                //AllowAutoRedirect = false
            };
            using (var client = new HttpClient(handler))
            {
                try
                {
                    //client.DefaultRequestHeaders.Accept.Clear();
                    //client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    Tuple<HttpRequestMessage, HttpResponseMessage> res = await new LoginHelper4().GetLoggedInRequestAsync(client);
                    Console.WriteLine(res.Item2);


                    var param = new Dictionary<string, string>() {
                    { "siteCallback", "CourtCallback" },
                    {"action", "GetPlayerBookings" },
                    {"", "{}"} };

                    var uri = new Uri(QueryHelpers.AddQueryString("https://clubmanager365.com/Club/ActionHandler.ashx", param));                    

                    var bookingsResponse = await client.GetAsync(uri);
                    var contents = await bookingsResponse.Content.ReadAsStringAsync();
                    return new OkObjectResult(contents);
                }
                catch (Exception ex)
                {
                    return new BadRequestObjectResult($"exception: {ex.Message}");
                }
            }
        }
        
    }
}
