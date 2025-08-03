using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Web;
using System.Collections.Generic;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http;
using System.Net;
using ClubManager;
using System.Text.RegularExpressions;
using System.Linq;

namespace Courts
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

    public static class Courts
    {
        [FunctionName("Courts")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                string date = HttpUtility.UrlDecode(req.Query["date"]);
                //1%20May%202022
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                date = date ?? data?.name;

                date = String.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("dd MMM yy") : date;
                /*
                string responseMessage = string.IsNullOrEmpty(date)
                    ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                    : $"Hello, {date}. This HTTP triggered function executed successfully.";
                */

                const string baseAddress = "https://clubmanager365.com/ActionHandler.ashx";
                /*
                 * https://clubmanager365.com/ActionHandler.ashx?
                 * siteCallback=CourtCallback
                 * &action=GetCourtDay
                 * &_=1651410654202 no needed
                 * 
                 * 
                 * &{%22Date%22:%222%20May%202022%22,%22DayParts%22:[%223%22],%22CourtTypes%22:[%220%22],%22SpaceAvailable%22:1280}
                 * 
                 * 
                 * 
                 * "Date":"2 May 2022","DayParts":["3"],"CourtTypes":["0"],"SpaceAvailable":1280
                 * */
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
                    var formContent = new FormUrlEncodedContent(new[]
                    {
                new KeyValuePair<string, string>("__EVENTARGUMENT", ""),
                new KeyValuePair<string, string>("__EVENTTARGET", ""),
                new KeyValuePair<string, string>("__VIEWSTATE", __VIEWSTATE),
                new KeyValuePair<string, string>("__VIEWSTATEGENERATOR", __VIEWSTATEGENERATOR),
                new KeyValuePair<string, string>("__PREVIOUSPAGE", __PREVIOUSPAGE),
                new KeyValuePair<string, string>("__EVENTVALIDATION", __EVENTVALIDATION),
                new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$UserName", "rioghan_c@hotmail.com"),
                new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$Password", "Rioghan1988"),
                new KeyValuePair<string, string>("ctl00$LoginView5$UserLogin$LoginButton", "Log In"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$LoginView1$ClubLinksControl$CourtsDropDownList", "-1"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$LoginView1$ClubLinksControl$PitchesDropDownList", "-1"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$LoginView1$ClubLinksControl$BoxLeaguesDropDownList", "-1"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$LoginView1$ClubLinksControl$BoxLeaguesDropDownList", "-1"),
                new KeyValuePair<string, string>("ctl00$ContentPlaceHolder1$LoginView1$ClubLinksControl$TournamentsDropDownList", "-1"),
            });*/

                    /*
                    var formContent = new FormUrlEncodedContent(new[]
                            {
                        new KeyValuePair<string, string>("__EVENTARGUMENT", ""),
                        new KeyValuePair<string, string>("__EVENTTARGET", ""),
                        new KeyValuePair<string, string>("__VIEWSTATE", __VIEWSTATE),
                        new KeyValuePair<string, string>("__VIEWSTATEGENERATOR", __VIEWSTATEGENERATOR),
                        new KeyValuePair<string, string>("__EVENTVALIDATION", __EVENTVALIDATION),
                        new KeyValuePair<string, string>("HeaderBarSection_NHP$LoginView5$UserLogin$UserName", "rioghan_c@hotmail.com"),
                        new KeyValuePair<string, string>("HeaderBarSection_NHP$LoginView5$UserLogin$Password", "Rioghan1988"),
                        new KeyValuePair<string, string>("HeaderBarSection_NHP$LoginView5$UserLogin$LoginSubmitButton", "Login")
                    });

                    uri = new Uri("https://clubmanager.ie/Homepage.aspx?ReturnUrl=%2fClub%2fBookings.aspx");
                    var newReq = new HttpRequestMessage(HttpMethod.Post, uri) { Content = formContent };
                    client.DefaultRequestHeaders.Add("Host", "clubmanager.ie");
                    client.DefaultRequestHeaders.Add("Origin", "https://clubmanager.ie");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

                    client.DefaultRequestHeaders.Add("Referer", "https://clubmanager.ie/Homepage.aspx");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");

                    response = await client.SendAsync(newReq);
                    */

                    var param = new Dictionary<string, string>() {
                    { "siteCallback", "CourtCallback" },
                    {"action", "GetCourtDay" },
                    {"", "{'Date':'" + date +"','DayParts':['3'],'CourtTypes':['0'],'SpaceAvailable':1280}"} };

                    var newUrl = new Uri(QueryHelpers.AddQueryString(baseAddress, param));

                    /*
                     //siteCallback=CourtCallback&action=GetCourtDay&_=1690645507124&{"Date":"29 Jul 2023","DayParts":["3"],"CourtTypes":["0"],"SpaceAvailable":1920}
                     https://clubmanager365.com/ActionHandler.ashx?siteCallback=CourtCallback&action=GetCourtDay&_=1690645507124&{%22Date%22:%2229%20Jul%202023%22,%22DayParts%22:[%223%22],%22CourtTypes%22:[%220%22],%22SpaceAvailable%22:1920}
                     https://clubmanager365.com/ActionHandler.ashx?siteCallback=CourtCallback&action=GetCourtDay&={%27Date%27%3A%2729 Jul 23%27,%27DayParts%27%3A%5B%273%27%5D,%27CourtTypes%27%3A%5B%270%27%5D,%27SpaceAvailable%27%3A1280}}
                     */

                    response = await client.GetAsync(newUrl);
                    contents = await response.Content.ReadAsStringAsync();
                    //var jsonContent = JsonConvert.DeserializeObject<object>(contents);
                    //var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(contents);
                    RootObject myDeserializedClass = JsonConvert.DeserializeObject<RootObject>(contents);

                    var court1 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 1"));
                    var court2 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 2"));
                    var court3 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 3"));
                    var court4 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 4"));

                    List<Cell> cellsCombined = new List<Cell>();
                    for (var i = 0; i < myDeserializedClass.Courts[0].Cells.Count; i++)
                    {
                        court1.Cells[i].ToolTip = court1.Cells[i].ToolTip.Split('(')[0].Trim();
                        court1.Cells[i].Court = "Court 1";
                        court1.Cells[i].CourtID = court1.CourtID;                        
                        SetPlayerNames(court1, i);

                        court2.Cells[i].ToolTip = court2.Cells[i].ToolTip.Split('(')[0].Trim();
                        court2.Cells[i].Court = "Court 2";
                        court2.Cells[i].CourtID = court2.CourtID;
                        SetPlayerNames(court2, i);

                        court3.Cells[i].ToolTip = court3.Cells[i].ToolTip.Split('(')[0].Trim();
                        court3.Cells[i].Court = "Court 3";
                        court3.Cells[i].CourtID = court3.CourtID;
                        SetPlayerNames(court3, i);

                        //court4.Cells[i].ToolTip = court4.Cells[i].ToolTip.Split('(')[0].Trim();
                        //SetPlayerNames(court4, i);

                        /*court1.Cells[i].TimeSlot = "1) " + court1.Cells[i].TimeSlot;
                        court2.Cells[i].TimeSlot = "2) " + court2.Cells[i].TimeSlot;
                        court3.Cells[i].TimeSlot = "3) " + court3.Cells[i].TimeSlot;
                        court4.Cells[i].TimeSlot = "4) " + court4.Cells[i].TimeSlot;*/

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