using ClubManager;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Courts
{    

    public static class BoxResults
    {
        [FunctionName("BoxResults")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {                
                const string baseAddress = "https://clubmanager365.com/ActionHandler.ashx";
                CookieContainer cookies = new CookieContainer();
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = cookies,
                    //ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
                };
                using (var client = new HttpClient(handler))
                {
                    Uri uri = new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash");
                    var response = await client.GetAsync(uri);                    
                    var contents = await response.Content.ReadAsStringAsync();

                    var __VIEWSTATE = "";
                    var __VIEWSTATEGENERATOR = "";
                    var __PREVIOUSPAGE = "";
                    var __EVENTVALIDATION = "";
                    Match match = Regex.Match(contents, "<input type=\"hidden\" name=\"__VIEWSTATE\"[^>]*? value=\"(.*)\"");
                    if (match.Success) __VIEWSTATE = match.Groups[1].Value;

                    match = Regex.Match(contents, "<input type=\"hidden\" name=\"__VIEWSTATEGENERATOR\"[^>]*? value=\"(.*)\"");
                    if (match.Success) __VIEWSTATEGENERATOR = match.Groups[1].Value;

                    match = Regex.Match(contents, "<input type=\"hidden\" name=\"__PREVIOUSPAGE\"[^>]*? value=\"(.*)\"");
                    if (match.Success) __PREVIOUSPAGE = match.Groups[1].Value;

                    match = Regex.Match(contents, "<input type=\"hidden\" name=\"__EVENTVALIDATION\"[^>]*? value=\"(.*)\"");
                    if (match.Success) __EVENTVALIDATION = match.Groups[1].Value;

                    /*
                     https://clubmanager365.com/Club/ActionHandler.ashx?siteCallback=BoxLeagueCallback&action=GetBoxLeagueResults&_=1691767925747&{%22LeagueID%22:4076,%22GroupID%22:%22418%22}
                    https://clubmanager365.com/Club/ActionHandler.ashx?siteCallback=BoxLeagueCallback&action=GetBoxLeagueResults&_=1691767925747&{"LeagueID":4076,"GroupID":"418"}
                     * */
                    /*
                     https://clubmanager365.com/ActionHandler.ashx?
                    siteCallback=BoxLeagueCallback&
                    action=GetBoxLeaguePositions&
                    _=1690740585187&{"GroupID":"418"}
                     * */
                    var parameters = new Dictionary<string, string>
                    {
                        { "siteCallback", "BoxLeagueCallback" },
                        { "action", "GetBoxLeagueResults" },
                    //    { "", "%7B%22LeagueID%22:4076,%22GroupID%22:%22418%22%7D" }
                    };
                    var url = QueryHelpers.AddQueryString(baseAddress, parameters);
                    url = url += "&{%22LeagueID%22:4076,%22GroupID%22:%22216%22}";
                    var newUrl = new Uri(url);

                    response = await client.GetAsync(newUrl);
                    contents = await response.Content.ReadAsStringAsync();                    
                    Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(contents);

                    foreach (var result in myDeserializedClass.Boxes.SelectMany(box => box.Results))
                    {
                        result.ResultTimeStamp = result.ResultTimeStamp.Trim();
                        var date = DateTime.Now;
                        DateTime.TryParse(result.ResultTimeStamp, out date);
                        result.Date = date;                        
                    }

                    return new OkObjectResult(JsonConvert.SerializeObject(myDeserializedClass));
                }
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"exception: {ex.Message}");
            }
        }

        private static void SetPlayerNames(Court court, int i)
        {
            if (court.Cells[i].ToolTip.Contains(" vs "))
            {
                var players = court.Cells[i].ToolTip.Split(" vs ");
                court.Cells[i].Player1 = players[0];
                court.Cells[i].Player2 = players[1];
            }
            else
            {
                court.Cells[i].Player1 = court.Cells[i].ToolTip;
            }
        }
    }
}
