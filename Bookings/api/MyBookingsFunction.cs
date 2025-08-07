using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using BookingsApi.Services;

namespace BookingsApi
{
    public class MyBookingsFunction
    {
        private readonly MyBookingsService _service = new();

        [Function("MyBookingsFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "test/my-bookings")] HttpRequestData req)
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
                var json = await _service.GetMyBookingsRawAsync();
                var res = req.CreateResponse(HttpStatusCode.OK);
                res.Headers.Add("Content-Type", "application/json");
                res.Headers.Add("Access-Control-Allow-Origin", "*");
                await res.WriteStringAsync(json);
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


