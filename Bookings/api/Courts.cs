using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Web;
using BookingsApi.Models;
using BookingsApi.Services;
using System.Net;

namespace BookingsApi
{
    public static class Courts
    {
        [Function("Courts")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            var logger = context.GetLogger("Courts");
            
            try
            {
                logger.LogInformation("C# HTTP trigger function processed a request.");

                var query = HttpUtility.ParseQueryString(req.Url.Query);
                string date = HttpUtility.UrlDecode(query["date"]);
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                date = date ?? data?.name;

                var courtAvailabilityService = new CourtAvailabilityService();
                var courtData = await courtAvailabilityService.GetCourtAvailabilityAsync(date, logger);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonConvert.SerializeObject(courtData));
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Exception in Courts: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                await errorResponse.WriteStringAsync(JsonConvert.SerializeObject(new { exception = ex.Message }));
                return errorResponse;
            }
        }
    }
} 