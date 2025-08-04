using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Azure.AI.OpenAI;
using BookingsApi.Models;
using BookingsApi.Agents;
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
        private readonly PrimaryAgent _primaryAgent;
        
        public PromptFunction()
        {
            // Get OpenAI API key from environment variable
            var apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI_API_Key environment variable is not set");
            }
            
            var openAIClient = new OpenAIClient(apiKey);
            _primaryAgent = new PrimaryAgent(openAIClient);
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
                
                // Use the PrimaryAgent to handle the request
                var agentResponse = await _primaryAgent.HandleAsync(
                    promptRequest.Prompt, 
                    promptRequest.UserId, 
                    promptRequest.SessionId);
                
                // Note: For now, we don't return detailed tool call information
                // This could be enhanced by modifying the agent interface to return structured data
                var toolCalls = new System.Collections.Generic.List<ToolCall>();
                
                var result = new OkObjectResult(new PromptResponse
                {
                    Success = true,
                    Response = agentResponse,
                    SessionId = promptRequest.SessionId,
                    ToolCalls = toolCalls.Any() ? toolCalls : null,
                    Metadata = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["agentArchitecture"] = "multi-agent",
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

    }
}