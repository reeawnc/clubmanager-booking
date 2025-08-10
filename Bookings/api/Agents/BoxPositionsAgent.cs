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
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are summarizing squash box positions.

When calling the tool:
- If the user mentions a named group (e.g., 'Club', 'SummerFriendlies'), pass it as 'group'.
- If they provide a numeric group id, pass it as 'groupId'.

When formatting the answer (after the tool returns JSON):
- Output Markdown only inside ONE fenced block using ```markdown ... ```.
- For each box:
  - Use an H3 heading: '### Box <name>'.
  - Then a GitHub‑flavored Markdown table with headers: Rank | Player | Pld | W | D | L | Pts.
  - Replace any source 'Pos' header with 'Rank'.
  - Right‑align numeric columns and left‑align text columns using GFM alignment markers in the header separator row.
  - Ensure the Player column has a minimum width for alignment: compute the width as max(26, longest displayed player name) and pad shorter names with spaces (do not truncate). This keeps numeric columns vertically aligned even when names vary in length.
  - Do not include any extra ASCII grid lines; each row must be exactly `| col | col | ... |` with spaces, nothing more.
  - Preserve the existing ranking order; do not sort.
  - Include ALL rows (no top-10 truncation). If the source had an '… and N more' marker, ignore it and print all rows provided in the JSON.
- Trim excess spaces in names. Keep original capitalisation unless the whole name is lowercase, then use Title Case.
- Bold the entire row for player name matching 'R Cunniffe' (case‑insensitive) by bolding each cell in that row.
- Do not calculate totals; if W/D/L are missing, leave them blank.
"),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions();
            foreach (var tool in _toolRegistry.GetAllTools())
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description, BinaryData.FromObjectAsJson(tool.Parameters)));
            }

            var response = await _chatClient.CompleteChatAsync(messages, options);
            if (response.Value.ToolCalls?.Count > 0)
            {
                foreach (var toolCall in response.Value.ToolCalls)
                {
                    if (toolCall is ChatToolCall functionCall)
                    {
                        var tool = _toolRegistry.GetTool(functionCall.FunctionName);
                        if (tool != null)
                        {
                            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(functionCall.FunctionArguments) ?? new();
                            var toolResult = await tool.ExecuteAsync(parameters);

                            // Ask the LLM to format into a single fenced Markdown block per the rules above
                            var followUp = new List<ChatMessage>
                            {
                                new SystemChatMessage(@"You will receive JSON containing box league positions. Convert it into ONE ```markdown fenced block containing multiple sections:
For each box: '### Box <name>' and a GFM table with headers: Rank | Player | Pld | W | D | L | Pts. Use alignment markers to right‑align numbers and left‑align text. Pad the Player column to a minimum width of 32 characters or the longest displayed name, whichever is larger (do not truncate), so numeric columns stay aligned. Do not output extra ASCII grid lines (no repeated dashes/vertical rules beyond the single header separator). Keep rank order and include ALL rows (no truncation). Trim extra spaces in names; if a name is all lowercase, title case it. Bold the entire row for player name 'R Cunniffe' (case‑insensitive) by bolding each cell. Do NOT compute missing stats; leave W/D/L blank if absent. Return only the fenced Markdown block."),
                                new UserChatMessage($"Box positions JSON: {toolResult}")
                            };
                            var final = await _chatClient.CompleteChatAsync(followUp);
                            return final.Value.Content[0].Text ?? toolResult;
                        }
                    }
                }
            }

            return response.Value.Content[0].Text ?? "Unable to retrieve box positions right now.";
        }

        // Retained for reference; no longer used because the LLM now formats the output as HTML tables.
        private static string? TryFormatBoxPositions(string json) => null;

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


