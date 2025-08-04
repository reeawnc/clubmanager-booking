using System.Threading.Tasks;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Interface for all agents in the multi-agent architecture.
    /// Each agent handles specific domain requests and returns responses to users.
    /// </summary>
    public interface IAgent
    {
        /// <summary>
        /// The unique name/identifier for this agent
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// A brief description of what this agent handles
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Handle a user prompt and return a response
        /// </summary>
        /// <param name="prompt">The user's prompt/request</param>
        /// <param name="userId">Optional user identifier</param>
        /// <param name="sessionId">Optional session identifier</param>
        /// <returns>A formatted response for the user</returns>
        Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null);
    }
}