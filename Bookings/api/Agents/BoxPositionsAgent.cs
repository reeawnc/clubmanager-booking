using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BookingsApi.Tools;

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
                new SystemChatMessage("If the user references a group, pass it as groupId to get_box_positions. Summarize positions in a short table-like list."),
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

                            var followUp = new List<ChatMessage>
                            {
                                new SystemChatMessage("Summarize positions: show position, player, played, points. If multiple boxes, identify the box name."),
                                new UserChatMessage($"Box positions data: {toolResult}")
                            };
                            var final = await _chatClient.CompleteChatAsync(followUp);
                            return final.Value.Content[0].Text ?? toolResult;
                        }
                    }
                }
            }

            return response.Value.Content[0].Text ?? "Unable to retrieve box positions right now.";
        }
    }
}


