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
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PromptFunction constructor started");
                
                // Initialize Sentry
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Initializing Sentry");
                SentrySdk.Init(options =>
                {
                    options.Dsn = "https://5b17f5890cb87b20cb1558c7854bc9ab04599786036018944.ingest.de.sentry.io/4509799";
                    options.Debug = true; // Enable in development
                    options.TracesSampleRate = 1.0; // Capture 100% of transactions for performance monitoring
                    options.Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development";
                });

                // Add some useful tags using ConfigureScope
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("service", "squash-court-booking-api");
                    scope.SetTag("version", "1.0.0");
                });

                // Get OpenAI API key from environment variable
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Getting OpenAI API key");
                var apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: OpenAI_API_Key environment variable is not set");
                    throw new InvalidOperationException("OpenAI_API_Key environment variable is not set");
                }
                
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Creating OpenAIClient");
                var openAIClient = new OpenAIClient(apiKey);
                
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Creating PrimaryAgent");
                _primaryAgent = new PrimaryAgent(openAIClient);
                
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PromptFunction constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] CONSTRUCTOR EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] CONSTRUCTOR STACK TRACE: {ex.StackTrace}");
                throw;
            }
        }
        
        [FunctionName("PromptFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "PromptFunction")] HttpRequest req)
        {
            try
            {
                // Add detailed logging for debugging
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PromptFunction called - Method: {req.Method}");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Headers: {string.Join(", ", req.Headers.Select(h => $"{h.Key}={h.Value}"))}");
                
                // Handle CORS preflight
                if (req.Method == "OPTIONS")
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] CORS preflight request handled");
                    var corsResponse = new OkResult();
                    req.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    req.HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                    req.HttpContext.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                    return corsResponse;
                }

                try
                {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Processing POST request");
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
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Reading request body");
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Request body length: {requestBody?.Length ?? 0}");
                
                // Add debug logging
                if (string.IsNullOrEmpty(requestBody))
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Request body is empty");
                    var emptyBodyError = new BadRequestObjectResult(new PromptResponse
                    {
                        Success = false,
                        ErrorMessage = "Request body is empty"
                    });
                    
                    // Log to Sentry
                    SentrySdk.CaptureMessage("Request body is empty", SentryLevel.Warning);
                    return emptyBodyError;
                }
                
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Deserializing request body");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var promptRequest = JsonSerializer.Deserialize<PromptRequest>(requestBody, options);
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Deserialization complete - PromptRequest: {(promptRequest != null ? "Success" : "Null")}");
                
                if (promptRequest == null || string.IsNullOrEmpty(promptRequest.Prompt))
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: Invalid request - Prompt is required. Body: {requestBody}");
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
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Calling PrimaryAgent.HandleAsync");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Prompt: {promptRequest.Prompt?.Substring(0, Math.Min(100, promptRequest.Prompt?.Length ?? 0))}...");
                
                var agentResponse = await _primaryAgent.HandleAsync(
                    promptRequest.Prompt, 
                    promptRequest.UserId, 
                    promptRequest.SessionId);
                
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PrimaryAgent response received - Length: {agentResponse?.Length ?? 0}");
                
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
                // Log detailed exception information
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] INNER EXCEPTION: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                
                // Capture exception to Sentry with full context
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("error_type", ex.GetType().Name);
                    scope.SetFingerprint(new[] { "prompt-function-error", ex.GetType().Name });
                });
                
                SentrySdk.CaptureException(ex);
                
                // Create detailed error response with full exception information
                var detailedErrorMessage = $"Exception Type: {ex.GetType().Name}\nMessage: {ex.Message}\nStack Trace: {ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    detailedErrorMessage += $"\nInner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}";
                }
                
                var errorResponse = new ObjectResult(new PromptResponse
                {
                    Success = false,
                    ErrorMessage = detailedErrorMessage
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
            catch (Exception outerEx)
            {
                // Catch any exceptions that might occur during function initialization or outer scope
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] OUTER EXCEPTION: {outerEx.GetType().Name}: {outerEx.Message}");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] OUTER STACK TRACE: {outerEx.StackTrace}");
                
                var outerErrorResponse = new ObjectResult(new PromptResponse
                {
                    Success = false,
                    ErrorMessage = $"Function initialization error: {outerEx.GetType().Name} - {outerEx.Message}\nStack Trace: {outerEx.StackTrace}"
                })
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };

                // Add CORS headers to error response
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                req.HttpContext.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                return outerErrorResponse;
            }
        }
    }
}