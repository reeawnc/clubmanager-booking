using OpenAI;
using OpenAI.Chat;
using BookingsApi.Tools;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Agent responsible for handling booking cancellation requests.
    /// This is a placeholder implementation that can be extended with actual cancellation functionality.
    /// </summary>
    public class CancellationAgent : IAgent
    {
        private readonly ChatClient _chatClient;

        public string Name => "cancellation";
        public string Description => "Handles booking cancellation and modification requests";

        private const string SYSTEM_PROMPT = @"You are a helpful assistant specializing in squash court booking cancellations. 
Your role is to help users cancel or modify their existing bookings.

Currently, the cancellation system is under development. For now:
- Acknowledge the user's cancellation request
- Explain that direct cancellation through this interface is not yet available
- Suggest alternative ways they can cancel bookings (e.g., calling the club, using the main website)
- Be helpful in providing cancellation policies and contact information
- Ask for booking details if needed (date, time, court) to help them when they contact the club

In the future, you will have access to tools that can:
- Look up existing bookings
- Cancel bookings directly
- Send cancellation confirmations
- Handle refund processing
- Modify booking times";

        public CancellationAgent(OpenAIClient openAIClient)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            try
            {
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(SYSTEM_PROMPT),
                    new UserChatMessage(prompt)
                };

                var response = await _chatClient.CompleteChatAsync(messages);
                return response.Value.Content[0].Text ?? "I apologize, but I couldn't process your cancellation request at this time.";
            }
            catch (Exception ex)
            {
                return $"I'm sorry, but I encountered an error while processing your cancellation request: {ex.Message}. Please try contacting the club directly.";
            }
        }
    }
}