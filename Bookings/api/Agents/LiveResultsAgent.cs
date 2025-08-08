using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Agents
{
    public class LiveResultsAgent : IAgent
    {
        private readonly BoxResultsService _resultsService;

        public string Name => "live_results";
        public string Description => "Handles live/current box results directly from ClubManager (no file search).";

        public LiveResultsAgent()
        {
            _resultsService = new BoxResultsService();
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            var lower = prompt.ToLowerInvariant();
            var group = Models.BoxGroupType.SummerFriendlies;
            if (lower.Contains("club")) group = Models.BoxGroupType.Club;

            // Optional box filter e.g. "Box A1"
            string? requestedBox = null;
            var m = Regex.Match(lower, @"box\s*[a-z]\s*\d+", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var raw = m.Value.Trim();
                var cleaned = Regex.Replace(raw, @"\s+", " ").ToLowerInvariant();
                if (cleaned.StartsWith("box ") && cleaned.Length >= 6)
                {
                    var letter = char.ToUpperInvariant(cleaned[4]);
                    var numberPart = cleaned.Substring(5).Replace(" ", "");
                    requestedBox = $"Box {letter}{numberPart}";
                }
            }

            var data = await _resultsService.GetBoxResultsAsync(group);
            if (data?.Boxes == null || data.Boxes.Count == 0)
            {
                return $"No live results found for {group}.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Here are the current match results for {group}:");

            foreach (var box in data.Boxes)
            {
                if (!string.IsNullOrEmpty(requestedBox))
                {
                    var normalizedName = Regex.Replace(box.Name ?? string.Empty, @"\s+", " ");
                    if (!normalizedName.Equals(requestedBox, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                if (box.Results == null || box.Results.Count == 0) continue;
                var orderedAll = box.Results
                    .OrderByDescending(r => r.Date)
                    .ThenByDescending(r => r.MatchID)
                    .ToList();
                var ordered = string.IsNullOrEmpty(requestedBox) ? orderedAll.Take(10).ToList() : orderedAll;

                sb.AppendLine($"\n### {box.Name}");
                int i = 1;
                foreach (var r in ordered)
                {
                    var date = r.Date != default ? r.Date.ToString("yyyy-MM-dd") : "Unknown";
                    var score = string.IsNullOrWhiteSpace(r.Score) ? "v" : r.Score;
                    string winner = "";
                    // Try infer winner from score "A B" format
                    var parts = score.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && int.TryParse(parts[0], out var p1) && int.TryParse(parts[2], out var p2))
                    {
                        winner = p1 > p2 ? r.P1 : p2 > p1 ? r.P2 : "Draw";
                    }
                    sb.AppendLine($"{i}. {r.P1} vs. {r.P2}\n- Score: {score}\n- Date: {date}\n- Winner: {winner}");
                    i++;
                }
                if (string.IsNullOrEmpty(requestedBox) && orderedAll.Count > ordered.Count)
                {
                    sb.AppendLine($"… and {orderedAll.Count - ordered.Count} more");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}


