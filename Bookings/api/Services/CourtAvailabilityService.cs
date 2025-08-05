using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using BookingsApi.Models;

namespace BookingsApi.Services
{
    public class CourtAvailabilityService
    {
        public async Task<RootObject> GetCourtAvailabilityAsync(string date, ILogger log = null)
        {
            try
            {
                date = String.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("dd MMM yy") : date;

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
                        {"", "{'Date':'" + date +"','DayParts':['3'],'CourtTypes':['0'],'SpaceAvailable':1280}"} 
                    };

                    var queryString = string.Join("&", param.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
                var newUrl = new Uri($"{baseAddress}?{queryString}");
                    response = await client.GetAsync(newUrl);
                    contents = await response.Content.ReadAsStringAsync();
                    
                    RootObject myDeserializedClass = JsonConvert.DeserializeObject<RootObject>(contents);

                    var court1 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 1"));
                    var court2 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 2"));
                    var court3 = myDeserializedClass.Courts.FirstOrDefault(c => c.ColumnHeading.StartsWith("Court 3"));

                    // Process each court separately and keep them separate
                    for (var i = 0; i < myDeserializedClass.Courts[0].Cells.Count; i++)
                    {
                        if (court1 != null && i < court1.Cells.Count)
                        {
                            court1.Cells[i].ToolTip = court1.Cells[i].ToolTip.Split('(')[0].Trim();
                            court1.Cells[i].Court = "Court 1";
                            court1.Cells[i].CourtID = court1.CourtID;                        
                            SetPlayerNames(court1, i);
                        }

                        if (court2 != null && i < court2.Cells.Count)
                        {
                            court2.Cells[i].ToolTip = court2.Cells[i].ToolTip.Split('(')[0].Trim();
                            court2.Cells[i].Court = "Court 2";
                            court2.Cells[i].CourtID = court2.CourtID;
                            SetPlayerNames(court2, i);
                        }

                        if (court3 != null && i < court3.Cells.Count)
                        {
                            court3.Cells[i].ToolTip = court3.Cells[i].ToolTip.Split('(')[0].Trim();
                            court3.Cells[i].Court = "Court 3";
                            court3.Cells[i].CourtID = court3.CourtID;
                            SetPlayerNames(court3, i);
                        }
                    }

                    // Keep courts separate instead of combining them
                    var separateCourts = new List<Court>();
                    if (court1 != null) separateCourts.Add(court1);
                    if (court2 != null) separateCourts.Add(court2);
                    if (court3 != null) separateCourts.Add(court3);

                    myDeserializedClass.Courts = separateCourts;
                    
                    return myDeserializedClass;
                }
            }
            catch (Exception ex)
            {
                log?.LogError($"Error getting court availability: {ex.Message}");
                throw;
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