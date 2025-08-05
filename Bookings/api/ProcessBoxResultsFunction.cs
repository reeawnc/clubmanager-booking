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

                // Get parameters from query string or request body
                object leagueId = null;
                BoxGroupType groupType = BoxGroupType.SummerFriendlies; // default

                if (req.Query.ContainsKey("leagueId"))
                {
                    if (int.TryParse(req.Query["leagueId"], out var parsedLeagueId))
                    {
                        leagueId = parsedLeagueId;
                    }
                }

                if (req.Query.ContainsKey("groupType"))
                {
                    if (Enum.TryParse<BoxGroupType>(req.Query["groupType"], true, out var parsedGroupType))
                    {
                        groupType = parsedGroupType;
                    }
                }

                // If it's a POST request, try to get parameters from body
                if (req.Method == "POST")
                {
                    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    if (!string.IsNullOrEmpty(requestBody))
                    {
                        try
                        {
                            var bodyParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(requestBody);
                            if (bodyParams.ContainsKey("leagueId") && bodyParams["leagueId"] != null)
                            {
                                if (int.TryParse(bodyParams["leagueId"].ToString(), out var bodyLeagueId))
                                {
                                    leagueId = bodyLeagueId;
                                }
                            }
                            if (bodyParams.ContainsKey("groupType") && bodyParams["groupType"] != null)
                            {
                                if (Enum.TryParse<BoxGroupType>(bodyParams["groupType"].ToString(), true, out var bodyGroupType))
                                {
                                    groupType = bodyGroupType;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogWarning($"Failed to parse request body: {ex.Message}");
                        }
                    }
                }

                var actualLeagueId = leagueId ?? "default";
                log.LogInformation($"Processing box results for LeagueId: {actualLeagueId}, GroupType: {groupType}");

                // Use the shared box results service
                var boxResultsService = new BoxResultsService();
                var boxResults = await boxResultsService.GetBoxResultsAsync(groupType, leagueId);

                if (boxResults?.Boxes == null || boxResults.Boxes.Count == 0)
                {
                    log.LogWarning("No box results found to process");
                    return new OkObjectResult(new { message = "No box results found to process" });
                }

                // Create JSON Lines content
                var content = CreateJsonLinesContent(boxResults, groupType, actualLeagueId);
                
                // Generate filename with timestamp
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var filename = $"box_results_{groupType}_{timestamp}.jsonl";

                // Upload to Azure Blob Storage
                var blobUrl = await UploadToBlobStorage(content, filename, log);

                log.LogInformation($"Successfully processed box results and uploaded to blob storage: {blobUrl}");
                
                return new OkObjectResult(new 
                { 
                    message = "Box results processed and uploaded successfully",
                    filename = filename,
                    blobUrl = blobUrl,
                    boxesProcessed = boxResults.Boxes.Count,
                    totalResults = boxResults.Boxes.Sum(box => box.Results?.Count ?? 0)
                });
            }
            catch (Exception ex)
            {
                log.LogError($"Exception in ProcessBoxResults: {ex.Message}");
                return new BadRequestObjectResult($"exception: {ex.Message}");
            }
        }

        private static string CreateJsonLinesContent(BoxResultsRoot boxResults, BoxGroupType groupType, object leagueId)
        {
            var sb = new StringBuilder();
            
            foreach (var box in boxResults.Boxes)
            {
                if (box.Results != null && box.Results.Count > 0)
                {
                    foreach (var result in box.Results)
                    {
                        var matchData = new Dictionary<string, object>
                        {
                            ["box"] = CleanHtmlTags(box.Name),
                            ["player1"] = CleanHtmlTags(result.P1),
                            ["player2"] = CleanHtmlTags(result.P2),
                            ["score"] = CleanHtmlTags(result.Score ?? "v"),
                            ["date"] = result.Date != default(DateTime) ? result.Date.ToString("yyyy-MM-dd") : "Unknown"
                        };

                        // Determine if match was played and who won/lost
                        var score = CleanHtmlTags(result.Score ?? "");
                        var hasValidScore = !string.IsNullOrEmpty(score) && score != "v" && score.Contains(" ");
                        
                        if (hasValidScore && result.Date != default(DateTime))
                        {
                            matchData["matchPlayed"] = true;
                            
                            // Parse winner and loser from score
                            var scoreParts = score.Split(' ');
                            if (scoreParts.Length >= 3 && int.TryParse(scoreParts[0], out var p1Score) && int.TryParse(scoreParts[2], out var p2Score))
                            {
                                if (p1Score > p2Score)
                                {
                                    matchData["winner"] = CleanHtmlTags(result.P1);
                                    matchData["loser"] = CleanHtmlTags(result.P2);
                                }
                                else if (p2Score > p1Score)
                                {
                                    matchData["winner"] = CleanHtmlTags(result.P2);
                                    matchData["loser"] = CleanHtmlTags(result.P1);
                                }
                                else
                                {
                                    // Draw
                                    matchData["winner"] = null;
                                    matchData["loser"] = null;
                                }
                            }
                        }
                        else
                        {
                            matchData["matchPlayed"] = false;
                        }

                        // Convert to JSON and add to output
                        var jsonLine = JsonConvert.SerializeObject(matchData);
                        sb.AppendLine(jsonLine);
                    }
                }
            }

            return sb.ToString();
        }

        private static string CleanHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove common HTML tags
            var cleaned = input
                .Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("<sup>", "")
                .Replace("</sup>", "")
                .Replace("<i>", "")
                .Replace("</i>", "")
                .Replace("<strong>", "")
                .Replace("</strong>", "")
                .Replace("<em>", "")
                .Replace("</em>", "")
                .Replace("<u>", "")
                .Replace("</u>", "")
                .Replace("<span>", "")
                .Replace("</span>", "")
                .Replace("<div>", "")
                .Replace("</div>", "")
                .Replace("<p>", "")
                .Replace("</p>", "");

            // Remove any remaining HTML tags using regex
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"<[^>]*>", "");

            return cleaned.Trim();
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