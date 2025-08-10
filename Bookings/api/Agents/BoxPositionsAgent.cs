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

            // Single formatting pass
            var formatMessages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are summarizing squash box positions.

Output Markdown only inside ONE fenced block using ```markdown ... ```.
For each box:
- Use an H3 heading: '### Box <name>'.
- Then a GitHub‑flavored Markdown table with headers: Rank | Player | Pld | W | D | L | Pts.
- Replace any source 'Pos' header with 'Rank'.
- Right‑align numeric columns and left‑align text columns using GFM alignment markers in the header separator row.
- Ensure each column has a stable minimum visual width so numeric columns remain perfectly vertically aligned. Compute widths as the max of a sensible minimum (Player ≥ 32) and the longest displayed cell in that column; pad with spaces as needed (do not truncate).
- Do not include extra ASCII grid lines; each data row must be exactly `| col | col | ... |`.
- Preserve ranking order; do not sort.
- Include ALL rows; do not truncate. If the JSON only has partial data, render what is present and omit any '+N more' hint.
- Trim excess spaces in names. Keep original capitalisation unless the whole name is lowercase, then use Title Case.
- Do not calculate totals; if W/D/L are missing, leave them blank.
Return only the single fenced Markdown block."),
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


