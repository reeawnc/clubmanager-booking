using ClubManager;
using ClubManager.Helpers;
using clubmanager_booking.Biz;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace clubmanager_booking
{
    public static class BookCourtEveryTuesday
    {
        public static CourtManager __courtManager { get; private set; }

        [FunctionName("BookCourtEveryTuesday")]
        public static async Task RunAsync([TimerTrigger("1 0 8 * * 2")] TimerInfo myTimer, ILogger log)
        {            
            try
            {
                log.LogInformation($"C# Timer trigger BookCourtEveryDay function executed at: {DateTime.Now}");
                __courtManager = new CourtManager();
                string date = DateTime.Now.AddDays(7).ToString("dd MMM yy");
                log.LogInformation($"Date used: {date}");

                const string baseAddress = "https://clubmanager365.com/ActionHandler.ashx";                
                CookieContainer cookies = new CookieContainer();
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = cookies,
                };
                using (var client = new HttpClient(handler))
                {

                    var cell = await __courtManager.GetCourt2Cell(date, baseAddress, client, log, time: "18:45");                    

                    Tuple<HttpRequestMessage, HttpResponseMessage> res = await new LoginHelper4().GetLoggedInRequestAsync(client);
                    log.LogInformation($"login success? IsSuccesStatusCode: {res.Item2.IsSuccessStatusCode}");

                    var param = new Dictionary<string, string>() {
                    { "siteCallback", "CourtCallback" },
                    {"action", "MakeBooking" } };

                    var url = QueryHelpers.AddQueryString("https://clubmanager365.com/Club/ActionHandler.ashx", param);
                    url = url + "&{\"OpponentPlayerIDs\":null,\"CourtsRequired\":[{\"c\":\"" + cell.CourtID + "\",\"s\":\"" + cell.CourtSlotID + "\"}],\"Notification\":\"-1\",\"Resources\":[],\"MatchDate\":\"" + date + "\",\"ExpectedBalanceAmount\":\"\",\"PaymentAmount\":0,\"SelectedMatchType\":\"4\",\"ExtensionCourtSlotID\":\"0\",\"CourtID\":\"" + cell.CourtID + "\",\"PackageItem1\":\"\",\"PackageItem2\":\"\",\"PackageItem3\":\"\"}";
                    var bookingsResponse = await client.GetAsync(new Uri(url));
                    log.LogInformation($"booking success? IsSuccesStatusCode: {bookingsResponse.IsSuccessStatusCode}");
                    var contents = await bookingsResponse.Content.ReadAsStringAsync();
                    log.LogInformation($"booking: {contents}");
                    dynamic data = JsonConvert.DeserializeObject(contents);
                    log.LogInformation($"data: {await data.Content.ReadAsStringAsync()}");                    
                    log.LogInformation($"data.WasSuccessful: {data.WasSuccessful}");
                    log.LogInformation($"data.WasSuccessful == false: {data.WasSuccessful == false}");
                    if (data.WasSuccessful == false)
                    {
                        throw new Exception(data.ErrorMessage);
                    }                    
                }
            }
            catch (Exception ex)
            {                
                log.LogError($"exception: {ex.Message}");
                throw new Exception(ex.Message);
            }
        }

        private static bool IsCellEmpty(Cell cell)
        {
            return cell == null || cell?.CourtID == null || cell?.CourtID == 0;
        }
    }
}