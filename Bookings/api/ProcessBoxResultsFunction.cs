using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using BookingsApi.Services;
using BookingsApi.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Linq; // Added for .Sum()
using System.Net;

namespace BookingsApi
{
    public static class ProcessBoxResultsFunction
    {
        [Function("ProcessBoxResults")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger("ProcessBoxResults");
            
            try
            {
                logger.LogInformation("ProcessBoxResults function processed a request.");

                logger.LogInformation("Processing all Summer Friendlies leagues...");

                // Initialize the processing service
                var processService = new ProcessBoxResultsService();
                var result = await processService.ProcessAllSummerFriendliesLeaguesAsync();
                
                // Log processing results
                foreach (var leagueResult in result.ProcessedLeagues)
                {
                    if (leagueResult.Success)
                    {
                        logger.LogInformation($"Processed league {leagueResult.League}: {leagueResult.MatchCount} matches");
                    }
                    else
                    {
                        logger.LogWarning($"Failed to process league {leagueResult.League}: {leagueResult.ErrorMessage}");
                    }
                }

                if (string.IsNullOrEmpty(result.Content))
                {
                    logger.LogWarning("No content generated from any leagues");
                    var noContentResponse = req.CreateResponse(HttpStatusCode.OK);
                    noContentResponse.Headers.Add("Content-Type", "application/json");
                    await noContentResponse.WriteStringAsync(JsonConvert.SerializeObject(new { message = "No box results found to process" }));
                    return noContentResponse;
                }

                // Upload to Azure Blob Storage
                var blobUrl = await UploadToBlobStorage(result.Content, result.Filename, logger);

                logger.LogInformation($"Successfully processed box results and uploaded to blob storage: {blobUrl}");

                // Upload to OpenAI
                var openAIService = new OpenAIFileUploadService();
                
                // First, delete any existing files with the same name
                logger.LogInformation("Checking for existing files with the same name...");
                var existingFiles = await openAIService.ListFilesAsync(logger);
                
                if (existingFiles.Success)
                {
                    var filesToDelete = existingFiles.Files
                        .Where(f => f.Filename == result.Filename)
                        .ToList();
                    
                    foreach (var file in filesToDelete)
                    {
                        logger.LogInformation($"Deleting existing file: {file.Filename} (ID: {file.Id})");
                        var deleteResult = await openAIService.DeleteFileAsync(file.Id!, logger);
                        if (deleteResult)
                        {
                            logger.LogInformation($"Successfully deleted existing file: {file.Filename}");
                        }
                        else
                        {
                            logger.LogWarning($"Failed to delete existing file: {file.Filename}");
                        }
                    }
                }
                else
                {
                    logger.LogWarning($"Failed to list existing files: {existingFiles.ErrorMessage}");
                }
                
                // Now upload the new file
                var openAIResult = await openAIService.UploadFileAsync(result.Content, result.Filename, logger);

                if (openAIResult.Success)
                {
                    logger.LogInformation($"Successfully uploaded file to OpenAI. File ID: {openAIResult.FileId}");
                }
                else
                {
                    logger.LogWarning($"Failed to upload file to OpenAI: {openAIResult.ErrorMessage}");
                }
                
                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                successResponse.Headers.Add("Content-Type", "application/json");
                await successResponse.WriteStringAsync(JsonConvert.SerializeObject(new 
                { 
                    success = true,
                    message = "Box results processed and uploaded successfully",
                    filename = result.Filename,
                    blobUrl = blobUrl,
                    openAIFileId = openAIResult.FileId,
                    openAISuccess = openAIResult.Success,
                    openAIErrorMessage = openAIResult.ErrorMessage,
                    leaguesProcessed = result.LeaguesProcessed,
                    totalResults = result.TotalMatches
                }));
                return successResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Exception in ProcessBoxResults: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                await errorResponse.WriteStringAsync(JsonConvert.SerializeObject(new { success = false, errorMessage = ex.Message }));
                return errorResponse;
            }
        }



        private static async Task<string> UploadToBlobStorage(string content, string filename, ILogger logger)
        {
            try
            {
                // Get connection string from environment variable
                var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("AzureWebJobsStorage connection string is not configured");
                }

                // Create blob client
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerName = "box-results";
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                // Create container if it doesn't exist
                await containerClient.CreateIfNotExistsAsync();

                // Get blob client
                var blobClient = containerClient.GetBlobClient(filename);

                // Upload content
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                logger.LogInformation($"Successfully uploaded {filename} to blob storage");
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to upload to blob storage: {ex.Message}");
                throw;
            }
        }
    }
} 