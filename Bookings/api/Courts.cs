using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Web;
using BookingsApi.Models;
using BookingsApi.Services;

namespace BookingsApi
{
    public static class Courts
    {
        [FunctionName("Courts")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                string date = HttpUtility.UrlDecode(req.Query["date"]);
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                date = date ?? data?.name;

                var courtAvailabilityService = new CourtAvailabilityService();
                var courtData = await courtAvailabilityService.GetCourtAvailabilityAsync(date, log);
                
                return new OkObjectResult(JsonConvert.SerializeObject(courtData));
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult($"exception: {ex.Message}");
            }
        }
    }
} 