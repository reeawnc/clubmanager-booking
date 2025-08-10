using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BookingsApi.Tools;
using System.Text;

namespace BookingsApi.Agents
{
    public class BoxPositionsAgent : IAgent
    {
        private readonly ChatClient _chatClient;
        private readonly ToolRegistry _toolRegistry;

        public string Name => "box_positions";
        public string Description => "Handles queries about current box league positions for a given group.";

        public BoxPositionsAgent(OpenAIClient openAIClient)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = new ToolRegistry();
            _toolRegistry.RegisterTool(new GetBoxPositionsTool());
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            // Fast path: parse prompt locally to decide group, call tool directly, then single LLM pass to format
            var (groupParamKey, groupParamValue) = ParseGroupFromPrompt(prompt);
            if (string.IsNullOrEmpty(groupParamKey) || string.IsNullOrEmpty(groupParamValue))
            {
                // If we cannot infer the group, fall back to a helpful message
                return "Please specify which group to show: 'Club' or 'SummerFriendlies', or provide a Group ID.";
            }

            var getBoxTool = _toolRegistry.GetTool("get_box_positions");
            if (getBoxTool == null)
            {
                return "Box positions tool is unavailable right now.";
            }

            var parameters = new Dictionary<string, object> { [groupParamKey] = groupParamValue };
            var toolResultJson = await getBoxTool.ExecuteAsync(parameters);

            // Single formatting pass for compact mobile leaderboard
            var formatMessages = new List<ChatMessage>
            {
                new SystemChatMessage("You will be given box league standings as JSON. Reformat into a compact, mobile-friendly CARD view with these rules:\n\n" +
                    "Parse & normalize: Extract Rank (or Pos), Name (Plyr), Points (Pts as integer), Wins (W), Losses (L), Played (Pld).\n\n" +
                    "Sorting: Sort by Points (desc), then Wins (desc), then Played (desc), then Name (asc, case-insensitive).\n\n" +
                    "Output: For each player output exactly two lines, then a blank line:\n" +
                    "#<rank> **<name>**\n" +
                    "<points>pts | <wins>-<losses>\n\n" +
                    "- Names: keep exactly as given (including apostrophes). Do not truncate names.\n" +
                    "- Align columns so the card is easy to scan; keep lines <= 40 characters where possible.\n" +
                    "- No draws: ignore D for display.\n" +
                    "- Text only. No commentary, no code fences, no extra headers, no trailing spaces.\n" +
                    "- Exactly one blank line between cards; no extra blank lines at start or end.\n\n" +
                    "Optional modifiers (defaults):\n" +
                    "- use_medals=false: If true, prefix rank with the medal emoji for top 3 (\U0001F947, \U0001F948, \U0001F949) while still showing numeric rank, e.g., \"\U0001F947 #1\".\n" +
                    "- highlight_name=\"\": If provided and matches a player name exactly (case-insensitive), append \U0001F525 immediately after the name inside the bold markers, e.g., **R Cunniffe\U0001F525**.\n\n" +
                    "If multiple boxes are present, output a minimal markdown heading before each group: '### Box <name>' on its own line, then a blank line, then that box's cards. Maintain exactly one blank line between cards. No trailing blank lines at the end."),
                new UserChatMessage($"Box positions JSON: {toolResultJson}")
            };
            var finalResponse = await _chatClient.CompleteChatAsync(formatMessages);
            return finalResponse.Value.Content[0].Text ?? toolResultJson;
        }

        // Retained for reference; no longer used
        private static string? TryFormatBoxPositions(string json) => null;

        private static (string key, string value) ParseGroupFromPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return (string.Empty, string.Empty);
            var text = prompt.ToLowerInvariant();
            if (text.Contains("summerfriendlies") || text.Contains("summer friendlies") || text.Contains("friendlies"))
            {
                return ("group", "SummerFriendlies");
            }
            if (text.Contains("club"))
            {
                return ("group", "Club");
            }
            // Look for a group id number in the text
            var digits = new StringBuilder();
            foreach (var ch in text)
            {
                if (char.IsDigit(ch)) digits.Append(ch);
                else if (digits.Length > 0) break;
            }
            if (digits.Length > 0)
            {
                return ("groupId", digits.ToString());
            }
            return (string.Empty, string.Empty);
        }

        private static int SafeGetInt(JsonElement element)
        {
            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Number => element.TryGetInt32(out var i) ? i : (int)element.GetDouble(),
                    JsonValueKind.String => int.TryParse(element.GetString(), out var s) ? s : 0,
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }
    }
}


