using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using BookingsApi.Services;
using BookingsApi.Models;

namespace BookingsApi
{
    public class LiveResultsFunction
    {
        private readonly BoxResultsService _resultsService = new();

        public class LiveResultsRequest
        {
            public string? group { get; set; } // "Club" or "SummerFriendlies"
            public int? leagueId { get; set; } // optional, defaults based on group
        }

        [Function("LiveResultsFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "test/live-results")] HttpRequestData req)
        {
            if (req.Method == "OPTIONS")
            {
                var pre = req.CreateResponse(HttpStatusCode.OK);
                pre.Headers.Add("Access-Control-Allow-Origin", "*");
                pre.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                pre.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                return pre;
            }

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var payload = JsonSerializer.Deserialize<LiveResultsRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new LiveResultsRequest();

                var groupType = BoxGroupType.SummerFriendlies;
                if (!string.IsNullOrWhiteSpace(payload.group) && Enum.TryParse<BoxGroupType>(payload.group, true, out var parsed))
                {
                    groupType = parsed;
                }

                var results = await _resultsService.GetBoxResultsAsync(groupType, payload.leagueId);

                var res = req.CreateResponse(HttpStatusCode.OK);
                res.Headers.Add("Content-Type", "application/json");
                res.Headers.Add("Access-Control-Allow-Origin", "*");
                await res.WriteStringAsync(JsonSerializer.Serialize(results));
                return res;
            }
            catch (Exception ex)
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                err.Headers.Add("Content-Type", "application/json");
                err.Headers.Add("Access-Control-Allow-Origin", "*");
                await err.WriteStringAsync(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                return err;
            }
        }
    }
}


