using Azure.AI.OpenAI;
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
        private readonly OpenAIClient _openAIClient;
        private readonly ToolRegistry _toolRegistry;

        public string Name => "court_availability";
        public string Description => "Handles queries about court availability, schedules, and who's playing";

        private string GetSystemPrompt()
        {
            var today = DateTime.Now;
            var formattedDate = today.ToString("dddd, MMMM d, yyyy"); // e.g., "Monday, January 15, 2024"
            
            return $@"You are a helpful assistant specializing in squash court availability. 
Your role is to help users find available court times and understand current bookings.

Today's date is: {formattedDate}

When responding about court availability:
- Always provide clear, organized information
- Use the get_court_availability tool to fetch current data
- ensure you pass a real date, if they day today get todays date.
- The data should always include the year, month and day.
- Format responses in a structured way that's easy to read
- Include both available slots and current bookings
- Be friendly and helpful in your tone

You have access to tools that can fetch real-time court availability data.";
        }

        public CourtAvailabilityAgent(OpenAIClient openAIClient)
        {
            _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
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
                if (response.Choices[0].Message.ToolCalls?.Count > 0)
                {
                    var toolResults = new List<string>();
                    var toolCalls = new List<ToolCall>();
                    
                    foreach (var toolCall in response.Choices[0].Message.ToolCalls)
                    {
                        if (toolCall is ChatCompletionsFunctionToolCall functionCall)
                        {
                            var tool = _toolRegistry.GetTool(functionCall.Name);
                            if (tool != null)
                            {
                                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    functionCall.Arguments) ?? new Dictionary<string, object>();
                                
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
                        
                        // Add court availability specific formatting instructions
                        var hasCourtData = toolCalls.Any(tc => tc.ToolName == "get_court_availability");
                        if (hasCourtData)
                        {
                            followUpPrompt += GetCourtAvailabilityFormattingInstructions();
                        }
                        
                        followUpPrompt += "Please provide a helpful response to the user based on this information.";
                        
                        // Make final call to format the response
                        var finalResponse = await CallOpenAIAsync(followUpPrompt, new List<ChatCompletionsToolDefinition>());
                        return finalResponse.Choices[0].Message.Content ?? "I apologize, but I couldn't process your request.";
                    }
                }
                
                return response.Choices[0].Message.Content ?? "I apologize, but I couldn't process your request.";
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

        private List<ChatCompletionsToolDefinition> CreateOpenAITools()
        {
            var tools = new List<ChatCompletionsToolDefinition>();
            
            foreach (var tool in _toolRegistry.GetAllTools())
            {
                var toolDefinition = new ChatCompletionsFunctionToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = BinaryData.FromObjectAsJson(tool.Parameters)
                };
                
                tools.Add(toolDefinition);
            }
            
            return tools;
        }

        private async Task<ChatCompletions> CallOpenAIAsync(string prompt, List<ChatCompletionsToolDefinition> tools)
        {
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = "gpt-4o-mini"
            };
            
            chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(GetSystemPrompt()));
            chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(prompt));
            
            if (tools.Any())
            {
                foreach (var tool in tools)
                {
                    chatCompletionsOptions.Tools.Add(tool);
                }
                chatCompletionsOptions.ToolChoice = ChatCompletionsToolChoice.Auto;
            }
            
            return await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
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

";
        }
    }
}