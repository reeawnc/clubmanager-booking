using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

namespace BookingsApi
{
    public static class ProcessBoxResultsFunction
    {
        [FunctionName("ProcessBoxResults")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("ProcessBoxResults function processed a request.");

                log.LogInformation("Processing all Summer Friendlies leagues...");

                // Initialize the processing service
                var processService = new ProcessBoxResultsService();
                var result = await processService.ProcessAllSummerFriendliesLeaguesAsync();
                
                // Log processing results
                foreach (var leagueResult in result.ProcessedLeagues)
                {
                    if (leagueResult.Success)
                    {
                        log.LogInformation($"Processed league {leagueResult.League}: {leagueResult.MatchCount} matches");
                    }
                    else
                    {
                        log.LogWarning($"Failed to process league {leagueResult.League}: {leagueResult.ErrorMessage}");
                    }
                }

                if (string.IsNullOrEmpty(result.Content))
                {
                    log.LogWarning("No content generated from any leagues");
                    return new OkObjectResult(new { message = "No box results found to process" });
                }

                // Upload to Azure Blob Storage
                var blobUrl = await UploadToBlobStorage(result.Content, result.Filename, log);

                log.LogInformation($"Successfully processed box results and uploaded to blob storage: {blobUrl}");

                // Upload to OpenAI
                var openAIService = new OpenAIFileUploadService();
                
                // First, delete any existing files with the same name
                log.LogInformation("Checking for existing files with the same name...");
                var existingFiles = await openAIService.ListFilesAsync(log);
                
                if (existingFiles.Success)
                {
                    var filesToDelete = existingFiles.Files
                        .Where(f => f.Filename == result.Filename)
                        .ToList();
                    
                    foreach (var file in filesToDelete)
                    {
                        log.LogInformation($"Deleting existing file: {file.Filename} (ID: {file.Id})");
                        var deleteResult = await openAIService.DeleteFileAsync(file.Id!, log);
                        if (deleteResult)
                        {
                            log.LogInformation($"Successfully deleted existing file: {file.Filename}");
                        }
                        else
                        {
                            log.LogWarning($"Failed to delete existing file: {file.Filename}");
                        }
                    }
                }
                else
                {
                    log.LogWarning($"Failed to list existing files: {existingFiles.ErrorMessage}");
                }
                
                // Now upload the new file
                var openAIResult = await openAIService.UploadFileAsync(result.Content, result.Filename, log);

                if (openAIResult.Success)
                {
                    log.LogInformation($"Successfully uploaded file to OpenAI. File ID: {openAIResult.FileId}");
                }
                else
                {
                    log.LogWarning($"Failed to upload file to OpenAI: {openAIResult.ErrorMessage}");
                }
                
                return new OkObjectResult(new 
                { 
                    message = "Box results processed and uploaded successfully",
                    filename = result.Filename,
                    blobUrl = blobUrl,
                    openAIFileId = openAIResult.FileId,
                    openAISuccess = openAIResult.Success,
                    openAIErrorMessage = openAIResult.ErrorMessage,
                    leaguesProcessed = result.LeaguesProcessed,
                    totalResults = result.TotalMatches
                });
            }
            catch (Exception ex)
            {
                log.LogError($"Exception in ProcessBoxResults: {ex.Message}");
                return new BadRequestObjectResult($"exception: {ex.Message}");
            }
        }



        private static async Task<string> UploadToBlobStorage(string content, string filename, ILogger log)
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

                log.LogInformation($"Successfully uploaded {filename} to blob storage");
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to upload to blob storage: {ex.Message}");
                throw;
            }
        }
    }
} 