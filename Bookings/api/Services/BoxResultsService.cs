using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using BookingsApi.Models;
using BookingsApi.Services;
using BookingsApi.Helpers;

namespace BookingsApi.Services
{
    public class BoxResultsService
    {
        private readonly ClubManagerLoginService _loginService;

        public BoxResultsService()
        {
            _loginService = new ClubManagerLoginService();
        }

        public async Task<BoxResultsRoot> GetBoxResultsAsync(BoxGroupType groupType = BoxGroupType.SummerFriendlies, object leagueId = null)
        {
            // Determine the league ID based on group type and provided league ID
            int actualLeagueId;
            
            if (leagueId != null)
            {
                // If a specific league ID is provided, use it
                actualLeagueId = Convert.ToInt32(leagueId);
            }
            else
            {
                // Use default league ID based on group type
                actualLeagueId = groupType switch
                {
                    BoxGroupType.SummerFriendlies => (int)SummerFriendliesLeague.JulyAug2025,
                    BoxGroupType.Club => (int)ClubLeague.MayJune2025,
                    _ => (int)SummerFriendliesLeague.JulyAug2025
                };
            }

            const string baseAddress = "https://clubmanager365.com/ActionHandler.ashx";

            var client = await _loginService.GetAuthenticatedClientAsync();
            // Ensure fresh authenticated session and cookies
            await new LoginHelper4().GetLoggedInRequestAsync(client);

            // Build API call parameters
            var parameters = new Dictionary<string, string>
            {
                { "siteCallback", "BoxLeagueCallback" },
                { "action", "GetBoxLeagueResults" },
            };
            var queryString = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            var url = $"{baseAddress}?{queryString}";
            url = url += $"&{{\"LeagueID\":{actualLeagueId},\"GroupID\":\"{(int)groupType}\"}}";
            var newUrl = new Uri(url);

            // Make the API call
            var response = await client.GetAsync(newUrl);
            var contents = await response.Content.ReadAsStringAsync();

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