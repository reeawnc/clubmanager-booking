using Azure.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Primary orchestrator agent that routes user prompts to appropriate domain-specific sub-agents.
    /// Uses a hybrid routing approach: keyword-based matching first, then LLM-based routing as fallback.
    /// </summary>
    public class PrimaryAgent
    {
        private readonly OpenAIClient _openAIClient;
        private readonly RoutingLLM _routingLLM;
        private readonly Dictionary<string, IAgent> _agents;

        public PrimaryAgent(OpenAIClient openAIClient)
        {
            _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
            _routingLLM = new RoutingLLM(openAIClient);
            _agents = new Dictionary<string, IAgent>();
            
            RegisterAgents();
        }

        /// <summary>
        /// Main entry point for handling user prompts. Routes to appropriate sub-agent.
        /// </summary>
        /// <param name="prompt">The user's prompt</param>
        /// <param name="userId">Optional user identifier</param>
        /// <param name="sessionId">Optional session identifier</param>
        /// <returns>Response from the appropriate sub-agent</returns>
        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            try
            {
                // Step 1: Try keyword-based routing first
                var agentRole = GetAgentRoleByKeywords(prompt);
                
                // Step 2: If no clear keyword match, use LLM routing
                if (agentRole == null)
                {
                    agentRole = await _routingLLM.GetAgentRoleAsync(prompt);
                }
                
                // Step 3: Route to the appropriate agent
                if (_agents.TryGetValue(agentRole, out var agent))
                {
                    return await agent.HandleAsync(prompt, userId, sessionId);
                }
                
                // Step 4: Fallback to court availability agent if no specific agent found
                return await _agents["court_availability"].HandleAsync(prompt, userId, sessionId);
            }
            catch (Exception ex)
            {
                return $"I apologize, but I encountered an error while processing your request: {ex.Message}. Please try rephrasing your question.";
            }
        }

        /// <summary>
        /// Keyword-based routing using simple pattern matching.
        /// Returns null if no clear match is found.
        /// </summary>
        private static string? GetAgentRoleByKeywords(string prompt)
        {
            var lowercasePrompt = prompt.ToLowerInvariant();
            
            // Booking keywords
            if ((lowercasePrompt.Contains("book") || lowercasePrompt.Contains("reserve") || lowercasePrompt.Contains("schedule")) 
                && lowercasePrompt.Contains("court"))
            {
                return "booking";
            }
            
            // Cancellation keywords
            if (lowercasePrompt.Contains("cancel") || lowercasePrompt.Contains("delete") || lowercasePrompt.Contains("remove"))
            {
                return "cancellation";
            }
            
            // Court availability keywords
            if ((lowercasePrompt.Contains("court") || lowercasePrompt.Contains("courts")) 
                && (lowercasePrompt.Contains("available") || lowercasePrompt.Contains("availability") 
                    || lowercasePrompt.Contains("free") || lowercasePrompt.Contains("open")
                    || lowercasePrompt.Contains("schedule") || lowercasePrompt.Contains("timetable")))
            {
                return "court_availability";
            }
            
            // Who's playing queries
            if ((lowercasePrompt.Contains("who") || lowercasePrompt.Contains("whos")) 
                && lowercasePrompt.Contains("playing"))
            {
                return "court_availability";
            }
            
            // Stats keywords
            if (lowercasePrompt.Contains("stats") || lowercasePrompt.Contains("statistics") 
                || lowercasePrompt.Contains("report") || lowercasePrompt.Contains("analytics"))
            {
                return "stats";
            }
            
            // No clear keyword match found
            return null;
        }

        /// <summary>
        /// Register all available sub-agents
        /// </summary>
        private void RegisterAgents()
        {
            _agents["court_availability"] = new CourtAvailabilityAgent(_openAIClient);
            _agents["booking"] = new BookingAgent(_openAIClient);
            _agents["cancellation"] = new CancellationAgent(_openAIClient);
            
            // Note: StatsAgent not implemented yet, but can be added here when ready
            // _agents["stats"] = new StatsAgent(_openAIClient);
        }

        /// <summary>
        /// Get information about available agents (useful for debugging/admin)
        /// </summary>
        public Dictionary<string, string> GetAvailableAgents()
        {
            var agentInfo = new Dictionary<string, string>();
            
            foreach (var kvp in _agents)
            {
                agentInfo[kvp.Key] = kvp.Value.Description;
            }
            
            return agentInfo;
        }
    }
}