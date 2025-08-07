using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BookingsApi.Tools;

namespace BookingsApi.Agents
{
    public class MessagesAgent : IAgent
    {
        private readonly ChatClient _chatClient;
        private readonly ToolRegistry _toolRegistry;

        public string Name => "messages";
        public string Description => "Handles user messaging queries: inbox, sent, and unread checks.";

        public MessagesAgent(OpenAIClient openAIClient)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = new ToolRegistry();
            _toolRegistry.RegisterTool(new GetUserHasMessagesTool());
            _toolRegistry.RegisterTool(new GetUserMessagesTool());
            _toolRegistry.RegisterTool(new GetSentUserMessagesTool());
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("Use the appropriate messaging tool to answer questions about messages. If user asks if they have messages, use get_user_has_messages. For inbox, use get_user_messages. For sent items, use get_sent_user_messages. Summarize clearly."),
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
                                new SystemChatMessage("Summarize these messages. Show sender, subject, and date succinctly. If none, state that clearly."),
                                new UserChatMessage($"Messages data: {toolResult}")
                            };
                            var final = await _chatClient.CompleteChatAsync(followUp);
                            return final.Value.Content[0].Text ?? toolResult;
                        }
                    }
                }
            }

            return response.Value.Content[0].Text ?? "Unable to retrieve messages right now.";
        }
    }
}


