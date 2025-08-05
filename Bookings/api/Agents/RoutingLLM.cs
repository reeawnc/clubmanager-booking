using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookingsApi.Agents
{
    /// <summary>
    /// LLM-based routing service that determines which agent should handle a user prompt
    /// when keyword-based routing fails to find a clear match.
    /// </summary>
    public class RoutingLLM
    {
        private readonly ChatClient _chatClient;
        
        private const string ROUTING_SYSTEM_PROMPT = @"You are a smart prompt router for a squash court booking system. 
Given a user request, respond with ONE of the following agent roles:

- ""court_availability"" - for questions about court availability, schedules, timetables, what's available, who's playing
- ""booking"" - for requests to book, reserve, or schedule courts
- ""cancellation"" - for requests to cancel, remove, or delete bookings
- ""stats"" - for questions about statistics, reports, or analytics

Only respond with the exact role name. Do not include extra explanation or punctuation.
If the request doesn't clearly fit any category, respond with ""court_availability"" as the default.

Examples:
- ""What courts are available today?"" → court_availability
- ""Book me a court at 6pm"" → booking
- ""Cancel my booking tomorrow"" → cancellation
- ""Show me usage statistics"" → stats";

        public RoutingLLM(OpenAIClient openAIClient)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
        }

        /// <summary>
        /// Uses OpenAI to determine which agent should handle the given prompt
        /// </summary>
        /// <param name="prompt">The user's prompt</param>
        /// <returns>The agent role name that should handle this prompt</returns>
        public async Task<string> GetAgentRoleAsync(string prompt)
        {
            try
            {
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(ROUTING_SYSTEM_PROMPT),
                    new UserChatMessage(prompt)
                };

                var options = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 50, // Keep response short
                    Temperature = 0.1f // Low temperature for consistent routing
                };

                var response = await _chatClient.CompleteChatAsync(messages, options);
                var agentRole = response.Value.Content[0].Text?.Trim().ToLowerInvariant();

                // Validate the response and provide fallback
                return ValidateAgentRole(agentRole);
            }
            catch (Exception)
            {
                // If LLM routing fails, default to court availability
                return "court_availability";
            }
        }

        private static string ValidateAgentRole(string? agentRole)
        {
            return agentRole switch
            {
                "court_availability" => "court_availability",
                "booking" => "booking", 
                "cancellation" => "cancellation",
                "stats" => "stats",
                _ => "court_availability" // Default fallback
            };
        }
    }
}