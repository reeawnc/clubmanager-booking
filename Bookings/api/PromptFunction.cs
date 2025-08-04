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
using Sentry; // Add Sentry using

namespace BookingsApi
{
    public class PromptFunction
    {
        private readonly PrimaryAgent _primaryAgent;
        
        public PromptFunction()
        {
            // Initialize Sentry
            SentrySdk.Init(options =>
            {
                options.Dsn = "https://5b17f5890cb87b20cb1558c7854bc9ab04599786036018944.ingest.de.sentry.io/4509799";
                options.Debug = true; // Enable in development
                options.TracesSampleRate = 1.0; // Capture 100% of transactions for performance monitoring
                options.Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development";
                
            });

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
                // Add Sentry context and verify step
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("function", "PromptFunction");
                    scope.SetTag("method", req.Method);
                    scope.SetExtra("user_agent", req.Headers["User-Agent"].ToString());
                    scope.SetExtra("content_type", req.ContentType);
                });

                // Sentry verify step - test message
                SentrySdk.CaptureMessage("Hello Sentry from PromptFunction!");

                // Parse the request
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                // Add debug logging
                if (string.IsNullOrEmpty(requestBody))
                {
                    var emptyBodyError = new BadRequestObjectResult(new PromptResponse
                    {
                        Success = false,
                        ErrorMessage = "Request body is empty"
                    });
                    
                    // Log to Sentry
                    SentrySdk.CaptureMessage("Request body is empty", SentryLevel.Warning);
                    return emptyBodyError;
                }
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var promptRequest = JsonSerializer.Deserialize<PromptRequest>(requestBody, options);
                
                if (promptRequest == null || string.IsNullOrEmpty(promptRequest.Prompt))
                {
                    var invalidRequestError = new BadRequestObjectResult(new PromptResponse
                    {
                        Success = false,
                        ErrorMessage = $"Invalid request: Prompt is required. Received body: {requestBody}"
                    });
                    
                    // Log to Sentry with context
                    SentrySdk.ConfigureScope(scope =>
                    {
                        scope.SetExtra("request_body", requestBody);
                    });
                    SentrySdk.CaptureMessage("Invalid request: Prompt is required", SentryLevel.Warning);
                    
                    return invalidRequestError;
                }
                
                // Add prompt context to Sentry
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetExtra("prompt_length", promptRequest.Prompt.Length);
                    scope.SetExtra("user_id", promptRequest.UserId ?? "anonymous");
                    scope.SetExtra("session_id", promptRequest.SessionId ?? "no_session");
                });
                
                // Use the PrimaryAgent to handle the request
                var agentResponse = await _primaryAgent.HandleAsync(
                    promptRequest.Prompt, 
                    promptRequest.UserId, 
                    promptRequest.SessionId);
                
                // Log successful processing
                SentrySdk.AddBreadcrumb("Agent response generated successfully");
                
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
                        ["toolCallsMade"] = toolCalls.Count,
                        ["sentryEnabled"] = true
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
                // Capture exception to Sentry with full context
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("error_type", ex.GetType().Name);
                    scope.SetFingerprint(new[] { "prompt-function-error", ex.GetType().Name });
                });
                
                SentrySdk.CaptureException(ex);
                
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
