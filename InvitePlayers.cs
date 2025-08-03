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
    public static class InvitePlayers
    {
        [FunctionName("InvitePlayers")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            //var matchDate = data.matchDate.ToString("d MMM yyyy");
            //var selectedMatchType = data.matchDate.ToString();
            //var courtID = data.courtID.ToString();
            //var courtSlotID = data.courtSlotID.ToString();
            var bookingID = data.bookingID.ToString();
            

            CookieContainer cookies = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = cookies,
            };
            using (var client = new HttpClient(handler))
            {
                try
                {
                    Tuple<HttpRequestMessage, HttpResponseMessage> res = await new LoginHelper4().GetLoggedInRequestAsync(client);
                    Console.WriteLine(res.Item2);

                    var param = new Dictionary<string, string>() {
                    { "siteCallback", "CourtCallback" },
                    {"action", "CancelBooking" } };
                    var url = QueryHelpers.AddQueryString("https://clubmanager365.com/Club/ActionHandler.ashx", param);
                    url = url + "&{\"BookingID\":" + bookingID + "}";
                    var uri = new Uri(url);
                    /*
                     * 
                     
https://clubmanager365.com/Club/ActionHandler.ashx?siteCallback=CourtCallback&action=InviteJoinBooking&_=1693133301043&{%22BookingID%22:%228357095%22,%22SelectedMatchType%22:%221%22,%22Notification%22:%221%22,%22DesiredPlayers%22:%222%22}

                    
https://clubmanager365.com/Club/ActionHandler.ashx?
                    siteCallback=CourtCallback&
                    action=InviteJoinBooking&
                    _=1693133301043&{%22BookingID%22:%228357095%22,%22SelectedMatchType%22:%221%22,%22Notification%22:%221%22,%22DesiredPlayers%22:%222%22}
                     * 
                     /payload
                    {%22BookingID%22:8273028}: 
                     * 
                     * */

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
