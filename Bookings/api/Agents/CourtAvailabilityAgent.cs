using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using BookingsApi.Tools;
using BookingsApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Agent responsible for handling court availability queries.
    /// Uses the GetCourtAvailabilityTool and formats responses in a user-friendly way.
    /// </summary>
    public class CourtAvailabilityAgent : IAgent
    {
        private readonly ChatClient _chatClient;
        private readonly ToolRegistry _toolRegistry;

        public string Name => "court_availability";
        public string Description => "Handles queries about court availability, schedules, and who's playing";

        private string GetSystemPrompt()
        {
            var today = DateTime.Now;
            var formattedDate = today.ToString("dddd, MMMM d, yyyy"); // e.g., "Monday, January 15, 2024"
            var currentTime = today.ToString("HH:mm"); // e.g., "14:30"
            
            return $@"You are a helpful assistant specializing in squash court availability. 
Your role is to help users find available court times and understand current bookings.

Current time in Dublin, Ireland: {formattedDate} at {currentTime}

When responding about court availability:
- Always provide clear, organized information
- Use the get_court_availability tool to fetch current data
- ensure you pass a real date, if they day today get todays date.
- The data should always include the year, month and day.
- Format responses in a structured way that's easy to read
- Include both available slots and current bookings
- Be friendly and helpful in your tone

You have access to tools that can fetch real-time court availability data.

{GetCourtAvailabilityFormattingInstructions()}";
        }

        public CourtAvailabilityAgent(OpenAIClient openAIClient)
        {
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = new ToolRegistry();
            
            // Register only the tools this agent needs
            RegisterAgentTools();
        }

        public async Task<string> HandleAsync(string prompt, string? userId = null, string? sessionId = null)
        {
            try
            {
                // Create tools for this agent
                var tools = CreateOpenAITools();
                
                // Initial call to OpenAI with tools
                var response = await CallOpenAIAsync(prompt, tools);
                
                // Process any tool calls
                if (response.Value.ToolCalls?.Count > 0)
                {
                    var toolResults = new List<string>();
                    var toolCalls = new List<ToolCall>();
                    
                    foreach (var toolCall in response.Value.ToolCalls)
                    {
                        if (toolCall is ChatToolCall functionCall)
                        {
                            var tool = _toolRegistry.GetTool(functionCall.FunctionName);
                            if (tool != null)
                            {
                                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    functionCall.FunctionArguments) ?? new Dictionary<string, object>();
                                
                                var toolResult = await tool.ExecuteAsync(parameters);
                                
                                toolCalls.Add(new ToolCall
                                {
                                    ToolName = tool.Name,
                                    Parameters = parameters,
                                    Result = toolResult
                                });
                                
                                toolResults.Add($"{tool.Name}: {toolResult}");
                            }
                        }
                    }
                    
                    // Create follow-up prompt with tool results and specific formatting instructions
                    if (toolResults.Any())
                    {
                        var followUpPrompt = $"Based on this user request: \"{prompt}\"\n\nI have gathered the following information:\n{string.Join("\n", toolResults)}\n\n";
                        
                        followUpPrompt += "Please provide a helpful response to the user based on this information.";
                        
                        // Make final call to format the response
                        var finalResponse = await CallOpenAIAsync(followUpPrompt, new List<ChatTool>());
                        return finalResponse.Value.Content[0].Text ?? "I apologize, but I couldn't process your request.";
                    }
                }
                
                return response.Value.Content[0].Text ?? "I apologize, but I couldn't process your request.";
            }
            catch (Exception ex)
            {
                return $"I'm sorry, but I encountered an error while checking court availability: {ex.Message}";
            }
        }

        private void RegisterAgentTools()
        {
            // Only register tools relevant to court availability
            _toolRegistry.RegisterTool(new GetCourtAvailabilityTool());
            _toolRegistry.RegisterTool(new GetCurrentTimeTool());
        }

        private List<ChatTool> CreateOpenAITools()
        {
            var tools = new List<ChatTool>();
            
            foreach (var tool in _toolRegistry.GetAllTools())
            {
                var toolDefinition = ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description,
                    BinaryData.FromObjectAsJson(tool.Parameters));
                
                tools.Add(toolDefinition);
            }
            
            return tools;
        }

        private async Task<ClientResult<ChatCompletion>> CallOpenAIAsync(string prompt, List<ChatTool> tools)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt()),
                new UserChatMessage(prompt)
            };
            
            var options = new ChatCompletionOptions();
            if (tools.Any())
            {
                foreach (var tool in tools)
                {
                    options.Tools.Add(tool);
                }
            }
            
            return await _chatClient.CompleteChatAsync(messages, options);
        }

        private static string GetCourtAvailabilityFormattingInstructions()
        {
            return @"Format your response with these rules in mind:

[Brief intro sentence with day and date]

[Information]

RULES:
- Always separate slots by court (Court 1, Court 2, Court 3) - do not mix courts together
- Always use ""- **Court X:**"" format for court headers
- Always use ""  - **HH:MM - HH:MM**: "" for time slots (note the 2 spaces)
- Use ""Bookable slot"" for available times
- Use ""Training"" for training sessions
- Use actual player names for booked slots
- Always include day and date in intro
- Each court should be listed separately with its own time slots
- Do not combine or mix time slots from different courts
- 'Player Name' is a placeholder for the actual player name, its not booked
- 'Training' is a placeholder for training sessions
- 'Bookable slot' is a placeholder for available times
- Show both players names if available 
- Show if its a friendly or box league game if available
- If the prompt asks about a specific time or who's playing next, focus only on relevant time slots rather than showing all available slots
";
        }
    }
}