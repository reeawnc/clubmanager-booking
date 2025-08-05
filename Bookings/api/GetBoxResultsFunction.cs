using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using BookingsApi.Services;
using BookingsApi.Models;
using System.Net;

namespace BookingsApi
{
    public static class GetBoxResultsFunction
    {
        [Function("GetBoxResults")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger("GetBoxResults");
            
            try
            {
                logger.LogInformation("GetBoxResults function processed a request.");

                // Get parameters from query string or request body
                object leagueId = null;
                BoxGroupType groupType = BoxGroupType.SummerFriendlies; // default

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                
                if (!string.IsNullOrEmpty(query["leagueId"]))
                {
                    if (int.TryParse(query["leagueId"], out var parsedLeagueId))
                    {
                        leagueId = parsedLeagueId;
                    }
                }

                if (!string.IsNullOrEmpty(query["groupType"]))
                {
                    if (Enum.TryParse<BoxGroupType>(query["groupType"], true, out var parsedGroupType))
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
                            logger.LogWarning($"Failed to parse request body: {ex.Message}");
                        }
                    }
                }

                var actualLeagueId = leagueId ?? "default";
                logger.LogInformation($"Getting box results for LeagueId: {actualLeagueId}, GroupType: {groupType}");

                // Use the shared box results service
                var boxResultsService = new BoxResultsService();
                var boxResults = await boxResultsService.GetBoxResultsAsync(groupType, leagueId);

                logger.LogInformation($"Successfully retrieved box results with {boxResults?.Boxes?.Count ?? 0} boxes");
                
                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                successResponse.Headers.Add("Content-Type", "application/json");
                await successResponse.WriteStringAsync(JsonConvert.SerializeObject(new { success = true, data = boxResults }));
                return successResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Exception in GetBoxResults: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                await errorResponse.WriteStringAsync(JsonConvert.SerializeObject(new { success = false, errorMessage = ex.Message }));
                return errorResponse;
            }
        }
    }
} 