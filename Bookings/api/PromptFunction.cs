using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using OpenAI;
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
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] PromptFunction constructor started");
                
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
        
        [Function("PromptFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "PromptFunction")] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger("PromptFunction");
            
            try
            {
                // Add detailed logging for debugging
                logger.LogInformation($"PromptFunction called - Method: {req.Method}");
                logger.LogInformation($"Headers: {string.Join(", ", req.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");
                
                // Handle CORS preflight
                if (req.Method == "OPTIONS")
                {
                    logger.LogInformation("CORS preflight request handled");
                    var corsResponse = req.CreateResponse(HttpStatusCode.OK);
                    corsResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                    corsResponse.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                    corsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                    return corsResponse;
                }

                try
                {
                    logger.LogInformation("Processing POST request");

                    // Parse the request
                    logger.LogInformation("Reading request body");
                    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    logger.LogInformation($"Request body length: {requestBody?.Length ?? 0}");
                    
                    // Add debug logging
                    if (string.IsNullOrEmpty(requestBody))
                    {
                        logger.LogError("Request body is empty");
                        var emptyBodyError = req.CreateResponse(HttpStatusCode.BadRequest);
                        emptyBodyError.Headers.Add("Content-Type", "application/json");
                        emptyBodyError.Headers.Add("Access-Control-Allow-Origin", "*");
                        emptyBodyError.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                        emptyBodyError.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                        
                        await emptyBodyError.WriteStringAsync(JsonSerializer.Serialize(new PromptResponse
                        {
                            Success = false,
                            ErrorMessage = "Request body is empty"
                        }));
                        
                        return emptyBodyError;
                    }
                    
                    logger.LogInformation("Deserializing request body");
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    var promptRequest = JsonSerializer.Deserialize<PromptRequest>(requestBody, options);
                    logger.LogInformation($"Deserialization complete - PromptRequest: {(promptRequest != null ? "Success" : "Null")}");
                    
                    if (promptRequest == null || string.IsNullOrEmpty(promptRequest.Prompt))
                    {
                        logger.LogError($"Invalid request - Prompt is required. Body: {requestBody}");
                        var invalidRequestError = req.CreateResponse(HttpStatusCode.BadRequest);
                        invalidRequestError.Headers.Add("Content-Type", "application/json");
                        invalidRequestError.Headers.Add("Access-Control-Allow-Origin", "*");
                        invalidRequestError.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                        invalidRequestError.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                        
                        await invalidRequestError.WriteStringAsync(JsonSerializer.Serialize(new PromptResponse
                        {
                            Success = false,
                            ErrorMessage = $"Invalid request: Prompt is required. Received body: {requestBody}"
                        }));
                        
                        return invalidRequestError;
                    }
                    
                    // Use the PrimaryAgent to handle the request
                    logger.LogInformation("Calling PrimaryAgent.HandleAsync");
                    logger.LogInformation($"Prompt: {promptRequest.Prompt?.Substring(0, Math.Min(100, promptRequest.Prompt?.Length ?? 0))}...");
                    
                    var agentResponse = await _primaryAgent.HandleAsync(
                        promptRequest.Prompt, 
                        promptRequest.UserId, 
                        promptRequest.SessionId);
                    
                    logger.LogInformation($"PrimaryAgent response received - Length: {agentResponse?.Length ?? 0}");
                    
                    // Note: For now, we don't return detailed tool call information
                    // This could be enhanced by modifying the agent interface to return structured data
                    var toolCalls = new System.Collections.Generic.List<ToolCall>();
                    
                    var successResponse = req.CreateResponse(HttpStatusCode.OK);
                    successResponse.Headers.Add("Content-Type", "application/json");
                    successResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                    successResponse.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                    successResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    var responseData = new PromptResponse
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
                    };

                    await successResponse.WriteStringAsync(JsonSerializer.Serialize(responseData));
                    return successResponse;
                }
                catch (Exception ex)
                {
                    // Log detailed exception information
                    logger.LogError(ex, $"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        logger.LogError($"INNER EXCEPTION: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    }
                    
                    // Create detailed error response with full exception information
                    var detailedErrorMessage = $"Exception Type: {ex.GetType().Name}\nMessage: {ex.Message}\nStack Trace: {ex.StackTrace}";
                    if (ex.InnerException != null)
                    {
                        detailedErrorMessage += $"\nInner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}";
                    }
                    
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    errorResponse.Headers.Add("Content-Type", "application/json");
                    errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                    errorResponse.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                    errorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new PromptResponse
                    {
                        Success = false,
                        ErrorMessage = detailedErrorMessage
                    }));

                    return errorResponse;
                }
            }
            catch (Exception outerEx)
            {
                // Catch any exceptions that might occur during function initialization or outer scope
                logger.LogError(outerEx, $"OUTER EXCEPTION: {outerEx.GetType().Name}: {outerEx.Message}");
                
                var outerErrorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                outerErrorResponse.Headers.Add("Content-Type", "application/json");
                outerErrorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                outerErrorResponse.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                outerErrorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                await outerErrorResponse.WriteStringAsync(JsonSerializer.Serialize(new PromptResponse
                {
                    Success = false,
                    ErrorMessage = $"Function initialization error: {outerEx.GetType().Name} - {outerEx.Message}\nStack Trace: {outerEx.StackTrace}"
                }));

                return outerErrorResponse;
            }
        }
    }
}