using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Azure.AI.OpenAI;
using BookingsApi.Models;
using BookingsApi.Tools;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;

namespace BookingsApi
{
    public class PromptFunction
    {
        private readonly ToolRegistry _toolRegistry;
        private readonly OpenAIClient _openAIClient;
        
        public PromptFunction()
        {
            _toolRegistry = new ToolRegistry();
            
            // Get OpenAI API key from environment variable
            var apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            Console.WriteLine($"API Key found: {!string.IsNullOrEmpty(apiKey)}");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI_API_Key environment variable is not set");
            }
            
            _openAIClient = new OpenAIClient(apiKey);
        }
        
        [FunctionName("PromptFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "PromptFunction")] HttpRequest req)
        {
            // Handle CORS preflight
            if (req.Method == "OPTIONS")
            {
                var corsResponse = new OkResult();
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                return corsResponse;
            }

            try
            {
                // Parse the request
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                // Add debug logging
                if (string.IsNullOrEmpty(requestBody))
                {
                    return new BadRequestObjectResult(new PromptResponse
                    {
                        Success = false,
                        ErrorMessage = "Request body is empty"
                    });
                }
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var promptRequest = JsonSerializer.Deserialize<PromptRequest>(requestBody, options);
                
                if (promptRequest == null || string.IsNullOrEmpty(promptRequest.Prompt))
                {
                    return new BadRequestObjectResult(new PromptResponse
                    {
                        Success = false,
                        ErrorMessage = $"Invalid request: Prompt is required. Received body: {requestBody}"
                    });
                }
                
                // Create tools for OpenAI
                var tools = CreateOpenAITools();
                
                // Call OpenAI API
                Console.WriteLine($"=== INITIAL PROMPT ===");
                Console.WriteLine($"Prompt: {promptRequest.Prompt}");
                Console.WriteLine($"=== END INITIAL PROMPT ===");
                
                var response = await CallOpenAIAsync(promptRequest.Prompt, tools);
                
                // Process tool calls and get AI response with tool results
                var toolCalls = new List<ToolCall>();
                var finalResponse = response;
                
                if (response.Choices[0].Message.ToolCalls?.Count > 0)
                {
                    var toolResults = new List<string>();
                    
                    foreach (var toolCall in response.Choices[0].Message.ToolCalls)
                    {
                        if (toolCall is ChatCompletionsFunctionToolCall functionCall)
                        {
                            var tool = _toolRegistry.GetTool(functionCall.Name);
                            if (tool != null)
                            {
                                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    functionCall.Arguments) ?? new Dictionary<string, object>();
                                
                                Console.WriteLine($"=== TOOL CALL START ===");
                                Console.WriteLine($"Tool: {tool.Name}");
                                Console.WriteLine($"Parameters: {JsonSerializer.Serialize(parameters)}");
                                
                                var toolResult = await tool.ExecuteAsync(parameters);
                                
                                Console.WriteLine($"Tool Result: {toolResult}");
                                Console.WriteLine($"=== TOOL CALL END ===");
                                
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
                    
                    // Create a follow-up prompt with tool results
                    if (toolResults.Any())
                    {
                        var followUpPrompt = $"Based on this user request: \"{promptRequest.Prompt}\"\n\nI have gathered the following information:\n{string.Join("\n", toolResults)}\n\n";
                        
                        Console.WriteLine($"=== FOLLOW-UP PROMPT ===");
                        Console.WriteLine($"Follow-up prompt: {followUpPrompt}");
                        Console.WriteLine($"=== END FOLLOW-UP PROMPT ===");
                        
                        // Add specific formatting instructions for court availability
                        var hasCourtData = toolCalls.Any(tc => tc.ToolName == "get_court_availability");
                        if (hasCourtData)
                        {
                            followUpPrompt += @"Format your response EXACTLY like this structure:

[Brief intro sentence with day and date]

### Booked Slots:
- **Court 1:**
  - **HH:MM - HH:MM**: Player Name
  - **HH:MM - HH:MM**: Player Name

- **Court 2:**
  - **HH:MM - HH:MM**: Player Name
  - **HH:MM - HH:MM**: Training

- **Court 3:**
  - **HH:MM - HH:MM**: Player Name

### Available Slots:
- **Court 1:**
  - **HH:MM - HH:MM**: Bookable slot
  - **HH:MM - HH:MM**: Bookable slot

- **Court 2:**
  - **HH:MM - HH:MM**: Bookable slot

- **Court 3:**
  - **HH:MM - HH:MM**: Bookable slot
  - **HH:MM - HH:MM**: Bookable slot

RULES:
- Always use ""### Booked Slots:"" and ""### Available Slots:""
- Always separate slots by court (Court 1, Court 2, Court 3) - do not mix courts together
- Always use ""- **Court X:**"" format for court headers
- Always use ""  - **HH:MM - HH:MM**: "" for time slots (note the 2 spaces)
- Use ""Bookable slot"" for available times
- Use ""Training"" for training sessions
- Use actual player names for booked slots
- Always include day and date in intro
- Each court should be listed separately with its own time slots
- Do not combine or mix time slots from different courts

";
                        }
                        
                        followUpPrompt += "Please provide a helpful response to the user based on this information.";
                        
                        var finalOptions = new ChatCompletionsOptions
                        {
                            DeploymentName = "gpt-4o-mini"
                        };
                        
                        finalOptions.Messages.Add(new ChatRequestUserMessage(followUpPrompt));
                        
                        finalResponse = await _openAIClient.GetChatCompletionsAsync(finalOptions);
                        
                        Console.WriteLine($"=== FINAL LLM RESPONSE ===");
                        Console.WriteLine($"Response: {finalResponse.Choices[0].Message.Content}");
                        Console.WriteLine($"=== END FINAL LLM RESPONSE ===");
                    }
                }
                
                var result = new OkObjectResult(new PromptResponse
                {
                    Success = true,
                    Response = finalResponse.Choices[0].Message.Content ?? "",
                    SessionId = promptRequest.SessionId,
                    ToolCalls = toolCalls.Any() ? toolCalls : null,
                    Metadata = new Dictionary<string, object>
                    {
                        ["usage"] = finalResponse.Usage,
                        ["toolCallsMade"] = toolCalls.Count
                    }
                });

                // Add CORS headers
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                return result;
            }
            catch (Exception ex)
            {
                var errorResponse = new ObjectResult(new PromptResponse
                {
                    Success = false,
                    ErrorMessage = $"An error occurred: {ex.Message}"
                })
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };

                // Add CORS headers to error response
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                return errorResponse;
            }
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