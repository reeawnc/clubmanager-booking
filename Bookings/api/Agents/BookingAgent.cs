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
    /// Agent responsible for handling court booking requests.
    /// This is a placeholder implementation that can be extended with actual booking functionality.
    /// </summary>
    public class BookingAgent : IAgent
    {
        private readonly OpenAIClient _openAIClient;
        private readonly ToolRegistry _toolRegistry;

        public string Name => "booking";
        public string Description => "Handles court booking and reservation requests";

        private string GetSystemPrompt()
        {
            var today = DateTime.Now;
            var formattedDate = today.ToString("dddd, MMMM d, yyyy"); // e.g., "Monday, January 15, 2024"
            
            return $@"You are a helpful assistant specializing in squash court bookings. 
Your role is to help users book and reserve court times.

Today's date is: {formattedDate}

You have access to tools that can:
- Check court availability (get_court_availability)
- Make actual bookings (book_court)

IMPORTANT: When a user wants to book a court, you MUST automatically complete the booking:
1. Use the book_court tool directly - it will check availability AND make the booking in one step
2. Do NOT ask for confirmation - proceed with booking immediately
3. The book_court tool handles everything automatically

NEVER just check availability and ask for confirmation. Always complete the booking when requested.

CRITICAL: When calling get_court_availability, always pass the date in format 'dd MMM yy' (e.g., '15 Jan 25')
- If user asks about 'today', pass today's date in 'dd MMM yy' format
- If user asks about 'tomorrow', pass tomorrow's date in 'dd MMM yy' format
- If user asks about a specific date, convert it to 'dd MMM yy' format
- The get_court_availability tool requires this exact date format to work properly

For book_court tool:
- Pass time in 'HH:MM' format (e.g., '18:00' for 6pm)
- Pass date in 'dd MMM yy' format (e.g., '15 Jan 25')
- if a user requests a time like 6pm it means 18:00 - 18:45 not 17:15 - 18:00
- The tool will automatically choose the best available court (Court 1 → Court 2 → Court 3)

When a user asks to book a court, use the book_court tool immediately. Do not use get_court_availability unless the user specifically asks to only check availability.

Always be helpful and provide clear information about the booking process.";
        }

        public BookingAgent(OpenAIClient openAIClient)
        {
            _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
            _toolRegistry = new ToolRegistry();
            
            // Register tools this agent needs
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
                    
                    // Create follow-up prompt with tool results
                    if (toolResults.Any())
                    {
                        var followUpPrompt = $"Based on this user request: \"{prompt}\"\n\nI have gathered the following information:\n{string.Join("\n", toolResults)}\n\nPlease provide a helpful response to the user based on this information.";
                        
                        // Make final call to format the response
                        var finalResponse = await CallOpenAIAsync(followUpPrompt, new List<ChatCompletionsToolDefinition>());
                        var finalResult = finalResponse.Choices[0].Message.Content ?? "I apologize, but I couldn't process your request.";
                        
                        // Add debug information with tool results
                        var debugInfo = "\n\n=== DEBUG INFO ===\n";
                        foreach (var toolCall in toolCalls)
                        {
                            debugInfo += $"Tool: {toolCall.ToolName}\n";
                            debugInfo += $"Parameters: {System.Text.Json.JsonSerializer.Serialize(toolCall.Parameters)}\n";
                            debugInfo += $"Result: {toolCall.Result}\n";
                            debugInfo += "---\n";
                        }
                        debugInfo += "=== END DEBUG ===";
                        
                        return finalResult + debugInfo;
                    }
                }
                
                return response.Choices[0].Message.Content ?? "I apologize, but I couldn't process your request.";
            }
            catch (Exception ex)
            {
                return $"I'm sorry, but I encountered an error while processing your booking request: {ex.Message}. Please try contacting the club directly.";
            }
        }

        private void RegisterAgentTools()
        {
            // Register tools relevant to booking
            _toolRegistry.RegisterTool(new GetCourtAvailabilityTool());
            _toolRegistry.RegisterTool(new BookCourtTool());
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
    }
}