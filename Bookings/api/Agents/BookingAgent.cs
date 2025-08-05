using OpenAI;
using OpenAI.Chat;
using BookingsApi.Tools;
using BookingsApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;

namespace BookingsApi.Agents
{
    /// <summary>
    /// Agent responsible for handling court booking requests.
    /// This is a placeholder implementation that can be extended with actual booking functionality.
    /// </summary>
    public class BookingAgent : IAgent
    {
        private readonly ChatClient _chatClient;
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
            _chatClient = openAIClient?.GetChatClient("gpt-4o-mini") ?? throw new ArgumentNullException(nameof(openAIClient));
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
                
                // Create chat messages
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(GetSystemPrompt()),
                    new UserChatMessage(prompt)
                };
                
                // Initial call to OpenAI with tools
                var options = new ChatCompletionOptions();
                if (tools.Any())
                {
                    foreach (var tool in tools)
                    {
                        options.Tools.Add(tool);
                    }
                }
                
                var response = await _chatClient.CompleteChatAsync(messages, options);
                
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
                    
                    // Create follow-up prompt with tool results
                    if (toolResults.Any())
                    {
                        var followUpPrompt = $"Based on this user request: \"{prompt}\"\n\nI have gathered the following information:\n{string.Join("\n", toolResults)}\n\nPlease provide a helpful response to the user based on this information.";
                        
                        // Make final call to format the response
                        var finalMessages = new List<ChatMessage>
                        {
                            new SystemChatMessage(GetSystemPrompt()),
                            new UserChatMessage(followUpPrompt)
                        };
                        
                        var finalResponse = await _chatClient.CompleteChatAsync(finalMessages);
                        var finalResult = finalResponse.Value.Content[0].Text ?? "I apologize, but I couldn't process your request.";
                        
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
                
                return response.Value.Content[0].Text ?? "I apologize, but I couldn't process your request.";
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
    }
}