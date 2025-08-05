using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using BookingsApi.Models;

namespace BookingsApi.Services
{
    public class ProcessBoxResultsService
    {
        private readonly BoxResultsService _boxResultsService;

        public ProcessBoxResultsService()
        {
            _boxResultsService = new BoxResultsService();
        }

        public async Task<ProcessBoxResultsResult> ProcessAllSummerFriendliesLeaguesAsync()
        {
            var allContent = new StringBuilder();
            var totalMatches = 0;
            var leaguesProcessed = 0;
            var processedLeagues = new List<LeagueProcessingResult>();

            // Get all Summer Friendlies league enums
            var summerLeagues = Enum.GetValues<SummerFriendliesLeague>();

            foreach (var league in summerLeagues)
            {
                try
                {
                    var boxResults = await _boxResultsService.GetBoxResultsAsync(BoxGroupType.SummerFriendlies, (int)league);

                    if (boxResults?.Boxes != null && boxResults.Boxes.Count > 0)
                    {
                        var leagueContent = CreateJsonLinesContent(boxResults, BoxGroupType.SummerFriendlies, (int)league);
                        allContent.Append(leagueContent);

                        var matchCount = boxResults.Boxes.Sum(box => box.Results?.Count ?? 0);
                        totalMatches += matchCount;
                        leaguesProcessed++;

                        processedLeagues.Add(new LeagueProcessingResult
                        {
                            League = league,
                            LeagueId = (int)league,
                            MatchCount = matchCount,
                            Success = true
                        });
                    }
                    else
                    {
                        processedLeagues.Add(new LeagueProcessingResult
                        {
                            League = league,
                            LeagueId = (int)league,
                            MatchCount = 0,
                            Success = false,
                            ErrorMessage = "No results found"
                        });
                    }
                }
                catch (Exception ex)
                {
                    processedLeagues.Add(new LeagueProcessingResult
                    {
                        League = league,
                        LeagueId = (int)league,
                        MatchCount = 0,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return new ProcessBoxResultsResult
            {
                Content = allContent.ToString(),
                TotalMatches = totalMatches,
                LeaguesProcessed = leaguesProcessed,
                ProcessedLeagues = processedLeagues,
                Filename = "summer_friendlies_all_results.jsonl"
            };
        }



        private string CreateJsonLinesContent(BoxResultsRoot boxResults, BoxGroupType groupType, object leagueId)
        {
            var sb = new StringBuilder();

            foreach (var box in boxResults.Boxes)
            {
                if (box.Results != null && box.Results.Count > 0)
                {
                    foreach (var result in box.Results)
                    {
                        var matchData = new Dictionary<string, object>
                        {
                            ["box"] = CleanHtmlTags(box.Name),
                            ["player1"] = CleanHtmlTags(result.P1),
                            ["player2"] = CleanHtmlTags(result.P2),
                            ["score"] = CleanHtmlTags(result.Score ?? "v"),
                            ["date"] = result.Date != default(DateTime) ? result.Date.ToString("yyyy-MM-dd") : "Unknown",
                            ["leagueId"] = leagueId
                        };

                        // Determine if match was played and who won/lost
                        var score = CleanHtmlTags(result.Score ?? "");
                        var hasValidScore = !string.IsNullOrEmpty(score) && score != "v" && score.Contains(" ");

                        if (hasValidScore && result.Date != default(DateTime))
                        {
                            matchData["matchPlayed"] = true;

                            // Parse winner and loser from score
                            var scoreParts = score.Split(' ');
                            if (scoreParts.Length >= 3 && int.TryParse(scoreParts[0], out var p1Score) && int.TryParse(scoreParts[2], out var p2Score))
                            {
                                if (p1Score > p2Score)
                                {
                                    matchData["winner"] = CleanHtmlTags(result.P1);
                                    matchData["loser"] = CleanHtmlTags(result.P2);
                                }
                                else if (p2Score > p1Score)
                                {
                                    matchData["winner"] = CleanHtmlTags(result.P2);
                                    matchData["loser"] = CleanHtmlTags(result.P1);
                                }
                                else
                                {
                                    // Draw
                                    matchData["winner"] = null;
                                    matchData["loser"] = null;
                                }
                            }
                        }
                        else
                        {
                            matchData["matchPlayed"] = false;
                        }

                        // Convert to JSON and add to output
                        var jsonLine = JsonConvert.SerializeObject(matchData);
                        sb.AppendLine(jsonLine);
                    }
                }
            }

            return sb.ToString();
        }

        private string CleanHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove common HTML tags
            var cleaned = input
                .Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("<sup>", "")
                .Replace("</sup>", "")
                .Replace("<i>", "")
                .Replace("</i>", "")
                .Replace("<strong>", "")
                .Replace("</strong>", "")
                .Replace("<em>", "")
                .Replace("</em>", "")
                .Replace("<u>", "")
                .Replace("</u>", "")
                .Replace("<span>", "")
                .Replace("</span>", "")
                .Replace("<div>", "")
                .Replace("</div>", "")
                .Replace("<p>", "")
                .Replace("</p>", "");

            // Remove any remaining HTML tags using regex
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"<[^>]*>", "");

            return cleaned.Trim();
        }
    }

    public class ProcessBoxResultsResult
    {
        public string Content { get; set; } = string.Empty;
        public int TotalMatches { get; set; }
        public int LeaguesProcessed { get; set; }
        public List<LeagueProcessingResult> ProcessedLeagues { get; set; } = new List<LeagueProcessingResult>();
        public string Filename { get; set; } = string.Empty;
    }

    public class LeagueProcessingResult
    {
        public SummerFriendliesLeague? League { get; set; }
        public int LeagueId { get; set; }
        public int MatchCount { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
} 