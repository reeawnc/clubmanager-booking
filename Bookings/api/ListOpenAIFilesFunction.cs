using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using BookingsApi.Services;
using System.Net;
using Newtonsoft.Json;

namespace BookingsApi
{
    public static class ListOpenAIFilesFunction
    {
        [Function("ListOpenAIFiles")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger("ListOpenAIFiles");
            
            try
            {
                logger.LogInformation("ListOpenAIFiles function processed a request.");

                // Initialize the OpenAI service
                var openAIService = new OpenAIFileUploadService();
                var result = await openAIService.ListFilesAsync(logger);

                if (result.Success)
                {
                    logger.LogInformation($"Successfully retrieved {result.Files.Count} files from OpenAI");
                    
                    var successResponse = req.CreateResponse(HttpStatusCode.OK);
                    successResponse.Headers.Add("Content-Type", "application/json");
                    await successResponse.WriteStringAsync(JsonConvert.SerializeObject(new 
                    { 
                        success = true,
                        message = "Files retrieved successfully from OpenAI",
                        totalFiles = result.Files.Count,
                        files = result.Files
                    }));
                    return successResponse;
                }
                else
                {
                    logger.LogError($"Failed to retrieve files from OpenAI: {result.ErrorMessage}");
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    errorResponse.Headers.Add("Content-Type", "application/json");
                    await errorResponse.WriteStringAsync(JsonConvert.SerializeObject(new 
                    { 
                        success = false,
                        message = "Failed to retrieve files from OpenAI",
                        errorMessage = result.ErrorMessage
                    }));
                    return errorResponse;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Exception in ListOpenAIFiles: {ex.Message}");
                var exceptionResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                exceptionResponse.Headers.Add("Content-Type", "application/json");
                await exceptionResponse.WriteStringAsync(JsonConvert.SerializeObject(new 
                { 
                    success = false,
                    message = "Exception occurred while listing files",
                    errorMessage = ex.Message
                }));
                return exceptionResponse;
            }
        }
    }
} 