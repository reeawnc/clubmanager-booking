using Azure.AI.OpenAI;
using BookingsApi.Tools;
using System;
using System.Threading.Tasks;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Agent responsible for handling court booking requests.
    /// This is a placeholder implementation that can be extended with actual booking functionality.
    /// </summary>
    public class BookingAgent : IAgent
    {
        private readonly OpenAIClient _openAIClient;

        public string Name => "booking";
        public string Description => "Handles court booking and reservation requests";

        private const string SYSTEM_PROMPT = @"You are a helpful assistant specializing in squash court bookings. 
Your role is to help users book and reserve court times.

Currently, the booking system is under development. For now:
- Acknowledge the user's booking request
- Explain that direct booking through this interface is not yet available
- Suggest alternative ways they can make bookings (e.g., calling the club, using the main website)
- Be apologetic but helpful in providing alternatives

In the future, you will have access to tools that can:
- Check court availability
- Make actual bookings
- Send confirmation emails
- Handle payment processing";

        public BookingAgent(OpenAIClient openAIClient)
        {
            _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            try
            {
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = "gpt-4o-mini"
                };

                chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(SYSTEM_PROMPT));
                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(prompt));

                var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                return response.Value.Choices[0].Message.Content ?? "I apologize, but I couldn't process your booking request at this time.";
            }
            catch (Exception ex)
            {
                return $"I'm sorry, but I encountered an error while processing your booking request: {ex.Message}. Please try contacting the club directly.";
            }
        }
    }
}