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

When formatting the answer:
- Use clear sections per box with '### Box <Name>' headings.
- Show the TOP 10 rows only by default for readability; if more, end with '… and N more'.
- For each row print: '<rank>. <Player> — Pld <Pld>, Pts <Pts>'.
- Include a one-line summary at the top: 'Showing current positions for <Group>'.
- Keep it concise and readable for humans; avoid walls of text.
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
                            // Deterministic formatting to avoid raw JSON appearing in UI
                            var formatted = TryFormatBoxPositions(toolResult);
                            if (!string.IsNullOrWhiteSpace(formatted))
                            {
                                return formatted!;
                            }

                            // Fallback to an LLM formatting attempt if parsing failed
                            var followUp = new List<ChatMessage>
                            {
                                new SystemChatMessage("Format the following JSON positions into human-readable text with: headings per box, top 10 rows, and '… and N more'. NEVER return JSON or a code block; return plain text only."),
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

        private static string? TryFormatBoxPositions(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Boxes", out var boxes) || boxes.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Showing current positions");

                foreach (var box in boxes.EnumerateArray())
                {
                    var name = box.TryGetProperty("Name", out var n) ? n.GetString() : "Box";
                    sb.AppendLine($"\n### {name}");

                    if (!box.TryGetProperty("Positions", out var positions) || positions.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    int count = 0;
                    int total = positions.GetArrayLength();
                    foreach (var pos in positions.EnumerateArray())
                    {
                        if (count >= 10) break;
                        var rank = pos.TryGetProperty("Pos", out var pPos) ? pPos.GetInt32() : count + 1;
                        var player = pos.TryGetProperty("Plyr", out var pPlyr) ? pPlyr.GetString() : "Unknown";
                        var played = pos.TryGetProperty("Pld", out var pPld) ? SafeGetInt(pPld) : 0;
                        var points = pos.TryGetProperty("Pts", out var pPts) ? SafeGetInt(pPts) : 0;
                        sb.AppendLine($"{rank}. {player} — Pld {played}, Pts {points}");
                        count++;
                    }
                    if (total > count)
                    {
                        sb.AppendLine($"… and {total - count} more");
                    }
                }

                return sb.ToString().TrimEnd();
            }
            catch
            {
                return null;
            }
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


