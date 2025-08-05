using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using System.Linq;

namespace BookingsApi.Services
{
    public class BoxResultsService
    {
        public async Task<BoxResultsRoot> GetBoxResultsAsync(int leagueId = 4076, string groupId = "216")
        {
            const string baseAddress = "https://clubmanager365.com/ActionHandler.ashx";
            CookieContainer cookies = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = cookies,
            };
            
            using (var client = new HttpClient(handler))
            {
                // Initial page load to get session cookies
                Uri uri = new Uri("https://clubmanager365.com/CourtCalendar.aspx?club=westwood&sport=squash");
                var response = await client.GetAsync(uri);                    
                var contents = await response.Content.ReadAsStringAsync();

                // Extract hidden fields (not used in this API call but kept for consistency)
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

                // Build API call parameters
                var parameters = new Dictionary<string, string>
                {
                    { "siteCallback", "BoxLeagueCallback" },
                    { "action", "GetBoxLeagueResults" },
                };
                var url = QueryHelpers.AddQueryString(baseAddress, parameters);
                url = url += $"&{{\"LeagueID\":{leagueId},\"GroupID\":\"{groupId}\"}}";
                var newUrl = new Uri(url);

                // Make the API call
                response = await client.GetAsync(newUrl);
                contents = await response.Content.ReadAsStringAsync();                    
                
                // Deserialize the response
                var boxResults = JsonConvert.DeserializeObject<BoxResultsRoot>(contents);

                // Process the results - add date parsing
                if (boxResults?.Boxes != null)
                {
                    foreach (var result in boxResults.Boxes.SelectMany(box => box.Results))
                    {
                        result.ResultTimeStamp = result.ResultTimeStamp?.Trim();
                        if (!string.IsNullOrEmpty(result.ResultTimeStamp))
                        {
                            DateTime.TryParse(result.ResultTimeStamp, out var date);
                            result.Date = date;
                        }
                    }
                }

                return boxResults;
            }
        }
    }

    // Box Results Models
    public class BoxResultsRoot
    {
        public List<BoxResult> Boxes { get; set; }
        public int RuleID { get; set; }
        public BoxesContainer BoxesContainer { get; set; }
        public LeaguesContainer LeaguesContainer { get; set; }
        public int SelectedLeagueID { get; set; }
        public bool LeagueIsOpen { get; set; }
    }

    public class BoxResult
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public int ID { get; set; }
        public List<Position> Positions { get; set; }
        public List<MatchResult> Results { get; set; }
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

    public class MatchResult
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
        public DateTime Date { get; set; }
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
} 