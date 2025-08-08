using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi.Agents
{
    public class LiveResultsAgent : IAgent
    {
        private readonly BoxResultsService _resultsService;
        private readonly ChatClient _chatClient;

        public string Name => "live_results";
        public string Description => "Handles live/current box results directly from ClubManager (no file search). Formats output via LLM.";

        public LiveResultsAgent(OpenAIClient openAIClient)
        {
            _resultsService = new BoxResultsService();
            _chatClient = openAIClient.GetChatClient("gpt-4o-mini");
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

            // Build a minimal JSON payload of played matches only
            var boxesOut = new List<Dictionary<string, object>>();
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

                var played = new List<Dictionary<string, object>>();
                foreach (var r in orderedAll)
                {
                    if (!TryParseScore(r.Score, out var s1, out var s2)) continue;
                    if (r.Date == default) continue;
                    var winner = s1 > s2 ? r.P1 : s2 > s1 ? r.P2 : "Draw";
                    played.Add(new Dictionary<string, object>
                    {
                        ["p1"] = r.P1,
                        ["p2"] = r.P2,
                        ["score"] = $"{s1} v {s2}",
                        ["date"] = r.Date.ToString("yyyy-MM-dd"),
                        ["winner"] = winner
                    });
                }

                if (played.Count == 0) continue;

                if (string.IsNullOrEmpty(requestedBox) && played.Count > 10)
                {
                    played = played.Take(10).ToList();
                }

                boxesOut.Add(new Dictionary<string, object>
                {
                    ["boxName"] = box.Name,
                    ["matches"] = played
                });
            }

            if (boxesOut.Count == 0)
            {
                return "No completed matches found.";
            }

            var json = JsonSerializer.Serialize(boxesOut);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You format squash box results. Show only played matches. For each box: heading '### <Box Name>' then a numbered list 'Player1 vs Player2 — Score S1–S2 — Date YYYY-MM-DD — Winner: Name'. Keep it concise; return plain text, no JSON."),
                new UserChatMessage($"Group: {group}. Data: {json}")
            };
            var completion = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = 0.1f, MaxOutputTokenCount = 600 });
            return completion.Value.Content[0].Text ?? "No output";
        }

        private static bool TryParseScore(string? score, out int s1, out int s2)
        {
            s1 = 0; s2 = 0;
            if (string.IsNullOrWhiteSpace(score)) return false;
            var m = Regex.Match(score, @"(?<a>\d+)\s*v\s*(?<b>\d+)", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            return int.TryParse(m.Groups["a"].Value, out s1) && int.TryParse(m.Groups["b"].Value, out s2);
        }
    }
}


