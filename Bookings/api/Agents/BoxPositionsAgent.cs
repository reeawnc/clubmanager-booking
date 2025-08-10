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
- Produce a compact, plain-text table for each box using a fixed-width (monospace) layout inside a SINGLE triple‑backtick code block.
- Start with the line: 'Showing current positions'.
- For each box, print a centered caption like: 'Box A1', then a header row: 'Pos  Player                         Pld  Pts'.
- Align numeric columns right; pad with spaces to align columns.
- Limit to the TOP 10 rows only; if more rows exist add a line '… and N more' after the table for that box.
- Keep to text only (no HTML). One code block wrapping all boxes is fine.
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

                            // Ask the LLM to format into a single plain-text monospaced table in a code block
                            var followUp = new List<ChatMessage>
                            {
                                new SystemChatMessage(@"You will receive JSON containing box league positions.
Transform it into a plain-text, fixed-width table inside one triple‑backtick code block. Include per-box captions, a header row, right-aligned numeric columns, and only the top 10 rows with an '… and N more' line if applicable. Do not return HTML; return only one code block of text."),
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


