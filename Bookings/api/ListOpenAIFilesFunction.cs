using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using BookingsApi.Services;

namespace BookingsApi
{
    public static class ListOpenAIFilesFunction
    {
        [FunctionName("ListOpenAIFiles")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("ListOpenAIFiles function processed a request.");

                // Initialize the OpenAI service
                var openAIService = new OpenAIFileUploadService();
                var result = await openAIService.ListFilesAsync(log);

                if (result.Success)
                {
                    log.LogInformation($"Successfully retrieved {result.Files.Count} files from OpenAI");
                    
                    return new OkObjectResult(new 
                    { 
                        message = "Files retrieved successfully from OpenAI",
                        totalFiles = result.Files.Count,
                        files = result.Files
                    });
                }
                else
                {
                    log.LogError($"Failed to retrieve files from OpenAI: {result.ErrorMessage}");
                    return new BadRequestObjectResult(new 
                    { 
                        message = "Failed to retrieve files from OpenAI",
                        error = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception in ListOpenAIFiles: {ex.Message}");
                return new BadRequestObjectResult(new 
                { 
                    message = "Exception occurred while listing files",
                    error = ex.Message
                });
            }
        }
    }
} 