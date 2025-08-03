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
    public static class BookCourt
    {
        [FunctionName("BookCourt")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            var matchDate = data.matchDate.ToString("d MMM yyyy");
            var selectedMatchType = data.matchDate.ToString();
            var courtID = data.courtID.ToString();
            var courtSlotID = data.courtSlotID.ToString();

            //var matchDate = data.matchDate.ToString("dd MMM yyyy");
            //return new OkObjectResult(requestBody);



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

                    //https://clubmanager365.com/Club/ActionHandler.ashx?siteCallback=CourtCallback&action=MakeBooking&_=1690990531395&{"OpponentPlayerIDs":null,"CourtsRequired":[{"c":"687","s":"6465"}],"Notification":"-1","Resources":[],"MatchDate":"5 Aug 2023","ExpectedBalanceAmount":"","PaymentAmount":0,"SelectedMatchType":"4","ExtensionCourtSlotID":"0","CourtID":"687","PackageItem1":"","PackageItem2":"","PackageItem3":""}



                    var param = new Dictionary<string, string>() {
                    { "siteCallback", "CourtCallback" },
                    {"action", "MakeBooking" } };
                    //{"", "{'OpponentPlayerIDs':null,'CourtsRequired':[{'c':'"+courtID+"','s':'"+ courtSlotID +"'}],'Notification':'-1','Resources':[],'MatchDate':'"+ matchDate + "','ExpectedBalanceAmount':'','PaymentAmount':0,'SelectedMatchType':'4','ExtensionCourtSlotID':'','CourtID':'"+courtID+"','PackageItem1':'','PackageItem2':'','PackageItem3':''}"} };

                    var url = QueryHelpers.AddQueryString("https://clubmanager365.com/Club/ActionHandler.ashx", param);
                    url = url + "&{\"OpponentPlayerIDs\":null,\"CourtsRequired\":[{\"c\":\"" + courtID + "\",\"s\":\"" + courtSlotID + "\"}],\"Notification\":\"-1\",\"Resources\":[],\"MatchDate\":\"" + matchDate + "\",\"ExpectedBalanceAmount\":\"\",\"PaymentAmount\":0,\"SelectedMatchType\":\"4\",\"ExtensionCourtSlotID\":\"0\",\"CourtID\":\"" + courtID + "\",\"PackageItem1\":\"\",\"PackageItem2\":\"\",\"PackageItem3\":\"\"}";
                    var uri = new Uri(url);

                    //var uriTest = new Uri("https://clubmanager365.com/Club/ActionHandler.ashx?siteCallback=CourtCallback&action=MakeBooking&_=1691059059762&{%22OpponentPlayerIDs%22:null,%22CourtsRequired%22:[{%22c%22:%22687%22,%22s%22:%226464%22}],%22Notification%22:%22-1%22,%22Resources%22:[],%22MatchDate%22:%225%20Aug%202023%22,%22ExpectedBalanceAmount%22:%22%22,%22PaymentAmount%22:0,%22SelectedMatchType%22:%224%22,%22ExtensionCourtSlotID%22:%220%22,%22CourtID%22:%22687%22,%22PackageItem1%22:%22%22,%22PackageItem2%22:%22%22,%22PackageItem3%22:%22%22}");
                    var bookingsResponse = await client.GetAsync(uri);
                    var contents = await bookingsResponse.Content.ReadAsStringAsync();
                    return new OkObjectResult(contents);

                    
                    //var uriTest = new Uri("https://clubmanager365.com/Club/ActionHandler.ashx?siteCallback=CourtCallback&action=MakeBooking&_=1691059059762&{%22OpponentPlayerIDs%22:null,%22CourtsRequired%22:[{%22c%22:%22687%22,%22s%22:%226464%22}],%22Notification%22:%22-1%22,%22Resources%22:[],%22MatchDate%22:%225%20Aug%202023%22,%22ExpectedBalanceAmount%22:%22%22,%22PaymentAmount%22:0,%22SelectedMatchType%22:%224%22,%22ExtensionCourtSlotID%22:%220%22,%22CourtID%22:%22687%22,%22PackageItem1%22:%22%22,%22PackageItem2%22:%22%22,%22PackageItem3%22:%22%22}");
                    //var matching = uri == uriTest;
                    //var bookingsResponse = await client.GetAsync(uriTest);
                    //var contents = await bookingsResponse.Content.ReadAsStringAsync();
                    //return new OkObjectResult(contents);
                    //return new OkObjectResult(uri);
                }
                catch (Exception ex)
                {
                    return new BadRequestObjectResult($"exception: {ex.Message}");
                }
            }
        }
    }
}
