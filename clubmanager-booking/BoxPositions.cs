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
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Courts
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Box
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public int ID { get; set; }
        public List<Position> Positions { get; set; }
        public List<Result> Results { get; set; }
    }

    public class BoxesContainer
    {
        public List<Option> Options { get; set; }
        public bool IsMultiSelect { get; set; }
    }

    public class LeaguesContainer
    {
        public List<Option> Options { get; set; }
        public bool IsMultiSelect { get; set; }
    }

    public class Option
    {
        public string DisplayText { get; set; }
        public string DisplayValue { get; set; }
    }

    public class Result
    {
        public string P1 { get; set; }
        public string P2 { get; set; }
        public string Score { get; set; }
        public string LinkDisplayText { get; set; }
        public int MatchID { get; set; }
        public string EditVisibility { get; set; }
        public int P1ID { get; set; }
        public int P2ID { get; set; }
        public bool P1IsMe { get; set; }
        public bool P2IsMe { get; set; }
        public string P1Css { get; set; }
        public string P2Css { get; set; }
        public bool IsEditable { get; set; }
        public string ResultTimeStamp { get; set; }
        public string AllowFixtureEntry { get; set; }
        public DateTime Date { get; internal set; }
    }

    public class Position
    {
        public int ID { get; set; }
        public int Pos { get; set; }
        public string Plyr { get; set; }
        public int Pld { get; set; }
        public int W { get; set; }
        public int D { get; set; }
        public int L { get; set; }
        public string B { get; set; }
        public int Pts { get; set; }
        public string Tgt { get; set; }
        public bool IsMe { get; set; }
    }

    public class Root
    {
        public List<Box> Boxes { get; set; }
        public int RuleID { get; set; }
        public BoxesContainer BoxesContainer { get; set; }
        public LeaguesContainer LeaguesContainer { get; set; }
        public int SelectedLeagueID { get; set; }
        public bool LeagueIsOpen { get; set; }
    }



    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

    public static class BoxPositions
    {
        [FunctionName("BoxPositions")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                //log.LogInformation("C# HTTP trigger function processed a request.");

                //string date = HttpUtility.UrlDecode(req.Query["date"]);
                //1%20May%202022
                //string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                //dynamic data = JsonConvert.DeserializeObject(requestBody);
                //date = date ?? data?.name;

                //date = String.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("dd MMM yy") : date;
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
                    //log.Info(response.ToString());
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
                     https://clubmanager365.com/ActionHandler.ashx?
                    siteCallback=BoxLeagueCallback&
                    action=GetBoxLeaguePositions&
                    _=1690740585187&{"GroupID":"418"}
                     * */
                    var param = new Dictionary<string, string>() {
                    { "siteCallback", "BoxLeagueCallback" },
                    {"action", "GetBoxLeaguePositions" },
                    {"", "{'GroupID':'216'}" } };
                    //{"", "{'Date':'" + date +"','DayParts':['3'],'CourtTypes':['0'],'SpaceAvailable':1280}"} };

                    var newUrl = new Uri(QueryHelpers.AddQueryString(baseAddress, param));

                    /*
                     * current
                     * https://clubmanager365.com/ActionHandler.ashx?siteCallback=BoxLeagueCallback&action=GetBoxLeaguePositions&={%27GroupID%27%3A%27418%27}}
                     * 
                     * https://clubmanager365.com/ActionHandler.ashx?siteCallback=BoxLeagueCallback&action=GetBoxLeaguePositions&{%22GroupID%22:%22418%22}
                     * Working
                     */

                    response = await client.GetAsync(newUrl);
                    contents = await response.Content.ReadAsStringAsync();
                    //var jsonContent = JsonConvert.DeserializeObject<object>(contents);
                    //var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(contents);
                    Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(contents);

                    /*
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

                        //court4.Cells[i].ToolTip = court4.Cells[i].ToolTip.Split('(')[0].Trim();
                        //SetPlayerNames(court4, i);

                        court1.Cells[i].TimeSlot = "1) " + court1.Cells[i].TimeSlot;
                        court2.Cells[i].TimeSlot = "2) " + court2.Cells[i].TimeSlot;
                        court3.Cells[i].TimeSlot = "3) " + court3.Cells[i].TimeSlot;
                        court4.Cells[i].TimeSlot = "4) " + court4.Cells[i].TimeSlot;

                        cellsCombined.Add(court1.Cells[i]);
                        cellsCombined.Add(court2.Cells[i]);
                        cellsCombined.Add(court3.Cells[i]);
                        //cellsCombined.Add(court4.Cells[i]);
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
                */
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
