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

namespace ClubManager
{
    public static class GetPlayerBookings
    {
        [FunctionName("GetPlayerBookings")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                //string date = HttpUtility.UrlDecode(req.Query["date"]);
                //1%20May%202022
                string date = HttpUtility.UrlDecode("1%20May%202022");

                const string baseAddress = "https://clubmanager365.com/ActionHandler.ashx";
                
                CookieContainer cookies = new CookieContainer();
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = cookies,
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
                    
                    var param = new Dictionary<string, string>() {
                    { "siteCallback", "CourtCallback" },
                    {"action", "GetCourtDay" },
                    {"", "{'Date':'" + date +"','DayParts':['3'],'CourtTypes':['0'],'SpaceAvailable':1280}"} };

                    var newUrl = new Uri(QueryHelpers.AddQueryString(baseAddress, param));

                    response = await client.GetAsync(newUrl);
                    contents = await response.Content.ReadAsStringAsync();
                    RootObject myDeserializedClass = JsonConvert.DeserializeObject<RootObject>(contents);

                    var court1 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 1"));
                    var court2 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 2"));
                    var court3 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 3"));
                    var court4 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 4"));

                    List<Cell> cellsCombined = new List<Cell>();
                    for (var i = 0; i < myDeserializedClass.Courts[0].Cells.Count; i++)
                    {
                        court1.Cells[i].ToolTip = court1.Cells[i].ToolTip.Split('(')[0].Trim();
                        SetPlayerNames(court1, i);

                        court2.Cells[i].ToolTip = court2.Cells[i].ToolTip.Split('(')[0].Trim();
                        SetPlayerNames(court2, i);

                        court3.Cells[i].ToolTip = court3.Cells[i].ToolTip.Split('(')[0].Trim();
                        SetPlayerNames(court3, i);

                        court4.Cells[i].ToolTip = court4.Cells[i].ToolTip.Split('(')[0].Trim();
                        SetPlayerNames(court4, i);

                        cellsCombined.Add(court1.Cells[i]);
                        cellsCombined.Add(court2.Cells[i]);
                        cellsCombined.Add(court3.Cells[i]);
                        cellsCombined.Add(court4.Cells[i]);
                    }

                    var finalCourt = new Court()
                    {
                        Cells = cellsCombined,
                        ColumnHeading = "All",
                        CssClass = "",
                        EarliestStartTime = ""
                    };

                    myDeserializedClass.Courts = new List<Court>();
                    myDeserializedClass.Courts.Add(finalCourt);
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
