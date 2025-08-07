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
    public class CancelCourtFunction
    {
        private readonly CancellationService _service = new();

        public class CancelRequest { public long bookingId { get; set; } }

        [Function("CancelCourtFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "test/cancel-court")] HttpRequestData req)
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
                var payload = JsonSerializer.Deserialize<CancelRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (payload == null || payload.bookingId <= 0)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    bad.Headers.Add("Access-Control-Allow-Origin", "*");
                    await bad.WriteStringAsync("{\"success\":false,\"error\":\"bookingId is required\"}");
                    return bad;
                }

                var json = await _service.CancelCourtAsync(payload.bookingId);
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


