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
using BookingsApi.Services;

namespace BookingsApi
{
    public static class GetBoxResultsFunction
    {
        [FunctionName("GetBoxResults")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("GetBoxResults function processed a request.");

                // Get parameters from query string or request body
                int leagueId = 4076; // default
                string groupId = "216"; // default

                if (req.Query.ContainsKey("leagueId"))
                {
                    if (int.TryParse(req.Query["leagueId"], out var parsedLeagueId))
                    {
                        leagueId = parsedLeagueId;
                    }
                }

                if (req.Query.ContainsKey("groupId"))
                {
                    groupId = req.Query["groupId"];
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
                            if (bodyParams.ContainsKey("groupId") && bodyParams["groupId"] != null)
                            {
                                groupId = bodyParams["groupId"].ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogWarning($"Failed to parse request body: {ex.Message}");
                        }
                    }
                }

                log.LogInformation($"Getting box results for LeagueId: {leagueId}, GroupId: {groupId}");

                // Use the shared box results service
                var boxResultsService = new BoxResultsService();
                var boxResults = await boxResultsService.GetBoxResultsAsync(leagueId, groupId);

                log.LogInformation($"Successfully retrieved box results with {boxResults?.Boxes?.Count ?? 0} boxes");
                return new OkObjectResult(JsonConvert.SerializeObject(boxResults));
            }
            catch (Exception ex)
            {
                log.LogError($"Exception in GetBoxResults: {ex.Message}");
                return new BadRequestObjectResult($"exception: {ex.Message}");
            }
        }
    }
} 